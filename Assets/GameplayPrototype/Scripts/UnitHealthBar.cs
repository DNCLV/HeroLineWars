using UnityEngine;

/// <summary>Small camera-facing mesh bar, created only after a unit first takes damage.</summary>
public sealed class UnitHealthBar : MonoBehaviour
{
    const float BarHeight = .075f;
    static readonly Color BackgroundColor = new Color(.035f, .045f, .055f, 1f);
    static Material backgroundMaterial;
    static Material friendlyFillMaterial;
    static Material enemyFillMaterial;

    Transform barRoot;
    Transform fill;
    GoblinHealth owner;
    Camera viewCamera;
    float verticalOffset;
    float barWidth;
    float fillAmount = 1f;
    bool persistent;
    HeroProgression hero;
    TextMesh heroLevelLabel;
    float barCenterX;
    int displayedLevel = -1;

    public void Show(GoblinHealth health, int current, int maximum, bool keepVisible = false)
    {
        owner = health;
        persistent |= keepVisible;
        if (!barRoot) CreateBar();
        if (!barRoot) return;
        fillAmount = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
        UpdateFill();
        barRoot.gameObject.SetActive(persistent || current < maximum);
        PositionBar();
    }

    public void Hide()
    {
        persistent = false;
        if (barRoot) barRoot.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (barRoot && owner && !owner.IsDead)
            barRoot.gameObject.SetActive(persistent || owner.CurrentHitPoints < owner.MaximumHitPoints);
    }

    void OnDisable()
    {
        if (barRoot) barRoot.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (!barRoot || !barRoot.gameObject.activeSelf || !owner) return;
        UpdateHeroLevel();
        PositionBar();
    }

    void OnDestroy()
    {
        if (!barRoot) return;
        if (Application.isPlaying) Destroy(barRoot.gameObject);
        else DestroyImmediate(barRoot.gameObject);
    }

    void CreateBar()
    {
        if (!owner) return;
        Renderer[] renderers = owner.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        Bounds bounds = new Bounds(owner.transform.position, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!renderer || renderer.name.Contains("Ring") || renderer.name.Contains("Marker") ||
                renderer.name.Contains("Level") || renderer.name.Contains("HealthBar")) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }

        hero = owner.GetComponent<HeroProgression>();
        barWidth = hero ? 1.25f :
            found ? Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * .85f, .55f, 1.35f) : .7f;
        barCenterX = hero ? .32f : 0f;
        verticalOffset = found ? Mathf.Max(.8f, bounds.max.y - owner.transform.position.y + .16f) : 1.5f;
        viewCamera = Camera.main;
        if (!backgroundMaterial)
            backgroundMaterial = GameplayVisuals.GetMaterial("HLW Unit HP Background", BackgroundColor);
        UnitFaction faction = owner.GetComponent<UnitFaction>();
        bool enemy = faction && faction.team == UnitTeam.Enemy;
        if (enemy && !enemyFillMaterial)
            enemyFillMaterial = GameplayVisuals.GetMaterial("HLW Unit HP Enemy", new Color(.9f, .14f, .16f));
        if (!enemy && !friendlyFillMaterial)
            friendlyFillMaterial = GameplayVisuals.GetMaterial("HLW Unit HP Friendly", new Color(.14f, .78f, .42f));
        if (!backgroundMaterial || (enemy ? !enemyFillMaterial : !friendlyFillMaterial)) return;

        GameObject root = new GameObject("DamageHealthBar");
        barRoot = root.transform;
        barRoot.localScale = Vector3.one;
        GameObject background = CreateQuad("Background", barRoot, backgroundMaterial, barWidth, BarHeight, 0f);
        background.transform.localPosition = new Vector3(barCenterX, 0f, 0f);
        GameObject fillObject = CreateQuad("Fill", barRoot, enemy ? enemyFillMaterial : friendlyFillMaterial,
            barWidth, BarHeight * .62f, -.003f);
        fill = fillObject.transform;
        if (hero) CreateHeroLevelLabel(enemy);
        barRoot.gameObject.SetActive(false);
    }

    void CreateHeroLevelLabel(bool enemy)
    {
        GameObject label = new GameObject("HeroLevel", typeof(TextMesh));
        label.transform.SetParent(barRoot, false);
        label.transform.localPosition = new Vector3(-barWidth * .5f - .08f, 0f, -.008f);
        label.transform.localRotation = Quaternion.identity;
        label.transform.localScale = Vector3.one;
        heroLevelLabel = label.GetComponent<TextMesh>();
        heroLevelLabel.anchor = TextAnchor.MiddleRight;
        heroLevelLabel.alignment = TextAlignment.Right;
        heroLevelLabel.characterSize = .075f;
        heroLevelLabel.fontSize = 42;
        heroLevelLabel.fontStyle = FontStyle.Bold;
        heroLevelLabel.color = enemy ? new Color(1f, .52f, .36f) : new Color(1f, .86f, .36f);
        MeshRenderer renderer = heroLevelLabel.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        UpdateHeroLevel();
    }

    void UpdateHeroLevel()
    {
        if (!hero || !heroLevelLabel || hero.Level == displayedLevel) return;
        displayedLevel = hero.Level;
        heroLevelLabel.text = $"LV{displayedLevel}";
    }

    GameObject CreateQuad(string objectName, Transform parent, Material material,
        float width, float height, float depth)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = objectName;
        Collider collider = quad.GetComponent<Collider>();
        if (collider) Destroy(collider);
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = new Vector3(0f, 0f, depth);
        quad.transform.localScale = new Vector3(width, height, 1f);
        Renderer renderer = quad.GetComponent<Renderer>();
        if (renderer)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        return quad;
    }

    void PositionBar()
    {
        if (!owner) return;
        if (!viewCamera) viewCamera = Camera.main;
        barRoot.position = owner.transform.position + Vector3.up * verticalOffset;
        if (viewCamera)
            barRoot.rotation = Quaternion.LookRotation(-viewCamera.transform.forward, viewCamera.transform.up);
        float zoomScale = viewCamera && viewCamera.orthographic
            ? Mathf.Clamp(viewCamera.orthographicSize / 12f, .82f, 1.55f) : 1f;
        barRoot.localScale = Vector3.one * zoomScale;
    }

    void UpdateFill()
    {
        if (!fill) return;
        fill.localScale = new Vector3(Mathf.Max(.001f, barWidth * fillAmount), BarHeight * .62f, 1f);
        fill.localPosition = new Vector3(barCenterX - barWidth * (1f - fillAmount) * .5f, 0f, -.003f);
    }
}
