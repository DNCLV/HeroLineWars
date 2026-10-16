using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Safe-area minimap showing generated walkability and pooled live unit markers.</summary>
public sealed class SchematicMinimapHud : MonoBehaviour
{
    [Header("Display")]
    [Range(.08f, .5f)] public float refreshInterval = .16f;
    [Range(4f, 14f)] public float unitMarkerSize = 6f;
    [Range(8f, 20f)] public float heroMarkerSize = 10f;

    RectTransform safeRoot;
    RectTransform mapRect;
    MinimapLaneGraphic laneGraphic;
    MinimapViewportGraphic viewportGraphic;
    Sprite circleSprite;
    LaneGenerator lane;
    Transform hero;
    LaneUnitCombat heroCombat;
    Image heroOuter;
    Image heroInner;
    Image heroCore;
    readonly Dictionary<LaneUnitCombat, Image> unitMarkers = new Dictionary<LaneUnitCombat, Image>();
    readonly HashSet<LaneUnitCombat> seenMarkers = new HashSet<LaneUnitCombat>();
    readonly List<LaneUnitCombat> activeUnits = new List<LaneUnitCombat>(64);
    readonly List<LaneUnitCombat> staleUnits = new List<LaneUnitCombat>(16);
    readonly Stack<Image> markerPool = new Stack<Image>(32);
    readonly Dictionary<int, int> overlapCounts = new Dictionary<int, int>(64);
    readonly List<Image> baseMarkers = new List<Image>(4);
    float nextRefresh;
    float nextReferenceSearch;
    const float Border = 10f;
    const float OverlapCell = 7f;
    static Sprite panelRoundedSprite;
    static Sprite contentRoundedSprite;

    public void Build(RectTransform root, Sprite frameSprite, Sprite markerSprite,
        Vector2 anchoredPosition, Vector2 size)
    {
        safeRoot = root;
        circleSprite = markerSprite;
        if (!panelRoundedSprite) panelRoundedSprite = CreateRoundedSprite("HLW Minimap Rounded 20", 20);
        if (!contentRoundedSprite) contentRoundedSprite = CreateRoundedSprite("HLW Minimap Rounded 16", 16);
        Transform previous = safeRoot.Find("SchematicMinimap");
        if (previous) Destroy(previous.gameObject);

        GameObject panel = new GameObject("SchematicMinimap", typeof(RectTransform), typeof(Image), typeof(Shadow));
        panel.transform.SetParent(safeRoot, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = size;
        Image frame = panel.GetComponent<Image>();
        frame.sprite = panelRoundedSprite;
        frame.type = Image.Type.Sliced;
        frame.color = new Color32(54, 90, 154, 255); // #365A9A border
        frame.raycastTarget = false;
        Shadow shadow = panel.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, .015f, .045f, .68f);
        shadow.effectDistance = new Vector2(0f, -5f);
        shadow.useGraphicAlpha = true;

        GameObject surfaceObject = new GameObject("RoundedNavySurface", typeof(RectTransform), typeof(Image), typeof(Mask));
        surfaceObject.transform.SetParent(panel.transform, false);
        RectTransform surfaceRect = surfaceObject.GetComponent<RectTransform>();
        surfaceRect.anchorMin = Vector2.zero;
        surfaceRect.anchorMax = Vector2.one;
        surfaceRect.offsetMin = new Vector2(2f, 2f);
        surfaceRect.offsetMax = new Vector2(-2f, -2f);
        Image surface = surfaceObject.GetComponent<Image>();
        surface.sprite = panelRoundedSprite;
        surface.type = Image.Type.Sliced;
        surface.color = new Color32(16, 26, 45, 255); // #101A2D
        surface.raycastTarget = false;
        surfaceObject.GetComponent<Mask>().showMaskGraphic = true;

        GameObject gradientObject = new GameObject("NavyTonalGradient", typeof(RectTransform), typeof(MinimapPanelGradient));
        gradientObject.transform.SetParent(surfaceObject.transform, false);
        RectTransform gradientRect = gradientObject.GetComponent<RectTransform>();
        gradientRect.anchorMin = Vector2.zero;
        gradientRect.anchorMax = Vector2.one;
        gradientRect.offsetMin = gradientRect.offsetMax = Vector2.zero;

        GameObject insetObject = new GameObject("RoundedMapClip", typeof(RectTransform), typeof(Image), typeof(Mask));
        insetObject.transform.SetParent(surfaceObject.transform, false);
        Image inset = insetObject.GetComponent<Image>();
        inset.sprite = contentRoundedSprite;
        inset.type = Image.Type.Sliced;
        inset.color = new Color32(17, 26, 40, 255); // #111A28
        inset.raycastTarget = false;
        insetObject.GetComponent<Mask>().showMaskGraphic = true;
        mapRect = insetObject.GetComponent<RectTransform>();
        mapRect.anchorMin = Vector2.zero;
        mapRect.anchorMax = Vector2.one;
        mapRect.offsetMin = new Vector2(Border - 2f, Border - 2f);
        mapRect.offsetMax = new Vector2(-Border + 2f, -Border + 2f);

        GameObject drawing = new GameObject("GeneratedWalkableMap", typeof(RectTransform), typeof(RawImage), typeof(MinimapLaneGraphic));
        drawing.transform.SetParent(mapRect, false);
        RectTransform drawingRect = drawing.GetComponent<RectTransform>();
        drawingRect.anchorMin = Vector2.zero;
        drawingRect.anchorMax = Vector2.one;
        drawingRect.offsetMin = drawingRect.offsetMax = Vector2.zero;
        laneGraphic = drawing.GetComponent<MinimapLaneGraphic>();
        laneGraphic.SetColors(new Color32(17, 26, 40, 255),
            new Color32(220, 235, 255, 255), new Color32(159, 180, 204, 255));
        drawing.transform.SetAsFirstSibling();

        GameObject viewport = new GameObject("CameraViewport", typeof(RectTransform), typeof(MinimapViewportGraphic));
        viewport.transform.SetParent(mapRect, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
        viewportGraphic = viewport.GetComponent<MinimapViewportGraphic>();

        heroOuter = CreateMarker("BattlemageMarkerOutline", new Color(.025f, .04f, .065f, 1f), heroMarkerSize + 5f);
        heroInner = CreateMarker("BattlemageMarker", new Color32(255, 214, 90, 255), heroMarkerSize);
        heroCore = CreateMarker("BattlemageMarkerCore", new Color(1f, .98f, .84f, 1f), heroMarkerSize * .38f);
        heroOuter.transform.SetAsLastSibling();
        heroInner.transform.SetAsLastSibling();
        heroCore.transform.SetAsLastSibling();
        hero = null;
        heroCombat = null;
        lane = FindAnyObjectByType<LaneGenerator>();
        if (laneGraphic) laneGraphic.SetLane(lane);
        if (viewportGraphic) viewportGraphic.Configure(lane, laneGraphic);
        BuildBaseMarkers();
        unitMarkers.Clear();
        seenMarkers.Clear();
        activeUnits.Clear();
        staleUnits.Clear();
        markerPool.Clear();
        overlapCounts.Clear();
        nextReferenceSearch = 0f;
        nextRefresh = 0f;
        UpdateMarkers(true);
    }

    void Update()
    {
        if (!mapRect) return;
        if ((!lane || !hero) && Time.unscaledTime >= nextReferenceSearch)
        {
            if (!lane) lane = FindAnyObjectByType<LaneGenerator>();
            if (!hero)
            {
                HeroLaneController heroController = FindAnyObjectByType<HeroLaneController>();
                if (heroController)
                {
                    hero = heroController.transform;
                    heroCombat = heroController.GetComponent<LaneUnitCombat>();
                }
            }
            nextReferenceSearch = Time.unscaledTime + 1f;
            if (laneGraphic && laneGraphic.Lane != lane)
            {
                laneGraphic.SetLane(lane);
                if (viewportGraphic) viewportGraphic.Configure(lane, laneGraphic);
                BuildBaseMarkers();
            }
        }
        if (laneGraphic && laneGraphic.RefreshIfChanged()) BuildBaseMarkers();
        if (Time.unscaledTime >= nextRefresh) UpdateMarkers(false);
    }

    void UpdateMarkers(bool force)
    {
        nextRefresh = Time.unscaledTime + refreshInterval;
        if (!lane || !mapRect) return;
        UnitCombatRegistry.CopyActiveUnits(activeUnits);
        seenMarkers.Clear();
        overlapCounts.Clear();

        for (int i = 0; i < activeUnits.Count; i++)
        {
            LaneUnitCombat unit = activeUnits[i];
            if (!unit || unit == heroCombat) continue;
            Vector3 local = lane.transform.InverseTransformPoint(unit.transform.position);
            if (!TryWorldToMap(local.x, local.z, out Vector2 point)) continue;

            seenMarkers.Add(unit);
            if (!unitMarkers.TryGetValue(unit, out Image marker) || !marker)
            {
                marker = GetPooledMarker(unit.Faction.team == UnitTeam.Friendly ? "AllyMarker" : "EnemyMarker");
                unitMarkers[unit] = marker;
            }
            marker.color = unit.Faction.team == UnitTeam.Friendly
                ? new Color32(73, 217, 133, 255) : new Color32(240, 82, 101, 255);
            SetMarker(marker, SeparateOverlaps(point));
            marker.gameObject.SetActive(true);
        }

        staleUnits.Clear();
        foreach (KeyValuePair<LaneUnitCombat, Image> entry in unitMarkers)
        {
            if (!entry.Key || !seenMarkers.Contains(entry.Key))
            {
                RecycleMarker(entry.Value);
                staleUnits.Add(entry.Key);
            }
        }
        for (int i = 0; i < staleUnits.Count; i++) unitMarkers.Remove(staleUnits[i]);

        Vector2 heroPoint = default;
        bool heroVisible = false;
        if (hero)
        {
            Vector3 heroLocal = lane.transform.InverseTransformPoint(hero.position);
            heroVisible = TryWorldToMap(heroLocal.x, heroLocal.z, out heroPoint);
        }
        if (heroOuter) heroOuter.gameObject.SetActive(heroVisible);
        if (heroInner) heroInner.gameObject.SetActive(heroVisible);
        if (heroCore) heroCore.gameObject.SetActive(heroVisible);
        if (heroVisible)
        {
            SetMarker(heroOuter, heroPoint);
            SetMarker(heroInner, heroPoint);
            SetMarker(heroCore, heroPoint);
            heroOuter.transform.SetAsLastSibling();
            heroInner.transform.SetAsLastSibling();
            heroCore.transform.SetAsLastSibling();
        }
        if (viewportGraphic) viewportGraphic.Refresh();
        if (force && laneGraphic && laneGraphic.Lane != lane) laneGraphic.SetLane(lane);
    }

    bool TryWorldToMap(float x, float z, out Vector2 point)
    {
        point = default;
        if (x < 0f || x > lane.WidthWorld || z < 0f || z > lane.LengthWorld) return false;
        Rect rect = mapRect.rect;
        point = laneGraphic ? laneGraphic.WorldToMap(new Vector3(x, 0f, z), rect) :
            new Vector2((.5f - Mathf.Clamp01(z / lane.LengthWorld)) * rect.width,
                (.5f - Mathf.Clamp01(x / lane.WidthWorld)) * rect.height);
        return true;
    }

    void BuildBaseMarkers()
    {
        for (int i = 0; i < baseMarkers.Count; i++)
            if (baseMarkers[i]) Destroy(baseMarkers[i].gameObject);
        baseMarkers.Clear();
        if (!lane || !mapRect) return;
        AddBaseMarker("BlueBaseMarker", lane.RoadCenterX(lane.battleLaneCount / 2), lane.cellSize * 2f,
            new Color32(67, 145, 255, 255));
        for (int i = 0; i < lane.battleLaneCount; i++)
            AddBaseMarker("RedBaseMarker_" + i, lane.RoadCenterX(i), lane.LengthWorld - lane.cellSize * 2f,
                new Color32(216, 74, 87, 255));
    }

    void AddBaseMarker(string name, float x, float z, Color color)
    {
        if (!TryWorldToMap(x, z, out Vector2 point)) return;
        Image outline = CreateDiamondMarker(name + "Outline", new Color(.005f, .012f, .02f, 1f), 18f);
        Image crest = CreateDiamondMarker(name, color, 12f);
        SetMarker(outline, point);
        SetMarker(crest, point);
        baseMarkers.Add(outline);
        baseMarkers.Add(crest);
    }

    Vector2 SeparateOverlaps(Vector2 point)
    {
        int col = Mathf.RoundToInt(point.x / OverlapCell);
        int row = Mathf.RoundToInt(point.y / OverlapCell);
        int key = (col * 73856093) ^ (row * 19349663);
        overlapCounts.TryGetValue(key, out int count);
        overlapCounts[key] = count + 1;
        if (count == 0) return point;
        float angle = count * 2.39996323f;
        float radius = Mathf.Min(5.5f, 1.8f + Mathf.Sqrt(count) * 1.4f);
        return point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    Image CreateMarker(string name, Color color, float size)
    {
        GameObject markerObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
        markerObject.transform.SetParent(mapRect, false);
        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.anchorMin = markerRect.anchorMax = markerRect.pivot = new Vector2(.5f, .5f);
        markerRect.sizeDelta = new Vector2(size, size);
        Image marker = markerObject.GetComponent<Image>();
        marker.sprite = circleSprite;
        marker.color = color;
        marker.raycastTarget = false;
        marker.preserveAspect = true;
        Outline markerOutline = markerObject.GetComponent<Outline>();
        markerOutline.effectColor = new Color(.004f, .012f, .024f, .9f);
        markerOutline.effectDistance = new Vector2(1f, -1f);
        return marker;
    }

    Image CreateDiamondMarker(string name, Color color, float size)
    {
        Image marker = CreateMarker(name, color, size);
        marker.sprite = null;
        marker.preserveAspect = false;
        marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        return marker;
    }

    Image GetPooledMarker(string name)
    {
        Image marker = markerPool.Count > 0 ? markerPool.Pop() : CreateMarker(name, Color.white, unitMarkerSize);
        marker.gameObject.name = name;
        RectTransform rect = (RectTransform)marker.transform;
        rect.sizeDelta = new Vector2(unitMarkerSize, unitMarkerSize);
        return marker;
    }

    void RecycleMarker(Image marker)
    {
        if (!marker) return;
        marker.gameObject.SetActive(false);
        markerPool.Push(marker);
    }

    static void SetMarker(Image marker, Vector2 point)
    {
        if (marker) ((RectTransform)marker.transform).anchoredPosition = point;
    }

    static Sprite CreateRoundedSprite(string spriteName, int radius)
    {
        const int size = 64;
        const int samples = 4;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
        {
            name = spriteName + " Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int covered = 0;
            for (int sy = 0; sy < samples; sy++)
            for (int sx = 0; sx < samples; sx++)
            {
                float px = x + (sx + .5f) / samples;
                float py = y + (sy + .5f) / samples;
                if (InsideRoundedRect(px, py, size, radius)) covered++;
            }
            pixels[y * size + x] = new Color32(255, 255, 255,
                (byte)Mathf.RoundToInt(255f * covered / (samples * samples)));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        sprite.name = spriteName;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    static bool InsideRoundedRect(float x, float y, float size, float radius)
    {
        float closestX = Mathf.Clamp(x, radius, size - radius);
        float closestY = Mathf.Clamp(y, radius, size - radius);
        float dx = x - closestX;
        float dy = y - closestY;
        return dx * dx + dy * dy <= radius * radius;
    }
}

/// <summary>Two-color app-panel gradient clipped by the rounded frame mask.</summary>
public sealed class MinimapPanelGradient : MaskableGraphic
{
    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect rect = rectTransform.rect;
        Color32 bottom = new Color32(11, 19, 34, 120);
        Color32 top = new Color32(35, 55, 88, 145);
        helper.AddVert(new Vector2(rect.xMin, rect.yMin), bottom, Vector2.zero);
        helper.AddVert(new Vector2(rect.xMin, rect.yMax), top, Vector2.zero);
        helper.AddVert(new Vector2(rect.xMax, rect.yMax), top, Vector2.zero);
        helper.AddVert(new Vector2(rect.xMax, rect.yMin), bottom, Vector2.zero);
        helper.AddTriangle(0, 1, 2);
        helper.AddTriangle(0, 2, 3);
    }
}

/// <summary>Lightweight camera-footprint overlay drawn above the cached map texture.</summary>
public sealed class MinimapViewportGraphic : MaskableGraphic
{
    LaneGenerator lane;
    MinimapLaneGraphic map;
    Camera gameplayCamera;
    readonly Vector2[] corners = new Vector2[4];
    readonly Vector2[] viewportCorners =
    {
        new Vector2(0f, 0f), new Vector2(0f, 1f),
        new Vector2(1f, 1f), new Vector2(1f, 0f)
    };

    public void Configure(LaneGenerator laneGenerator, MinimapLaneGraphic mapGraphic)
    {
        lane = laneGenerator;
        map = mapGraphic;
        gameplayCamera = Camera.main;
        color = new Color(.44f, .82f, 1f, .34f);
        raycastTarget = false;
        SetVerticesDirty();
    }

    public void Refresh()
    {
        if (!gameplayCamera) gameplayCamera = Camera.main;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (!lane || !map || !gameplayCamera) return;

        Plane ground = new Plane(lane.transform.up, lane.transform.position);
        for (int i = 0; i < corners.Length; i++)
        {
            Ray ray = gameplayCamera.ViewportPointToRay(viewportCorners[i]);
            if (!ground.Raycast(ray, out float distance) || distance < 0f) return;
            Vector3 laneLocal = lane.transform.InverseTransformPoint(ray.GetPoint(distance));
            corners[i] = map.WorldToMap(laneLocal, rectTransform.rect);
        }

        for (int i = 0; i < corners.Length; i++)
            AddLine(vertexHelper, corners[i], corners[(i + 1) % corners.Length], 1.25f, color);
    }

    static void AddLine(VertexHelper helper, Vector2 from, Vector2 to, float width, Color tint)
    {
        Vector2 direction = to - from;
        if (direction.sqrMagnitude < .01f) return;
        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * width * .5f;
        int start = helper.currentVertCount;
        helper.AddVert(from - normal, tint, Vector2.zero);
        helper.AddVert(from + normal, tint, Vector2.zero);
        helper.AddVert(to + normal, tint, Vector2.zero);
        helper.AddVert(to - normal, tint, Vector2.zero);
        helper.AddTriangle(start, start + 1, start + 2);
        helper.AddTriangle(start, start + 2, start + 3);
    }
}
