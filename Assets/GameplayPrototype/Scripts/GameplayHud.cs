using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Responsive, safe-area-aware mobile overlay for the lane prototype.</summary>
[DefaultExecutionOrder(-50)]
public sealed class GameplayHud : MonoBehaviour
{
    const int HudLayoutVersion = 4;
    [Min(120f)] public float abilityButtonSize = 176f;
    [Min(8f)] public float safeAreaPadding = 18f;
    [Min(200f)] public float minimapWidth = 436f;
    [Min(140f)] public float minimapHeight = 280f;
    public MatchLoopController matchLoop;

    [Header("Fireball Button Layout")]
    [Range(.5f, 1.1f)] public float fireballFrameScale = 1f;
    [Range(.5f, 1.1f)] public float fireballBackgroundScale = 1f;
    [Range(.5f, 1.1f)] public float fireballIconScale = .68f;
    [Tooltip("Optional empty slots for future abilities. Hidden by default.")]
    public bool showFutureAbilityPlaceholders;

    [Header("Fireball Ability Art Overrides")]
    [Tooltip("Optional art overrides. Leave empty to load the integrated Fireball sprites from Resources.")]
    public Sprite fireballFrameOverride;
    public Sprite fireballBackgroundOverride;
    public Sprite fireballIconOverride;

    static readonly Color Cyan = new Color(.16f, .78f, 1f, .95f);
    static readonly Color Blue = new Color(.2f, .58f, .95f, .95f);
    static readonly Color Crimson = new Color(.95f, .28f, .3f, .95f);
    static readonly Color Gold = new Color(1f, .73f, .2f, 1f);
    static readonly Color IncomeGreen = new Color(.39f, .88f, .35f, 1f);
    static readonly Color TimerViolet = new Color(.48f, .68f, 1f, 1f);

    Canvas canvas;
    RectTransform safeRoot;
    Sprite circleSprite;
    Sprite capsuleSprite;
    Sprite buttonCyanSprite;
    Sprite buttonGreenSprite;
    Sprite buttonPurpleSprite;
    Sprite ornateButtonFrameSprite;
    Sprite panelFrameSprite;
    Sprite healthBarFrameSprite;
    Sprite blueHealthFillSprite;
    Sprite redHealthFillSprite;
    Sprite goldIconSprite;
    Sprite incomeIconSprite;
    Sprite heartIconSprite;
    Sprite unitsIconSprite;
    Sprite itemsIconSprite;
    Sprite backIconSprite;

    Button unitsToggle;
    Button itemsToggle;
    Button fireballButton;
    Button archerButton;
    Button knightButton;
    Button wizardButton;
    Button restartButton;
    Image unitsToggleSelected;
    Image itemsToggleSelected;
    AbilityButtonView fireballView;
    Text goldLabel;
    Text incomeLabel;
    Text payoutLabel;
    Text blueBaseLabel;
    Text redBaseLabel;
    Image blueBaseFill;
    Image redBaseFill;
    Text resultLabel;
    Text archerLabel;
    Text knightLabel;
    Text wizardLabel;
    GameObject unitsPanel;
    GameObject itemsPanel;
    GameObject resultPanel;
    SchematicMinimapHud minimapHud;
    float activeMinimapWidth;
    float activeMinimapHeight;
    bool topHudTwoRows;
    FireballAbility ability;
    FriendlyUnitSender sender;
    float nextReferenceSearch;
    int lastGold = -1;
    int lastIncome = -1;
    int lastCountdown = -1;
    int lastBlueBase = -1;
    int lastRedBase = -1;
    int lastArcherCost = -1;
    int lastKnightCost = -1;
    int lastWizardCost = -1;
    bool reportedRecovery;
    int builtHudLayoutVersion;

    void Awake()
    {
        ConfigureLandscape();
        EnsureEventSystem();
        LoadFantasySprites();
        BuildCanvas();
    }

    void OnEnable()
    {
        // With Unity's reload-scene/domain options disabled, a subsequent Play
        // session can enable this component without constructing the overlay anew.
        if (Application.isPlaying && (builtHudLayoutVersion != HudLayoutVersion ||
            !canvas || !safeRoot || !unitsToggle || !fireballButton))
        {
            EnsureEventSystem();
            LoadFantasySprites();
            BuildCanvas();
        }
    }

    void Start()
    {
        // Start is still invoked when Enter Play Mode has scene/domain reloads
        // disabled. Awake/OnEnable alone are not a reliable reconstruction point
        // for this runtime-created overlay after Unity restores a backup scene.
        EnsureHudInitialized();
    }

    void EnsureHudInitialized()
    {
        if (builtHudLayoutVersion == HudLayoutVersion && canvas && safeRoot && unitsToggle && itemsToggle && fireballButton &&
            goldLabel && incomeLabel && payoutLabel && blueBaseLabel && redBaseLabel)
        {
            canvas.gameObject.SetActive(true);
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            safeRoot.gameObject.SetActive(true);
            unitsToggle.gameObject.SetActive(true);
            itemsToggle.gameObject.SetActive(true);
            fireballButton.gameObject.SetActive(true);
            return;
        }

        EnsureEventSystem();
        LoadFantasySprites();
        BuildCanvas();
        if (!reportedRecovery)
        {
            Debug.Log("[HLW HUD] Fantasy HUD overlay initialized/recovered.", this);
            reportedRecovery = true;
        }
    }

    void LoadFantasySprites()
    {
        buttonCyanSprite = Resources.Load<Sprite>("HlwFantasyUI/Buttons/Button6_cyan");
        buttonGreenSprite = Resources.Load<Sprite>("HlwFantasyUI/Buttons/Button6_green");
        buttonPurpleSprite = Resources.Load<Sprite>("HlwFantasyUI/Buttons/Button6_purple");
        circleSprite = Resources.Load<Sprite>("HlwFantasyUI/Buttons/Button6_ultramarine");
        ornateButtonFrameSprite = Resources.Load<Sprite>("HlwFantasyUI/Buttons/FantasyButton1_ultramarine");
        capsuleSprite = Resources.Load<Sprite>("HlwFantasyUI/Frames/Container3_blue");
        panelFrameSprite = Resources.Load<Sprite>("HlwFantasyUI/Frames/Container1_default");
        healthBarFrameSprite = Resources.Load<Sprite>("HlwFantasyUI/Bars/ProgressBarBackground4");
        blueHealthFillSprite = Resources.Load<Sprite>("HlwFantasyUI/Bars/ProgressBarMaxFill5_blue");
        redHealthFillSprite = Resources.Load<Sprite>("HlwFantasyUI/Bars/ProgressBarMaxFill5_yellow");
        goldIconSprite = Resources.Load<Sprite>("HlwFantasyUI/Icons/Icon79_256px");
        incomeIconSprite = Resources.Load<Sprite>("HlwFantasyUI/Icons/Icon97_256px");
        heartIconSprite = Resources.Load<Sprite>("HlwFantasyUI/Icons/Icon81_256px");
        unitsIconSprite = Resources.Load<Sprite>("HlwFantasyUI/Icons/Icon54_256px");
        itemsIconSprite = Resources.Load<Sprite>("HlwFantasyUI/Icons/Icon44_256px");
        backIconSprite = Resources.Load<Sprite>("HlwFantasyUI/Icons/Icon86_256px");

        if (!fireballFrameOverride)
            fireballFrameOverride = Resources.Load<Sprite>("HlwFireball/FireballFrame");
        if (!fireballBackgroundOverride)
            fireballBackgroundOverride = Resources.Load<Sprite>("HlwFireball/FireballBlueBackground");
        if (!fireballIconOverride)
            fireballIconOverride = Resources.Load<Sprite>("HlwFireball/FireballIcon");

        if (!circleSprite || !capsuleSprite || !ornateButtonFrameSprite || !healthBarFrameSprite)
            Debug.LogWarning("Some Fantasy RPG HUD sprites are unavailable. The HUD will use dark readable fallbacks. Run Tools > Hero Line Wars > Configure Fantasy RPG HUD Sprites.", this);
    }

    void Update()
    {
        EnsureHudInitialized();
        if ((!ability || !sender || !matchLoop) && Time.unscaledTime >= nextReferenceSearch)
        {
            if (!ability) ability = FindAnyObjectByType<FireballAbility>();
            if (!sender) sender = FindAnyObjectByType<FriendlyUnitSender>();
            if (!matchLoop) matchLoop = GetComponent<MatchLoopController>() ?? FindAnyObjectByType<MatchLoopController>();
            nextReferenceSearch = Time.unscaledTime + .25f;
        }

        bool running = matchLoop && matchLoop.IsRunning;
        UpdateFireball(running);
        if (sender)
        {
            UpdatePurchaseVisual(archerButton, archerLabel, sender.CanPurchase(UnitArchetype.GreenArcher));
            UpdatePurchaseVisual(knightButton, knightLabel, sender.CanPurchase(UnitArchetype.HolyKnight));
            UpdatePurchaseVisual(wizardButton, wizardLabel, sender.CanPurchase(UnitArchetype.BlueWizard));
            UpdateUnitLabels();
        }

        int gold = matchLoop ? matchLoop.Gold : 0;
        int income = matchLoop ? matchLoop.Income : 0;
        int countdown = matchLoop ? Mathf.CeilToInt(matchLoop.PayoutCountdown) : 0;
        int blueBase = matchLoop ? matchLoop.BlueBaseHealth : 0;
        int redBase = matchLoop ? matchLoop.RedBaseHealth : 0;
        if (goldLabel && gold != lastGold) { goldLabel.text = gold.ToString(); lastGold = gold; }
        if (incomeLabel && income != lastIncome) { incomeLabel.text = $"+{income}"; lastIncome = income; }
        if (payoutLabel && countdown != lastCountdown)
        { payoutLabel.text = $"{countdown}s"; lastCountdown = countdown; }
        if (blueBaseLabel && blueBase != lastBlueBase)
        { blueBaseLabel.text = $"{blueBase} HP"; lastBlueBase = blueBase; }
        if (redBaseLabel && redBase != lastRedBase)
        { redBaseLabel.text = $"{redBase} HP"; lastRedBase = redBase; }
        int maxBase = matchLoop ? Mathf.Max(1, matchLoop.startingBaseHealth) : 100;
        float blueFill = Mathf.Clamp01(blueBase / (float)maxBase);
        float redFill = Mathf.Clamp01(redBase / (float)maxBase);
        if (blueBaseFill && !Mathf.Approximately(blueBaseFill.fillAmount, blueFill)) blueBaseFill.fillAmount = blueFill;
        if (redBaseFill && !Mathf.Approximately(redBaseFill.fillAmount, redFill)) redBaseFill.fillAmount = redFill;

        if (resultPanel)
        {
            bool ended = matchLoop && matchLoop.Result != MatchResult.None;
            resultPanel.SetActive(ended);
            if (ended && resultLabel)
                resultLabel.text = matchLoop.Result == MatchResult.Victory ? "VICTORY" : "DEFEAT";
        }
    }

    void LateUpdate()
    {
        if (!safeRoot || !canvas || Screen.width <= 0 || Screen.height <= 0) return;
        Rect safe = Screen.safeArea;
        if (safe.width < 1f || safe.height < 1f)
            safe = new Rect(0f, 0f, Screen.width, Screen.height);
        safeRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        safeRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        ResizeAbilityButton();
    }

    void BuildCanvas()
    {
        Transform existing = transform.Find("GameplayHUD");
        if (existing)
        {
            canvas = existing.GetComponent<Canvas>();
            if (!canvas) canvas = existing.gameObject.AddComponent<Canvas>();
            existing.gameObject.SetActive(true);
            safeRoot = existing.Find("SafeArea") as RectTransform;
            if (!safeRoot)
            {
                safeRoot = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
                safeRoot.SetParent(existing, false);
            }
        }
        else
        {
            GameObject canvasObject = new GameObject("GameplayHUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            safeRoot = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            safeRoot.SetParent(canvas.transform, false);
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.enabled = true;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 50;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = canvasRect.offsetMax = Vector2.zero;
        canvasRect.localScale = Vector3.one;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (!scaler) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.enabled = true;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (!raycaster) raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = true;
        safeRoot.anchorMin = Vector2.zero;
        safeRoot.anchorMax = Vector2.one;
        safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        safeRoot.localScale = Vector3.one;
        ResolveTopHudLayout(scaler);
        safeRoot.gameObject.SetActive(true);

        // Rebuild the existing HUD root in-place so editor/runtime regeneration never
        // leaves a second set of controls or stale purchase buttons behind.
        for (int i = safeRoot.childCount - 1; i >= 0; i--) Destroy(safeRoot.GetChild(i).gameObject);

        BuildTopInfo();
        BuildMinimap();
        BuildUnitMenu();
        BuildItemsPanel();
        BuildAbilityControls();
        BuildResultPanel();
        builtHudLayoutVersion = HudLayoutVersion;
    }

    void BuildTopInfo()
    {
        const float widgetWidth = 174f;
        const float widgetHeight = 72f;
        const float gap = 8f;
        Vector2 widgetSize = new Vector2(widgetWidth, widgetHeight);
        int firstRowCount = topHudTwoRows ? 3 : 5;
        float topY = -safeAreaPadding;
        float secondY = -safeAreaPadding - widgetHeight - gap;

        goldLabel = CreateTopStatWidget("GoldPanel", "GOLD", safeRoot,
            new Vector2(-safeAreaPadding - (firstRowCount - 1) * (widgetWidth + gap), topY),
            widgetSize, Gold, goldIconSprite, false, true);
        incomeLabel = CreateTopStatWidget("IncomePanel", "INCOME", safeRoot,
            new Vector2(-safeAreaPadding - (firstRowCount - 2) * (widgetWidth + gap), topY),
            widgetSize, IncomeGreen, incomeIconSprite, false, true);
        payoutLabel = CreateTopStatWidget("NextPanel", "NEXT", safeRoot,
            new Vector2(-safeAreaPadding - (firstRowCount - 3) * (widgetWidth + gap), topY),
            widgetSize, TimerViolet, null, true, true);

        int baseRowCount = topHudTwoRows ? 2 : 5;
        float baseY = topHudTwoRows ? secondY : topY;
        int redIndex = topHudTwoRows ? 0 : 3;
        int blueIndex = topHudTwoRows ? 1 : 4;
        CreateHealthWidget("RedBasePanel", "RED BASE", safeRoot,
            new Vector2(-safeAreaPadding - (baseRowCount - 1 - redIndex) * (widgetWidth + gap), baseY),
            widgetSize, Crimson, redHealthFillSprite, false, out redBaseLabel, out redBaseFill);
        CreateHealthWidget("BlueBasePanel", "BLUE BASE", safeRoot,
            new Vector2(-safeAreaPadding - (baseRowCount - 1 - blueIndex) * (widgetWidth + gap), baseY),
            widgetSize, Blue, blueHealthFillSprite, true, out blueBaseLabel, out blueBaseFill);
    }

    void ResolveTopHudLayout(CanvasScaler scaler)
    {
        float uiScale = Mathf.Max(.01f, scaler ? scaler.scaleFactor : 1f);
        float safeWidth = (Screen.safeArea.width > 0f ? Screen.safeArea.width : Screen.width) / uiScale;
        const float widgetWidth = 174f;
        const float widgetGap = 8f;
        float singleRowWidth = widgetWidth * 5f + widgetGap * 4f;
        float threeWidgetWidth = widgetWidth * 3f + widgetGap * 2f;
        float margins = safeAreaPadding * 2f + 24f;
        topHudTwoRows = safeWidth < minimapWidth + singleRowWidth + margins;
        activeMinimapWidth = minimapWidth;
        if (safeWidth < activeMinimapWidth + (topHudTwoRows ? threeWidgetWidth : singleRowWidth) + margins)
            activeMinimapWidth = Mathf.Min(activeMinimapWidth, Mathf.Max(240f,
                (safeWidth - (topHudTwoRows ? threeWidgetWidth : singleRowWidth) - margins) * .88f));
        activeMinimapHeight = minimapHeight * (activeMinimapWidth / Mathf.Max(1f, minimapWidth));
    }

    void BuildMinimap()
    {
        if (!minimapHud) minimapHud = canvas.GetComponent<SchematicMinimapHud>();
        if (!minimapHud) minimapHud = canvas.gameObject.AddComponent<SchematicMinimapHud>();
        minimapHud.Build(safeRoot, panelFrameSprite, circleSprite, new Vector2(safeAreaPadding, -safeAreaPadding),
            new Vector2(activeMinimapWidth, activeMinimapHeight));
    }

    void BuildUnitMenu()
    {
        // A transparent anchored group lets the three purchase buttons fan out
        // above the primary Units control without drawing a rectangular tray.
        unitsPanel = new GameObject("UnitsFanMenu", typeof(RectTransform));
        unitsPanel.transform.SetParent(safeRoot, false);
        RectTransform fanRect = unitsPanel.GetComponent<RectTransform>();
        fanRect.anchorMin = fanRect.anchorMax = Vector2.zero;
        fanRect.pivot = Vector2.zero;
        fanRect.anchoredPosition = new Vector2(safeAreaPadding, safeAreaPadding);
        fanRect.sizeDelta = new Vector2(350f, 310f);
        unitsPanel.SetActive(false);
        CreatePurchaseCard("SendArcherButton", "A", "ARCHER", new Vector2(0f, 144f), out archerButton, out archerLabel);
        CreatePurchaseCard("SendKnightButton", "K", "KNIGHT", new Vector2(120f, 190f), out knightButton, out knightLabel);
        CreatePurchaseCard("SendWizardButton", "W", "WIZARD", new Vector2(240f, 144f), out wizardButton, out wizardLabel);
        archerButton.onClick.AddListener(SendArcher);
        knightButton.onClick.AddListener(SendKnight);
        wizardButton.onClick.AddListener(SendWizard);

        CreateButton("UnitsToggle", safeRoot, "UNITS", new Vector2(safeAreaPadding, safeAreaPadding),
            new Vector2(128f, 128f), true, Cyan, out unitsToggle, out _);
        CreateButton("ItemsToggle", safeRoot, "ITEMS", new Vector2(safeAreaPadding + 148f, safeAreaPadding + 8f),
            new Vector2(112f, 112f), true, Cyan, out itemsToggle, out _);
        unitsToggleSelected = CreateSelectedButtonLayer(unitsToggle.transform);
        itemsToggleSelected = CreateSelectedButtonLayer(itemsToggle.transform);
        unitsToggle.onClick.AddListener(ToggleUnitsPanel);
    }

    void CreatePurchaseCard(string objectName, string emblem, string unitName, Vector2 position,
        out Button button, out Text purchaseLabel)
    {
        GameObject card = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        card.transform.SetParent(unitsPanel.transform, false);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(100f, 100f);
        Image background = card.GetComponent<Image>();
        background.sprite = buttonGreenSprite ? buttonGreenSprite : circleSprite;
        background.color = background.sprite ? Color.white : new Color(.045f, .14f, .2f, .98f);
        background.raycastTarget = true;
        button = card.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        SetButtonColors(button, Cyan);
        Outline outline = card.GetComponent<Outline>();
        outline.effectColor = new Color(.2f, .72f, .92f, .7f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);
        AddShadow(background, new Color(0f, .01f, .025f, .64f), new Vector2(0f, -3f));
        Image frame = CreateImage("FantasyFrame", card.transform, ornateButtonFrameSprite,
            Vector2.zero, new Vector2(108f, 108f), Color.white, false);
        frame.preserveAspect = true;
        Text crest = CreateText("UnitEmblem", card.transform, emblem, new Vector2(0f, 16f),
            new Vector2(44f, 40f), 28, TextAnchor.MiddleCenter, new Color(.91f, .97f, 1f), false);
        AddTextShadow(crest);
        purchaseLabel = CreateText("UnitAndCost", card.transform, unitName + "\n1 GOLD",
            new Vector2(0f, -30f), new Vector2(92f, 38f), 13, TextAnchor.MiddleCenter, Color.white, false);
        AddTextShadow(purchaseLabel);
    }

    Image CreateSelectedButtonLayer(Transform buttonRoot)
    {
        Image glow = CreateImage("SelectedGlow", buttonRoot, buttonGreenSprite ? buttonGreenSprite : circleSprite,
            Vector2.zero, ((RectTransform)buttonRoot).sizeDelta * 1.14f,
            new Color(.35f, .9f, 1f, 0f), false);
        glow.preserveAspect = true;
        glow.transform.SetAsFirstSibling();
        return glow;
    }

    void BuildItemsPanel()
    {
        itemsPanel = CreatePanel("ItemsPanel", safeRoot, new Vector2(safeAreaPadding + 148f, 144f),
            new Vector2(220f, 92f), TextAnchor.LowerLeft);
        Image icon = CreateImage("ItemsPanelIcon", itemsPanel.transform, itemsIconSprite,
            new Vector2(-72f, 3f), new Vector2(38f, 38f), Color.white, false);
        icon.preserveAspect = true;
        CreateText("ItemsPanelTitle", itemsPanel.transform, "ITEMS", new Vector2(26f, 18f),
            new Vector2(138f, 24f), 15, TextAnchor.MiddleLeft, new Color(.65f, .9f, 1f), false);
        CreateText("ComingSoon", itemsPanel.transform, "COMING SOON", new Vector2(26f, -12f),
            new Vector2(162f, 22f), 12, TextAnchor.MiddleLeft, Color.white, false);
        itemsPanel.SetActive(false);
        itemsToggle.onClick.AddListener(ToggleItemsPanel);
    }

    void BuildAbilityControls()
    {
        // RectTransform.rect is still zero during Awake. Size from the configured
        // artwork first, then adapt to the actual safe area in LateUpdate.
        float size = abilityButtonSize;
        fireballView = AbilityButtonView.Create(safeRoot, "FireballButton",
            new Vector2(size, size),
            new Vector2(-safeAreaPadding, safeAreaPadding), true,
            fireballFrameOverride ? fireballFrameOverride : ornateButtonFrameSprite,
            fireballBackgroundOverride ? fireballBackgroundOverride : circleSprite,
            fireballIconOverride, buttonCyanSprite);
        fireballView.SetLayerScales(fireballFrameScale, fireballBackgroundScale, fireballIconScale);
        fireballButton = fireballView.Button;
        fireballView.SetLabel("FIREBALL");
        fireballButton.onClick.AddListener(ToggleFireballTargeting);

        if (showFutureAbilityPlaceholders)
        {
            CreatePlaceholderAbility("AbilitySlot2", new Vector2(-safeAreaPadding - size - 12f, safeAreaPadding + 6f));
            CreatePlaceholderAbility("AbilitySlot3", new Vector2(-safeAreaPadding - size - 84f, safeAreaPadding + 6f));
        }
    }

    void ResizeAbilityButton()
    {
        if (!fireballButton || !safeRoot) return;
        float height = safeRoot.rect.height;
        float width = safeRoot.rect.width;
        if (height < 1f || width < 1f) return;
        float size = Mathf.Min(abilityButtonSize,
            (height - safeAreaPadding - 4f) / 1.045f,
            (width - safeAreaPadding - 4f) / 1.045f);
        size = Mathf.Max(72f, size);
        RectTransform rect = fireballButton.GetComponent<RectTransform>();
        if (Mathf.Abs(rect.sizeDelta.x - size) < .5f) return;
        rect.sizeDelta = new Vector2(size, size);
    }

    void CreatePlaceholderAbility(string name, Vector2 position)
    {
        GameObject slot = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
        slot.transform.SetParent(safeRoot, false);
        RectTransform rect = slot.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(62f, 62f);
        Image image = slot.GetComponent<Image>();
        image.sprite = circleSprite;
        image.color = circleSprite ? Color.white : new Color(.06f, .15f, .23f, .9f);
        image.raycastTarget = true;
        Outline outline = slot.GetComponent<Outline>();
        outline.effectColor = new Color(.2f, .6f, .78f, .42f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);
        Image rim = CreateImage("OrnateFrame", slot.transform, ornateButtonFrameSprite,
            Vector2.zero, new Vector2(68f, 54f), Color.white, false);
        rim.preserveAspect = true;
        CreateText("EmptySlotLabel", slot.transform, "-", Vector2.zero, new Vector2(62f, 62f),
            22, TextAnchor.MiddleCenter, new Color(.65f, .8f, .88f, .75f), false);
    }

    void BuildResultPanel()
    {
        resultPanel = CreatePanel("MatchResultPanel", safeRoot, Vector2.zero, new Vector2(500f, 280f), TextAnchor.MiddleCenter);
        RectTransform panelRect = resultPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
        panelRect.pivot = new Vector2(.5f, .5f);
        resultLabel = CreateText("ResultLabel", resultPanel.transform, "VICTORY", new Vector2(0f, 92f),
            new Vector2(460f, 90f), 48, TextAnchor.MiddleCenter, Color.white, false);
        CreateButton("RestartButton", resultPanel.transform, "RESTART MATCH", new Vector2(80f, 22f),
            new Vector2(340f, 72f), false, Cyan, out restartButton, out _);
        restartButton.onClick.AddListener(RestartMatch);
        resultPanel.SetActive(false);
    }

    void UpdateFireball(bool running)
    {
        if (!fireballButton || !fireballView) return;
        bool targeting = ability && ability.IsTargeting;
        float remaining = ability ? ability.CooldownRemaining : 0f;
        fireballView.SetInteractable(running && ability && (targeting || remaining <= 0f));
        fireballView.SetLabel(targeting ? "AIM / TAP" : "FIREBALL");
        float cooldownFraction = ability && ability.cooldown > 0f ? remaining / ability.cooldown : 0f;
        fireballView.SetCooldown(cooldownFraction, Mathf.CeilToInt(remaining));
        fireballView.SetSelected(targeting);
    }

    void UpdateUnitLabels()
    {
        int archerCost = sender.Cost(UnitArchetype.GreenArcher);
        int knightCost = sender.Cost(UnitArchetype.HolyKnight);
        int wizardCost = sender.Cost(UnitArchetype.BlueWizard);
        if (archerCost != lastArcherCost)
        { archerLabel.text = $"ARCHER\n{archerCost} GOLD"; lastArcherCost = archerCost; }
        if (knightCost != lastKnightCost)
        { knightLabel.text = $"KNIGHT\n{knightCost} GOLD"; lastKnightCost = knightCost; }
        if (wizardCost != lastWizardCost)
        { wizardLabel.text = $"WIZARD\n{wizardCost} GOLD"; lastWizardCost = wizardCost; }
    }

    void ToggleUnitsPanel()
    {
        bool open = !unitsPanel.activeSelf;
        unitsPanel.SetActive(open);
        itemsPanel.SetActive(false);
        SetButtonSelected(unitsToggle, unitsToggleSelected, open);
        SetButtonSelected(itemsToggle, itemsToggleSelected, false);
        SetButtonColors(unitsToggle, open ? Cyan : Blue);
        SetButtonColors(itemsToggle, Blue);
    }

    void ToggleItemsPanel()
    {
        bool open = !itemsPanel.activeSelf;
        itemsPanel.SetActive(open);
        unitsPanel.SetActive(false);
        SetButtonSelected(itemsToggle, itemsToggleSelected, open);
        SetButtonSelected(unitsToggle, unitsToggleSelected, false);
        SetButtonColors(itemsToggle, open ? Cyan : Blue);
        SetButtonColors(unitsToggle, Blue);
    }

    void SetButtonSelected(Button button, Image selectedLayer, bool selected)
    {
        if (selectedLayer)
        {
            selectedLayer.color = selected ? new Color(.35f, .9f, 1f, .34f) : new Color(.35f, .9f, 1f, 0f);
            selectedLayer.transform.SetAsFirstSibling();
        }
        if (button) button.transform.localScale = selected ? Vector3.one * 1.055f : Vector3.one;
        SetButtonSkin(button, selected ? buttonGreenSprite : buttonCyanSprite,
            new Color(.06f, selected ? .28f : .18f, .25f, .98f));
    }

    void ToggleFireballTargeting()
    {
        if (!ability) ability = FindAnyObjectByType<FireballAbility>();
        if (!ability) return;
        if (ability.IsTargeting) ability.CancelTargeting();
        else if (ability.BeginTargeting())
        {
            unitsPanel.SetActive(false);
            itemsPanel.SetActive(false);
        }
    }

    void SendArcher() { if (!sender) sender = FindAnyObjectByType<FriendlyUnitSender>(); if (sender) sender.SendArcher(); }
    void SendKnight() { if (!sender) sender = FindAnyObjectByType<FriendlyUnitSender>(); if (sender) sender.SendKnight(); }
    void SendWizard() { if (!sender) sender = FindAnyObjectByType<FriendlyUnitSender>(); if (sender) sender.SendWizard(); }
    void RestartMatch() { if (!matchLoop) matchLoop = GetComponent<MatchLoopController>(); if (matchLoop) matchLoop.RestartMatch(); }

    Text CreateTopStatWidget(string name, string title, Transform parent, Vector2 position,
        Vector2 size, Color accent, Sprite iconSprite, bool useClockGlyph, bool topRight = false)
    {
        GameObject panel = CreateFantasyTopFrame(name, parent, position, size, accent, topRight);
        float iconX = -size.x * .5f + 28f;
        Sprite badgeSprite = useClockGlyph && buttonPurpleSprite ? buttonPurpleSprite : circleSprite;
        Image badge = CreateImage("IconWell", panel.transform, badgeSprite,
            new Vector2(iconX, 0f), new Vector2(39f, 39f),
            Color.Lerp(new Color(.3f, .35f, .48f, 1f), accent, .48f), false);
        badge.preserveAspect = true;

        if (iconSprite)
        {
            Image icon = CreateImage("Icon", panel.transform, iconSprite,
                new Vector2(iconX, 0f), new Vector2(29f, 29f), Color.white, false);
            icon.preserveAspect = true;
        }
        else if (useClockGlyph)
            CreateClockGlyph(panel.transform, new Vector2(iconX, 0f), accent);

        float textX = 24f;
        Text caption = CreateText("Caption", panel.transform, title,
            new Vector2(textX, 14f), new Vector2(size.x - 70f, 16f), 10,
            TextAnchor.MiddleLeft, new Color(accent.r, accent.g, accent.b, .95f), false);
        AddTextShadow(caption);
        Text value = CreateText("Value", panel.transform, string.Empty,
            new Vector2(textX, -8f), new Vector2(size.x - 70f, 25f), 18,
            TextAnchor.MiddleLeft, title == "GOLD" ? new Color(1f, .88f, .52f) : Color.white, false);
        AddTextShadow(value);
        return value;
    }

    GameObject CreateFantasyTopFrame(string name, Transform parent, Vector2 position,
        Vector2 size, Color accent, bool topRight)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = topRight ? Vector2.one : new Vector2(0f, 1f);
        rect.pivot = topRight ? Vector2.one : new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image frame = panel.GetComponent<Image>();
        frame.sprite = panelFrameSprite ? panelFrameSprite : capsuleSprite;
        frame.type = frame.sprite ? Image.Type.Sliced : Image.Type.Simple;
        frame.color = frame.sprite
            ? Color.Lerp(new Color(.13f, .17f, .24f, .99f), accent, .13f)
            : new Color(.025f, .04f, .07f, .96f);
        frame.raycastTarget = false;
        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = new Color(accent.r, accent.g, accent.b, .48f);
        outline.effectDistance = new Vector2(1.25f, 1.25f);
        outline.useGraphicAlpha = true;
        AddShadow(frame, new Color(.005f, .01f, .025f, .78f), new Vector2(0f, -4f));

        Image inset = CreateImage("DarkInset", panel.transform, capsuleSprite,
            new Vector2(0f, -1f), new Vector2(size.x - 16f, size.y - 15f),
            new Color(.035f, .052f, .085f, .88f), false);
        inset.type = capsuleSprite ? Image.Type.Sliced : Image.Type.Simple;
        inset.preserveAspect = false;
        CreateSolidImage("AccentInlay", panel.transform,
            new Vector2(0f, -size.y * .5f + 8f), new Vector2(size.x - 30f, 2f),
            new Color(accent.r, accent.g, accent.b, .78f));
        CreateSolidImage("AccentGlow", panel.transform,
            new Vector2(0f, -size.y * .5f + 8f), new Vector2(size.x - 48f, 5f),
            new Color(accent.r, accent.g, accent.b, .12f));
        return panel;
    }

    void CreateClockGlyph(Transform parent, Vector2 center, Color accent)
    {
        CreateSolidImage("ClockMinuteHand", parent, center + new Vector2(0f, 4f),
            new Vector2(2.5f, 12f), Color.white);
        Image hourHand = CreateSolidImage("ClockHourHand", parent,
            center + new Vector2(4f, -1f), new Vector2(9f, 2.5f),
            new Color(.86f, .94f, 1f));
        hourHand.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        CreateSolidImage("ClockHub", parent, center, new Vector2(4f, 4f),
            new Color(accent.r, accent.g, accent.b, 1f));
    }

    void CreateHealthWidget(string name, string title, Transform parent, Vector2 position,
        Vector2 size, Color accent, Sprite fillSprite, bool mirrored,
        out Text value, out Image fill)
    {
        GameObject card = CreateFantasyTopFrame(name, parent, position, size, accent, true);
        float iconX = mirrored ? size.x * .5f - 27f : -size.x * .5f + 27f;
        float contentX = mirrored ? -22f : 22f;
        float contentWidth = size.x - 64f;
        TextAnchor alignment = mirrored ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;

        Image crestWell = CreateImage("CrestWell", card.transform, circleSprite,
            new Vector2(iconX, 4f), new Vector2(43f, 43f),
            Color.Lerp(new Color(.28f, .32f, .45f, 1f), accent, .5f), false);
        crestWell.preserveAspect = true;
        Image crest = CreateImage("BaseCrest", card.transform, heartIconSprite,
            new Vector2(iconX, 4f), new Vector2(30f, 30f),
            new Color(1f, .96f, .98f), false);
        crest.preserveAspect = true;
        Text caption = CreateText("Caption", card.transform, title,
            new Vector2(contentX, 17f), new Vector2(contentWidth, 17f), 10,
            alignment, new Color(accent.r, accent.g, accent.b, .96f), false);
        AddTextShadow(caption);
        value = CreateText("Value", card.transform, string.Empty,
            new Vector2(contentX, 1f), new Vector2(contentWidth, 22f),
            17, alignment, Color.white, false);
        AddTextShadow(value);

        fill = CreateImage("HealthFill", card.transform, fillSprite,
            new Vector2(contentX, -21f), new Vector2(contentWidth - 10f, 7f),
            title == "RED BASE" ? new Color(1f, .2f, .23f, 1f) : new Color(.28f, .68f, 1f), false);
        if (!fillSprite) fill.color = accent;
        fill.preserveAspect = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        Image barFrame = CreateImage("HealthFrame", card.transform, healthBarFrameSprite,
            new Vector2(contentX, -21f), new Vector2(contentWidth, 15f),
            new Color(.78f, .87f, .94f, 1f), false);
        barFrame.type = healthBarFrameSprite ? Image.Type.Sliced : Image.Type.Simple;
        barFrame.preserveAspect = false;
    }

    GameObject CreatePanel(string name, Transform parent, Vector2 position, Vector2 size, TextAnchor anchor)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor == TextAnchor.MiddleCenter
            ? new Vector2(.5f, .5f) : new Vector2(0f, 0f);
        rect.pivot = anchor == TextAnchor.MiddleCenter ? new Vector2(.5f, .5f) : Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = panel.GetComponent<Image>();
        image.sprite = name == "MatchResultPanel" ? panelFrameSprite : capsuleSprite;
        image.type = image.sprite ? Image.Type.Sliced : Image.Type.Simple;
        image.color = image.sprite
            ? (name == "MatchResultPanel" ? new Color(.36f, .48f, .7f, .96f) : Color.white)
            : new Color(.025f, .055f, .095f, .96f);
        image.raycastTarget = true;
        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = new Color(.16f, .62f, .82f, .65f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);
        AddShadow(image, new Color(0f, .01f, .025f, .7f), new Vector2(0f, -4f));
        return panel;
    }

    void CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size,
        bool circle, Color accent, out Button button, out Text text, bool bottomRight = false)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        if (bottomRight)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
        }
        else
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = Vector2.zero;
        }
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = buttonObject.GetComponent<Image>();
        image.sprite = ButtonSprite(name);
        image.type = image.sprite && !circle ? Image.Type.Sliced : Image.Type.Simple;
        image.color = image.sprite ? Color.white : FallbackButtonColor(name);
        image.raycastTarget = true;
        button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        SetButtonColors(button, accent);
        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = accent;
        outline.effectDistance = circle ? new Vector2(2f, 2f) : new Vector2(1.5f, 1.5f);
        outline.useGraphicAlpha = true;
        AddShadow(image, new Color(0f, .01f, .025f, .68f), new Vector2(0f, -4f));
        if (circle && ornateButtonFrameSprite)
        {
            Image frame = CreateImage("FantasyFrame", buttonObject.transform, ornateButtonFrameSprite,
                Vector2.zero, size * 1.1f, Color.white, false);
            frame.preserveAspect = true;
        }

        Sprite iconSprite = ButtonIcon(name);
        if (circle && iconSprite)
        {
            Image icon = CreateImage("Icon", buttonObject.transform, iconSprite,
                new Vector2(0f, size.y * .13f), new Vector2(size.x * .46f, size.y * .46f),
                Color.white, false);
            icon.preserveAspect = true;
        }
        text = CreateText("Label", buttonObject.transform, label,
            circle && iconSprite ? new Vector2(0f, -size.y * .31f) : Vector2.zero,
            circle && iconSprite ? new Vector2(size.x - 8f, size.y * .28f) : size,
            circle ? 14 : 19, TextAnchor.MiddleCenter, Color.white, false);
        text.raycastTarget = false;
        if (circle && iconSprite) AddTextShadow(text);
    }

    static void AddTextShadow(Text text)
    {
        if (!text || text.GetComponent<Shadow>()) return;
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(.005f, .015f, .035f, .9f);
        shadow.effectDistance = new Vector2(1f, -1f);
        shadow.useGraphicAlpha = true;
    }

    static void AddShadow(Graphic graphic, Color color, Vector2 distance)
    {
        Shadow shadow = graphic.gameObject.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    Image CreateSolidImage(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size,
        Color tint, bool raycastTarget)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = sprite ? tint : Color.clear;
        image.raycastTarget = raycastTarget;
        image.preserveAspect = true;
        return image;
    }

    Sprite ButtonSprite(string name)
    {
        if (name == "FireballButton") return circleSprite;
        if (name == "ItemsToggle") return circleSprite;
        if (name.StartsWith("Send", System.StringComparison.Ordinal)) return buttonGreenSprite;
        return buttonCyanSprite ? buttonCyanSprite : circleSprite;
    }

    Sprite ButtonIcon(string name)
    {
        if (name == "UnitsToggle") return unitsIconSprite;
        if (name == "ItemsToggle") return itemsIconSprite;
        if (name == "RestartButton") return backIconSprite;
        return null;
    }

    static Color FallbackButtonColor(string name)
    {
        if (name == "FireballButton") return new Color(.28f, .1f, .22f, .98f);
        if (name.StartsWith("Send", System.StringComparison.Ordinal)) return new Color(.08f, .2f, .27f, .98f);
        return new Color(.06f, .15f, .23f, .98f);
    }

    void SetButtonSkin(Button button, Sprite sprite, Color fallbackColor)
    {
        if (!button) return;
        Image image = button.GetComponent<Image>();
        if (!image) return;
        image.sprite = sprite;
        image.color = sprite ? Color.white : fallbackColor;
    }

    Text CreateText(string name, Transform parent, string value, Vector2 position, Vector2 size,
        int fontSize, TextAnchor alignment, Color color, bool raycastTarget)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = raycastTarget;
        return text;
    }

    static void SetButtonColors(Button button, Color accent)
    {
        if (!button) return;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(accent.r, accent.g, accent.b, .98f);
        colors.pressedColor = new Color(accent.r * .72f, accent.g * .72f, accent.b * .72f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(.18f, .22f, .27f, .52f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        Outline outline = button.GetComponent<Outline>();
        if (outline) outline.effectColor = accent;
    }

    void UpdatePurchaseVisual(Button button, Text label, bool affordable)
    {
        if (!button) return;
        button.interactable = affordable;
        Image background = button.GetComponent<Image>();
        Sprite stateSprite = affordable ? buttonGreenSprite : circleSprite;
        if (background)
        {
            if (background.sprite != stateSprite) background.sprite = stateSprite;
            background.color = stateSprite ? Color.white : FallbackButtonColor(button.name);
        }
        if (label) label.color = affordable ? Color.white : new Color(.58f, .65f, .7f, .58f);
        Outline outline = button.GetComponent<Outline>();
        if (outline) outline.effectColor = affordable ? Cyan : new Color(.2f, .38f, .48f, .32f);
    }

    static void ConfigureLandscape()
    {
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }

    static void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (!eventSystem) eventSystem = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
        GameObject eventObject = eventSystem ? eventSystem.gameObject : new GameObject("EventSystem", typeof(EventSystem));
        eventObject.SetActive(true);
        eventSystem = eventObject.GetComponent<EventSystem>();
        eventSystem.enabled = true;
        InputSystemUIInputModule module = eventObject.GetComponent<InputSystemUIInputModule>();
        if (!module) module = eventObject.AddComponent<InputSystemUIInputModule>();
        module.enabled = true;
        module.AssignDefaultActions();
        foreach (BaseInputModule other in eventObject.GetComponents<BaseInputModule>())
            if (other != module) other.enabled = false;
    }
}
