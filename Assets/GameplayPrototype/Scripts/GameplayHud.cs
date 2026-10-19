using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>Responsive, safe-area-aware mobile overlay for the lane prototype.</summary>
[DefaultExecutionOrder(-50)]
public sealed class GameplayHud : MonoBehaviour
{
    const int HudLayoutVersion = 54;
    // Painted XP track in Level_XP_Bar_Runtime.png, normalized to the full sprite.
    static readonly Rect XpArtworkTrack = new Rect(.246f, .407f, .387f, .115f);
    const float HudReferenceWidth = 1672f;
    const float HudReferenceHeight = 941f;
    [Header("Premium HUD artwork")]
    [Tooltip("Optional presentation-only sprite kit. Empty slots retain the current generated/fantasy fallback visuals.")]
    public HlwHudSkin hudSkin;
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
    Sprite xpSolidFillSprite;
    Sprite goldIconSprite;
    Sprite incomeIconSprite;
    Sprite heartIconSprite;
    Sprite unitsIconSprite;
    Sprite itemsIconSprite;
    Sprite backIconSprite;
    Sprite archerProfileSprite;
    Sprite knightProfileSprite;
    Sprite wizardProfileSprite;

    Button unitsToggle;
    Button itemsToggle;
    Button potionButton;
    Text potionLabel;
    Text potionCooldownLabel;
    HealingPotionItem potionItem;
    Button speedToggle;
    Button fireballButton;
    Button autoAttackButton;
    Button statsToggleButton;
    Button fireballUpgradeButton;
    Button geomancerSecondaryButton;
    Button geomancerSecondaryUpgradeButton;
    Button archerButton;
    Button knightButton;
    Button wizardButton;
    Button restartButton;
    Button returnToMenuButton;
    Button enemyHeroFocusButton;
    Button playerHeroFocusButton;
    Text enemyHeroFocusLevel;
    Text playerHeroFocusLevel;
    Image enemyHeroFocusGlow;
    Image playerHeroFocusGlow;
    Image enemyHeroFocusPortrait;
    Image playerHeroFocusPortrait;
    Text enemyHeroFocusName;
    Text playerHeroFocusName;
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
    Text resultDurationValue;
    Text resultLevelValue;
    Text resultIncomeValue;
    MatchResult lastPresentedResult = MatchResult.None;
    int lastKnownHeroLevel = 1;
    Text archerLabel;
    Text knightLabel;
    Text wizardLabel;
    Text speedLabel;
    Text matchTimeLabel;
    Text heroLevelCaption;
    Text heroLevelLabel;
    Text heroExperienceLabel;
    Image heroExperienceFill;
    public bool enableXpDiagnostics;
    int lastReportedXp = -1;
    int lastReportedXpNeeded = -1;
    // These point at runtime-built UI. Serialize them for Unity's hot-reload backup,
    // then rebind by hierarchy name when domain reload restores only part of the HUD.
    [SerializeField, HideInInspector] Image[] futureAbilitySlots = new Image[3];
    [SerializeField, HideInInspector] Text[] futureAbilityLabels = new Text[3];
    readonly Button[] attributeUpgradeButtons = new Button[4];
    readonly TextMeshProUGUI[] attributeValueLabels = new TextMeshProUGUI[4];
    readonly TextMeshProUGUI[] attributeEffectLabels = new TextMeshProUGUI[4];
    readonly TextMeshProUGUI[] attributePreviewLabels = new TextMeshProUGUI[4];
    readonly int[] displayedAttributeValues = { -1, -1, -1, -1 };
    readonly float[] displayedAttributeRates = { -1f, -1f, -1f, -1f };
    int displayedSkillPoints = -1;
    TextMeshProUGUI skillPointLabel;
    Text statsToggleLabel;
    Image statsPointBadge;
    Text statsPointCount;
    GameObject unitsPanel;
    sealed class UnitPurchaseEntry
    {
        public UnitDefinition definition;
        public Button button;
        public Text costLabel;
    }
    readonly List<UnitPurchaseEntry> purchaseEntries = new List<UnitPurchaseEntry>(16);
    readonly ChamferedFrameGraphic[] tierFrames = new ChamferedFrameGraphic[4];
    UnitTier selectedUnitTier = UnitTier.Starter;
    GameObject itemsPanel;
    GameObject resultPanel;
    GameObject statsPanel;
    RectTransform bottomLeftRoot;
    RectTransform bottomRightRoot;
    RectTransform progressionRoot;
    SchematicMinimapHud minimapHud;
    float activeMinimapWidth;
    float activeMinimapHeight;
    bool topHudTwoRows;
    bool lastGeomancerBottomLayout;
    FireballAbility ability;
    GeomancerAbilityController geomancerAbilities;
    Text geomancerPassiveLabel;
    AbilityButtonGlyph geomancerSecondaryGlyph;
    HeroProgression heroProgression;
    HeroProgression subscribedLevelHero;
    Renderer[] levelUpHeroRenderers;
    TextMeshProUGUI levelUpFeedback;
    RectTransform levelUpFeedbackRect;
    float levelUpFeedbackUntil;
    HeroAbilityLoadout abilityLoadout;
    HeroProgression enemyHeroProgression;
    HlwCameraController cameraController;
    readonly List<LaneUnitCombat> cameraCardUnits = new List<LaneUnitCombat>(64);
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
    bool reportedPortraitDiagnostics;
    int lastLayoutScreenWidth = -1;
    int lastLayoutScreenHeight = -1;
    Rect lastLayoutSafeArea = new Rect(-1f, -1f, -1f, -1f);
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
        bool abilityHudReady = RebindAbilityHudReferences();
        // With Unity's reload-scene/domain options disabled, a subsequent Play
        // session can enable this component without constructing the overlay anew.
        if (Application.isPlaying && (builtHudLayoutVersion != HudLayoutVersion ||
            !canvas || !safeRoot || !unitsToggle || !fireballButton || !autoAttackButton || !abilityHudReady))
        {
            EnsureEventSystem();
            LoadFantasySprites();
            BuildCanvas();
        }
    }

    void OnDisable()
    {
        BindLevelUpHero(null);
        HlwGameSpeed.ResetToNormal();
    }
    void OnDestroy()
    {
        BindLevelUpHero(null);
        HlwGameSpeed.ResetToNormal();
        if (xpSolidFillSprite) Destroy(xpSolidFillSprite);
    }
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
        bool abilityHudReady = RebindAbilityHudReferences();
        if (builtHudLayoutVersion == HudLayoutVersion && canvas && safeRoot && unitsToggle && itemsToggle && speedToggle &&
            fireballButton && autoAttackButton && potionButton &&
            goldLabel && incomeLabel && payoutLabel && blueBaseLabel && redBaseLabel && abilityHudReady)
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
        if (!hudSkin) hudSkin = Resources.Load<HlwHudSkin>("HlwHud/HLW_PremiumHudSkin");
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
        archerProfileSprite = LoadUnitProfile("HlwUnitProfiles/GreenArcherProfile");
        knightProfileSprite = LoadUnitProfile("HlwUnitProfiles/HolyKnightProfile");
        wizardProfileSprite = LoadUnitProfile("HlwUnitProfiles/BlueWizardProfile");

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
            if (!geomancerAbilities && HeroProgression.PlayerHero)
                geomancerAbilities = HeroProgression.PlayerHero.GetComponent<GeomancerAbilityController>();
            if (!sender) sender = FindAnyObjectByType<FriendlyUnitSender>();
            if (!matchLoop) matchLoop = GetComponent<MatchLoopController>() ?? FindAnyObjectByType<MatchLoopController>();
            if (!heroProgression) heroProgression = HeroProgression.PlayerHero;
            if (!cameraController)
            {
                Camera mainCamera = Camera.main;
                cameraController = mainCamera ? mainCamera.GetComponent<HlwCameraController>() :
                    FindAnyObjectByType<HlwCameraController>();
            }
            if (!enemyHeroProgression) ResolveEnemyHeroProgression();
            if (heroProgression && !abilityLoadout) abilityLoadout = heroProgression.GetComponent<HeroAbilityLoadout>();
            nextReferenceSearch = Time.unscaledTime + .25f;
        }
        if (subscribedLevelHero != heroProgression) BindLevelUpHero(heroProgression);
        UpdateLevelUpFeedback();

        bool running = matchLoop && matchLoop.IsRunning;
        if (autoAttackButton)
            autoAttackButton.interactable = running && HeroProgression.PlayerHero &&
                HeroProgression.PlayerHero.Health && !HeroProgression.PlayerHero.Health.IsDead;
        UpdatePotionButton();
        UpdateFireball(running);
        UpdateHeroProgression();
        UpdateSkillUpgradeHud();
        UpdateHeroFocusCards();
        UpdateSpeedVisual();
        if (sender)
        {
            if (purchaseEntries.Count == 0 && knightButton && archerButton && wizardButton)
            {
                UpdatePurchaseVisual(knightButton, knightLabel, sender.CanPurchase(UnitArchetype.HolyKnight));
                UpdatePurchaseVisual(archerButton, archerLabel, sender.CanPurchase(UnitArchetype.GreenArcher));
                UpdatePurchaseVisual(wizardButton, wizardLabel, sender.CanPurchase(UnitArchetype.BlueWizard));
                UpdateUnitLabels();
            }
            foreach (UnitPurchaseEntry entry in purchaseEntries)
            {
                if (!entry.button || !entry.definition) continue;
                UpdatePurchaseVisual(entry.button, entry.costLabel,
                    sender.CanPurchase(entry.definition.archetype));
                if (entry.costLabel) entry.costLabel.text = $"{sender.Cost(entry.definition.archetype)} GOLD";
            }
            LogPortraitDiagnosticsOnce();
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
            if (resultPanel.activeSelf != ended) resultPanel.SetActive(ended);
            if (ended && resultLabel)
            {
                bool won = matchLoop.Result == MatchResult.SurvivalVictory || matchLoop.Result == MatchResult.Victory;
                resultLabel.text = won ? "VICTORY" : "DEFEAT";
                resultLabel.color = won ? Gold : Crimson;
                if (resultDurationValue)
                    resultDurationValue.text = $"{elapsedSecond / 60:00}:{elapsedSecond % 60:00}";
                if (resultLevelValue) resultLevelValue.text = lastKnownHeroLevel.ToString();
                if (resultIncomeValue) resultIncomeValue.text = income.ToString();
                if (lastPresentedResult != matchLoop.Result)
                {
                    UpdateResultAccent(won);
                    lastPresentedResult = matchLoop.Result;
                }
            }
            else if (!ended) lastPresentedResult = MatchResult.None;
        }
    }

    void LateUpdate()
    {
        if (!safeRoot || !canvas || Screen.width <= 0 || Screen.height <= 0) return;
        Rect safe = ValidSafeArea();
        bool geomancerLayout = geomancerAbilities || (abilityLoadout && abilityLoadout.IsGeomancer);
        if (Screen.width != lastLayoutScreenWidth || Screen.height != lastLayoutScreenHeight ||
            !HlwResponsiveCanvas.Approximately(safe, lastLayoutSafeArea) ||
            geomancerLayout != lastGeomancerBottomLayout)
            ApplyResponsiveLayout(true);
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

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (!scaler) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        HlwResponsiveCanvas.Configure(canvas, scaler, 50);
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (!raycaster) raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = true;
        safeRoot.anchorMin = Vector2.zero;
        safeRoot.anchorMax = Vector2.one;
        safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        safeRoot.localScale = Vector3.one;
        Canvas.ForceUpdateCanvases();
        ApplySafeAreaRect(ValidSafeArea());
        Canvas.ForceUpdateCanvases();
        ResolveTopHudLayout();
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
        BuildSkillUpgradeControls();
        BuildLevelUpFeedback();
        BuildResultPanel();
        builtHudLayoutVersion = HudLayoutVersion;
        ApplyResponsiveLayout(true);
        ConfigureLayoutValidator();
    }

    void BuildTopInfo()
    {
        topHudTwoRows = false;
        GameObject rootObject = new GameObject("TopHudRoot", typeof(RectTransform));
        rootObject.transform.SetParent(safeRoot, false);
        RectTransform root = (RectTransform)rootObject.transform;
        root.anchorMin = root.anchorMax = new Vector2(.5f, 1f);
        root.pivot = new Vector2(.5f, 1f);
        root.anchoredPosition = new Vector2(0f, -safeAreaPadding);

        GameObject economy = CreateTopbarPanel("EconomyPanel", root,
            hudSkin ? hudSkin.topbarEconomy : null);
        RectTransform economyInterior = CreateRectTransform("Interior", economy.transform.Find("Content"));
        goldLabel = CreateTopbarStatCell("GoldCell", "GOLD", economyInterior,
            Gold, goldIconSprite, false);
        incomeLabel = CreateTopbarStatCell("IncomeCell", "INCOME", economyInterior,
            IncomeGreen, incomeIconSprite, false);
        payoutLabel = CreateTopbarStatCell("NextCell", "NEXT", economyInterior,
            TimerViolet, null, true);

        GameObject redPanel = CreateTopbarPanel("RedBasePanel", root,
            hudSkin ? hudSkin.topbarRedBase : null);
        CreateTopbarHealthContent(redPanel.transform.Find("Content"), "RED BASE", Crimson, redHealthFillSprite,
            out redBaseLabel, out redBaseFill);
        GameObject bluePanel = CreateTopbarPanel("BlueBasePanel", root,
            hudSkin ? hudSkin.topbarBlueBase : null);
        CreateTopbarHealthContent(bluePanel.transform.Find("Content"), "BLUE BASE", Blue, blueHealthFillSprite,
            out blueBaseLabel, out blueBaseFill);

        LayoutThreePartTopbar(root, 1f, 1920f, 0f);
    }

    void ResolveTopHudLayout()
    {
        float safeWidth = safeRoot && safeRoot.rect.width > 0f ? safeRoot.rect.width : 1920f;
        float safeHeight = safeRoot && safeRoot.rect.height > 0f ? safeRoot.rect.height : 1080f;
        float heightScale = Mathf.Clamp(safeHeight / 960f, .86f, 1f);
        activeMinimapWidth = Mathf.Clamp(safeWidth * .18f, 272f, 320f) * heightScale;
        float minimapAspect = hudSkin && hudSkin.minimapFrame
            ? Mathf.Max(1.2f, hudSkin.minimapFrame.rect.width / hudSkin.minimapFrame.rect.height)
            : 320f / 190f;
        activeMinimapHeight = activeMinimapWidth / minimapAspect;
        topHudTwoRows = false;
    }

    void BuildMinimap()
    {
        if (!minimapHud) minimapHud = canvas.GetComponent<SchematicMinimapHud>();
        if (!minimapHud) minimapHud = canvas.gameObject.AddComponent<SchematicMinimapHud>();
        minimapHud.Build(safeRoot, hudSkin ? hudSkin.minimapFrame : null, circleSprite, new Vector2(24f, -24f),
            new Vector2(activeMinimapWidth, activeMinimapHeight));
    }

    void BuildCameraFocusControls()
    {
        const float buttonHeight = 54f;
        const float gap = 8f;
        float firstY = -24f - activeMinimapHeight - gap;
        HeroDefinition playerDefinition = HlwGameSession.Current
            ? HlwGameSession.Current.SelectedHeroDefinition : HeroCatalog.Find(HlwHeroSelection.Battlemage);
        HeroDefinition enemyDefinition = HeroCatalog.Find(HlwHeroSelection.Battlemage);
        CreateHeroFocusCard("PlayerHeroFocusCard", "MY HERO", playerDefinition, firstY,
            Cyan, out playerHeroFocusButton, out playerHeroFocusLevel, out playerHeroFocusGlow,
            out playerHeroFocusPortrait, out playerHeroFocusName);
        playerHeroFocusButton.onClick.AddListener(() => FocusCamera(true));
        CreateHeroFocusCard("EnemyHeroFocusCard", "ENEMY HERO", enemyDefinition,
            firstY - buttonHeight - gap, Crimson, out enemyHeroFocusButton,
            out enemyHeroFocusLevel, out enemyHeroFocusGlow,
            out enemyHeroFocusPortrait, out enemyHeroFocusName);
        enemyHeroFocusButton.onClick.AddListener(() => FocusCamera(false));
    }

    void CreateHeroFocusCard(string objectName, string title, HeroDefinition definition, float y, Color accent,
        out Button button, out Text levelLabel, out Image selectedGlow, out Image portrait, out Text heroName)
    {
        GameObject card = new GameObject(objectName, typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(Outline));
        card.transform.SetParent(safeRoot, false);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, y);
        rect.sizeDelta = new Vector2(activeMinimapWidth, 54f);
        Image background = card.GetComponent<Image>();
        bool customNavigationCard = hudSkin && hudSkin.heroNavigationCard;
        background.sprite = SkinOrFallback(customNavigationCard ? hudSkin.heroNavigationCard : null, capsuleSprite);
        SetImageType(background, true);
        background.color = customNavigationCard ? Color.white : new Color(.025f, .045f, .078f, .98f);
        background.raycastTarget = true;
        AddShadow(background, new Color(0f, .01f, .03f, .65f), new Vector2(0f, -3f));
        Outline outline = card.GetComponent<Outline>();
        outline.effectColor = customNavigationCard
            ? new Color(0f, 0f, 0f, 0f) : new Color(accent.r, accent.g, accent.b, .62f);
        outline.effectDistance = new Vector2(1.25f, 1.25f);
        button = card.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        SetButtonColors(button, accent);
        MobileButtonPressFeedback feedback = card.AddComponent<MobileButtonPressFeedback>();
        feedback.Configure(button);

        if (!hudSkin || !hudSkin.heroNavigationCard)
        {
            ChamferedFrameGraphic metal = CreateChamferedLayer("MetallicCardFrame", card.transform,
                new Vector2(activeMinimapWidth - 4f, 50f),
                new Color(.018f, .035f, .064f, .92f), new Color(accent.r, accent.g, accent.b, .72f), 9f, 2f);
            metal.transform.SetAsFirstSibling();
        }

        selectedGlow = CreateImage("SelectedGlow", card.transform,
            SkinOrFallback(hudSkin ? hudSkin.selectedOverlay : null, capsuleSprite), Vector2.zero,
            new Vector2(activeMinimapWidth + 6f, 60f), new Color(accent.r, accent.g, accent.b, 0f), false);
        SetImageType(selectedGlow, true);
        selectedGlow.transform.SetAsFirstSibling();
        levelLabel = CreateText("HeroLevel", card.transform, "LV--", new Vector2(-126f, 0f),
            new Vector2(54f, 38f), 15, TextAnchor.MiddleCenter, accent, false);
        Image portraitWell = CreateImage("PortraitWell", card.transform, circleSprite,
            new Vector2(-78f, 0f), new Vector2(44f, 44f), Color.Lerp(ControlNavy, accent, .45f), false);
        portraitWell.preserveAspect = true;
        portrait = CreateImage("HeroPortrait", card.transform, definition ? definition.portrait : null,
            new Vector2(-78f, 0f), new Vector2(38f, 38f), Color.white, false);
        portrait.preserveAspect = true;
        if (!portrait.sprite)
        {
            portrait.sprite = fireballIconOverride ? fireballIconOverride : heartIconSprite;
            portrait.color = new Color(1f, .82f, .38f, 1f);
        }
        Text titleText = CreateText("CardTitle", card.transform, title, new Vector2(25f, 10f),
            new Vector2(162f, 22f), 14, TextAnchor.MiddleLeft, Color.white, false);
        AddTextShadow(titleText);
        heroName = CreateText("HeroName", card.transform,
            definition ? definition.displayName.ToUpperInvariant() : "BATTLEMAGE", new Vector2(25f, -11f),
            new Vector2(162f, 18f), 10, TextAnchor.MiddleLeft,
            new Color(.62f, .72f, .82f, 1f), false);
    }

    void FocusCamera(bool playerHero)
    {
        Camera mainCamera = Camera.main;
        HlwCameraController controller = mainCamera ? mainCamera.GetComponent<HlwCameraController>() : null;
        if (!controller) controller = FindAnyObjectByType<HlwCameraController>();
        if (!controller) return;
        cameraController = controller;
        if (playerHero) controller.FocusOnPlayerHero();
        else controller.FocusOnEnemyHero();
    }

    void ResolveEnemyHeroProgression()
    {
        UnitCombatRegistry.CopyActiveUnits(cameraCardUnits);
        for (int i = 0; i < cameraCardUnits.Count; i++)
        {
            LaneUnitCombat unit = cameraCardUnits[i];
            if (!unit || unit.IsDead || !unit.Faction || unit.Faction.team != UnitTeam.Enemy ||
                unit.Faction.archetype != UnitArchetype.Battlemage) continue;
            enemyHeroProgression = unit.GetComponent<HeroProgression>();
            return;
        }
    }

    void UpdateHeroFocusCards()
    {
        if (playerHeroFocusLevel)
            playerHeroFocusLevel.text = heroProgression && heroProgression.Health && !heroProgression.Health.IsDead
                ? $"LV{heroProgression.Level}" : "LV--";
        if (enemyHeroFocusLevel)
            enemyHeroFocusLevel.text = enemyHeroProgression && enemyHeroProgression.Health && !enemyHeroProgression.Health.IsDead
                ? $"LV{enemyHeroProgression.Level}" : "LV--";
        HeroCameraFocus selected = cameraController ? cameraController.SelectedHeroFocus : HeroCameraFocus.None;
        if (playerHeroFocusGlow)
            playerHeroFocusGlow.color = selected == HeroCameraFocus.PlayerHero
                ? new Color(Cyan.r, Cyan.g, Cyan.b, .3f) : new Color(Cyan.r, Cyan.g, Cyan.b, 0f);
        if (enemyHeroFocusGlow)
            enemyHeroFocusGlow.color = selected == HeroCameraFocus.EnemyHero
                ? new Color(Crimson.r, Crimson.g, Crimson.b, .3f) : new Color(Crimson.r, Crimson.g, Crimson.b, 0f);
        if (playerHeroFocusButton) playerHeroFocusButton.interactable = heroProgression &&
            heroProgression.Health && !heroProgression.Health.IsDead;
        if (enemyHeroFocusButton) enemyHeroFocusButton.interactable = enemyHeroProgression &&
            enemyHeroProgression.Health && !enemyHeroProgression.Health.IsDead;
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
        bottomLeftRoot = CreateBottomLayoutRoot("BottomLeftRoot");
        unitsPanel = new GameObject("UnitsFanMenu", typeof(RectTransform), typeof(Image));
        unitsPanel.transform.SetParent(bottomLeftRoot, false);
        RectTransform fanRect = unitsPanel.GetComponent<RectTransform>();
        fanRect.anchorMin = fanRect.anchorMax = Vector2.zero;
        fanRect.pivot = new Vector2(.5f, .5f);
        fanRect.anchoredPosition = new Vector2(322f, 352f);
        fanRect.sizeDelta = new Vector2(610f, 265f);
        Image fanInputBlocker = unitsPanel.GetComponent<Image>();
        fanInputBlocker.color = new Color(.013f, .035f, .065f, .94f);
        fanInputBlocker.raycastTarget = true;
        CreateChamferedLayer("TierMenuFrame", unitsPanel.transform, fanRect.sizeDelta,
            new Color(.018f, .048f, .08f, .94f), new Color(.25f, .62f, .84f, .84f), 14f, 2f);
        unitsPanel.SetActive(false);
        purchaseEntries.Clear();
        string[] tabNames = { "STARTER", "NATURE", "GOBLINS", "UNDEAD" };
        for (int i = 0; i < tierFrames.Length; i++)
        {
            int tierIndex = i;
            GameObject tab = new GameObject("TierTab" + tabNames[i], typeof(RectTransform), typeof(Image), typeof(Button));
            tab.transform.SetParent(unitsPanel.transform, false);
            RectTransform tabRect = (RectTransform)tab.transform;
            tabRect.anchorMin = tabRect.anchorMax = Vector2.zero;
            tabRect.pivot = new Vector2(.5f, .5f);
            tabRect.anchoredPosition = new Vector2(78f + i * 151f, 226f);
            tabRect.sizeDelta = new Vector2(137f, 38f);
            Image hitArea = tab.GetComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            Button tabButton = tab.GetComponent<Button>();
            tabButton.transition = Selectable.Transition.None;
            tierFrames[i] = CreateChamferedLayer("TabFrame", tab.transform, tabRect.sizeDelta,
                ControlNavy, Cyan, 8f, 2f);
            Text caption = CreateText("Label", tab.transform, tabNames[i], Vector2.zero,
                new Vector2(125f, 28f), 14, TextAnchor.MiddleCenter, Color.white, false);
            caption.fontStyle = FontStyle.Bold;
            tabButton.onClick.AddListener(() => SelectUnitTier((UnitTier)tierIndex));
        }
        foreach (UnitDefinition definition in UnitRoster.All)
        {
            if (!definition) continue;
            CreatePurchaseCard("Send" + definition.archetype + "Button", definition.displayName.ToUpperInvariant(),
                Vector2.zero, Cyan, buttonCyanSprite, definition.portrait, out Button button, out Text costLabel);
            UnitArchetype archetype = definition.archetype;
            button.onClick.AddListener(() =>
            {
                if (!sender) sender = FindAnyObjectByType<FriendlyUnitSender>();
                if (sender) sender.TrySend(archetype);
            });
            purchaseEntries.Add(new UnitPurchaseEntry { definition = definition, button = button, costLabel = costLabel });
            if (archetype == UnitArchetype.HolyKnight) { knightButton = button; knightLabel = costLabel; }
            else if (archetype == UnitArchetype.GreenArcher) { archerButton = button; archerLabel = costLabel; }
            else if (archetype == UnitArchetype.BlueWizard) { wizardButton = button; wizardLabel = costLabel; }
        }
        // Unity may open the scene before the Editor has generated roster assets.
        // Keep the existing starter purchases available during that import window.
        if (purchaseEntries.Count == 0)
        {
            CreatePurchaseCard("SendKnightButton", "KNIGHT", Vector2.zero, Cyan, buttonCyanSprite,
                knightProfileSprite, out knightButton, out knightLabel);
            CreatePurchaseCard("SendArcherButton", "ARCHER", Vector2.zero, Cyan, buttonCyanSprite,
                archerProfileSprite, out archerButton, out archerLabel);
            CreatePurchaseCard("SendWizardButton", "WIZARD", Vector2.zero, Cyan, buttonCyanSprite,
                wizardProfileSprite, out wizardButton, out wizardLabel);
            knightButton.onClick.AddListener(() => { if (sender) sender.TrySend(UnitArchetype.HolyKnight); });
            archerButton.onClick.AddListener(() => { if (sender) sender.TrySend(UnitArchetype.GreenArcher); });
            wizardButton.onClick.AddListener(() => { if (sender) sender.TrySend(UnitArchetype.BlueWizard); });
        }
        SelectUnitTier(UnitTier.Starter);

        CreateButton("UnitsToggle", bottomLeftRoot, "UNITS", new Vector2(24f, 24f),
            new Vector2(124f, 124f), true, Cyan, out unitsToggle, out _);
        CreateButton("ItemsToggle", bottomLeftRoot, "ITEMS", new Vector2(164f, 24f),
            new Vector2(124f, 124f), true, Blue, out itemsToggle, out _);
        unitsToggleSelected = CreateSelectedButtonLayer(unitsToggle.transform);
        itemsToggleSelected = CreateSelectedButtonLayer(itemsToggle.transform);
        unitsToggle.onClick.AddListener(ToggleUnitsPanel);
    }

    void SelectUnitTier(UnitTier tier)
    {
        selectedUnitTier = tier;
        for (int i = 0; i < tierFrames.Length; i++)
            if (tierFrames[i]) tierFrames[i].Configure(
                i == (int)tier ? new Color(.06f, .17f, .25f, .98f) : ControlNavy,
                i == (int)tier ? Gold : new Color(.25f, .55f, .7f, .75f), 8f, 2f);
        foreach (UnitPurchaseEntry entry in purchaseEntries)
            if (entry.button) entry.button.gameObject.SetActive(entry.definition.tier == tier);
        if (purchaseEntries.Count == 0)
        {
            if (knightButton) knightButton.gameObject.SetActive(tier == UnitTier.Starter);
            if (archerButton) archerButton.gameObject.SetActive(tier == UnitTier.Starter);
            if (wizardButton) wizardButton.gameObject.SetActive(tier == UnitTier.Starter);
        }
        LayoutTierPurchaseCards(lastBottomLayoutScale > 0f ? lastBottomLayoutScale : 1f);
    }

    float lastBottomLayoutScale = 1f;

    void LayoutTierPurchaseCards(float scale)
    {
        if (!unitsPanel) return;
        lastBottomLayoutScale = scale;
        RectTransform frame = unitsPanel.transform.Find("TierMenuFrame") as RectTransform;
        if (frame) LayoutCenteredChild(frame, Vector2.zero, new Vector2(610f, 265f), scale);
        for (int i = 0; i < tierFrames.Length; i++)
        {
            RectTransform tab = tierFrames[i] ? tierFrames[i].transform.parent as RectTransform : null;
            if (!tab) continue;
            tab.anchoredPosition = new Vector2(78f + i * 151f, 226f) * scale;
            tab.sizeDelta = new Vector2(137f, 38f) * scale;
            LayoutCenteredChild(tierFrames[i].rectTransform, Vector2.zero, new Vector2(137f, 38f), scale);
            RectTransform label = tab.Find("Label") as RectTransform;
            LayoutCenteredChild(label, Vector2.zero, new Vector2(125f, 28f), scale);
            SetScaledFont(label, 14, scale);
        }
        int count = 0;
        if (purchaseEntries.Count == 0)
        {
            Button[] starters = { knightButton, archerButton, wizardButton };
            for (int i = 0; i < starters.Length; i++)
            {
                if (!starters[i]) continue;
                RectTransform card = starters[i].GetComponent<RectTransform>();
                card.anchorMin = card.anchorMax = card.pivot = Vector2.zero;
                card.anchoredPosition = new Vector2(104f + i * 151f, 66f) * scale;
                card.localScale = Vector3.one * scale;
            }
            return;
        }
        foreach (UnitPurchaseEntry entry in purchaseEntries) if (entry.definition.tier == selectedUnitTier) count++;
        if (count == 0) return;
        float pitch = count == 5 ? 108f : count == 4 ? 128f : 151f;
        float left = (610f - (count - 1) * pitch - 100f) * .5f;
        int index = 0;
        foreach (UnitPurchaseEntry entry in purchaseEntries)
        {
            if (entry.definition.tier != selectedUnitTier || !entry.button) continue;
            RectTransform card = entry.button.GetComponent<RectTransform>();
            card.anchorMin = card.anchorMax = card.pivot = Vector2.zero;
            card.anchoredPosition = new Vector2(left + index * pitch, 66f) * scale;
            card.localScale = Vector3.one * scale;
            index++;
        }
    }

    RectTransform CreateBottomLayoutRoot(string objectName)
    {
        GameObject rootObject = new GameObject(objectName, typeof(RectTransform));
        rootObject.transform.SetParent(safeRoot, false);
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(.5f, .5f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;
        return root;
    }

    void CreatePurchaseCard(string objectName, string unitName, Vector2 position,
        Color accent, Sprite accentSprite, Sprite profileSprite,
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
        Sprite premiumPurchaseFrame = hudSkin ? hudSkin.abilityFrame : null;
        background.sprite = premiumPurchaseFrame ? circleSprite :
            (accentSprite ? accentSprite : circleSprite);
        background.color = premiumPurchaseFrame ? Color.white :
            (background.sprite ? Color.white : ControlNavy);
        background.raycastTarget = true;
        button = card.GetComponent<Button>();
        button.targetGraphic = background;
        // Purchase availability is styled explicitly below. Unity's disabled ColorTint
        // otherwise compounds the portrait treatment and makes affordable units look muddy.
        button.transition = Selectable.Transition.None;
        MobileButtonPressFeedback pressFeedback = card.AddComponent<MobileButtonPressFeedback>();
        pressFeedback.Configure(button);
        Outline outline = card.GetComponent<Outline>();
        outline.effectColor = new Color(.28f, .78f, 1f, .42f);
        outline.effectDistance = Vector2.one;
        AddShadow(background, new Color(0f, .01f, .025f, .64f), new Vector2(0f, -3f));
        Image interior = CreateImage("DarkInterior", card.transform, circleSprite,
            Vector2.zero, new Vector2(86f, 86f), Color.Lerp(ControlNavy, accent, .055f), false);
        interior.preserveAspect = true;
        if (hudSkin) interior.gameObject.SetActive(false);

        // FantasyButton1 has an opaque navy center. Keep it behind the portrait;
        // placing it later in the hierarchy darkens even affordable portraits.
        Image frame = null;
        if (premiumPurchaseFrame || !hudSkin)
        {
            frame = CreateImage("FantasyFrame", card.transform,
                premiumPurchaseFrame ? premiumPurchaseFrame : ornateButtonFrameSprite,
                Vector2.zero, new Vector2(106f, 106f),
                premiumPurchaseFrame ? Color.white : new Color(accent.r, accent.g, accent.b, .86f), false);
            frame.preserveAspect = true;
        }
        ArcaneRingGraphic ring = premiumPurchaseFrame ? null : CreateArcaneRing("PurchaseRing", card.transform,
            new Vector2(102f, 102f), new Color(.08f, .38f, .62f, .94f), Color.clear,
            hudSkin ? 0 : 4);

        GameObject portraitMaskObject = new GameObject("PortraitMask", typeof(RectTransform),
            typeof(Image), typeof(Mask));
        portraitMaskObject.transform.SetParent(card.transform, false);
        RectTransform portraitMask = portraitMaskObject.GetComponent<RectTransform>();
        portraitMask.anchorMin = portraitMask.anchorMax = new Vector2(.5f, .5f);
        portraitMask.pivot = new Vector2(.5f, .5f);
        portraitMask.anchoredPosition = new Vector2(0f, 12f);
        portraitMask.sizeDelta = new Vector2(64f, 64f);
        portraitMask.localScale = Vector3.one;
        Image maskImage = portraitMaskObject.GetComponent<Image>();
        maskImage.sprite = circleSprite;
        maskImage.color = Color.white;
        maskImage.raycastTarget = false;
        portraitMaskObject.GetComponent<Mask>().showMaskGraphic = false;
        float portraitAspect = profileSprite && profileSprite.rect.height > 0f
            ? profileSprite.rect.width / profileSprite.rect.height : 1f;
        Vector2 portraitSize = new Vector2(70f * Mathf.Max(1f, portraitAspect),
            70f * Mathf.Max(1f, 1f / portraitAspect));
        Image portrait = CreateImage("UnitPortrait", portraitMask, profileSprite,
            Vector2.zero, portraitSize, Color.white, false);
        portrait.preserveAspect = false;
        Image unavailableOverlay = CreateSolidImage("UnaffordableOverlay", portraitMask,
            Vector2.zero, new Vector2(64f, 64f), new Color(.015f, .035f, .075f, 0f));
        unavailableOverlay.raycastTarget = false;
        if (ring) ring.transform.SetAsLastSibling();
        // The premium frame has an opaque navy center. It must remain behind the
        // circular portrait; its outer metal rim is still visible around the mask.
        Text unitLabel = CreateText("UnitName", card.transform, unitName,
            new Vector2(0f, -26f), new Vector2(92f, 14f), unitName.Length > 15 ? 8 : unitName.Length > 11 ? 9 : 10,
            TextAnchor.MiddleCenter, Color.white, false);
        AddTextShadow(unitLabel);
        Image costBadge = CreateImage("CostBadge", card.transform, capsuleSprite,
            new Vector2(0f, -40f), new Vector2(84f, 18f),
            new Color(.018f, .045f, .08f, .96f), false);
        SetImageType(costBadge, true);
        costBadge.preserveAspect = false;
        Outline costRim = costBadge.gameObject.AddComponent<Outline>();
        costRim.effectColor = new Color(.9f, .67f, .25f, .72f);
        costRim.effectDistance = Vector2.one;
        purchaseLabel = CreateText("UnitAndCost", card.transform, PurchaseLabel(unitName, 1),
            new Vector2(0f, -40f), new Vector2(88f, 18f), 13,
            TextAnchor.MiddleCenter, new Color(1f, .9f, .52f, 1f), false);
        purchaseLabel.fontStyle = FontStyle.Bold;
        AddTextShadow(purchaseLabel);
    }

    Image CreateSelectedButtonLayer(Transform buttonRoot)
    {
        // The supplied Units/Items PNG already includes its complete rim, icon,
        // and label. A second sprite, even behind it, doubles the frame when open.
        return null;
    }

    void BuildItemsPanel()
    {
        if (!potionItem) potionItem = GetComponent<HealingPotionItem>() ?? gameObject.AddComponent<HealingPotionItem>();
        itemsPanel = CreatePanel("ItemsPanel", bottomLeftRoot, new Vector2(172f, 150f),
            new Vector2(230f, 98f), TextAnchor.LowerLeft);
        Image icon = CreateImage("ItemsPanelIcon", itemsPanel.transform, itemsIconSprite,
            new Vector2(30f, 75f), new Vector2(30f, 30f), Color.white, false);
        icon.preserveAspect = true;
        CreateText("ItemsPanelTitle", itemsPanel.transform, "ITEMS", new Vector2(58f, 63f),
            new Vector2(150f, 24f), 15, TextAnchor.MiddleLeft, new Color(.65f, .9f, 1f), false);
        CreateButton("HealingPotionButton", itemsPanel.transform, "POTION  10 GOLD",
            new Vector2(16f, 9f), new Vector2(198f, 44f), false, Cyan,
            out potionButton, out potionLabel);
        potionLabel.fontSize = 16;
        potionLabel.fontStyle = FontStyle.Bold;
        potionLabel.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        potionLabel.rectTransform.sizeDelta = new Vector2(190f, 25f);
        potionCooldownLabel = CreateText("PotionStatus", potionButton.transform, "HEAL 75 HP",
            new Vector2(0f, -12f), new Vector2(188f, 16f), 10,
            TextAnchor.MiddleCenter, new Color(.68f, .9f, 1f), false);
        potionButton.onClick.AddListener(() => { if (potionItem) potionItem.TryUse(); UpdatePotionButton(); });
        itemsPanel.SetActive(false);
        itemsToggle.onClick.AddListener(ToggleItemsPanel);
    }

    void UpdatePotionButton()
    {
        if (!potionButton) return;
        if (!potionItem) potionItem = GetComponent<HealingPotionItem>();
        potionButton.interactable = potionItem && potionItem.CanUse;
        if (!potionLabel || !potionItem) return;
        int seconds = Mathf.CeilToInt(potionItem.CooldownRemaining);
        potionLabel.text = $"POTION  {potionItem.cost} GOLD";
        potionLabel.color = potionButton.interactable ? Color.white : new Color(.75f, .83f, .9f, 1f);
        if (potionCooldownLabel)
            potionCooldownLabel.text = seconds > 0 ? $"READY IN {seconds}s" : $"HEAL {potionItem.healing} HP";
    }

    void BuildAbilityControls()
    {
        bottomRightRoot = CreateBottomLayoutRoot("BottomRightRoot");
        EnsureAbilityReferenceArrays();
        const float primarySize = 148f;
        const float slotSize = 108f;
        abilitySlotSize = slotSize;
        float clusterBottom = safeAreaPadding + 140f;
        CreateButton("AutoAttackButton", bottomRightRoot, "AA",
            new Vector2(-safeAreaPadding, clusterBottom), new Vector2(primarySize, primarySize),
            true, Gold, out autoAttackButton, out _, true);
        Text aaLabel = autoAttackButton.transform.Find("Label")?.GetComponent<Text>();
        if (aaLabel)
        {
            aaLabel.fontSize = 27;
            aaLabel.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            aaLabel.rectTransform.sizeDelta = new Vector2(primarySize - 16f, 42f);
        }
        if (!HasCompleteButtonArtwork("AutoAttackButton"))
        {
            Text aaCaption = CreateText("AutoAttackCaption", autoAttackButton.transform, "AUTO ATTACK",
                new Vector2(0f, -31f), new Vector2(primarySize - 18f, 20f), 10,
                TextAnchor.MiddleCenter, new Color(1f, .86f, .58f, .95f), false);
            AddTextShadow(aaCaption);
        }
        autoAttackButton.onClick.AddListener(TriggerAutoAttack);

        // Ability controls follow a spacious right-thumb arc. The fourth future
        // slot remains available without colliding with the active controls.
        fireballView = AbilityButtonView.Create(bottomRightRoot, "FireballButton",
            new Vector2(slotSize, slotSize),
            new Vector2(-safeAreaPadding - primarySize - 16f, clusterBottom + 4f), true,
            hudSkin && hudSkin.abilityFrame ? hudSkin.abilityFrame :
                (fireballFrameOverride ? fireballFrameOverride : ornateButtonFrameSprite),
            fireballBackgroundOverride ? fireballBackgroundOverride : circleSprite,
            fireballIconOverride, buttonCyanSprite);
        fireballView.SetLayerScales(fireballFrameScale, fireballBackgroundScale, fireballIconScale);
        if (hudSkin && hudSkin.abilityFrame) fireballView.UseCleanMobileRing();
        else fireballView.SetDecorativeRingVisible(true);
        if (hudSkin) fireballView.SetStateSprites(hudSkin.selectedOverlay, hudSkin.pressedOverlay,
            hudSkin.disabledOverlay, hudSkin.cooldownOverlay);
        fireballButton = fireballView.Button;
        fireballView.SetLabel(string.Empty);
        MobileButtonPressFeedback fireballPress = fireballButton.gameObject.AddComponent<MobileButtonPressFeedback>();
        fireballPress.Configure(fireballButton);
        fireballButton.onClick.AddListener(ToggleFireballTargeting);
        CreateFireballUpgradeBadge();

        CreatePlaceholderAbility("AbilitySlot2",
            new Vector2(-safeAreaPadding - primarySize - 4f, clusterBottom + 94f),
            2, out futureAbilitySlots[0], out futureAbilityLabels[0]);
        geomancerSecondaryButton = futureAbilitySlots[0].GetComponent<Button>();
        geomancerSecondaryButton.onClick.AddListener(TriggerEarthSpikes);
        GameObject earthGlyphObject = new GameObject("EarthSpikesGlyph", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(AbilityButtonGlyph));
        earthGlyphObject.transform.SetParent(geomancerSecondaryButton.transform, false);
        RectTransform earthGlyphRect = earthGlyphObject.GetComponent<RectTransform>();
        earthGlyphRect.anchorMin = earthGlyphRect.anchorMax = new Vector2(.5f, .5f);
        earthGlyphRect.pivot = new Vector2(.5f, .5f);
        earthGlyphRect.anchoredPosition = Vector2.zero;
        earthGlyphRect.sizeDelta = Vector2.one * (abilitySlotSize * .58f);
        geomancerSecondaryGlyph = earthGlyphObject.GetComponent<AbilityButtonGlyph>();
        geomancerSecondaryGlyph.SetStyle(AbilityGlyphStyle.EarthSpikes);
        geomancerSecondaryGlyph.raycastTarget = false;
        geomancerSecondaryGlyph.gameObject.SetActive(false);
        CreateGeomancerSecondaryUpgradeBadge();
        geomancerPassiveLabel = CreateText("SeismicFuryIndicator", bottomRightRoot, "FURY 0/5",
            new Vector2(-safeAreaPadding - primarySize - 18f, clusterBottom + 184f),
            new Vector2(150f, 32f), 13, TextAnchor.MiddleCenter, new Color(.4f, .82f, 1f), false);
        RectTransform passiveRect = geomancerPassiveLabel.rectTransform;
        passiveRect.anchorMin = passiveRect.anchorMax = new Vector2(1f, 0f);
        passiveRect.pivot = new Vector2(1f, 0f);
        geomancerPassiveLabel.gameObject.SetActive(false);
        CreatePlaceholderAbility("AbilitySlot3",
            new Vector2(-safeAreaPadding - 58f, clusterBottom + 150f),
            4, out futureAbilitySlots[1], out futureAbilityLabels[1]);
        CreatePlaceholderAbility("AbilitySlot4",
            new Vector2(-safeAreaPadding, clusterBottom + 162f),
            6, out futureAbilitySlots[2], out futureAbilityLabels[2]);
    }

    void EnsureAbilityReferenceArrays()
    {
        if (futureAbilitySlots == null || futureAbilitySlots.Length != 3)
            futureAbilitySlots = new Image[3];
        if (futureAbilityLabels == null || futureAbilityLabels.Length != 3)
            futureAbilityLabels = new Text[3];
    }

    bool RebindAbilityHudReferences()
    {
        EnsureAbilityReferenceArrays();
        if (!safeRoot) return false;

        bottomLeftRoot = safeRoot.Find("BottomLeftRoot") as RectTransform;
        bottomRightRoot = safeRoot.Find("BottomRightRoot") as RectTransform;
        progressionRoot = safeRoot.Find("ProgressionRoot") as RectTransform;
        Transform abilityParent = bottomRightRoot ? bottomRightRoot : safeRoot;
        Transform fireballRoot = abilityParent.Find("FireballButton");
        if (!fireballView && fireballRoot) fireballView = fireballRoot.GetComponent<AbilityButtonView>();
        if (!fireballButton && fireballRoot) fireballButton = fireballRoot.GetComponent<Button>();

        string[] slotNames = { "AbilitySlot2", "AbilitySlot3", "AbilitySlot4" };
        for (int i = 0; i < slotNames.Length; i++)
        {
            Transform slotRoot = abilityParent.Find(slotNames[i]);
            if (!slotRoot) continue;
            if (!futureAbilitySlots[i]) futureAbilitySlots[i] = slotRoot.GetComponent<Image>();
            if (!futureAbilityLabels[i])
            {
                Transform label = slotRoot.Find("EmptySlotLabel");
                if (label) futureAbilityLabels[i] = label.GetComponent<Text>();
            }
        }

        if (!geomancerSecondaryButton && futureAbilitySlots[0])
            geomancerSecondaryButton = futureAbilitySlots[0].GetComponent<Button>();
        if (!geomancerSecondaryGlyph && futureAbilitySlots[0])
            geomancerSecondaryGlyph = futureAbilitySlots[0].GetComponentInChildren<AbilityButtonGlyph>(true);
        if (!geomancerPassiveLabel)
        {
            Transform passive = abilityParent.Find("SeismicFuryIndicator");
            if (passive) geomancerPassiveLabel = passive.GetComponent<Text>();
        }

        if (!fireballView || !fireballButton || !geomancerSecondaryButton ||
            !geomancerSecondaryGlyph || !geomancerPassiveLabel) return false;
        for (int i = 0; i < 3; i++)
            if (!futureAbilitySlots[i] || !futureAbilityLabels[i]) return false;
        return true;
    }

    Rect ValidSafeArea()
    {
        return HlwResponsiveCanvas.ValidSafeArea();
    }

    void ApplySafeAreaRect(Rect safe)
    {
        HlwResponsiveCanvas.ApplySafeArea(safeRoot, safe);
    }

    void ApplyResponsiveLayout(bool force)
    {
        if (!canvas || !safeRoot || Screen.width < 1 || Screen.height < 1) return;

        Rect safe = ValidSafeArea();
        if (!force && Screen.width == lastLayoutScreenWidth && Screen.height == lastLayoutScreenHeight &&
            HlwResponsiveCanvas.Approximately(safe, lastLayoutSafeArea)) return;

        ApplySafeAreaRect(safe);
        Canvas.ForceUpdateCanvases();
        ResolveTopHudLayout();

        float shortestSide = Mathf.Min(safeRoot.rect.width, safeRoot.rect.height);
        float edge = Mathf.Clamp(shortestSide * .022f, 16f, Mathf.Max(18f, safeAreaPadding));

        float designScale = Mathf.Min(safeRoot.rect.width / HudReferenceWidth,
            safeRoot.rect.height / HudReferenceHeight);
        LayoutReferenceTopHud(designScale);

        // Bottom controls use the approved 1129x629 artwork reference. Scaling is
        // uniform; left and right groups remain pinned to their respective safe edges.
        float bottomScale = designScale;
        LayoutBottomRoot(bottomLeftRoot);
        LayoutBottomRoot(bottomRightRoot);
        LayoutBottomRoot(progressionRoot);

        // Sizes compensate for measured alpha/glow padding in the supplied PNGs.
        FitVisibleButton(bottomLeftRoot, "UnitsToggle", new Vector2(111f, 137f), 174f,
            bottomScale, false, new Rect(80f, 130f, 1056f, 1013f));
        FitVisibleButton(bottomLeftRoot, "ItemsToggle", new Vector2(296f, 137f), 174f,
            bottomScale, false, new Rect(77f, 127f, 1062f, 1018f));
        LayoutBottomElement(bottomLeftRoot, "UnitsFanMenu", new Vector2(322f, 352f),
            new Vector2(610f, 265f), bottomScale, false);
        LayoutBottomElement(bottomLeftRoot, "ItemsPanel", new Vector2(296f, 240f),
            new Vector2(230f, 98f), bottomScale, false);
        LayoutTierPurchaseCards(bottomScale);

        FitVisibleButton(bottomRightRoot, "AutoAttackButton", new Vector2(159f, 203f), 211f,
            bottomScale, true, new Rect(31f, 48f, 1187f, 1177f));
        bool geomancerBottomLayout = geomancerAbilities || (abilityLoadout && abilityLoadout.IsGeomancer);
        // Every secondary control uses one visible diameter and one radial distance.
        // Different full PNG sizes only compensate for each sprite's transparent padding.
        FitVisibleButton(bottomRightRoot, "FireballButton", new Vector2(322f, 154f), 95.2f,
            bottomScale, true, geomancerBottomLayout
                ? new Rect(0f, 0f, 1254f, 1254f)
                : new Rect(106f, 126f, 1034f, 1019f));
        FitVisibleButton(bottomRightRoot, "AbilitySlot2", new Vector2(325f, 263f), 95.2f,
            bottomScale, true, new Rect(150f, 118f, 951f, 1013f));
        FitVisibleButton(bottomRightRoot, "AbilitySlot3", new Vector2(252f, 346f), 95.2f,
            bottomScale, true, new Rect(150f, 118f, 951f, 1013f));
        FitVisibleButton(bottomRightRoot, "AbilitySlot4", new Vector2(131f, 388f), 95.2f,
            bottomScale, true, new Rect(150f, 118f, 951f, 1013f));
        LayoutBottomElement(bottomRightRoot, "SeismicFuryIndicator", new Vector2(430f, 410f),
            new Vector2(170f, 30f), bottomScale, true);

        // Stats is rectangular. Do not route it through the square authored-size path;
        // that previously changed its 144x64 touch target into 144x144 and pushed it
        // below the safe area after responsive scaling.
        FitVisibleRect(progressionRoot, "StatsToggleButton", new Vector2(574f, 77f),
            new Vector2(216f, 79f), bottomScale, true,
            new Rect(77f, 147f, 1611f, 595f));
        LayoutBottomElement(progressionRoot, "HeroProgressionPanel", new Vector2(245f, 76f),
            new Vector2(413f, 138f), bottomScale, true);
        LayoutProgressionContents(bottomScale);
        LayoutResultPanel(designScale);
        LayoutSkillPointsPanel(designScale);

        Canvas.ForceUpdateCanvases();
        lastLayoutScreenWidth = Screen.width;
        lastLayoutScreenHeight = Screen.height;
        lastLayoutSafeArea = safe;
        lastGeomancerBottomLayout = geomancerBottomLayout;
    }

    void LayoutSkillPointsPanel(float designScale)
    {
        RectTransform panel = FindNewestDirectRect("HeroStatsPanel");
        if (!panel || !safeRoot) return;
        float safeWidth = safeRoot.rect.width;
        float safeHeight = safeRoot.rect.height;
        float rightClearance = Mathf.Max(24f, 245f * designScale);
        float scale = Mathf.Max(.1f, Mathf.Min(designScale,
            (safeWidth - rightClearance - 24f) / 420f, (safeHeight - 36f) / 300f));
        float bottom = Mathf.Clamp(116f * designScale, 18f,
            Mathf.Max(18f, safeHeight - 300f * scale - 18f));
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, 0f);
        panel.sizeDelta = new Vector2(420f, 300f);
        panel.localScale = Vector3.one * scale;
        panel.anchoredPosition = new Vector2(-rightClearance, bottom);
    }

    void LayoutReferenceTopHud(float scale)
    {
        RectTransform topHud = FindNewestDirectRect("TopHudRoot");
        if (topHud)
        {
            topHud.anchorMin = Vector2.zero;
            topHud.anchorMax = Vector2.one;
            topHud.pivot = new Vector2(.5f, .5f);
            topHud.offsetMin = Vector2.zero;
            topHud.offsetMax = Vector2.zero;
            topHud.localScale = Vector3.one;

            // Coordinates are the measured 1672x941 visible-art reference.
            LayoutTopReferencePanel(topHud.Find("EconomyPanel") as RectTransform,
                new Vector2(-10.5f, 52f), new Vector2(743f, 78f), scale, false,
                new Vector2(755.6f, 98f), new Vector2(0f, 3f));
            float safeWidth = safeRoot.rect.width / scale;
            // Preserve Economy's centered position and a 10-unit gap to each
            // neighbouring panel. Wider phones get the full 11% extension.
            float availableBaseWidth = Mathf.Max(0f, safeWidth * .5f - 361f - 25f);
            float baseWidthFactor = Mathf.Min(1f, availableBaseWidth / (254f + 242f));
            float redWidth = 254f * baseWidthFactor;
            float blueWidth = 242f * baseWidthFactor;
            float blueCenterFromRight = 5f + blueWidth * .5f;
            float redCenterFromRight = 5f + blueWidth + 10f + redWidth * .5f;
            LayoutTopReferencePanel(topHud.Find("RedBasePanel") as RectTransform,
                new Vector2(redCenterFromRight, 54f), new Vector2(redWidth, 74f), scale, true,
                new Vector2(redWidth * 230.5f / 228f, 85.6f), Vector2.zero);
            LayoutTopReferencePanel(topHud.Find("BlueBasePanel") as RectTransform,
                new Vector2(blueCenterFromRight, 54f), new Vector2(blueWidth, 74f), scale, true,
                new Vector2(blueWidth * 219.6f / 217f, 85.8f), Vector2.zero);
            LayoutTopReferenceContent(topHud, scale);
        }

        LayoutTopLeftReferenceElement("SchematicMinimap", new Rect(15f, 18f, 291f, 183f), scale);
        LayoutTopLeftReferenceElement("GameSpeedToggle", new Rect(316f, 23f, 86f, 68f), scale);
        LayoutTopLeftReferenceElement("PlayerHeroFocusCard", new Rect(12f, 209f, 295f, 64f), scale);
        LayoutTopLeftReferenceElement("EnemyHeroFocusCard", new Rect(12f, 282f, 295f, 64f), scale);
        LayoutHeroFocusCardContents("PlayerHeroFocusCard", new Vector2(295f, 64f), scale);
        LayoutHeroFocusCardContents("EnemyHeroFocusCard", new Vector2(295f, 64f), scale);
        LayoutSpeedContents(scale);
    }

    static void LayoutTopReferencePanel(RectTransform panel, Vector2 centerFromAnchor,
        Vector2 visibleSize, float scale, bool fromRight, Vector2 artworkSize, Vector2 artworkOffset)
    {
        if (!panel) return;
        panel.anchorMin = panel.anchorMax = new Vector2(fromRight ? 1f : .5f, 1f);
        panel.pivot = new Vector2(.5f, .5f);
        panel.anchoredPosition = new Vector2((fromRight ? -1f : 1f) * centerFromAnchor.x * scale,
            -centerFromAnchor.y * scale);
        panel.sizeDelta = visibleSize * scale;
        panel.localScale = Vector3.one;
        RectTransform artwork = panel.Find("Artwork") as RectTransform;
        if (artwork)
        {
            artwork.anchorMin = artwork.anchorMax = artwork.pivot = new Vector2(.5f, .5f);
            artwork.anchoredPosition = artworkOffset * scale;
            artwork.sizeDelta = artworkSize * scale;
            artwork.localScale = Vector3.one;
        }
        RectTransform content = panel.Find("Content") as RectTransform;
        if (content)
        {
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.localScale = Vector3.one;
        }
    }

    void LayoutTopReferenceContent(RectTransform topHud, float scale)
    {
        RectTransform economy = topHud.Find("EconomyPanel") as RectTransform;
        RectTransform interior = economy ? economy.Find("Content/Interior") as RectTransform : null;
        if (interior)
        {
            interior.anchorMin = Vector2.zero;
            interior.anchorMax = Vector2.one;
            interior.offsetMin = Vector2.zero;
            interior.offsetMax = Vector2.zero;
            float[] textX = { -184.5f, 27.5f, 227.5f };
            string[] names = { "GoldCell", "IncomeCell", "NextCell" };
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform cell = interior.Find(names[i]) as RectTransform;
                if (!cell) continue;
                cell.anchorMin = cell.anchorMax = cell.pivot = new Vector2(.5f, .5f);
                cell.anchoredPosition = new Vector2(textX[i], 0f) * scale;
                cell.sizeDelta = new Vector2(190f, 78f) * scale;
                LayoutTopStatCellContent(cell, scale);
            }
        }
        LayoutBaseReferenceContent(topHud.Find("RedBasePanel") as RectTransform, scale);
        LayoutBaseReferenceContent(topHud.Find("BlueBasePanel") as RectTransform, scale);
    }

    static void LayoutTopStatCellContent(RectTransform cell, float scale)
    {
        RectTransform icon = cell.Find("Icon") as RectTransform;
        RectTransform iconWell = cell.Find("IconWell") as RectTransform;
        RectTransform caption = cell.Find("Caption") as RectTransform;
        RectTransform value = cell.Find("Value") as RectTransform;
        LayoutCenteredChild(icon ? icon : iconWell, new Vector2(-75f, -1f), new Vector2(40f, 40f), scale);
        LayoutCenteredChild(caption, new Vector2(0f, 12f), new Vector2(118f, 22f), scale);
        LayoutCenteredChild(value, new Vector2(0f, -14f), new Vector2(128f, 28f), scale);
        SetScaledFont(caption, 18, scale);
        SetScaledFont(value, 23, scale);
        LayoutClockChild(cell.Find("ClockMinuteHand") as RectTransform,
            new Vector2(-75f, 3f), new Vector2(2.5f, 12f), scale);
        LayoutClockChild(cell.Find("ClockHourHand") as RectTransform,
            new Vector2(-71f, -1f), new Vector2(9f, 2.5f), scale);
        LayoutClockChild(cell.Find("ClockHub") as RectTransform,
            new Vector2(-75f, -1f), new Vector2(4f, 4f), scale);
    }

    static void LayoutBaseReferenceContent(RectTransform panel, float scale)
    {
        RectTransform group = panel ? panel.Find("Content/BaseContentGroup") as RectTransform : null;
        if (!group) return;
        group.anchorMin = Vector2.zero;
        group.anchorMax = Vector2.one;
        group.offsetMin = Vector2.zero;
        group.offsetMax = Vector2.zero;
        const float iconX = -64f;
        const float textX = 20f;
        LayoutCenteredChild(group.Find("FactionIcon") as RectTransform,
            new Vector2(iconX, -1f), new Vector2(40f, 40f), scale);
        LayoutCenteredChild(group.Find("Caption") as RectTransform,
            new Vector2(textX, 14f), new Vector2(140f, 22f), scale);
        LayoutCenteredChild(group.Find("Value") as RectTransform,
            new Vector2(textX, -12f), new Vector2(140f, 28f), scale);
        SetScaledFont(group.Find("Caption") as RectTransform, 18, scale);
        SetScaledFont(group.Find("Value") as RectTransform, 23, scale);
    }

    static void LayoutCenteredChild(RectTransform child, Vector2 position, Vector2 size, float scale)
    {
        if (!child) return;
        child.anchorMin = child.anchorMax = child.pivot = new Vector2(.5f, .5f);
        child.anchoredPosition = position * scale;
        child.sizeDelta = size * scale;
        child.localScale = Vector3.one;
    }

    static void SetScaledFont(RectTransform rect, int referenceSize, float scale)
    {
        Text text = rect ? rect.GetComponent<Text>() : null;
        if (text) text.fontSize = Mathf.Max(1, Mathf.RoundToInt(referenceSize * scale));
    }

    static void LayoutClockChild(RectTransform child, Vector2 position, Vector2 size, float scale)
    {
        if (!child) return;
        child.anchoredPosition = position * scale;
        child.sizeDelta = size * scale;
    }

    void LayoutTopLeftReferenceElement(string objectName, Rect reference, float scale)
    {
        RectTransform rect = FindNewestDirectRect(objectName);
        if (!rect) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(reference.x * scale, -reference.y * scale);
        rect.sizeDelta = new Vector2(reference.width, reference.height) * scale;
        rect.localScale = Vector3.one;
    }

    void LayoutHeroFocusCardContents(string objectName, Vector2 referenceSize, float scale)
    {
        RectTransform card = FindNewestDirectRect(objectName);
        if (!card) return;
        LayoutCenteredChild(card.Find("HeroLevel") as RectTransform,
            new Vector2(-112f, 0f), new Vector2(52f, 38f), scale);
        LayoutCenteredChild(card.Find("PortraitWell") as RectTransform,
            new Vector2(-62f, 0f), new Vector2(44f, 44f), scale);
        LayoutCenteredChild(card.Find("HeroPortrait") as RectTransform,
            new Vector2(-62f, 0f), new Vector2(40f, 40f), scale);
        LayoutCenteredChild(card.Find("CardTitle") as RectTransform,
            new Vector2(43f, 8f), new Vector2(146f, 20f), scale);
        LayoutCenteredChild(card.Find("HeroName") as RectTransform,
            new Vector2(43f, -12f), new Vector2(146f, 18f), scale);
        SetScaledFont(card.Find("HeroLevel") as RectTransform, 16, scale);
        SetScaledFont(card.Find("CardTitle") as RectTransform, 12, scale);
        SetScaledFont(card.Find("HeroName") as RectTransform, 10, scale);
        RectTransform glow = card.Find("SelectedGlow") as RectTransform;
        if (glow) LayoutCenteredChild(glow, Vector2.zero, referenceSize + new Vector2(4f, 4f), scale);
        RectTransform metal = card.Find("MetallicCardFrame") as RectTransform;
        if (metal) LayoutCenteredChild(metal, Vector2.zero, referenceSize, scale);
    }

    void LayoutSpeedContents(float scale)
    {
        RectTransform speed = FindNewestDirectRect("GameSpeedToggle");
        if (!speed) return;
        LayoutCenteredChild(speed.Find("ActiveGlow") as RectTransform,
            Vector2.zero, new Vector2(73f, 46f), scale);
        RectTransform label = speed.Find("SpeedLabel") as RectTransform;
        RectTransform timer = speed.Find("MatchTime") as RectTransform;
        LayoutCenteredChild(label, new Vector2(0f, 7f), new Vector2(61f, 18f), scale);
        LayoutCenteredChild(timer, new Vector2(0f, -10f), new Vector2(61f, 14f), scale);
        SetScaledFont(label, 16, scale);
        SetScaledFont(timer, 9, scale);
    }

    static void LayoutBottomRoot(RectTransform root)
    {
        if (!root) return;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(.5f, .5f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;
    }

    static void LayoutBottomElement(RectTransform root, string objectName, Vector2 centerFromEdge,
        Vector2 fullArtworkSize, float scale, bool fromRight, float authoredSquareSize = 0f)
    {
        if (!root) return;
        RectTransform rect = root.Find(objectName) as RectTransform;
        if (!rect) return;
        rect.anchorMin = rect.anchorMax = new Vector2(fromRight ? 1f : 0f, 0f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2((fromRight ? -1f : 1f) * centerFromEdge.x * scale,
            centerFromEdge.y * scale);
        if (authoredSquareSize > 0f)
        {
            rect.sizeDelta = Vector2.one * authoredSquareSize;
            float artworkScale = fullArtworkSize.x / authoredSquareSize * scale;
            rect.localScale = Vector3.one * artworkScale;
        }
        else
        {
            rect.sizeDelta = fullArtworkSize * scale;
            rect.localScale = Vector3.one;
        }
    }

    // Alpha bounds are measured from the supplied PNGs at alpha > 16/255.
    // Keeping the button RectTransform larger than the visible art retains a generous touch target.
    static void FitVisibleButton(RectTransform root, string objectName, Vector2 centerFromEdge,
        float visibleDiameter, float scale, bool fromRight, Rect alphaPixels)
    {
        if (!root) return;
        RectTransform rect = root.Find(objectName) as RectTransform;
        if (!rect) return;
        const float spritePixels = 1254f;
        // Geometric mean shares small asymmetries in the source glow between X/Y,
        // keeping both visible dimensions within the reference tolerance.
        float fullDiameter = visibleDiameter * spritePixels /
            Mathf.Sqrt(alphaPixels.width * alphaPixels.height);
        float normalizedCenterX = (alphaPixels.center.x / spritePixels - .5f) * fullDiameter;
        float normalizedCenterY = (.5f - alphaPixels.center.y / spritePixels) * fullDiameter;
        Vector2 rootCenter = centerFromEdge - new Vector2(fromRight ? -normalizedCenterX : normalizedCenterX,
            normalizedCenterY);
        float authoredSize = rect.sizeDelta.x;
        LayoutBottomElement(root, objectName, rootCenter, Vector2.one * fullDiameter,
            scale, fromRight, authoredSize);
    }

    static void FitVisibleRect(RectTransform root, string objectName, Vector2 centerFromEdge,
        Vector2 visibleSize, float scale, bool fromRight, Rect alphaPixels)
    {
        if (!root) return;
        const float sourceWidth = 1774f;
        const float sourceHeight = 887f;
        float fullWidth = visibleSize.x * sourceWidth / alphaPixels.width;
        float fullHeight = fullWidth * sourceHeight / sourceWidth;
        Vector2 artOffset = new Vector2((alphaPixels.center.x / sourceWidth - .5f) * fullWidth,
            (.5f - alphaPixels.center.y / sourceHeight) * fullHeight);
        Vector2 rootCenter = centerFromEdge - new Vector2(fromRight ? -artOffset.x : artOffset.x,
            artOffset.y);
        LayoutBottomElement(root, objectName, rootCenter, new Vector2(fullWidth, fullHeight), scale, fromRight);
    }

    void LayoutPurchaseCard(string objectName, Vector2 referenceCenter, float scale)
    {
        RectTransform menu = bottomLeftRoot ? bottomLeftRoot.Find("UnitsFanMenu") as RectTransform : null;
        RectTransform card = menu ? menu.Find(objectName) as RectTransform : null;
        if (!card) return;
        // The menu spans x=69..469 and bottom=241 in the 1672x941 reference.
        card.anchorMin = card.anchorMax = card.pivot = Vector2.zero;
        card.anchoredPosition = new Vector2(referenceCenter.x - 58f - 69f,
            referenceCenter.y - 58f - 241f) * scale;
        card.localScale = Vector3.one * (1.16f * scale);
    }

    void LayoutProgressionContents(float scale)
    {
        if (!progressionRoot) return;
        RectTransform panel = progressionRoot.Find("HeroProgressionPanel") as RectTransform;
        if (!panel) return;
        RectTransform level = panel.Find("LevelBadge") as RectTransform;
        RectTransform xp = panel.Find("XPBarRoot") as RectTransform;
        if (level)
        {
            level.anchorMin = level.anchorMax = level.pivot = new Vector2(.5f, .5f);
            level.anchoredPosition = new Vector2(-153f, 0f) * scale;
            level.sizeDelta = new Vector2(79f, 90f) * scale;
            level.localScale = Vector3.one;
            RectTransform levelCaption = level.Find("LevelLabelText") as RectTransform;
            if (levelCaption)
            {
                levelCaption.anchoredPosition = new Vector2(0f, 28f) * scale;
                levelCaption.sizeDelta = new Vector2(44f, 22f) * scale;
                SetScaledFont(levelCaption, 16, scale);
            }
            RectTransform levelText = level.Find("LevelValueText") as RectTransform;
            if (levelText)
            {
                levelText.anchoredPosition = new Vector2(0f, -5f) * scale;
                levelText.sizeDelta = new Vector2(58f, 48f) * scale;
                SetScaledFont(levelText, 36, scale);
            }
        }
        if (xp)
        {
            xp.anchorMin = xp.anchorMax = xp.pivot = new Vector2(.5f, .5f);
            xp.anchoredPosition = new Vector2(36f, -4f) * scale;
            xp.sizeDelta = new Vector2(319f, 52f) * scale;
            xp.localScale = Vector3.one;
            RectTransform track = xp.Find("TrackBackground") as RectTransform;
            RectTransform clip = xp.Find("FillClip") as RectTransform;
            RectTransform fill = clip ? clip.Find("XPFill") as RectTransform : null;
            RectTransform text = xp.Find("XPText") as RectTransform;
            // Layout from the *visible painted track* of the supplied sprite, not
            // the much larger transparent PNG/RectTransform bounds. This also
            // corrects the previous ~9-unit vertical displacement.
            bool customXpArtwork = hudSkin && hudSkin.progressionBarArtwork;
            // sizeDelta is available immediately after layout; rect can still contain
            // the previous frame's dimensions during a CanvasScaler rebuild.
            Vector2 artworkSize = panel.sizeDelta;
            Vector2 trackCenterInPanel = customXpArtwork
                ? new Vector2((XpArtworkTrack.center.x - .5f) * artworkSize.x,
                    (.5f - XpArtworkTrack.center.y) * artworkSize.y)
                : new Vector2(30f, 0f) * scale;
            Vector2 trackCenter = trackCenterInPanel - xp.anchoredPosition;
            Vector2 paintedSize = customXpArtwork
                ? new Vector2(XpArtworkTrack.width * artworkSize.x,
                    XpArtworkTrack.height * artworkSize.y)
                : new Vector2(244f, 18f) * scale;
            Vector2 interiorSize = paintedSize - new Vector2(9f, 6f) * scale;
            if (track) LayoutCenteredChild(track, trackCenter, interiorSize, 1f);
            if (clip) LayoutCenteredChild(clip, trackCenter, interiorSize, 1f);
            if (fill) LayoutCenteredChild(fill, Vector2.zero, interiorSize, 1f);
            if (text)
            {
                // The artwork reserves the right side of the bar for the XP readout.
                // Placing it over the narrow painted fill track made both illegible.
                Vector2 textCenterInPanel = new Vector2((.76f - .5f) * artworkSize.x,
                    (.5f - XpArtworkTrack.center.y) * artworkSize.y);
                Vector2 textCenter = customXpArtwork
                    ? textCenterInPanel - xp.anchoredPosition : trackCenter;
                Vector2 textSize = customXpArtwork
                    ? new Vector2(.23f * artworkSize.x, 24f * scale)
                    : new Vector2(interiorSize.x - 20f * scale, 24f * scale);
                LayoutCenteredChild(text, textCenter, textSize, 1f);
                SetScaledFont(text, 13, scale);
            }
        }
    }

    void SetAbilityOnArc(string objectName, Vector2 center, float radius, float degrees,
        float visibleSize, float authoredSize)
    {
        float radians = degrees * Mathf.Deg2Rad;
        Vector2 buttonCenter = center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
        Vector2 anchoredPosition = buttonCenter + new Vector2(visibleSize * .5f, -visibleSize * .5f);
        SetDirectRect(objectName, new Vector2(1f, 0f), new Vector2(1f, 0f),
            anchoredPosition, new Vector2(visibleSize, visibleSize));
        RectTransform root = FindNewestDirectRect(objectName);
        if (!root) return;
        float contentScale = visibleSize / Mathf.Max(1f, authoredSize);
        for (int i = 0; i < root.childCount; i++)
            root.GetChild(i).localScale = Vector3.one * contentScale;
    }

    static float RequiredArcRadiusForRectGap(float[] angles, float requiredAxisSeparation)
    {
        float requiredRadius = 0f;
        for (int i = 1; i < angles.Length; i++)
        {
            float previous = angles[i - 1] * Mathf.Deg2Rad;
            float current = angles[i] * Mathf.Deg2Rad;
            float dx = Mathf.Abs(Mathf.Cos(previous) - Mathf.Cos(current));
            float dy = Mathf.Abs(Mathf.Sin(previous) - Mathf.Sin(current));
            requiredRadius = Mathf.Max(requiredRadius,
                requiredAxisSeparation / Mathf.Max(.001f, Mathf.Max(dx, dy)));
        }
        return requiredRadius;
    }

    void LayoutThreePartTopbar(RectTransform root, float scale, float safeWidth, float rightInset)
    {
        if (!root) return;
        const float economyWidth = 740f;
        const float baseWidth = 290f;
        const float panelHeight = 130f;
        const float gap = 12f;
        root.sizeDelta = new Vector2(safeWidth, panelHeight);
        root.localScale = Vector3.one;

        RectTransform economy = root.Find("EconomyPanel") as RectTransform;
        RectTransform red = root.Find("RedBasePanel") as RectTransform;
        RectTransform blue = root.Find("BlueBasePanel") as RectTransform;
        LayoutTopbarPanel(economy, Vector2.zero, new Vector2(economyWidth, panelHeight), scale);
        float right = safeWidth * .5f - rightInset;
        float blueCenter = right - baseWidth * scale * .5f;
        float redCenter = right - baseWidth * scale - gap * scale - baseWidth * scale * .5f;
        LayoutTopbarPanel(red, new Vector2(redCenter, 0f), new Vector2(baseWidth, panelHeight), scale);
        LayoutTopbarPanel(blue, new Vector2(blueCenter, 0f), new Vector2(baseWidth, panelHeight), scale);

        if (economy)
        {
            RectTransform interior = economy.Find("Content/Interior") as RectTransform;
            if (interior)
            {
                const float horizontalPadding = 42f;
                const float verticalPadding = 8f;
                interior.anchorMin = interior.anchorMax = interior.pivot = new Vector2(.5f, .5f);
                interior.anchoredPosition = new Vector2(0f, -1f);
                interior.sizeDelta = new Vector2(economyWidth - horizontalPadding * 2f,
                    TopbarArtworkHeight(economy, economyWidth) - verticalPadding * 2f);
                string[] cells = { "GoldCell", "IncomeCell", "NextCell" };
                float columnWidth = interior.sizeDelta.x / cells.Length;
                for (int i = 0; i < cells.Length; i++)
                {
                    RectTransform cell = interior.Find(cells[i]) as RectTransform;
                    if (!cell) continue;
                    cell.anchorMin = cell.anchorMax = cell.pivot = new Vector2(.5f, .5f);
                    cell.anchoredPosition = new Vector2((i - 1f) * columnWidth, 0f);
                    cell.sizeDelta = new Vector2(columnWidth, interior.sizeDelta.y);
                }
            }
        }
    }

    static void LayoutTopbarPanel(RectTransform panel, Vector2 position, Vector2 size, float scale)
    {
        if (!panel) return;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 1f);
        panel.anchoredPosition = position;
        panel.sizeDelta = size;
        panel.localScale = new Vector3(scale, scale, 1f);
        // Increase only the vertical presentation by 22.5%. Widths and panel
        // centers stay unchanged, while the top pivot makes the extra height
        // extend downward into the battlefield.
        float artworkHeight = TopbarArtworkHeight(panel, size.x) * 1.225f;
        RectTransform artwork = panel.Find("Artwork") as RectTransform;
        RectTransform content = panel.Find("Content") as RectTransform;
        // Lift only the transparently padded artwork. Keeping the panel/root at y=0
        // preserves safe-area bounds while bringing the visible ornamental edge closer
        // to the physical top edge without clipping its endcaps.
        LayoutTopAlignedChild(artwork, new Vector2(size.x, artworkHeight), 4f);
        bool economyPanel = panel.name == "EconomyPanel";
        Vector2 contentSize = economyPanel
            ? new Vector2(size.x, artworkHeight)
            : new Vector2(size.x - 28f, Mathf.Max(40f, artworkHeight - 16f));
        LayoutTopAlignedChild(content, contentSize, economyPanel ? 0f : -9f);
    }

    static float TopbarArtworkHeight(RectTransform panel, float width)
    {
        Image artwork = panel ? panel.Find("Artwork")?.GetComponent<Image>() : null;
        return artwork && artwork.sprite
            ? width * artwork.sprite.rect.height / artwork.sprite.rect.width
            : panel ? panel.sizeDelta.y : 68f;
    }

    static void LayoutTopAlignedChild(RectTransform child, Vector2 size, float y = 0f)
    {
        if (!child) return;
        child.anchorMin = child.anchorMax = child.pivot = new Vector2(.5f, 1f);
        child.anchoredPosition = new Vector2(0f, y);
        child.sizeDelta = size;
    }

    float ResponsivePrimaryButtonSize()
    {
        if (!safeRoot || safeRoot.rect.height < 1f || safeRoot.rect.width < 1f)
            return autoAttackButtonSize;
        return Mathf.Max(96f, Mathf.Min(autoAttackButtonSize, 130f,
            (safeRoot.rect.height - safeAreaPadding - 4f) / 1.9f,
            (safeRoot.rect.width - safeAreaPadding - 4f) / 1.9f));
    }

    void SetDirectRect(string objectName, Vector2 anchor, Vector2 pivot, Vector2 position,
        Vector2? size = null)
    {
        if (!safeRoot) return;
        RectTransform rect = FindNewestDirectRect(objectName);
        if (!rect) return;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        if (size.HasValue) rect.sizeDelta = size.Value;
        rect.localScale = Vector3.one;
    }

    RectTransform FindNewestDirectRect(string objectName)
    {
        if (!safeRoot) return null;
        // BuildCanvas destroys the previous runtime hierarchy at end-of-frame.
        // Search backwards so a same-frame rebuild always lays out the newly-created group.
        for (int i = safeRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = safeRoot.GetChild(i);
            if (child.name == objectName) return child as RectTransform;
        }
        return null;
    }

    void ConfigureLayoutValidator()
    {
        if (!canvas || !safeRoot) return;
        HlwUiLayoutValidator validator = canvas.GetComponent<HlwUiLayoutValidator>();
        if (!validator) validator = canvas.gameObject.AddComponent<HlwUiLayoutValidator>();
        validator.Configure(canvas, safeRoot,
            "SchematicMinimap", "PlayerHeroFocusCard", "EnemyHeroFocusCard", "GameSpeedToggle",
            "TopHudRoot", "EconomyPanel", "GoldCell", "IncomeCell", "NextCell",
            "RedBasePanel", "BlueBasePanel");
    }

    void LogPortraitDiagnosticsOnce()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;
        if (reportedPortraitDiagnostics || !archerButton || !knightButton || !wizardButton) return;
        reportedPortraitDiagnostics = true;
        Debug.Log("[HLW Unit Portraits] " + PortraitDiagnostic(knightButton) + " | " +
            PortraitDiagnostic(archerButton) + " | " + PortraitDiagnostic(wizardButton), this);
    }

    static string PortraitDiagnostic(Button button)
    {
        Transform portraitRoot = button.transform.Find("PortraitMask/UnitPortrait");
        Transform overlayRoot = button.transform.Find("PortraitMask/UnaffordableOverlay");
        Transform frameRoot = button.transform.Find("FantasyFrame");
        Image portrait = portraitRoot ? portraitRoot.GetComponent<Image>() : null;
        Image overlay = overlayRoot ? overlayRoot.GetComponent<Image>() : null;
        return $"{button.name}(affordable={button.interactable}, portrait={portrait?.color.ToString() ?? "missing"}, " +
            $"overlayAlpha={(overlay ? overlay.color.a : -1f):0.##}, " +
            $"frameSibling={(frameRoot ? frameRoot.GetSiblingIndex() : -1)}, " +
            $"portraitSibling={(portraitRoot ? portraitRoot.parent.GetSiblingIndex() : -1)})";
    }

    void CreatePlaceholderAbility(string name, Vector2 position, int requiredLevel,
        out Image slotImage, out Text slotLabel)
    {
        GameObject slot = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        slot.transform.SetParent(bottomRightRoot ? bottomRightRoot : safeRoot, false);
        RectTransform rect = slot.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(abilitySlotSize, abilitySlotSize);
        Image image = slot.GetComponent<Image>();
        slotImage = image;
        // The root is a touch target only; disabled Button tint must not dim the art.
        image.sprite = null;
        image.color = Color.clear;
        image.raycastTarget = true;
        Button slotButton = slot.GetComponent<Button>();
        slotButton.transition = Selectable.Transition.None;
        if (hudSkin && hudSkin.abilityFrame)
        {
            Image activeFrame = CreateImage("ActiveAbilityFrame", slot.transform, hudSkin.abilityFrame,
                Vector2.zero, Vector2.one * abilitySlotSize, Color.white, false);
            activeFrame.preserveAspect = true;
            activeFrame.gameObject.SetActive(false);
        }
        Image activeIcon = CreateImage("ActiveAbilityIcon", slot.transform, null, Vector2.zero,
            Vector2.one * (abilitySlotSize * .52f), Color.white, false);
        activeIcon.gameObject.SetActive(false);
        Image lockedArtwork = CreateImage("LockedArtwork", slot.transform,
            hudSkin ? hudSkin.lockedAbilityOverlay : null, Vector2.zero,
            Vector2.one * abilitySlotSize, Color.white, false);
        lockedArtwork.preserveAspect = true;
        if (!lockedArtwork.sprite)
            Debug.LogWarning("[HLW HUD] Premium locked ability artwork is not assigned in HlwHudSkin.", this);
        slotButton.interactable = false;
        slotLabel = CreateText("EmptySlotLabel", slot.transform, $"LV {requiredLevel}",
            new Vector2(0f, -abilitySlotSize * .29f),
            new Vector2(abilitySlotSize * .56f, abilitySlotSize * .2f), 10,
            TextAnchor.MiddleCenter, new Color(.84f, .94f, 1f, 1f), false);
        AddTextShadow(slotLabel);
        MobileButtonPressFeedback feedback = slot.AddComponent<MobileButtonPressFeedback>();
        feedback.Configure(slotButton);
    }

    void BuildHeroProgressionPanel()
    {
        progressionRoot = CreateBottomLayoutRoot("ProgressionRoot");

        GameObject panel = new GameObject("HeroProgressionPanel", typeof(RectTransform), typeof(Image), typeof(Outline));
        panel.transform.SetParent(progressionRoot, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(-160f, 38f);
        rect.sizeDelta = new Vector2(350f, 88f);
        Image background = panel.GetComponent<Image>();
        bool customProgression = hudSkin && hudSkin.progressionBarArtwork;
        background.sprite = customProgression ? hudSkin.progressionBarArtwork : capsuleSprite;
        background.type = Image.Type.Simple;
        background.preserveAspect = customProgression;
        background.color = customProgression ? Color.white : new Color(.025f, .045f, .075f, .98f);
        background.raycastTarget = false;
        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = customProgression
            ? new Color(0f, 0f, 0f, 0f) : new Color(.36f, .72f, 1f, .72f);
        outline.effectDistance = new Vector2(1f, 1f);
        if (!customProgression)
        {
            ChamferedFrameGraphic stripFrame = CreateChamferedLayer("ProgressionMetalwork", panel.transform,
                new Vector2(316f, 42f), new Color(.018f, .034f, .06f, .96f),
                new Color(.28f, .62f, .86f, .8f), 9f, 2f);
            stripFrame.transform.SetAsFirstSibling();
        }
        GameObject levelBadgeRoot = new GameObject("LevelBadge", typeof(RectTransform));
        levelBadgeRoot.transform.SetParent(panel.transform, false);
        RectTransform levelBadgeRect = levelBadgeRoot.GetComponent<RectTransform>();
        levelBadgeRect.anchorMin = levelBadgeRect.anchorMax = levelBadgeRect.pivot = new Vector2(.5f, .5f);
        levelBadgeRect.anchoredPosition = customProgression ? new Vector2(-139f, 0f) : new Vector2(-150f, 0f);
        levelBadgeRect.sizeDelta = customProgression ? new Vector2(64f, 64f) : new Vector2(48f, 48f);
        if (!customProgression)
        {
            Sprite levelArtwork = hudSkin && hudSkin.levelBadge ? hudSkin.levelBadge : circleSprite;
            Image fallbackBadge = CreateImage("BadgeArtwork", levelBadgeRoot.transform, levelArtwork,
                Vector2.zero, levelBadgeRect.sizeDelta, hudSkin && hudSkin.levelBadge ? Color.white : Gold, false);
            fallbackBadge.preserveAspect = true;
        }
        heroLevelCaption = CreateText("LevelLabelText", levelBadgeRoot.transform, "LV",
            new Vector2(0f, 28f), new Vector2(44f, 22f), 16,
            TextAnchor.MiddleCenter, new Color(1f, .84f, .44f, 1f), false);
        heroLevelCaption.fontStyle = FontStyle.Bold;
        heroLevelLabel = CreateText("LevelValueText", levelBadgeRoot.transform, "1",
            new Vector2(0f, -5f), new Vector2(58f, 48f), 36,
            TextAnchor.MiddleCenter, Gold, false);
        heroLevelLabel.fontStyle = FontStyle.Bold;

        GameObject xpBar = new GameObject("XPBarRoot", typeof(RectTransform));
        xpBar.transform.SetParent(panel.transform, false);
        RectTransform xpBarRect = xpBar.GetComponent<RectTransform>();
        xpBarRect.anchorMin = xpBarRect.anchorMax = xpBarRect.pivot = new Vector2(.5f, .5f);
        xpBarRect.anchoredPosition = Vector2.zero;
        xpBarRect.sizeDelta = rect.sizeDelta;
        CreateSolidImage("TrackBackground", xpBar.transform, Vector2.zero,
            new Vector2(150f, 10f), new Color(.012f, .024f, .05f, .95f));
        GameObject fillClip = new GameObject("FillClip", typeof(RectTransform), typeof(RectMask2D));
        fillClip.transform.SetParent(xpBar.transform, false);
        RectTransform clipRect = fillClip.GetComponent<RectTransform>();
        clipRect.anchorMin = clipRect.anchorMax = clipRect.pivot = new Vector2(.5f, .5f);
        clipRect.sizeDelta = new Vector2(150f, 10f);
        // Image.Type.Filled requires a Sprite. The skin's XP fill is unassigned,
        // while the old health-bar sprite has transparent ornamental margins.
        if (!xpSolidFillSprite)
            xpSolidFillSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(.5f, .5f), 100f);
        heroExperienceFill = CreateImage("XPFill", fillClip.transform, xpSolidFillSprite,
            Vector2.zero, new Vector2(150f, 10f), new Color(1f, .76f, .22f, 1f), false);
        heroExperienceFill.type = Image.Type.Filled;
        heroExperienceFill.fillMethod = Image.FillMethod.Horizontal;
        heroExperienceFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        heroExperienceFill.fillAmount = 0f;
        heroExperienceFill.preserveAspect = false;
        heroExperienceLabel = CreateText("XPText", xpBar.transform, "0 / 70 XP",
            Vector2.zero, new Vector2(145f, 16f),
            13, TextAnchor.MiddleRight, Color.white, false);
        heroExperienceLabel.resizeTextForBestFit = true;
        heroExperienceLabel.resizeTextMinSize = 8;
        heroExperienceLabel.resizeTextMaxSize = 13;
        AddTextShadow(heroExperienceLabel);
        Outline xpTextOutline = heroExperienceLabel.gameObject.AddComponent<Outline>();
        xpTextOutline.effectColor = new Color(.015f, .025f, .055f, .95f);
        xpTextOutline.effectDistance = Vector2.one;
    }

    void BuildSkillUpgradeControls()
    {
        displayedSkillPoints = -1;
        for (int i = 0; i < displayedAttributeValues.Length; i++)
        { displayedAttributeValues[i] = -1; displayedAttributeRates[i] = -1f; }
        Transform statsParent = progressionRoot ? progressionRoot : safeRoot;
        CreateButton("StatsToggleButton", statsParent, "STATS +", new Vector2(0f, 12f),
            new Vector2(144f, 64f), false, Cyan, out statsToggleButton, out statsToggleLabel, true);
        RectTransform statsRect = statsToggleButton.GetComponent<RectTransform>();
        statsRect.anchorMin = statsRect.anchorMax = statsRect.pivot = Vector2.zero;
        statsRect.anchoredPosition = new Vector2(0f, 12f);
        statsToggleButton.transition = Selectable.Transition.None;
        Image statsBackground = statsToggleButton.GetComponent<Image>();
        bool premiumStats = hudSkin && hudSkin.statsFrame;
        statsBackground.sprite = premiumStats ? hudSkin.statsFrame : capsuleSprite;
        statsBackground.type = premiumStats ? Image.Type.Simple : Image.Type.Sliced;
        statsBackground.preserveAspect = premiumStats;
        if (premiumStats)
        {
            statsBackground.color = Color.white;
            if (statsToggleLabel) statsToggleLabel.gameObject.SetActive(false);
            Outline statsOutline = statsToggleButton.GetComponent<Outline>();
            if (statsOutline) statsOutline.effectColor = Color.clear;
        }
        else
        {
            ChamferedFrameGraphic statsFrame = CreateChamferedLayer("StatsMetalwork", statsToggleButton.transform,
                new Vector2(128f, 62f), new Color(.02f, .055f, .082f, .94f),
                new Color(.25f, .76f, .94f, .84f), 8f, 2f);
            statsFrame.transform.SetAsFirstSibling();
        }
        statsPointBadge = CreateImage("AvailablePointBadge", statsToggleButton.transform, circleSprite,
            new Vector2(54f, 18f), new Vector2(26f, 26f), new Color(.06f, .53f, .73f, .96f), false);
        statsPointCount = CreateText("AvailablePointCount", statsPointBadge.transform, "1", Vector2.zero,
            new Vector2(24f, 24f), 15, TextAnchor.MiddleCenter, Color.white, false);
        statsPointBadge.gameObject.SetActive(false);
        statsToggleButton.onClick.AddListener(ToggleStatsPanel);

        statsPanel = new GameObject("HeroStatsPanel", typeof(RectTransform), typeof(Image));
        statsPanel.transform.SetParent(safeRoot, false);
        RectTransform panelRect = statsPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-245f, 116f);
        panelRect.sizeDelta = new Vector2(420f, 300f);
        Image blocker = statsPanel.GetComponent<Image>();
        blocker.color = new Color(.024f, .086f, .16f, .98f); // #061629
        blocker.raycastTarget = true;
        AddShadow(blocker, new Color(0f, .005f, .025f, .78f), new Vector2(0f, -7f));
        CreateChamferedLayer("MetallicOuterFrame", statsPanel.transform, new Vector2(420f, 300f),
            new Color(.024f, .086f, .16f, .98f), new Color(0f, .81f, 1f, .96f), 22f, 4f);
        CreateChamferedLayer("InnerGoldTrim", statsPanel.transform, new Vector2(412f, 292f),
            new Color(.043f, .125f, .22f, .82f), new Color(.95f, .76f, .36f, .56f), 17f, 1.5f);
        ChamferedFrameGraphic header = CreateChamferedLayer("HeaderFrame", statsPanel.transform,
            new Vector2(300f, 36f), new Color(.024f, .09f, .16f, .96f),
            new Color(.65f, .79f, .91f, .8f), 12f, 2f);
        header.rectTransform.anchoredPosition = new Vector2(0f, 114f);
        skillPointLabel = CreateSkillText("SkillPoints", statsPanel.transform, "SKILL POINTS: 0",
            new Vector2(0f, 114f), new Vector2(276f, 30f), 19f,
            TextAlignmentOptions.Center, new Color(.95f, .76f, .36f), FontStyles.Bold);
        CreateSolidImage("HeaderDivider", statsPanel.transform, new Vector2(0f, 86f),
            new Vector2(390f, 1f), new Color(0f, .81f, 1f, .68f)).raycastTarget = false;
        CreateSkillUpgradeButton("CloseStatsPanel", statsPanel.transform, new Vector2(184f, 115f),
            28f, 28f, "×", ToggleStatsPanel, out _);
        CreateAttributeRow(0, "STR", HeroAttribute.Strength, 61.5f,
            new Color(.84f, .25f, .29f, 1f), unitsIconSprite, string.Empty);
        CreateAttributeRow(1, "AGI", HeroAttribute.Agility, 7.5f,
            new Color(.27f, .75f, .4f, 1f), null, "➤");
        CreateAttributeRow(2, "INT", HeroAttribute.Intelligence, -46.5f,
            new Color(.52f, .38f, .91f, 1f), null, "✦");
        CreateAttributeRow(3, "STA", HeroAttribute.Stamina, -100.5f,
            new Color(.64f, .78f, .38f, 1f), heartIconSprite, string.Empty);
        statsPanel.SetActive(false);
    }

    void CreateAttributeRow(int index, string abbreviation, HeroAttribute attribute, float y,
        Color iconColor, Sprite iconSprite, string glyph)
    {
        GameObject row = new GameObject(abbreviation + "Row", typeof(RectTransform));
        row.transform.SetParent(statsPanel.transform, false);
        RectTransform rowRect = (RectTransform)row.transform;
        rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(.5f, .5f);
        rowRect.anchoredPosition = new Vector2(0f, y);
        rowRect.sizeDelta = new Vector2(398f, 47f);
        CreateChamferedLayer("RowSurface", row.transform, rowRect.sizeDelta,
            new Color(.03f, .1f, .17f, .96f), new Color(0f, .72f, .9f, .68f), 11f, 1.5f);
        Image iconFrame = CreateImage("PremiumIconFrame", row.transform,
            hudSkin && hudSkin.abilityFrame ? hudSkin.abilityFrame : circleSprite,
            new Vector2(-174f, 0f), new Vector2(34f, 34f), Color.white, false);
        iconFrame.preserveAspect = true;
        CreateImage("AttributeColor", row.transform, circleSprite, new Vector2(-174f, 0f),
            new Vector2(20f, 20f), new Color(iconColor.r, iconColor.g, iconColor.b, .68f), false);
        if (iconSprite)
            CreateImage("AttributeSymbol", row.transform, iconSprite, new Vector2(-174f, 0f),
                new Vector2(18f, 18f), Color.white, false);
        else
            CreateSkillText("AttributeSymbol", row.transform, glyph, new Vector2(-174f, 0f),
                new Vector2(22f, 22f), 18f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        CreateSkillText("AttributeName", row.transform, abbreviation,
            new Vector2(-133f, 9f), new Vector2(52f, 18f), 17f,
            TextAlignmentOptions.MidlineLeft, new Color(.95f, .76f, .36f), FontStyles.Bold);
        attributeValueLabels[index] = CreateSkillText("CurrentValue", row.transform, "0",
            new Vector2(-133f, -10f), new Vector2(52f, 17f), 15f,
            TextAlignmentOptions.MidlineLeft, Color.white, FontStyles.Bold);
        attributeEffectLabels[index] = CreateSkillText("EffectPerPoint", row.transform, string.Empty,
            new Vector2(42f, 9f), new Vector2(214f, 18f), 11f,
            TextAlignmentOptions.MidlineLeft, new Color(.83f, .92f, 1f), FontStyles.Normal);
        attributePreviewLabels[index] = CreateSkillText("BonusPreview", row.transform, string.Empty,
            new Vector2(42f, -10f), new Vector2(214f, 18f), 11f,
            TextAlignmentOptions.MidlineLeft, new Color(.95f, .76f, .36f), FontStyles.Bold);
        CreateSkillUpgradeButton(abbreviation + "Upgrade", row.transform, new Vector2(174f, 0f),
            36f, 32f, "+", () => UpgradeAttribute(attribute), out Button button);
        attributeUpgradeButtons[index] = button;
    }

    void CreateSkillUpgradeButton(string name, Transform parent, Vector2 center,
        float targetSize, float artworkSize, string glyph, UnityEngine.Events.UnityAction action,
        out Button button)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = center;
        rect.sizeDelta = new Vector2(targetSize, targetSize);
        Image hitArea = root.GetComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        Image artwork = CreateImage("PremiumFrame", root.transform,
            hudSkin && hudSkin.abilityFrame ? hudSkin.abilityFrame : circleSprite,
            Vector2.zero, new Vector2(artworkSize, artworkSize), Color.white, false);
        artwork.preserveAspect = true;
        Outline glow = artwork.gameObject.AddComponent<Outline>();
        glow.effectColor = new Color(0f, .81f, 1f, .5f);
        glow.effectDistance = Vector2.one;
        TextMeshProUGUI symbol = CreateSkillText("Symbol", root.transform, glyph, Vector2.zero,
            new Vector2(artworkSize * .6f, artworkSize * .6f), artworkSize * .45f,
            TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        symbol.raycastTarget = false;
        button = root.GetComponent<Button>();
        button.targetGraphic = hitArea;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(action);
        MobileButtonPressFeedback press = root.AddComponent<MobileButtonPressFeedback>();
        press.Configure(button);
    }

    TextMeshProUGUI CreateSkillText(string name, Transform parent, string content,
        Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment,
        Color color, FontStyles style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        if (label.font) label.fontSharedMaterial = label.font.material;
        label.text = content;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = color;
        label.enableAutoSizing = false;
        label.overflowMode = TextOverflowModes.Truncate;
        label.raycastTarget = false;
        return label;
    }

    void BuildLevelUpFeedback()
    {
        levelUpFeedback = CreateSkillText("HeroLevelUpFeedback", safeRoot, string.Empty,
            Vector2.zero, new Vector2(240f, 46f), 27f, TextAlignmentOptions.Center,
            new Color(1f, .82f, .34f, 0f), FontStyles.Bold);
        levelUpFeedbackRect = levelUpFeedback.rectTransform;
        levelUpFeedback.gameObject.SetActive(false);
    }

    void BindLevelUpHero(HeroProgression next)
    {
        if (subscribedLevelHero == next) return;
        if (subscribedLevelHero) subscribedLevelHero.LevelledUp -= ShowLevelUpFeedback;
        subscribedLevelHero = next;
        levelUpHeroRenderers = next ? next.GetComponentsInChildren<Renderer>(true) : null;
        if (subscribedLevelHero && subscribedLevelHero.IsPlayerHero)
            subscribedLevelHero.LevelledUp += ShowLevelUpFeedback;
    }

    void ShowLevelUpFeedback(int level)
    {
        if (!levelUpFeedback || level <= 1) return;
        levelUpFeedback.text = $"LEVEL UP: {level}";
        levelUpFeedbackUntil = Time.unscaledTime + 4f;
        levelUpFeedback.gameObject.SetActive(true);
    }

    void UpdateLevelUpFeedback()
    {
        if (!levelUpFeedback || !levelUpFeedback.gameObject.activeSelf) return;
        float remaining = levelUpFeedbackUntil - Time.unscaledTime;
        if (remaining <= 0f || !subscribedLevelHero)
        {
            levelUpFeedback.gameObject.SetActive(false);
            return;
        }
        Camera gameplayCamera = Camera.main;
        if (!gameplayCamera) return;
        float headY = subscribedLevelHero.transform.position.y + 2.8f;
        if (levelUpHeroRenderers != null)
            for (int i = 0; i < levelUpHeroRenderers.Length; i++)
            {
                Renderer renderer = levelUpHeroRenderers[i];
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                    headY = Mathf.Max(headY, renderer.bounds.max.y + .35f);
            }
        Vector3 world = subscribedLevelHero.transform.position;
        world.y = headY;
        Vector3 screen = gameplayCamera.WorldToScreenPoint(world);
        if (screen.z <= 0f)
        {
            levelUpFeedback.enabled = false;
            return;
        }
        levelUpFeedback.enabled = true;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(safeRoot, screen, null, out Vector2 point))
            levelUpFeedbackRect.anchoredPosition = point + Vector2.up * (12f + 24f * (1f - remaining / 4f));
        float fade = Mathf.Min(1f, (4f - remaining) * 5f, remaining * 1.5f);
        levelUpFeedback.color = new Color(1f, .82f, .34f, fade);
        levelUpFeedbackRect.localScale = Vector3.one * Mathf.Lerp(.85f, 1f, Mathf.Clamp01((4f - remaining) * 4f));
    }

    void CreateFireballUpgradeBadge()
    {
        GameObject badge = new GameObject("FireballUpgradeBadge", typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(Outline));
        badge.transform.SetParent(fireballButton.transform, false);
        RectTransform rect = badge.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(-5f, -5f);
        rect.sizeDelta = new Vector2(30f, 30f);
        Image image = badge.GetComponent<Image>();
        image.sprite = circleSprite;
        image.color = new Color(.12f, .72f, .92f, 1f);
        fireballUpgradeButton = badge.GetComponent<Button>();
        fireballUpgradeButton.targetGraphic = image;
        fireballUpgradeButton.onClick.AddListener(UpgradeFireball);
        MobileButtonPressFeedback feedback = badge.AddComponent<MobileButtonPressFeedback>();
        feedback.Configure(fireballUpgradeButton);
        Outline outline = badge.GetComponent<Outline>();
        outline.effectColor = new Color(.7f, .96f, 1f, 1f);
        outline.effectDistance = new Vector2(1f, 1f);
        Text plus = CreateText("Plus", badge.transform, "+", Vector2.zero,
            new Vector2(28f, 28f), 20, TextAnchor.MiddleCenter, Color.white, false);
        plus.raycastTarget = false;
        badge.SetActive(false);
    }

    void CreateGeomancerSecondaryUpgradeBadge()
    {
        GameObject badge = new GameObject("EarthSpikesUpgradeBadge", typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(Outline));
        badge.transform.SetParent(geomancerSecondaryButton.transform, false);
        RectTransform rect = badge.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(-5f, -5f);
        rect.sizeDelta = new Vector2(30f, 30f);
        Image image = badge.GetComponent<Image>();
        image.sprite = circleSprite;
        image.color = new Color(.12f, .72f, .92f, 1f);
        geomancerSecondaryUpgradeButton = badge.GetComponent<Button>();
        geomancerSecondaryUpgradeButton.targetGraphic = image;
        geomancerSecondaryUpgradeButton.onClick.AddListener(UpgradeGeomancerSecondary);
        Outline outline = badge.GetComponent<Outline>();
        outline.effectColor = new Color(.7f, .96f, 1f, 1f);
        outline.effectDistance = Vector2.one;
        CreateText("Plus", badge.transform, "+", Vector2.zero, new Vector2(28f, 28f),
            20, TextAnchor.MiddleCenter, Color.white, false);
        badge.SetActive(false);
    }

    void ToggleStatsPanel()
    {
        if (!statsPanel) return;
        bool open = !statsPanel.activeSelf;
        statsPanel.SetActive(open);
        if (!open) return;
        UpdateSkillUpgradeHud();
        if (unitsPanel) unitsPanel.SetActive(false);
        if (itemsPanel) itemsPanel.SetActive(false);
    }

    void UpgradeAttribute(HeroAttribute attribute)
    {
        if (heroProgression && heroProgression.TryUpgradeAttribute(attribute)) UpdateSkillUpgradeHud();
    }

    void UpgradeFireball()
    {
        if (abilityLoadout && abilityLoadout.TryUpgrade(1, heroProgression)) UpdateSkillUpgradeHud();
    }

    void UpgradeGeomancerSecondary()
    {
        if (abilityLoadout && abilityLoadout.TryUpgrade(2, heroProgression)) UpdateSkillUpgradeHud();
    }

    void UpdateSkillUpgradeHud()
    {
        int points = heroProgression ? heroProgression.UnspentSkillPoints : 0;
        if (!statsPointBadge && statsToggleButton)
        {
            Transform badge = statsToggleButton.transform.Find("AvailablePointBadge");
            if (badge) statsPointBadge = badge.GetComponent<Image>();
            if (badge) statsPointCount = badge.GetComponentInChildren<Text>(true);
        }
        if (skillPointLabel && displayedSkillPoints != points) skillPointLabel.text = $"SKILL POINTS: {points}";
        displayedSkillPoints = points;
        if (statsToggleLabel) statsToggleLabel.text = points > 0 ? $"STATS +  {points}" : "STATS +";
        if (statsToggleButton)
        {
            // The panel remains readable at zero points; only upgrades are disabled.
            statsToggleButton.interactable = true;
            Image background = statsToggleButton.GetComponent<Image>();
            if (background) background.color = points > 0 ? Color.white : new Color(.76f, .83f, .9f, .92f);
            Outline outline = statsToggleButton.GetComponent<Outline>();
            if (outline) outline.effectColor = points > 0
                ? new Color(Cyan.r, Cyan.g, Cyan.b,
                    .27f + .16f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 4f)))
                : new Color(.30f, .38f, .46f, .45f);
            if (statsToggleLabel) statsToggleLabel.color = points > 0
                ? Color.white : new Color(.78f, .84f, .9f, .94f);
        }
        if (statsPointBadge) statsPointBadge.gameObject.SetActive(points > 0);
        if (statsPointCount) statsPointCount.text = points.ToString();
        if (statsPanel && statsPanel.activeSelf)
        {
            int strength = heroProgression ? heroProgression.Strength : 0;
            int agility = heroProgression ? heroProgression.Agility : 0;
            int intelligence = heroProgression ? heroProgression.Intelligence : 0;
            int stamina = heroProgression ? heroProgression.Stamina : 0;
            for (int i = 0; i < attributeValueLabels.Length; i++)
            {
                int value = i == 0 ? strength : i == 1 ? agility : i == 2 ? intelligence : stamina;
                float perPoint = !heroProgression ? 0f : i == 0 ? heroProgression.strengthDamagePercentPerPoint :
                    i == 1 ? heroProgression.attackSpeedPerAgility :
                    i == 2 ? heroProgression.intelligenceAbilityPercentPerPoint :
                    heroProgression.staminaHealthPercentPerPoint;
                if (displayedAttributeValues[i] != value ||
                    !Mathf.Approximately(displayedAttributeRates[i], perPoint))
                {
                    if (attributeValueLabels[i]) attributeValueLabels[i].text = value.ToString();
                    string effect = i == 0 ? "AA dmg" : i == 1 ? "attack speed" :
                        i == 2 ? "ability dmg" : "max HP";
                    if (attributeEffectLabels[i]) attributeEffectLabels[i].text =
                        $"+{perPoint * 100f:0.#}% {effect} / point" +
                        (i == 3 && heroProgression ?
                            $" · +{heroProgression.regenerationPerStamina:0.##} HP/s" : "");
                    if (attributePreviewLabels[i]) attributePreviewLabels[i].text =
                        $"Bonus +{value * perPoint * 100f:0.#}%  →  +{(value + 1) * perPoint * 100f:0.#}%";
                    displayedAttributeValues[i] = value;
                    displayedAttributeRates[i] = perPoint;
                }
                if (attributeUpgradeButtons[i])
                {
                    Button upgrade = attributeUpgradeButtons[i];
                    upgrade.interactable = points > 0;
                    Transform frameTransform = upgrade.transform.Find("PremiumFrame");
                    Image buttonImage = frameTransform ? frameTransform.GetComponent<Image>() : null;
                    if (buttonImage) buttonImage.color = points > 0 ? Color.white :
                        new Color(.6f, .68f, .75f, .9f);
                    Outline rim = frameTransform ? frameTransform.GetComponent<Outline>() : null;
                    if (rim) rim.effectColor = points > 0 ? new Color(.2f, .85f, 1f, .54f) :
                        new Color(.22f, .35f, .45f, .34f);
                }
            }
        }
        if (fireballUpgradeButton)
            fireballUpgradeButton.gameObject.SetActive(abilityLoadout &&
                abilityLoadout.CanUpgrade(1, heroProgression));
        if (geomancerSecondaryUpgradeButton)
            geomancerSecondaryUpgradeButton.gameObject.SetActive(abilityLoadout && abilityLoadout.IsGeomancer &&
                abilityLoadout.CanUpgrade(2, heroProgression));
    }

    void UpdateHeroProgression()
    {
        if (!heroProgression) return;
        int level = heroProgression.Level;
        lastKnownHeroLevel = level;
        int needed = Mathf.Max(1, heroProgression.ExperienceToNextLevel);
        if (heroLevelCaption) heroLevelCaption.text = "LV";
        if (heroLevelLabel) heroLevelLabel.text = level.ToString();
        if (heroExperienceLabel) heroExperienceLabel.text = $"{heroProgression.Experience} / {needed} XP";
        float ratio = Mathf.Clamp01(heroProgression.Experience / (float)needed);
        if (heroExperienceFill) heroExperienceFill.fillAmount = ratio;
        if (enableXpDiagnostics && (lastReportedXp != heroProgression.Experience || lastReportedXpNeeded != needed))
        {
            lastReportedXp = heroProgression.Experience;
            lastReportedXpNeeded = needed;
            LogXpRatio(heroProgression.Experience, needed, ratio);
        }
        int[] requirements = { 2, 4, 6 };
        for (int i = 0; i < futureAbilitySlots.Length; i++)
        {
            if (geomancerAbilities && i == 0) continue;
            HeroAbilitySlot definition = abilityLoadout && abilityLoadout.slots != null &&
                abilityLoadout.slots.Length > i + 1 ? abilityLoadout.slots[i + 1] : null;
            bool active = definition != null && definition.implemented && abilityLoadout.IsUnlocked(i + 2);
            if (futureAbilitySlots[i])
            {
                Image icon = futureAbilitySlots[i].transform.Find("ActiveAbilityIcon")?.GetComponent<Image>();
                if (icon) icon.sprite = active ? definition.icon : null;
                SetLockedAbilityVisual(futureAbilitySlots[i].transform, !active);
            }
            if (futureAbilityLabels[i])
            {
                futureAbilityLabels[i].text = active ? string.Empty : $"Lv {requirements[i]}";
                futureAbilityLabels[i].color = new Color(.84f, .94f, 1f, 1f);
            }
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEBUG")]
    void LogXpRatio(int current, int required, float ratio)
    {
        Debug.Log($"[XP HUD] {current}/{required} = {ratio:P1}, rendered fill=" +
            $"{(heroExperienceFill ? heroExperienceFill.fillAmount : -1f):P1}.", this);
    }

    void BuildResultPanel()
    {
        lastPresentedResult = MatchResult.None;
        resultPanel = new GameObject("MatchResultOverlay", typeof(RectTransform), typeof(Image));
        resultPanel.transform.SetParent(safeRoot, false);
        RectTransform overlay = (RectTransform)resultPanel.transform;
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        Image overlayImage = resultPanel.GetComponent<Image>();
        overlayImage.color = Color.clear;
        overlayImage.raycastTarget = false;
        Image backdrop = CreateSolidImage("FullscreenBackdrop", resultPanel.transform, Vector2.zero,
            Vector2.zero, new Color(.005f, .012f, .026f, .55f));
        backdrop.raycastTarget = true;

        GameObject modal = new GameObject("MatchResultPanel", typeof(RectTransform));
        modal.transform.SetParent(resultPanel.transform, false);
        RectTransform modalRect = (RectTransform)modal.transform;
        modalRect.anchorMin = modalRect.anchorMax = modalRect.pivot = new Vector2(.5f, .5f);
        ChamferedFrameGraphic navyFrame = CreateChamferedLayer("NavyFrame", modal.transform, new Vector2(670f, 520f),
            new Color(.015f, .033f, .065f, .985f), new Color(.30f, .69f, .87f, .92f), 20f, 3f);
        AddShadow(navyFrame, new Color(0f, .005f, .02f, .8f), new Vector2(0f, -9f));
        CreateChamferedLayer("InnerTrim", modal.transform, new Vector2(646f, 496f),
            new Color(.018f, .037f, .07f, .96f), new Color(.49f, .41f, .22f, .65f), 15f, 1.4f);
        GameObject gradientRoot = new GameObject("NavyGradient", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(MenuGradientGraphic));
        gradientRoot.transform.SetParent(modal.transform, false);
        MenuGradientGraphic gradient = gradientRoot.GetComponent<MenuGradientGraphic>();
        gradient.Configure(new Color(.012f, .028f, .057f, .95f), new Color(.037f, .076f, .13f, .94f));
        gradient.raycastTarget = false;
        CreateSolidImage("TitleRule", modal.transform, new Vector2(0f, 130f),
            new Vector2(490f, 2f), new Color(.28f, .65f, .85f, .48f));
        resultLabel = CreateText("ResultLabel", modal.transform, "VICTORY", new Vector2(0f, 175f),
            new Vector2(590f, 68f), 48, TextAnchor.MiddleCenter, Gold, false);
        AddTextShadow(resultLabel);
        resultDurationValue = CreateResultField(modal.transform, "DurationField", "MATCH DURATION", 96f);
        resultLevelValue = CreateResultField(modal.transform, "LevelField", "HERO LEVEL", 50f);
        resultIncomeValue = CreateResultField(modal.transform, "IncomeField", "FINAL INCOME", 4f);
        CreateResultButton("RestartButton", modal.transform, "RESTART MATCH", new Vector2(0f, -65f),
            Cyan, true, out restartButton);
        restartButton.onClick.AddListener(RestartMatch);
        CreateResultButton("ReturnToMenuButton", modal.transform, "MAIN MENU", new Vector2(0f, -160f),
            Blue, false, out returnToMenuButton);
        returnToMenuButton.onClick.AddListener(ReturnToMainMenu);
        resultPanel.SetActive(false);
    }

    Text CreateResultField(Transform parent, string name, string label, float y)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)row.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(500f, 38f);
        CreateChamferedLayer("RowSurface", row.transform, rect.sizeDelta,
            new Color(.035f, .07f, .115f, .72f), new Color(.21f, .39f, .54f, .38f), 6f, 1f);
        Text heading = CreateText("Heading", row.transform, label, new Vector2(-105f, 0f),
            new Vector2(245f, 30f), 19, TextAnchor.MiddleLeft, new Color(.68f, .81f, .9f), false);
        heading.fontStyle = FontStyle.Normal;
        Text value = CreateText("Value", row.transform, string.Empty, new Vector2(145f, 0f),
            new Vector2(145f, 30f), 23, TextAnchor.MiddleRight, Color.white, false);
        return value;
    }

    void CreateResultButton(string name, Transform parent, string label, Vector2 position,
        Color accent, bool primary, out Button button)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(440f, 74f);
        Image touchSurface = root.GetComponent<Image>();
        touchSurface.color = primary ? new Color(.05f, .16f, .24f, 1f) : new Color(.025f, .055f, .09f, 1f);
        button = root.GetComponent<Button>();
        button.targetGraphic = touchSurface;
        SetButtonColors(button, accent);
        ChamferedFrameGraphic bevel = CreateChamferedLayer("BeveledFrame", root.transform, rect.sizeDelta,
            primary ? new Color(.045f, .15f, .23f, .98f) : new Color(.025f, .052f, .09f, .98f),
            primary ? new Color(.25f, .79f, .95f, .95f) : new Color(.32f, .48f, .62f, .8f), 11f, 2.5f);
        AddShadow(bevel, new Color(0f, .01f, .025f, .5f), new Vector2(0f, -3f));
        Text caption = CreateText("Label", root.transform, label, Vector2.zero,
            new Vector2(390f, 46f), 24, TextAnchor.MiddleCenter, Color.white, false);
        caption.fontStyle = FontStyle.Bold;
    }

    void UpdateResultAccent(bool won)
    {
        if (!resultPanel) return;
        Transform modal = resultPanel.transform.Find("MatchResultPanel");
        if (!modal) return;
        ChamferedFrameGraphic frame = modal.Find("NavyFrame")?.GetComponent<ChamferedFrameGraphic>();
        if (frame) frame.Configure(new Color(.015f, .033f, .065f, .985f),
            won ? new Color(.32f, .72f, .88f, .92f) : new Color(.75f, .26f, .3f, .92f), 20f, 3f);
        Image rule = modal.Find("TitleRule")?.GetComponent<Image>();
        if (rule) rule.color = won ? new Color(.28f, .65f, .85f, .48f) : new Color(.76f, .28f, .31f, .52f);
    }

    void LayoutResultPanel(float scale)
    {
        if (!resultPanel) return;
        RectTransform backdrop = resultPanel.transform.Find("FullscreenBackdrop") as RectTransform;
        RectTransform canvasRect = canvas ? canvas.GetComponent<RectTransform>() : null;
        if (backdrop && canvasRect)
        {
            // Extend the dimmer beyond the safe-area root to the physical canvas edges.
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = new Vector2(-safeRoot.anchorMin.x * canvasRect.rect.width,
                -safeRoot.anchorMin.y * canvasRect.rect.height);
            backdrop.offsetMax = new Vector2((1f - safeRoot.anchorMax.x) * canvasRect.rect.width,
                (1f - safeRoot.anchorMax.y) * canvasRect.rect.height);
        }
        RectTransform modal = resultPanel.transform.Find("MatchResultPanel") as RectTransform;
        if (!modal) return;
        float fittedScale = Mathf.Min(scale, safeRoot.rect.width / 726f, safeRoot.rect.height / 576f);
        modal.sizeDelta = new Vector2(670f, 520f) * fittedScale;
        modal.localScale = Vector3.one;
        RectTransform frame = modal.Find("NavyFrame") as RectTransform;
        RectTransform trim = modal.Find("InnerTrim") as RectTransform;
        LayoutCenteredChild(frame, Vector2.zero, new Vector2(670f, 520f), fittedScale);
        LayoutCenteredChild(trim, Vector2.zero, new Vector2(646f, 496f), fittedScale);
        LayoutCenteredChild(modal.Find("NavyGradient") as RectTransform, Vector2.zero,
            new Vector2(620f, 470f), fittedScale);
        LayoutCenteredChild(modal.Find("TitleRule") as RectTransform, new Vector2(0f, 130f),
            new Vector2(490f, 2f), fittedScale);
        LayoutCenteredChild(modal.Find("ResultLabel") as RectTransform, new Vector2(0f, 175f),
            new Vector2(590f, 68f), fittedScale);
        LayoutResultField(modal.Find("DurationField") as RectTransform, 96f, fittedScale);
        LayoutResultField(modal.Find("LevelField") as RectTransform, 50f, fittedScale);
        LayoutResultField(modal.Find("IncomeField") as RectTransform, 4f, fittedScale);
        LayoutCenteredChild(modal.Find("RestartButton") as RectTransform, new Vector2(0f, -65f),
            new Vector2(440f, 74f), fittedScale);
        LayoutCenteredChild(modal.Find("ReturnToMenuButton") as RectTransform, new Vector2(0f, -160f),
            new Vector2(440f, 74f), fittedScale);
        SetScaledFont(modal.Find("ResultLabel") as RectTransform, 48, fittedScale);
        LayoutResultButtonContents(modal.Find("RestartButton") as RectTransform, fittedScale);
        LayoutResultButtonContents(modal.Find("ReturnToMenuButton") as RectTransform, fittedScale);
    }

    static void LayoutResultField(RectTransform row, float y, float scale)
    {
        LayoutCenteredChild(row, new Vector2(0f, y), new Vector2(500f, 38f), scale);
        if (!row) return;
        LayoutCenteredChild(row.Find("RowSurface") as RectTransform, Vector2.zero,
            new Vector2(500f, 38f), scale);
        RectTransform heading = row.Find("Heading") as RectTransform;
        RectTransform value = row.Find("Value") as RectTransform;
        LayoutCenteredChild(heading, new Vector2(-105f, 0f), new Vector2(245f, 30f), scale);
        LayoutCenteredChild(value, new Vector2(145f, 0f), new Vector2(145f, 30f), scale);
        SetScaledFont(heading, 19, scale);
        SetScaledFont(value, 23, scale);
    }

    static void LayoutResultButtonContents(RectTransform root, float scale)
    {
        if (!root) return;
        LayoutCenteredChild(root.Find("BeveledFrame") as RectTransform, Vector2.zero,
            new Vector2(440f, 74f), scale);
        RectTransform label = root.Find("Label") as RectTransform;
        LayoutCenteredChild(label, Vector2.zero, new Vector2(390f, 46f), scale);
        SetScaledFont(label, 24, scale);
    }

    static void SetLockedAbilityVisual(Transform slot, bool locked)
    {
        Transform lockedArtwork = slot.Find("LockedArtwork");
        if (lockedArtwork) lockedArtwork.gameObject.SetActive(locked);
        Transform activeFrame = slot.Find("ActiveAbilityFrame");
        if (activeFrame) activeFrame.gameObject.SetActive(!locked);
        Image activeIcon = slot.Find("ActiveAbilityIcon")?.GetComponent<Image>();
        if (activeIcon) activeIcon.gameObject.SetActive(!locked && activeIcon.sprite);
    }

    void UpdateFireball(bool running)
    {
        if (!fireballButton || !fireballView) return;
        bool geomancerSelected = geomancerAbilities || (abilityLoadout && abilityLoadout.IsGeomancer);
        if (geomancerSelected)
        {
            fireballView.SetCompositeArtwork(null);
            fireballView.SetIcon(null);
            fireballView.SetFallbackStyle(AbilityGlyphStyle.Faultbreaker);
            if (!geomancerAbilities)
            {
                fireballView.SetInteractable(false);
                fireballView.SetLabel(string.Empty);
                fireballView.SetCooldown(0f);
                return;
            }
            bool active = running && geomancerAbilities.FaultbreakerRemaining <= 0f;
            fireballView.SetInteractable(active);
            fireballView.SetLabel(string.Empty);
            fireballView.SetCooldown(Mathf.Clamp01(geomancerAbilities.FaultbreakerRemaining / 5f),
                Mathf.CeilToInt(geomancerAbilities.FaultbreakerRemaining));
            fireballView.SetSelected(geomancerAbilities.IsTargeting);
            if (geomancerSecondaryButton)
            {
                bool unlocked = abilityLoadout && abilityLoadout.IsUnlocked(2);
                geomancerSecondaryButton.interactable = running && unlocked && geomancerAbilities.EarthSpikesRemaining <= 0f;
                Image activeIcon = geomancerSecondaryButton.transform.Find("ActiveAbilityIcon")?.GetComponent<Image>();
                if (activeIcon && abilityLoadout.slots != null && abilityLoadout.slots.Length > 1)
                    activeIcon.sprite = abilityLoadout.slots[1].icon;
                SetLockedAbilityVisual(geomancerSecondaryButton.transform, !unlocked);
                if (geomancerSecondaryGlyph)
                    geomancerSecondaryGlyph.gameObject.SetActive(unlocked && (!activeIcon || !activeIcon.sprite));
                futureAbilityLabels[0].text = unlocked
                    ? (geomancerAbilities.EarthSpikesRemaining > 0f ? $"{Mathf.CeilToInt(geomancerAbilities.EarthSpikesRemaining)}" : string.Empty)
                    : "Lv 2";
                RectTransform labelRect = futureAbilityLabels[0].rectTransform;
                labelRect.anchoredPosition = new Vector2(0f, -abilitySlotSize * .29f);
                labelRect.sizeDelta = new Vector2(abilitySlotSize * .56f, abilitySlotSize * .2f);
                futureAbilityLabels[0].fontSize = 10;
            }
            if (geomancerPassiveLabel)
            {
                geomancerPassiveLabel.gameObject.SetActive(true);
                geomancerPassiveLabel.text = geomancerAbilities.IsFuryActive
                    ? $"SEISMIC FURY  {geomancerAbilities.FuryRemaining:0.0}s"
                    : $"SEISMIC FURY  {geomancerAbilities.FuryProgress}/5";
                geomancerPassiveLabel.color = geomancerAbilities.IsFuryActive ? new Color(.35f, .9f, 1f) : new Color(.4f, .66f, .8f);
            }
            return;
        }
        fireballView.SetIcon(fireballIconOverride);
        fireballView.SetFallbackStyle(AbilityGlyphStyle.Fireball);
        fireballView.SetCompositeArtwork(hudSkin ? hudSkin.fireballButtonArtwork : null);
        if (geomancerSecondaryGlyph) geomancerSecondaryGlyph.gameObject.SetActive(false);
        if (futureAbilityLabels[0])
        {
            futureAbilityLabels[0].rectTransform.anchoredPosition =
                new Vector2(0f, -abilitySlotSize * .29f);
            futureAbilityLabels[0].rectTransform.sizeDelta =
                new Vector2(abilitySlotSize * .56f, abilitySlotSize * .2f);
            futureAbilityLabels[0].fontSize = 10;
        }
        if (geomancerPassiveLabel) geomancerPassiveLabel.gameObject.SetActive(false);
        bool targeting = ability && ability.IsTargeting;
        float remaining = ability ? ability.CooldownRemaining : 0f;
        fireballView.SetInteractable(running && ability && (targeting || remaining <= 0f));
        fireballView.SetLabel(string.Empty);
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
        { archerLabel.text = PurchaseLabel("ARCHER", archerCost); lastArcherCost = archerCost; }
        if (knightCost != lastKnightCost)
        { knightLabel.text = PurchaseLabel("KNIGHT", knightCost); lastKnightCost = knightCost; }
        if (wizardCost != lastWizardCost)
        { wizardLabel.text = PurchaseLabel("WIZARD", wizardCost); lastWizardCost = wizardCost; }
    }

    void ToggleUnitsPanel()
    {
        bool open = !unitsPanel.activeSelf;
        unitsPanel.SetActive(open);
        itemsPanel.SetActive(false);
        if (statsPanel) statsPanel.SetActive(false);
        SetButtonSelected(unitsToggle, unitsToggleSelected, open);
        SetButtonSelected(itemsToggle, itemsToggleSelected, false);
    }

    void ToggleItemsPanel()
    {
        bool open = !itemsPanel.activeSelf;
        itemsPanel.SetActive(open);
        unitsPanel.SetActive(false);
        if (statsPanel) statsPanel.SetActive(false);
        SetButtonSelected(itemsToggle, itemsToggleSelected, open);
        SetButtonSelected(unitsToggle, unitsToggleSelected, false);
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
            selectedLayer.color = hudSkin && hudSkin.selectedOverlay
                ? new Color(1f, 1f, 1f, selected ? 1f : 0f)
                : (selected ? new Color(.35f, .9f, 1f, .34f) : new Color(.35f, .9f, 1f, 0f));
            selectedLayer.transform.SetAsFirstSibling();
        }
        if (button)
        {
            // The responsive layout owns root scale. Replacing the target sprite or
            // resetting localScale here stripped the premium artwork after toggles.
            // Selected state is presentation-only and lives in the dedicated overlay.
            // Complete Units/Items art lives on the root Image; the transparent
            // TouchTarget is a separate raycast layer and must stay transparent.
            Image target = button.GetComponent<Image>();
            if (target)
            {
                bool generatedCleanToggle = hudSkin && !HasCompleteButtonArtwork(button.name) &&
                    (button.name == "UnitsToggle" || button.name == "ItemsToggle");
                Sprite expected = generatedCleanToggle ? null : ButtonSprite(button.name);
                target.sprite = expected;
                target.color = expected ? Color.white :
                    (generatedCleanToggle ? Color.clear : ControlNavy);
                target.enabled = true;
                target.gameObject.SetActive(true);
            }
            Transform interiorTransform = button.transform.Find("DarkInterior");
            Image interior = interiorTransform ? interiorTransform.GetComponent<Image>() : null;
            if (interior) interior.color = selected
                ? new Color(.055f, .16f, .22f, .99f) : ControlNavy;
            Outline rim = button.GetComponent<Outline>();
            if (rim) rim.effectColor = selected
                ? new Color(.36f, .92f, 1f, .55f) : new Color(.2f, .62f, .88f, .18f);
        }
    }

    void ToggleFireballTargeting()
    {
        if (!geomancerAbilities && HeroProgression.PlayerHero)
            geomancerAbilities = HeroProgression.PlayerHero.GetComponent<GeomancerAbilityController>();
        if (geomancerAbilities)
        {
            if (geomancerAbilities.IsTargeting) geomancerAbilities.CancelTargeting();
            else geomancerAbilities.BeginFaultbreaker();
            return;
        }
        if (!ability) ability = HeroProgression.PlayerHero
            ? HeroProgression.PlayerHero.GetComponent<FireballAbility>() : FindAnyObjectByType<FireballAbility>();
        if (!ability) return;
        if (ability.IsTargeting) ability.CancelTargeting();
        else if (ability.BeginTargeting())
        {
            unitsPanel.SetActive(false);
            itemsPanel.SetActive(false);
            if (statsPanel) statsPanel.SetActive(false);
            SetButtonSelected(unitsToggle, unitsToggleSelected, false);
            SetButtonSelected(itemsToggle, itemsToggleSelected, false);
        }
    }

    void TriggerEarthSpikes()
    {
        if (!geomancerAbilities && HeroProgression.PlayerHero)
            geomancerAbilities = HeroProgression.PlayerHero.GetComponent<GeomancerAbilityController>();
        if (geomancerAbilities) geomancerAbilities.BeginEarthSpikes();
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
    void ReturnToMainMenu() => HlwGameSession.ReturnToMainMenu();

    RectTransform CreateRectTransform(string name, Transform parent)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return (RectTransform)child.transform;
    }

    GameObject CreateTopbarPanel(string name, Transform parent, Sprite artwork)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        GameObject artworkObject = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
        artworkObject.transform.SetParent(panel.transform, false);
        Image background = artworkObject.GetComponent<Image>();
        background.sprite = artwork;
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.color = artwork ? Color.white : new Color(.025f, .055f, .095f, .96f);
        background.raycastTarget = false;
        CreateRectTransform("Content", panel.transform);
        return panel;
    }

    Text CreateTopbarStatCell(string name, string title, Transform parent, Color accent,
        Sprite iconSprite, bool useClockGlyph)
    {
        RectTransform cell = CreateRectTransform(name, parent);
        const float iconX = -48f;
        if (iconSprite)
        {
            Image icon = CreateImage("Icon", cell, iconSprite, new Vector2(iconX, 0f),
                new Vector2(24f, 24f), Color.white, false);
            icon.preserveAspect = true;
        }
        else if (useClockGlyph)
        {
            Image clockWell = CreateImage("IconWell", cell, circleSprite,
                new Vector2(iconX, 0f), new Vector2(24f, 24f),
                new Color(accent.r, accent.g, accent.b, .88f), false);
            clockWell.preserveAspect = true;
            CreateSmallClockGlyph(cell, new Vector2(iconX, 0f), accent);
        }

        Text caption = CreateText("Caption", cell, title, new Vector2(24f, 8f),
            new Vector2(120f, 18f), 14, TextAnchor.MiddleCenter,
            new Color(accent.r, accent.g, accent.b, .98f), false);
        AddTextShadow(caption);
        Text value = CreateText("Value", cell, string.Empty, new Vector2(24f, -10f),
            new Vector2(124f, 23f), 20, TextAnchor.MiddleCenter,
            title == "GOLD" ? new Color(1f, .88f, .52f) : Color.white, false);
        value.fontStyle = FontStyle.Bold;
        AddTextShadow(value);
        return value;
    }

    void CreateTopbarHealthContent(Transform parent, string title, Color accent, Sprite fillSprite,
        out Text value, out Image fill)
    {
        RectTransform group = CreateRectTransform("BaseContentGroup", parent);
        group.anchorMin = Vector2.zero;
        group.anchorMax = Vector2.one;
        group.offsetMin = Vector2.zero;
        group.offsetMax = Vector2.zero;
        Image crest = CreateImage("FactionIcon", group, heartIconSprite,
            new Vector2(-58f, 0f), new Vector2(24f, 24f), Color.white, false);
        crest.preserveAspect = true;
        Text caption = CreateText("Caption", group, title, new Vector2(20f, 8f),
            new Vector2(132f, 18f), 14, TextAnchor.MiddleCenter,
            new Color(accent.r, accent.g, accent.b, 1f), false);
        AddTextShadow(caption);
        value = CreateText("Value", group, string.Empty, new Vector2(20f, -10f),
            new Vector2(136f, 23f), 20, TextAnchor.MiddleCenter, Color.white, false);
        value.fontStyle = FontStyle.Bold;
        AddTextShadow(value);
        fill = null;
    }

    void CreateSmallClockGlyph(Transform parent, Vector2 center, Color accent)
    {
        CreateSolidImage("ClockMinuteHand", parent, center + new Vector2(0f, 2.5f),
            new Vector2(1.5f, 7f), Color.white);
        Image hourHand = CreateSolidImage("ClockHourHand", parent,
            center + new Vector2(2.5f, -.5f), new Vector2(5.5f, 1.5f),
            new Color(.86f, .94f, 1f));
        hourHand.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        CreateSolidImage("ClockHub", parent, center, new Vector2(2.5f, 2.5f),
            new Color(accent.r, accent.g, accent.b, 1f));
    }

    Text CreateTopStatWidget(string name, string title, Transform parent, Vector2 position,
        Vector2 size, Color accent, Sprite iconSprite, bool useClockGlyph, bool topRight = false)
    {
        GameObject panel = CreateFantasyTopFrame(name, parent, position, size, accent, topRight);
        float iconX = -size.x * .5f + 29f;
        Sprite badgeSprite = useClockGlyph && buttonPurpleSprite ? buttonPurpleSprite : circleSprite;
        Image badge = CreateImage("IconWell", panel.transform, badgeSprite,
            new Vector2(iconX, 0f), new Vector2(36f, 36f),
            Color.Lerp(new Color(.3f, .35f, .48f, 1f), accent, .48f), false);
        badge.preserveAspect = true;

        if (iconSprite)
        {
            Image icon = CreateImage("Icon", panel.transform, iconSprite,
                new Vector2(iconX, 0f), new Vector2(26f, 26f), Color.white, false);
            icon.preserveAspect = true;
        }
        else if (useClockGlyph)
            CreateClockGlyph(panel.transform, new Vector2(iconX, 0f), accent);

        float textX = 25f;
        Text caption = CreateText("Caption", panel.transform, title,
            new Vector2(textX, 11f), new Vector2(size.x - 70f, 16f), 10,
            TextAnchor.MiddleLeft, new Color(accent.r, accent.g, accent.b, .95f), false);
        AddTextShadow(caption);
        Text value = CreateText("Value", panel.transform, string.Empty,
            new Vector2(textX, -10f), new Vector2(size.x - 70f, 24f), 18,
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
        frame.sprite = SkinOrFallback(hudSkin ? hudSkin.topHudBackground : null,
            panelFrameSprite ? panelFrameSprite : capsuleSprite);
        SetImageType(frame, true);
        bool customTopFrame = hudSkin && hudSkin.topHudBackground && !topHudTwoRows;
        if (customTopFrame) frame.sprite = null;
        frame.color = customTopFrame ? Color.clear : frame.sprite
            ? Color.Lerp(new Color(.055f, .075f, .11f, .98f), accent, .09f)
            : new Color(.025f, .04f, .07f, .96f);
        frame.raycastTarget = false;
        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = customTopFrame
            ? new Color(0f, 0f, 0f, 0f) : new Color(accent.r, accent.g, accent.b, .48f);
        outline.effectDistance = new Vector2(1.25f, 1.25f);
        outline.useGraphicAlpha = true;
        AddShadow(frame, new Color(.005f, .01f, .025f, .78f), new Vector2(0f, -4f));

        if (!hudSkin || !hudSkin.topHudBackground)
        {
            ChamferedFrameGraphic metal = CreateChamferedLayer("MetallicShell", panel.transform,
                new Vector2(size.x - 4f, size.y - 4f), new Color(.018f, .032f, .055f, .9f),
                new Color(accent.r, accent.g, accent.b, .72f), 10f, 2f);
            metal.transform.SetAsFirstSibling();
        }

        if (!customTopFrame)
        {
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
        }
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
        float iconX = mirrored ? size.x * .5f - 29f : -size.x * .5f + 29f;
        float contentX = mirrored ? -25f : 25f;
        float contentWidth = size.x - 64f;
        TextAnchor alignment = mirrored ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;

        Image crestWell = CreateImage("CrestWell", card.transform, circleSprite,
            new Vector2(iconX, 0f), new Vector2(36f, 36f),
            Color.Lerp(new Color(.28f, .32f, .45f, 1f), accent, .5f), false);
        crestWell.preserveAspect = true;
        Image crest = CreateImage("BaseCrest", card.transform, heartIconSprite,
            new Vector2(iconX, 0f), new Vector2(26f, 26f),
            new Color(1f, .96f, .98f), false);
        crest.preserveAspect = true;
        Text caption = CreateText("Caption", card.transform, title,
            new Vector2(contentX, 11f), new Vector2(contentWidth, 16f), 10,
            alignment, new Color(accent.r, accent.g, accent.b, .96f), false);
        AddTextShadow(caption);
        value = CreateText("Value", card.transform, string.Empty,
            new Vector2(contentX, -10f), new Vector2(contentWidth, 24f),
            17, alignment, Color.white, false);
        AddTextShadow(value);

        fill = CreateImage("HealthFill", card.transform, fillSprite,
            new Vector2(contentX, -25f), new Vector2(contentWidth - 10f, 7f),
            title == "RED BASE" ? new Color(1f, .2f, .23f, 1f) : new Color(.28f, .68f, 1f), false);
        if (!fillSprite) fill.color = accent;
        fill.preserveAspect = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        Image barFrame = CreateImage("HealthFrame", card.transform, healthBarFrameSprite,
            new Vector2(contentX, -25f), new Vector2(contentWidth, 15f),
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
        if (circle) image.type = Image.Type.Simple;
        else SetImageType(image, true);
        image.color = image.sprite ? Color.white : FallbackButtonColor(name);
        bool completeArtwork = circle && HasCompleteButtonArtwork(name);
        image.preserveAspect = completeArtwork;
        image.raycastTarget = true;
        button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        SetButtonColors(button, accent);
        if (completeArtwork && (name == "UnitsToggle" || name == "ItemsToggle"))
        {
            image.raycastTarget = false;
            Image touchTarget = CreateSolidImage("TouchTarget", buttonObject.transform,
                Vector2.zero, size, Color.clear);
            touchTarget.raycastTarget = true;
            button.targetGraphic = touchTarget;
            button.transition = Selectable.Transition.None;
        }
        MobileButtonPressFeedback pressFeedback = buttonObject.AddComponent<MobileButtonPressFeedback>();
        pressFeedback.Configure(button);
        Outline outline = buttonObject.GetComponent<Outline>();
        bool customCircleArtwork = circle && CustomCircleFrameFor(name);
        outline.effectColor = completeArtwork ? Color.clear : customCircleArtwork
            ? (name == "AutoAttackButton" ? new Color(1f, .76f, .3f, .42f) : new Color(.28f, .8f, 1f, .42f))
            : accent;
        outline.effectDistance = customCircleArtwork ? Vector2.one
            : (circle ? new Vector2(2f, 2f) : new Vector2(1.5f, 1.5f));
        outline.useGraphicAlpha = true;
        AddShadow(image, new Color(0f, .01f, .025f, .68f), new Vector2(0f, -4f));
        Image circleInterior = null;
        if (circle && !completeArtwork)
        {
            circleInterior = CreateImage("DarkInterior", buttonObject.transform, circleSprite,
                Vector2.zero, size * .82f, ControlNavy, false);
            circleInterior.preserveAspect = true;
        }
        bool cleanSecondaryCircle = !completeArtwork && hudSkin &&
            (name == "UnitsToggle" || name == "ItemsToggle");
        if (cleanSecondaryCircle)
        {
            image.sprite = null;
            image.color = Color.clear;
            if (circleInterior) circleInterior.gameObject.SetActive(false);
        }
        Sprite circularFrame = completeArtwork || cleanSecondaryCircle ? null : CircleFrameFor(name);
        if (circle && circularFrame)
        {
            Image frame = CreateImage("FantasyFrame", buttonObject.transform, circularFrame,
                Vector2.zero, size * 1.1f,
                name == "AutoAttackButton" ? Color.white : new Color(.72f, .9f, 1f, .96f), false);
            frame.preserveAspect = true;
        }
        if (circle && !completeArtwork && (cleanSecondaryCircle || !CustomCircleFrameFor(name)))
        {
            CreateArcaneRing("ArcaneMetalRing", buttonObject.transform, size * .94f,
                cleanSecondaryCircle ? new Color(.08f, .38f, .62f, .94f) : accent,
                cleanSecondaryCircle ? Color.clear : new Color(1f, .72f, .25f, .78f),
                cleanSecondaryCircle ? 0 : (name == "AutoAttackButton" ? 8 : 4));
        }

        Sprite iconSprite = ButtonIcon(name);
        if (circle && iconSprite && !completeArtwork)
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
        text.gameObject.SetActive(!completeArtwork);
        AddSkinStateLayers(button, buttonObject.transform, size, circle);
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

    ChamferedFrameGraphic CreateChamferedLayer(string name, Transform parent, Vector2 size,
        Color fill, Color border, float corner, float borderWidth)
    {
        GameObject layer = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(ChamferedFrameGraphic));
        layer.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)layer.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        ChamferedFrameGraphic graphic = layer.GetComponent<ChamferedFrameGraphic>();
        graphic.Configure(fill, border, corner, borderWidth);
        graphic.raycastTarget = false;
        return graphic;
    }

    ArcaneRingGraphic CreateArcaneRing(string name, Transform parent, Vector2 size,
        Color accent, Color ticks, int tickCount)
    {
        GameObject layer = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(ArcaneRingGraphic));
        layer.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)layer.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        ArcaneRingGraphic ring = layer.GetComponent<ArcaneRingGraphic>();
        ring.Configure(new Color(accent.r * .55f, accent.g * .55f, accent.b * .55f, .88f),
            new Color(accent.r, accent.g, accent.b, .96f), ticks, .055f);
        ring.accentTicks = tickCount;
        ring.raycastTarget = false;
        return ring;
    }

    void AddSkinStateLayers(Button button, Transform parent, Vector2 size, bool preserveAspect)
    {
        if (!hudSkin || (!hudSkin.pressedOverlay && !hudSkin.disabledOverlay)) return;
        Image pressed = hudSkin.pressedOverlay
            ? CreateImage("PressedArtwork", parent, hudSkin.pressedOverlay, Vector2.zero,
                size, Color.white, false) : null;
        Image disabled = hudSkin.disabledOverlay
            ? CreateImage("DisabledArtwork", parent, hudSkin.disabledOverlay, Vector2.zero,
                size, Color.white, false) : null;
        if (pressed) { pressed.preserveAspect = preserveAspect; SetImageType(pressed, !preserveAspect); }
        if (disabled) { disabled.preserveAspect = preserveAspect; SetImageType(disabled, !preserveAspect); }
        HlwHudSpriteState state = button.gameObject.AddComponent<HlwHudSpriteState>();
        state.Configure(button, pressed, disabled);
    }

    Sprite CustomCircleFrameFor(string name)
    {
        if (!hudSkin) return null;
        if (name == "AutoAttackButton")
            return SkinOrFallback(hudSkin.autoAttackButtonFrame, hudSkin.primaryCircularButtonFrame);
        if (name == "UnitsToggle")
            return SkinOrFallback(hudSkin.unitsButtonFrame, hudSkin.primaryCircularButtonFrame);
        if (name == "ItemsToggle")
            return SkinOrFallback(hudSkin.itemsButtonFrame, hudSkin.secondaryCircularButtonFrame);
        return null;
    }

    bool HasCompleteButtonArtwork(string name)
    {
        if (!hudSkin) return false;
        if (name == "UnitsToggle") return hudSkin.unitsButtonFrame;
        if (name == "ItemsToggle") return hudSkin.itemsButtonFrame;
        if (name == "AutoAttackButton") return hudSkin.autoAttackButtonFrame;
        return false;
    }

    Sprite CircleFrameFor(string name) =>
        SkinOrFallback(CustomCircleFrameFor(name), ornateButtonFrameSprite);

    static Sprite SkinOrFallback(Sprite skinSprite, Sprite fallback) => skinSprite ? skinSprite : fallback;

    static void SetImageType(Image image, bool scalable)
    {
        if (!image) return;
        Vector4 border = image.sprite ? image.sprite.border : Vector4.zero;
        image.type = scalable && image.sprite &&
            (border.x > 0f || border.y > 0f || border.z > 0f || border.w > 0f)
            ? Image.Type.Sliced : Image.Type.Simple;
    }

    Sprite ButtonSprite(string name)
    {
        if (HasCompleteButtonArtwork(name)) return CustomCircleFrameFor(name);
        if (name == "FireballButton") return circleSprite;
        if (name == "UnitsToggle") return circleSprite;
        if (name == "ItemsToggle") return circleSprite;
        if (name == "StatsToggleButton")
            return SkinOrFallback(hudSkin ? hudSkin.statsFrame : null, capsuleSprite);
        if (name.StartsWith("Send", System.StringComparison.Ordinal)) return buttonGreenSprite;
        return buttonCyanSprite ? buttonCyanSprite : circleSprite;
    }

    Sprite ButtonIcon(string name)
    {
        if (name == "UnitsToggle") return unitsIconSprite;
        if (name == "ItemsToggle") return itemsIconSprite;
        if (name == "RestartButton" || name == "ReturnToMenuButton") return backIconSprite;
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
            background.color = hudSkin && hudSkin.abilityFrame ? Color.white :
                (affordable ? Color.white : new Color(.42f, .47f, .52f, .72f));
        }
        if (label) label.color = affordable
            ? new Color(1f, .9f, .52f, 1f) : new Color(.84f, .77f, .57f, 1f);
        Transform portraitTransform = button.transform.Find("PortraitMask/UnitPortrait");
        Image portrait = portraitTransform ? portraitTransform.GetComponent<Image>() : null;
        if (portrait) portrait.color = Color.white;
        Transform overlayTransform = button.transform.Find("PortraitMask/UnaffordableOverlay");
        Image overlay = overlayTransform ? overlayTransform.GetComponent<Image>() : null;
        if (overlay) overlay.color = affordable
            ? new Color(.015f, .035f, .075f, 0f)
            : new Color(.015f, .035f, .075f, .26f);
        Transform interiorTransform = button.transform.Find("DarkInterior");
        Image interior = interiorTransform ? interiorTransform.GetComponent<Image>() : null;
        if (interior) interior.color = affordable
            ? Color.Lerp(ControlNavy, accent, .08f) : new Color(.055f, .065f, .075f, .74f);
        Transform frameTransform = button.transform.Find("FantasyFrame");
        Image frame = frameTransform ? frameTransform.GetComponent<Image>() : null;
        if (frame) frame.color = hudSkin &&
            (hudSkin.abilityFrame || hudSkin.secondaryCircularButtonFrame)
            ? (affordable ? new Color(.9f, .96f, 1f, .94f) : new Color(.68f, .76f, .82f, .78f))
            : (affordable ? new Color(accent.r, accent.g, accent.b, .92f)
                : new Color(.34f, .39f, .44f, .46f));
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

    static string PurchaseLabel(string unitName, int cost) => $"{cost} GOLD";

    static Sprite LoadUnitProfile(string resourcePath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite) return sprite;
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        return texture ? Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(.5f, .5f), 100f, 0u, SpriteMeshType.FullRect) : null;
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
