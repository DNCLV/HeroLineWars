using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent camera-facing health widget. The detached world-space canvas avoids inheriting
/// mirrored GLB transforms and is created once for the lifetime of the pooled unit.
/// </summary>
public sealed class UnitHealthBar : MonoBehaviour
{
    const float HeroPixelWidth = 220f;
    const float HeroPixelHeight = 28f;
    const float UnitPixelWidth = 112f;
    const float UnitPixelHeight = 14f;

    static readonly Color BackgroundColor = new Color(.025f, .035f, .055f, .96f);
    static readonly Color BorderColor = new Color(.47f, .62f, .75f, .92f);
    static readonly Color FriendlyColor = new Color(.12f, .82f, .39f, 1f);
    static readonly Color EnemyColor = new Color(.94f, .15f, .2f, 1f);

    RectTransform widgetRoot;
    RectTransform fill;
    Canvas canvas;
    GoblinHealth owner;
    Camera viewCamera;
    HeroProgression hero;
    Text heroLevelLabel;
    float verticalOffset;
    float baseWorldScale;
    float fillAmount = 1f;
    bool persistent;
    int displayedLevel = -1;

    public void Show(GoblinHealth health, int current, int maximum, bool keepVisible = false)
    {
        owner = health;
        persistent |= keepVisible;
        if (!widgetRoot) CreateWidget();
        if (!widgetRoot) return;

        fillAmount = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
        UpdateFill();
        widgetRoot.gameObject.SetActive(persistent || current < maximum);
        UpdateHeroLevel();
        PositionWidget();
    }

    public void Hide()
    {
        persistent = false;
        if (widgetRoot) widgetRoot.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (widgetRoot && owner && !owner.IsDead)
            widgetRoot.gameObject.SetActive(persistent || owner.CurrentHitPoints < owner.MaximumHitPoints);
    }

    void OnDisable()
    {
        if (widgetRoot) widgetRoot.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (!widgetRoot || !widgetRoot.gameObject.activeSelf || !owner) return;
        UpdateHeroLevel();
        PositionWidget();
    }

    void OnDestroy()
    {
        if (!widgetRoot) return;
        if (Application.isPlaying) Destroy(widgetRoot.gameObject);
        else DestroyImmediate(widgetRoot.gameObject);
    }

    void CreateWidget()
    {
        if (!owner) return;

        Bounds bounds = CalculateModelBounds(out bool foundBounds);
        hero = owner.GetComponent<HeroProgression>();
        bool isHero = hero;
        UnitFaction faction = owner.GetComponent<UnitFaction>();
        bool isEnemy = faction && faction.team == UnitTeam.Enemy;

        float modelFootprint = foundBounds ? Mathf.Max(bounds.size.x, bounds.size.z) : .8f;
        float worldWidth = isHero ? 2.2f : Mathf.Clamp(modelFootprint * .9f, .8f, 1.5f);
        float pixelWidth = isHero ? HeroPixelWidth : UnitPixelWidth;
        float pixelHeight = isHero ? HeroPixelHeight : UnitPixelHeight;
        baseWorldScale = worldWidth / pixelWidth;
        verticalOffset = foundBounds
            ? Mathf.Max(.9f, bounds.max.y - owner.transform.position.y + .22f)
            : (isHero ? 2.15f : 1.25f);

        GameObject root = new GameObject(isHero ? "HeroHealthWidget" : "UnitHealthWidget",
            typeof(RectTransform), typeof(Canvas));
        root.layer = owner.gameObject.layer;
        widgetRoot = root.GetComponent<RectTransform>();
        widgetRoot.sizeDelta = new Vector2(pixelWidth, pixelHeight);
        widgetRoot.pivot = new Vector2(.5f, .5f);

        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 120;

        if (isHero)
        {
            CreateHeroLevelLabel(isEnemy);
            CreateBar(new Vector2(54f, 2f), new Vector2(pixelWidth - 56f, pixelHeight - 4f), isEnemy);
        }
        else
        {
            CreateBar(new Vector2(1f, 1f), new Vector2(pixelWidth - 2f, pixelHeight - 2f), isEnemy);
        }

        widgetRoot.gameObject.SetActive(false);
    }

    Bounds CalculateModelBounds(out bool found)
    {
        Renderer[] renderers = owner.GetComponentsInChildren<Renderer>(true);
        found = false;
        Bounds bounds = new Bounds(owner.transform.position, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer candidate = renderers[i];
            if (!candidate || candidate.name.Contains("Ring") || candidate.name.Contains("Marker") ||
                candidate.name.Contains("Level") || candidate.name.Contains("HealthBar") ||
                candidate.name.Contains("HealthWidget")) continue;
            if (!found)
            {
                bounds = candidate.bounds;
                found = true;
            }
            else bounds.Encapsulate(candidate.bounds);
        }
        return bounds;
    }

    void CreateBar(Vector2 position, Vector2 size, bool enemy)
    {
        RectTransform border = CreateImage("Border", widgetRoot, BorderColor);
        SetRect(border, position, size);

        RectTransform background = CreateImage("Background", border, BackgroundColor);
        background.anchorMin = Vector2.zero;
        background.anchorMax = Vector2.one;
        background.offsetMin = new Vector2(2f, 2f);
        background.offsetMax = new Vector2(-2f, -2f);

        fill = CreateImage("Fill", background, enemy ? EnemyColor : FriendlyColor);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(1f, 1f);
        fill.offsetMax = new Vector2(-1f, -1f);
    }

    void CreateHeroLevelLabel(bool enemy)
    {
        GameObject labelObject = new GameObject("HeroLevel", typeof(RectTransform), typeof(Text));
        labelObject.layer = widgetRoot.gameObject.layer;
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.SetParent(widgetRoot, false);
        SetRect(rect, Vector2.zero, new Vector2(52f, HeroPixelHeight));

        heroLevelLabel = labelObject.GetComponent<Text>();
        heroLevelLabel.raycastTarget = false;
        heroLevelLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        heroLevelLabel.fontSize = 19;
        heroLevelLabel.fontStyle = FontStyle.Bold;
        heroLevelLabel.alignment = TextAnchor.MiddleCenter;
        heroLevelLabel.color = enemy ? new Color(1f, .54f, .35f) : new Color(1f, .86f, .35f);
        heroLevelLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        heroLevelLabel.verticalOverflow = VerticalWrapMode.Overflow;
    }

    static RectTransform CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject element = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        element.layer = parent.gameObject.layer;
        RectTransform rect = element.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Image image = element.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    void UpdateHeroLevel()
    {
        if (!hero || !heroLevelLabel || hero.Level == displayedLevel) return;
        displayedLevel = hero.Level;
        heroLevelLabel.text = $"LV{displayedLevel}";
    }

    void PositionWidget()
    {
        if (!owner || !widgetRoot) return;
        if (!viewCamera) viewCamera = Camera.main;
        if (canvas && canvas.worldCamera != viewCamera) canvas.worldCamera = viewCamera;

        widgetRoot.position = owner.transform.position + Vector3.up * verticalOffset;
        if (viewCamera)
            widgetRoot.rotation = viewCamera.transform.rotation;

        float zoomScale = viewCamera && viewCamera.orthographic
            ? Mathf.Clamp(viewCamera.orthographicSize / 12f, .82f, 1.55f)
            : 1f;
        widgetRoot.localScale = Vector3.one * (baseWorldScale * zoomScale);
    }

    void UpdateFill()
    {
        if (!fill) return;
        Vector2 maximum = fill.anchorMax;
        maximum.x = Mathf.Max(.001f, fillAmount);
        fill.anchorMax = maximum;
        fill.offsetMax = new Vector2(fillAmount >= .999f ? -1f : 0f, -1f);
    }
}
