using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared Canvas-space configuration and safe-area conversion for runtime-built UI.</summary>
public static class HlwResponsiveCanvas
{
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    public static void Configure(Canvas canvas, CanvasScaler scaler, int sortingOrder)
    {
        if (!canvas || !scaler) return;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.enabled = true;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        RectTransform root = canvas.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;

        scaler.enabled = true;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
    }

    public static Rect ValidSafeArea()
    {
        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        Rect safe = Screen.safeArea;
        if (safe.width < 1f || safe.height < 1f || safe.xMin < 0f || safe.yMin < 0f ||
            safe.xMax > width + 1f || safe.yMax > height + 1f)
            safe = new Rect(0f, 0f, width, height);
        return safe;
    }

    public static void ApplySafeArea(RectTransform safeRoot, Rect safe)
    {
        if (!safeRoot || Screen.width < 1 || Screen.height < 1) return;
        safeRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        safeRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        safeRoot.offsetMin = Vector2.zero;
        safeRoot.offsetMax = Vector2.zero;
        safeRoot.localScale = Vector3.one;
    }

    public static bool Approximately(Rect a, Rect b, float tolerance = .5f)
    {
        return Mathf.Abs(a.x - b.x) < tolerance && Mathf.Abs(a.y - b.y) < tolerance &&
            Mathf.Abs(a.width - b.width) < tolerance && Mathf.Abs(a.height - b.height) < tolerance;
    }

    public static Rect ScreenBounds(RectTransform rect, Canvas canvas)
    {
        if (!rect || !canvas) return Rect.zero;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 first = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
        float xMin = first.x;
        float xMax = first.x;
        float yMin = first.y;
        float yMax = first.y;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]);
            xMin = Mathf.Min(xMin, point.x);
            xMax = Mathf.Max(xMax, point.x);
            yMin = Mathf.Min(yMin, point.y);
            yMax = Mathf.Max(yMax, point.y);
        }
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
}

/// <summary>Editor/development-only bounds and major-group overlap validation.</summary>
[DisallowMultipleComponent]
public sealed class HlwUiLayoutValidator : MonoBehaviour
{
    const float BoundsTolerance = 2f;
    Canvas targetCanvas;
    RectTransform safeRoot;
    string[] majorGroupNames;
    int settleFrames;
    int lastSignature;

    public void Configure(Canvas canvas, RectTransform root, params string[] groupNames)
    {
        targetCanvas = canvas;
        safeRoot = root;
        majorGroupNames = groupNames;
        settleFrames = 2;
        lastSignature = int.MinValue;
    }

    void LateUpdate()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;
        if (!targetCanvas || !safeRoot || settleFrames-- > 0) return;
        int signature = LayoutSignature();
        if (signature == lastSignature) return;
        lastSignature = signature;
        Canvas.ForceUpdateCanvases();
        ValidateNow();
    }

    int LayoutSignature()
    {
        unchecked
        {
            int hash = Screen.width * 397 ^ Screen.height;
            Rect safe = HlwResponsiveCanvas.ValidSafeArea();
            hash = hash * 31 + safe.GetHashCode();
            Selectable[] controls = safeRoot.GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < controls.Length; i++)
                hash = hash * 31 + (controls[i] && controls[i].gameObject.activeInHierarchy
                    ? controls[i].GetEntityId().GetHashCode() : 0);
            return hash;
        }
    }

    void ValidateNow()
    {
        Rect safe = HlwResponsiveCanvas.ValidSafeArea();
        List<string> failures = new List<string>();
        List<string> warnings = new List<string>();
        List<RectTransform> strictRects = new List<RectTransform>();
        HashSet<int> seen = new HashSet<int>();

        Selectable[] controls = safeRoot.GetComponentsInChildren<Selectable>(false);
        for (int i = 0; i < controls.Length; i++)
        {
            RectTransform rect = controls[i] ? controls[i].transform as RectTransform : null;
            AddStrictRect(rect, seen, strictRects);
        }

        List<RectTransform> majorRects = new List<RectTransform>();
        if (majorGroupNames != null)
        {
            for (int i = 0; i < majorGroupNames.Length; i++)
            {
                Transform found = FindDescendant(safeRoot, majorGroupNames[i]);
                RectTransform rect = found as RectTransform;
                if (!rect || !rect.gameObject.activeInHierarchy) continue;
                AddStrictRect(rect, seen, strictRects);
                majorRects.Add(rect);
            }
        }

        for (int i = 0; i < strictRects.Count; i++)
        {
            Rect bounds = ValidationBounds(strictRects[i]);
            Vector4 overflow = Overflow(bounds, safe);
            if (ExceedsTolerance(overflow))
                failures.Add($"OUT {Path(strictRects[i])} bounds={Format(bounds)} safe={Format(safe)} " +
                    $"overflow={FormatOverflow(overflow)} tolerance={BoundsTolerance:0.#}px");
        }

        for (int i = 0; i < majorRects.Count; i++)
        {
            Rect a = ValidationBounds(majorRects[i]);
            for (int j = i + 1; j < majorRects.Count; j++)
            {
                if (majorRects[j].IsChildOf(majorRects[i]) || majorRects[i].IsChildOf(majorRects[j])) continue;
                Rect b = ValidationBounds(majorRects[j]);
                Rect intersection = Intersect(a, b);
                if (intersection.width > BoundsTolerance && intersection.height > BoundsTolerance)
                    failures.Add($"OVERLAP {Path(majorRects[i])} / {Path(majorRects[j])} area={Format(intersection)}");
            }
        }

        ValidateContainedText(failures);

        StringBuilder message = new StringBuilder();
        message.Append($"[HLW UI Bounds] {gameObject.scene.name} screen={Screen.width}x{Screen.height} " +
            $"safe={Format(safe)} checked={strictRects.Count} major={majorRects.Count}");
        AppendDiagnosticBounds(message, "SchematicMinimap", warnings, true);
        AppendDiagnosticBounds(message, "TopHudRoot");
        AppendDiagnosticBounds(message, "EconomyPanel");
        AppendDiagnosticBounds(message, "RedBasePanel");
        AppendDiagnosticBounds(message, "BlueBasePanel");
        AppendDiagnosticBounds(message, "BottomLeftRoot");
        AppendDiagnosticBounds(message, "BottomRightRoot");
        AppendDiagnosticBounds(message, "ProgressionRoot");
        AppendDiagnosticBounds(message, "UnitsToggle");
        AppendDiagnosticBounds(message, "ItemsToggle");
        if (failures.Count == 0)
            Debug.Log(message.Append(warnings.Count == 0 ? " PASS" : $" PASS warnings={warnings.Count}"), this);
        else
            Debug.Log(message.Append($" FAIL count={failures.Count}"), this);
        for (int i = 0; i < warnings.Count; i++)
            Debug.LogWarning($"[HLW UI Bounds] WARNING {warnings[i]}", this);
        for (int i = 0; i < failures.Count; i++)
        {
            // Every red Console entry names one confirmed offender and includes the
            // measured overflow or overlap. Informational bounds never reach this path.
            Debug.LogError($"[HLW UI Bounds] ERROR {failures[i]}", this);
        }
    }

    Rect ValidationBounds(RectTransform rect)
    {
        if (!rect) return Rect.zero;
        // Layout roots span the safe area but draw nothing themselves. Validate the
        // union of their active children so empty root space cannot create false
        // overflow or overlap failures.
        bool layoutOnlyRoot = rect.name == "TopHudRoot" || rect.name == "BottomLeftRoot" ||
            rect.name == "BottomRightRoot" || rect.name == "ProgressionRoot";
        if (!layoutOnlyRoot) return HlwResponsiveCanvas.ScreenBounds(rect, targetCanvas);
        bool found = false;
        Rect visible = Rect.zero;
        for (int i = 0; i < rect.childCount; i++)
        {
            RectTransform child = rect.GetChild(i) as RectTransform;
            if (!child || !child.gameObject.activeInHierarchy) continue;
            Rect childBounds = HlwResponsiveCanvas.ScreenBounds(child, targetCanvas);
            visible = found ? Union(visible, childBounds) : childBounds;
            found = true;
        }
        return found ? visible : HlwResponsiveCanvas.ScreenBounds(rect, targetCanvas);
    }

    void AppendDiagnosticBounds(StringBuilder message, string objectName,
        List<string> warnings = null, bool includeGraphicChildren = false)
    {
        Transform found = safeRoot ? FindDescendant(safeRoot, objectName) : null;
        RectTransform rect = found as RectTransform;
        if (!rect || !rect.gameObject.activeInHierarchy) return;
        Rect safe = HlwResponsiveCanvas.ValidSafeArea();
        Rect bounds = ValidationBounds(rect);
        Vector4 overflow = Overflow(bounds, safe);
        message.Append($"\nINFO BOUNDS {objectName}={Format(bounds)} status=" +
            $"{(ExceedsTolerance(overflow) ? "OUTSIDE" : "INSIDE")} overflow={FormatOverflow(overflow)}");
        if (!includeGraphicChildren) return;
        Rect graphicBounds = ActiveGraphicBounds(rect);
        Vector4 graphicOverflow = Overflow(graphicBounds, safe);
        message.Append($" graphics={Format(graphicBounds)} graphicsStatus=" +
            $"{(ExceedsTolerance(graphicOverflow) ? "OUTSIDE" : "INSIDE")} " +
            $"graphicsOverflow={FormatOverflow(graphicOverflow)}");
        if (!ExceedsTolerance(overflow) && ExceedsTolerance(graphicOverflow) && warnings != null)
            warnings.Add($"{objectName} decorative graphics extend outside safe area: " +
                $"bounds={Format(graphicBounds)} overflow={FormatOverflow(graphicOverflow)}");
    }

    Rect ActiveGraphicBounds(RectTransform root)
    {
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(false);
        bool found = false;
        Rect result = Rect.zero;
        for (int i = 0; i < graphics.Length; i++)
        {
            RectTransform graphicRect = graphics[i] ? graphics[i].rectTransform : null;
            if (!graphicRect || !graphicRect.gameObject.activeInHierarchy) continue;
            Rect bounds = HlwResponsiveCanvas.ScreenBounds(graphicRect, targetCanvas);
            result = found ? Union(result, bounds) : bounds;
            found = true;
        }
        return found ? result : HlwResponsiveCanvas.ScreenBounds(root, targetCanvas);
    }

    void ValidateContainedText(List<string> failures)
    {
        if (!safeRoot) return;
        string[] names = { "LevelLabelText", "LevelValueText", "XpValueText" };
        for (int i = 0; i < names.Length; i++)
        {
            RectTransform text = FindDescendant(safeRoot, names[i]) as RectTransform;
            RectTransform parent = text && text.parent ? text.parent as RectTransform : null;
            if (!text || !parent || !text.gameObject.activeInHierarchy) continue;
            Rect textBounds = HlwResponsiveCanvas.ScreenBounds(text, targetCanvas);
            Rect parentBounds = HlwResponsiveCanvas.ScreenBounds(parent, targetCanvas);
            Vector4 overflow = Overflow(textBounds, parentBounds);
            if (ExceedsTolerance(overflow))
                failures.Add($"TEXT_OUT {Path(text)} parent={Path(parent)} bounds={Format(textBounds)} " +
                    $"parentBounds={Format(parentBounds)} overflow={FormatOverflow(overflow)}");
            // Valid containment is already represented by the single PASS summary.
        }
    }

    static Transform FindDescendant(Transform root, string objectName)
    {
        if (!root) return null;
        if (root.name == objectName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendant(root.GetChild(i), objectName);
            if (found) return found;
        }
        return null;
    }

    static Vector4 Overflow(Rect bounds, Rect safe) => new Vector4(
        Mathf.Max(0f, safe.xMin - bounds.xMin),
        Mathf.Max(0f, bounds.xMax - safe.xMax),
        Mathf.Max(0f, safe.yMin - bounds.yMin),
        Mathf.Max(0f, bounds.yMax - safe.yMax));

    static bool ExceedsTolerance(Vector4 overflow) => overflow.x > BoundsTolerance ||
        overflow.y > BoundsTolerance || overflow.z > BoundsTolerance || overflow.w > BoundsTolerance;

    static string FormatOverflow(Vector4 value) =>
        $"L{value.x:0.#}/R{value.y:0.#}/B{value.z:0.#}/T{value.w:0.#}";

    static void AddStrictRect(RectTransform rect, HashSet<int> seen, List<RectTransform> output)
    {
        if (!rect || !rect.gameObject.activeInHierarchy || !seen.Add(rect.GetEntityId().GetHashCode())) return;
        output.Add(rect);
    }

    static Rect Intersect(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMax = Mathf.Min(a.yMax, b.yMax);
        return xMax > xMin && yMax > yMin ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : Rect.zero;
    }

    static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(
        Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
        Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

    static string Path(Transform value)
    {
        string path = value.name;
        while (value.parent && value.parent.name != "SafeArea")
        {
            value = value.parent;
            path = value.name + "/" + path;
        }
        return path;
    }

    static string Format(Rect value) =>
        $"[{value.xMin:0.#},{value.yMin:0.#}..{value.xMax:0.#},{value.yMax:0.#}]";
}
