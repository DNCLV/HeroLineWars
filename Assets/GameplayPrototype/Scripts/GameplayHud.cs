using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Responsive, safe-area-aware mobile overlay for the lane prototype.</summary>
[DefaultExecutionOrder(-50)]
public sealed class GameplayHud : MonoBehaviour
{
    const int HudLayoutVersion = 15;
    [Min(120f)] public float abilityButtonSize = 176f;
    [Min(96f)] public float autoAttackButtonSize = 136f;
    [Min(64f)] public float abilitySlotSize = 78f;
    [Min(8f)] public float safeAreaPadding = 18f;
    [Min(200f)] public float minimapWidth = 320f;
    [Min(109f)] public float minimapHeight = 190f;
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
    static readonly Color ArcherAccent = new Color(.24f, .9f, .68f, 1f);
    static readonly Color KnightAccent = new Color(1f, .72f, .28f, 1f);
    static readonly Color WizardAccent = new Color(.67f, .46f, 1f, 1f);
    static readonly Color ControlNavy = new Color(.035f, .065f, .115f, .98f);

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
    Button speedToggle;
    Button fireballButton;
    Button autoAttackButton;
    Button archerButton;
    Button knightButton;
    Button wizardButton;
    Button restartButton;
    Button enemyHeroFocusButton;
    Button playerHeroFocusButton;
    Image unitsToggleSelected;
    Image itemsToggleSelected;
    Image speedToggleGlow;
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
    Text speedLabel;
    Text matchTimeLabel;
    Text heroLevelLabel;
    Text heroExperienceLabel;
    Image heroExperienceFill;
    readonly Image[] futureAbilitySlots = new Image[3];
    readonly Text[] futureAbilityLabels = new Text[3];
    GameObject unitsPanel;
    GameObject itemsPanel;
    GameObject resultPanel;
    SchematicMinimapHud minimapHud;
    float activeMinimapWidth;
    float activeMinimapHeight;
    bool topHudTwoRows;
    FireballAbility ability;
    HeroProgression heroProgression;
    HeroAbilityLoadout abilityLoadout;
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
    int lastSpeedMultiplier = -1;
    int lastElapsedSecond = -1;
    bool reportedRecovery;
    int builtHudLayoutVersion;

    void Awake()
    {
        HlwGameSpeed.ResetToNormal();
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
            !canvas || !safeRoot || !unitsToggle || !fireballButton || !autoAttackButton))
        {
            EnsureEventSystem();
            LoadFantasySprites();
            BuildCanvas();
        }
    }

    void OnDisable() { HlwGameSpeed.ResetToNormal(); }
    void OnDestroy() { HlwGameSpeed.ResetToNormal(); }
    void OnApplicationQuit() { HlwGameSpeed.ResetToNormal(); }

    void Start()
    {
        // Start is still invoked when Enter Play Mode has scene/domain reloads
        // disabled. Awake/OnEnable alone are not a reliable reconstruction point
        // for this runtime-created overlay after Unity restores a backup scene.
        EnsureHudInitialized();
    }

    void EnsureHudInitialized()
    {
        if (builtHudLayoutVersion == HudLayoutVersion && canvas && safeRoot && unitsToggle && itemsToggle && speedToggle &&
            fireballButton && autoAttackButton &&
            goldLabel && incomeLabel && payoutLabel && blueBaseLabel && redBaseLabel)
        {
            canvas.gameObject.SetActive(true);
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            safeRoot.gameObject.SetActive(true);
            unitsToggle.gameObject.SetActive(true);
            itemsToggle.gameObject.SetActive(true);
            speedToggle.gameObject.SetActive(true);
            fireballButton.gameObject.SetActive(true);
            autoAttackButton.gameObject.SetActive(true);
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
        if ((!ability || !sender || !matchLoop || !heroProgression) && Time.unscaledTime >= nextReferenceSearch)
        {
            if (!ability) ability = HeroProgression.PlayerHero
                ? HeroProgression.PlayerHero.GetComponent<FireballAbility>() : FindAnyObjectByType<FireballAbility>();
            if (!sender) sender = FindAnyObjectByType<FriendlyUnitSender>();
            if (!matchLoop) matchLoop = GetComponent<MatchLoopController>() ?? FindAnyObjectByType<MatchLoopController>();
            if (!heroProgression) heroProgression = HeroProgression.PlayerHero;
            if (heroProgression && !abilityLoadout) abilityLoadout = heroProgression.GetComponent<HeroAbilityLoadout>();
            nextReferenceSearch = Time.unscaledTime + .25f;
        }

        bool running = matchLoop && matchLoop.IsRunning;
        if (autoAttackButton)
            autoAttackButton.interactable = running && HeroProgression.PlayerHero &&
                HeroProgression.PlayerHero.Health && !HeroProgression.PlayerHero.Health.IsDead;
        UpdateFireball(running);
        UpdateHeroProgression();
        UpdateSpeedVisual();
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
        int elapsedSecond = matchLoop ? Mathf.FloorToInt(matchLoop.ElapsedTime) : 0;
        int blueBase = matchLoop ? matchLoop.BlueBaseHealth : 0;
        int redBase = matchLoop ? matchLoop.RedBaseHealth : 0;
        if (goldLabel && gold != lastGold) { goldLabel.text = gold.ToString(); lastGold = gold; }
        if (incomeLabel && income != lastIncome) { incomeLabel.text = $"+{income}"; lastIncome = income; }
        if (payoutLabel && countdown != lastCountdown)
        { payoutLabel.text = $"{countdown}s"; lastCountdown = countdown; }
        if (matchTimeLabel && elapsedSecond != lastElapsedSecond)
        {
            matchTimeLabel.text = $"{elapsedSecond / 60:00}:{elapsedSecond % 60:00}";
            lastElapsedSecond = elapsedSecond;
        }
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
                resultLabel.text = matchLoop.Result == MatchResult.SurvivalVictory
                    ? "SURVIVAL VICTORY" : matchLoop.Result == MatchResult.Victory ? "VICTORY" : "DEFEAT";
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
        BuildCameraFocusControls();
        BuildSpeedControl();
        BuildUnitMenu();
        BuildItemsPanel();
        BuildAbilityControls();
        BuildHeroProgressionPanel();
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
        float margins = safeAreaPadding * 2f + 24f;
        const float styledMinimapWidth = 320f;
        const float styledMinimapHeight = 190f;
        topHudTwoRows = safeWidth < styledMinimapWidth + singleRowWidth + margins;
        activeMinimapWidth = styledMinimapWidth;
        activeMinimapHeight = styledMinimapHeight;
    }

    void BuildMinimap()
    {
        if (!minimapHud) minimapHud = canvas.GetComponent<SchematicMinimapHud>();
        if (!minimapHud) minimapHud = canvas.gameObject.AddComponent<SchematicMinimapHud>();
        minimapHud.Build(safeRoot, panelFrameSprite, circleSprite, new Vector2(24f, -24f),
            new Vector2(activeMinimapWidth, activeMinimapHeight));
    }

    void BuildCameraFocusControls()
    {
        const float buttonHeight = 44f;
        const float gap = 8f;
        float firstY = -24f - activeMinimapHeight - gap;
        CreateButton("EnemyHeroFocusButton", safeRoot, "ENEMY HERO", Vector2.zero,
            new Vector2(activeMinimapWidth, buttonHeight), false, Crimson,
            out enemyHeroFocusButton, out _, false);
        PositionBelowMinimap(enemyHeroFocusButton, firstY);
        enemyHeroFocusButton.onClick.AddListener(() => FocusCamera(false));

        CreateButton("PlayerHeroFocusButton", safeRoot, "MY HERO", Vector2.zero,
            new Vector2(activeMinimapWidth, buttonHeight), false, Cyan,
            out playerHeroFocusButton, out _, false);
        PositionBelowMinimap(playerHeroFocusButton, firstY - buttonHeight - gap);
        playerHeroFocusButton.onClick.AddListener(() => FocusCamera(true));
    }

    static void PositionBelowMinimap(Button button, float y)
    {
        if (!button) return;
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, y);
    }

    void FocusCamera(bool playerHero)
    {
        Camera mainCamera = Camera.main;
        HlwCameraController controller = mainCamera ? mainCamera.GetComponent<HlwCameraController>() : null;
        if (!controller) controller = FindAnyObjectByType<HlwCameraController>();
        if (!controller) return;
        if (playerHero) controller.FocusOnPlayerHero();
        else controller.FocusOnEnemyHero();
    }

    void BuildSpeedControl()
    {
        GameObject speedObject = new GameObject("GameSpeedToggle", typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(Outline));
        speedObject.transform.SetParent(safeRoot, false);
        RectTransform rect = speedObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(364f, -24f);
        rect.sizeDelta = new Vector2(92f, 56f);

        Image background = speedObject.GetComponent<Image>();
        background.sprite = capsuleSprite;
        background.type = capsuleSprite ? Image.Type.Sliced : Image.Type.Simple;
        background.color = ControlNavy;
        background.raycastTarget = true;
        AddShadow(background, new Color(0f, .01f, .03f, .72f), new Vector2(0f, -4f));

        speedToggle = speedObject.GetComponent<Button>();
        speedToggle.targetGraphic = background;
        speedToggle.transition = Selectable.Transition.ColorTint;
        SetButtonColors(speedToggle, Cyan);
        MobileButtonPressFeedback feedback = speedObject.AddComponent<MobileButtonPressFeedback>();
        feedback.Configure(speedToggle);

        Outline outline = speedObject.GetComponent<Outline>();
        outline.effectColor = new Color(.22f, .58f, .82f, .68f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);
        speedToggleGlow = CreateImage("ActiveGlow", speedObject.transform,
            capsuleSprite, Vector2.zero, new Vector2(100f, 64f), new Color(.2f, .86f, 1f, 0f), false);
        speedToggleGlow.type = capsuleSprite ? Image.Type.Sliced : Image.Type.Simple;
        speedToggleGlow.preserveAspect = false;
        speedToggleGlow.transform.SetAsFirstSibling();
        speedLabel = CreateText("SpeedLabel", speedObject.transform, "1×", new Vector2(0f, 9f),
            new Vector2(82f, 25f), 18, TextAnchor.MiddleCenter, Color.white, false);
        AddTextShadow(speedLabel);
        matchTimeLabel = CreateText("MatchTime", speedObject.transform, "00:00", new Vector2(0f, -14f),
            new Vector2(82f, 18f), 11, TextAnchor.MiddleCenter, new Color(.68f, .78f, .88f), false);
        AddTextShadow(matchTimeLabel);
        speedToggle.onClick.AddListener(ToggleGameSpeed);
        lastSpeedMultiplier = -1;
        lastElapsedSecond = -1;
        UpdateSpeedVisual();
    }

    void BuildUnitMenu()
    {
        // A transparent anchored group lets the three purchase buttons fan out
        // above the primary Units control without drawing a rectangular tray.
        unitsPanel = new GameObject("UnitsFanMenu", typeof(RectTransform), typeof(Image));
        unitsPanel.transform.SetParent(safeRoot, false);
        RectTransform fanRect = unitsPanel.GetComponent<RectTransform>();
        fanRect.anchorMin = fanRect.anchorMax = Vector2.zero;
        fanRect.pivot = Vector2.zero;
        fanRect.anchoredPosition = new Vector2(24f, 24f);
        fanRect.sizeDelta = new Vector2(348f, 336f);
        Image fanInputBlocker = unitsPanel.GetComponent<Image>();
        fanInputBlocker.color = Color.clear;
        fanInputBlocker.raycastTarget = true;
        unitsPanel.SetActive(false);
        CreatePurchaseCard("SendArcherButton", "A", "ARCHER", new Vector2(8f, 154f),
            ArcherAccent, buttonGreenSprite, out archerButton, out archerLabel);
        CreatePurchaseCard("SendKnightButton", "K", "KNIGHT", new Vector2(118f, 212f),
            KnightAccent, buttonCyanSprite, out knightButton, out knightLabel);
        CreatePurchaseCard("SendWizardButton", "W", "WIZARD", new Vector2(228f, 154f),
            WizardAccent, buttonPurpleSprite, out wizardButton, out wizardLabel);
        archerButton.onClick.AddListener(SendArcher);
        knightButton.onClick.AddListener(SendKnight);
        wizardButton.onClick.AddListener(SendWizard);

        CreateButton("UnitsToggle", safeRoot, "UNITS", new Vector2(24f, 24f),
            new Vector2(128f, 128f), true, Cyan, out unitsToggle, out _);
        CreateButton("ItemsToggle", safeRoot, "ITEMS", new Vector2(172f, 31f),
            new Vector2(102f, 102f), true, Blue, out itemsToggle, out _);
        unitsToggleSelected = CreateSelectedButtonLayer(unitsToggle.transform);
        itemsToggleSelected = CreateSelectedButtonLayer(itemsToggle.transform);
        unitsToggle.onClick.AddListener(ToggleUnitsPanel);
    }

    void CreatePurchaseCard(string objectName, string emblem, string unitName, Vector2 position,
        Color accent, Sprite accentSprite,
        out Button button, out Text purchaseLabel)
    {
        GameObject card = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        card.transform.SetParent(unitsPanel.transform, false);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(96f, 96f);
        Image background = card.GetComponent<Image>();
        background.sprite = accentSprite ? accentSprite : circleSprite;
        background.color = background.sprite ? Color.white : ControlNavy;
        background.raycastTarget = true;
        button = card.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        SetButtonColors(button, accent);
        MobileButtonPressFeedback pressFeedback = card.AddComponent<MobileButtonPressFeedback>();
        pressFeedback.Configure(button);
        Outline outline = card.GetComponent<Outline>();
        outline.effectColor = new Color(accent.r, accent.g, accent.b, .82f);
        outline.effectDistance = new Vector2(2f, 2f);
        AddShadow(background, new Color(0f, .01f, .025f, .64f), new Vector2(0f, -3f));
        Image interior = CreateImage("DarkInterior", card.transform, circleSprite,
            Vector2.zero, new Vector2(82f, 82f), Color.Lerp(ControlNavy, accent, .08f), false);
        interior.preserveAspect = true;
        Image frame = CreateImage("FantasyFrame", card.transform, ornateButtonFrameSprite,
            Vector2.zero, new Vector2(104f, 104f), new Color(accent.r, accent.g, accent.b, .92f), false);
        frame.preserveAspect = true;
        Text crest = CreateText("UnitEmblem", card.transform, emblem, new Vector2(0f, 16f),
            new Vector2(44f, 40f), 28, TextAnchor.MiddleCenter,
            Color.Lerp(Color.white, accent, .32f), false);
        AddTextShadow(crest);
        purchaseLabel = CreateText("UnitAndCost", card.transform, unitName + "\n1 GOLD",
            new Vector2(0f, -29f), new Vector2(90f, 38f), 12, TextAnchor.MiddleCenter, Color.white, false);
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
        itemsPanel = CreatePanel("ItemsPanel", safeRoot, new Vector2(172f, 150f),
            new Vector2(230f, 98f), TextAnchor.LowerLeft);
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
        float primarySize = autoAttackButtonSize;
        float slotSize = abilitySlotSize;
        float clusterBottom = safeAreaPadding + 58f;
        CreateButton("AutoAttackButton", safeRoot, "AA",
            new Vector2(-safeAreaPadding, clusterBottom), new Vector2(primarySize, primarySize),
            true, Gold, out autoAttackButton, out _, true);
        autoAttackButton.onClick.AddListener(TriggerAutoAttack);

        // Four compact abilities follow a right-thumb arc above and to the left
        // of the large primary attack button.
        fireballView = AbilityButtonView.Create(safeRoot, "FireballButton",
            new Vector2(slotSize, slotSize),
            new Vector2(-safeAreaPadding - primarySize - 16f, clusterBottom + 4f), true,
            fireballFrameOverride ? fireballFrameOverride : ornateButtonFrameSprite,
            fireballBackgroundOverride ? fireballBackgroundOverride : circleSprite,
            fireballIconOverride, buttonCyanSprite);
        fireballView.SetLayerScales(fireballFrameScale, fireballBackgroundScale, fireballIconScale);
        fireballButton = fireballView.Button;
        fireballView.SetLabel("FIREBALL");
        MobileButtonPressFeedback fireballPress = fireballButton.gameObject.AddComponent<MobileButtonPressFeedback>();
        fireballPress.Configure(fireballButton);
        fireballButton.onClick.AddListener(ToggleFireballTargeting);

        CreatePlaceholderAbility("AbilitySlot2",
            new Vector2(-safeAreaPadding - primarySize - 4f, clusterBottom + 94f),
            2, out futureAbilitySlots[0], out futureAbilityLabels[0]);
        CreatePlaceholderAbility("AbilitySlot3",
            new Vector2(-safeAreaPadding - 58f, clusterBottom + 150f),
            4, out futureAbilitySlots[1], out futureAbilityLabels[1]);
        CreatePlaceholderAbility("AbilitySlot4",
            new Vector2(-safeAreaPadding, clusterBottom + 162f),
            6, out futureAbilitySlots[2], out futureAbilityLabels[2]);
    }

    void ResizeAbilityButton()
    {
        if (!autoAttackButton || !safeRoot) return;
        float height = safeRoot.rect.height;
        float width = safeRoot.rect.width;
        if (height < 1f || width < 1f) return;
        float size = Mathf.Min(autoAttackButtonSize,
            (height - safeAreaPadding - 4f) / 1.9f,
            (width - safeAreaPadding - 4f) / 1.9f);
        size = Mathf.Max(96f, size);
        RectTransform rect = autoAttackButton ? autoAttackButton.GetComponent<RectTransform>() : null;
        if (!rect) return;
        if (Mathf.Abs(rect.sizeDelta.x - size) < .5f) return;
        rect.sizeDelta = new Vector2(size, size);
    }

    void CreatePlaceholderAbility(string name, Vector2 position, int requiredLevel,
        out Image slotImage, out Text slotLabel)
    {
        GameObject slot = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Button));
        slot.transform.SetParent(safeRoot, false);
        RectTransform rect = slot.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(abilitySlotSize, abilitySlotSize);
        Image image = slot.GetComponent<Image>();
        slotImage = image;
        image.sprite = circleSprite;
        image.color = circleSprite ? Color.white : new Color(.06f, .15f, .23f, .9f);
        image.raycastTarget = true;
        Outline outline = slot.GetComponent<Outline>();
        outline.effectColor = new Color(.2f, .6f, .78f, .42f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);
        Image rim = CreateImage("OrnateFrame", slot.transform, ornateButtonFrameSprite,
            Vector2.zero, Vector2.one * (abilitySlotSize + 8f), Color.white, false);
        rim.preserveAspect = true;
        slot.GetComponent<Button>().interactable = false;
        slotLabel = CreateText("EmptySlotLabel", slot.transform, $"Lv {requiredLevel}", Vector2.zero,
            Vector2.one * abilitySlotSize, 13, TextAnchor.MiddleCenter,
            new Color(.65f, .8f, .88f, .75f), false);
        MobileButtonPressFeedback feedback = slot.AddComponent<MobileButtonPressFeedback>();
        feedback.Configure(slot.GetComponent<Button>());
    }

    void BuildHeroProgressionPanel()
    {
        GameObject panel = new GameObject("HeroProgressionPanel", typeof(RectTransform), typeof(Image), typeof(Outline));
        panel.transform.SetParent(safeRoot, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-safeAreaPadding, safeAreaPadding);
        rect.sizeDelta = new Vector2(320f, 46f);
        Image background = panel.GetComponent<Image>();
        background.sprite = capsuleSprite;
        background.type = capsuleSprite ? Image.Type.Sliced : Image.Type.Simple;
        background.color = ControlNavy;
        background.raycastTarget = false;
        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = new Color(.36f, .72f, 1f, .72f);
        outline.effectDistance = new Vector2(1f, 1f);
        heroLevelLabel = CreateText("HeroLevel", panel.transform, "LV 1", new Vector2(-116f, 7f),
            new Vector2(70f, 22f), 15, TextAnchor.MiddleCenter, Gold, false);
        heroExperienceLabel = CreateText("HeroExperience", panel.transform, "0 / 70 XP", new Vector2(32f, 7f),
            new Vector2(200f, 22f), 12, TextAnchor.MiddleCenter, Color.white, false);
        heroExperienceFill = CreateImage("HeroXpFill", panel.transform, null, new Vector2(39f, -11f),
            new Vector2(224f, 7f), new Color(.3f, .72f, 1f, 1f), false);
        heroExperienceFill.type = Image.Type.Filled;
        heroExperienceFill.fillMethod = Image.FillMethod.Horizontal;
        heroExperienceFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        Image frame = CreateImage("HeroXpFrame", panel.transform, healthBarFrameSprite, new Vector2(39f, -11f),
            new Vector2(234f, 15f), new Color(.75f, .86f, 1f, 1f), false);
        frame.type = healthBarFrameSprite ? Image.Type.Sliced : Image.Type.Simple;
        frame.preserveAspect = false;
    }

    void UpdateHeroProgression()
    {
        if (!heroProgression) return;
        int level = heroProgression.Level;
        int needed = Mathf.Max(1, heroProgression.ExperienceToNextLevel);
        if (heroLevelLabel) heroLevelLabel.text = $"LV {level}";
        if (heroExperienceLabel) heroExperienceLabel.text = $"{heroProgression.Experience} / {needed} XP";
        if (heroExperienceFill) heroExperienceFill.fillAmount = Mathf.Clamp01(heroProgression.Experience / (float)needed);
        int[] requirements = { 2, 4, 6 };
        for (int i = 0; i < futureAbilitySlots.Length; i++)
        {
            bool unlocked = abilityLoadout ? abilityLoadout.IsUnlocked(i + 2) : level >= requirements[i];
            if (futureAbilitySlots[i]) futureAbilitySlots[i].color = unlocked
                ? new Color(.25f, .62f, .78f, .92f) : new Color(.18f, .24f, .32f, .78f);
            if (futureAbilityLabels[i])
            {
                futureAbilityLabels[i].text = unlocked ? "OPEN" : $"Lv {requirements[i]}";
                futureAbilityLabels[i].color = unlocked ? new Color(.75f, .95f, 1f) : new Color(.55f, .64f, .72f);
            }
        }
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

    void ToggleGameSpeed()
    {
        HlwGameSpeed.SetMultiplier(HlwGameSpeed.Multiplier == 2 ? 1 : 2);
        UpdateSpeedVisual();
    }

    void UpdateSpeedVisual()
    {
        int multiplier = HlwGameSpeed.Multiplier;
        if (multiplier == lastSpeedMultiplier || !speedToggle) return;
        lastSpeedMultiplier = multiplier;
        bool fast = multiplier == 2;
        if (speedLabel)
        {
            speedLabel.text = fast ? "2×" : "1×";
            speedLabel.color = fast ? new Color(.72f, .96f, 1f, 1f) : Color.white;
        }
        if (speedToggleGlow) speedToggleGlow.color = fast
            ? new Color(.2f, .86f, 1f, .28f) : new Color(.2f, .86f, 1f, 0f);
        Image background = speedToggle.GetComponent<Image>();
        if (background) background.color = fast
            ? new Color(.045f, .13f, .19f, .99f) : ControlNavy;
        Outline outline = speedToggle.GetComponent<Outline>();
        if (outline) outline.effectColor = fast
            ? new Color(.25f, .9f, 1f, .95f) : new Color(.22f, .58f, .82f, .68f);
    }

    void SetButtonSelected(Button button, Image selectedLayer, bool selected)
    {
        if (selectedLayer)
        {
            selectedLayer.color = selected ? new Color(.35f, .9f, 1f, .34f) : new Color(.35f, .9f, 1f, 0f);
            selectedLayer.transform.SetAsFirstSibling();
        }
        if (button)
        {
            button.transform.localScale = selected ? Vector3.one * 1.035f : Vector3.one;
            SetButtonSkin(button, circleSprite, ControlNavy);
            Transform interiorTransform = button.transform.Find("DarkInterior");
            Image interior = interiorTransform ? interiorTransform.GetComponent<Image>() : null;
            if (interior) interior.color = selected
                ? new Color(.055f, .16f, .22f, .99f) : ControlNavy;
            Outline rim = button.GetComponent<Outline>();
            if (rim) rim.effectColor = selected
                ? new Color(.36f, .92f, 1f, .95f) : new Color(.2f, .62f, .88f, .72f);
        }
    }

    void ToggleFireballTargeting()
    {
        if (!ability) ability = HeroProgression.PlayerHero
            ? HeroProgression.PlayerHero.GetComponent<FireballAbility>() : FindAnyObjectByType<FireballAbility>();
        if (!ability) return;
        if (ability.IsTargeting) ability.CancelTargeting();
        else if (ability.BeginTargeting())
        {
            unitsPanel.SetActive(false);
            itemsPanel.SetActive(false);
            SetButtonSelected(unitsToggle, unitsToggleSelected, false);
            SetButtonSelected(itemsToggle, itemsToggleSelected, false);
            SetButtonColors(unitsToggle, Blue);
            SetButtonColors(itemsToggle, Blue);
        }
    }

    void TriggerAutoAttack()
    {
        HeroProgression player = HeroProgression.PlayerHero;
        if (!player || !player.Health || player.Health.IsDead) return;
        HeroLaneController movement = player.GetComponent<HeroLaneController>();
        if (movement) movement.CancelMovement();
        HeroAutoAttack attack = player.GetComponent<HeroAutoAttack>();
        if (attack) attack.TryAttackInRange(true);
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
        MobileButtonPressFeedback pressFeedback = buttonObject.AddComponent<MobileButtonPressFeedback>();
        pressFeedback.Configure(button);
        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = accent;
        outline.effectDistance = circle ? new Vector2(2f, 2f) : new Vector2(1.5f, 1.5f);
        outline.useGraphicAlpha = true;
        AddShadow(image, new Color(0f, .01f, .025f, .68f), new Vector2(0f, -4f));
        if (circle)
        {
            Image interior = CreateImage("DarkInterior", buttonObject.transform, circleSprite,
                Vector2.zero, size * .82f, ControlNavy, false);
            interior.preserveAspect = true;
        }
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
        if (name == "UnitsToggle") return circleSprite;
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
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.Lerp(Color.white, accent, .22f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(.44f, .48f, .52f, .58f);
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
        Color accent = PurchaseAccent(button.name);
        if (background)
        {
            background.color = affordable ? Color.white : new Color(.42f, .47f, .52f, .72f);
        }
        if (label) label.color = affordable ? Color.white : new Color(.58f, .65f, .7f, .58f);
        Transform emblemTransform = button.transform.Find("UnitEmblem");
        Text emblem = emblemTransform ? emblemTransform.GetComponent<Text>() : null;
        if (emblem) emblem.color = affordable
            ? Color.Lerp(Color.white, accent, .32f) : new Color(.52f, .57f, .62f, .62f);
        Transform interiorTransform = button.transform.Find("DarkInterior");
        Image interior = interiorTransform ? interiorTransform.GetComponent<Image>() : null;
        if (interior) interior.color = affordable
            ? Color.Lerp(ControlNavy, accent, .08f) : new Color(.055f, .065f, .075f, .74f);
        Transform frameTransform = button.transform.Find("FantasyFrame");
        Image frame = frameTransform ? frameTransform.GetComponent<Image>() : null;
        if (frame) frame.color = affordable
            ? new Color(accent.r, accent.g, accent.b, .92f) : new Color(.34f, .39f, .44f, .46f);
        Outline outline = button.GetComponent<Outline>();
        if (outline) outline.effectColor = affordable
            ? new Color(accent.r, accent.g, accent.b, .82f) : new Color(.2f, .28f, .34f, .34f);
    }

    static Color PurchaseAccent(string buttonName)
    {
        if (buttonName.Contains("Knight")) return KnightAccent;
        if (buttonName.Contains("Wizard")) return WizardAccent;
        return ArcherAccent;
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

/// <summary>Touch-only scale and rim feedback; no hover state is required.</summary>
public sealed class MobileButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    Button button;
    Outline rim;
    Vector3 restingScale = Vector3.one;
    Color restingRim;
    bool pressed;

    public void Configure(Button target)
    {
        button = target;
        rim = GetComponent<Outline>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!button || !button.interactable) return;
        pressed = true;
        restingScale = transform.localScale;
        if (rim) restingRim = rim.effectColor;
        transform.localScale = restingScale * .94f;
        BrightenRim();
    }

    public void OnPointerUp(PointerEventData eventData) { Restore(); }
    public void OnPointerExit(PointerEventData eventData) { Restore(); }

    void LateUpdate()
    {
        if (!pressed) return;
        if (!button || !button.interactable) Restore();
        else BrightenRim();
    }

    void BrightenRim()
    {
        if (rim) rim.effectColor = Color.Lerp(restingRim, Color.white, .38f);
    }

    void Restore()
    {
        if (!pressed) return;
        pressed = false;
        transform.localScale = restingScale;
        if (rim) rim.effectColor = restingRim;
    }

    void OnDisable() { Restore(); }
}

/// <summary>Single source of truth for scaled gameplay time and its physics cadence.</summary>
public static class HlwGameSpeed
{
    static float normalFixedDeltaTime = .02f;
    static bool initialized;
    static int multiplier = 1;

    public static int Multiplier => multiplier;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntimeState()
    {
        CaptureNormalFixedDeltaTime();
        Apply(1);
    }

    public static void SetMultiplier(int value)
    {
        EnsureInitialized();
        Apply(value >= 2 ? 2 : 1);
    }

    public static void ResetToNormal()
    {
        EnsureInitialized();
        Apply(1);
    }

    static void EnsureInitialized()
    {
        if (initialized) return;
        CaptureNormalFixedDeltaTime();
    }

    static void CaptureNormalFixedDeltaTime()
    {
        float currentScale = Mathf.Max(.0001f, Time.timeScale);
        normalFixedDeltaTime = Mathf.Max(.001f, Time.fixedDeltaTime / currentScale);
        initialized = true;
    }

    static void Apply(int value)
    {
        multiplier = value;
        Time.timeScale = multiplier;
        Time.fixedDeltaTime = normalFixedDeltaTime * multiplier;
    }
}
