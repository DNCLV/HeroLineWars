using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Premium, safe-area-aware landscape front end for Hero Line Wars.</summary>
public sealed class MainMenuController : MonoBehaviour
{
    static readonly Color DeepNavy = new Color(.008f, .018f, .043f, 1f);
    static readonly Color PanelNavy = new Color(.018f, .037f, .069f, .91f);
    static readonly Color CardNavy = new Color(.025f, .052f, .094f, .94f);
    static readonly Color Cyan = new Color(.18f, .79f, 1f, 1f);
    static readonly Color BlueMetal = new Color(.20f, .42f, .65f, .95f);
    static readonly Color Gold = new Color(1f, .72f, .20f, 1f);
    static readonly Color PaleGold = new Color(1f, .89f, .56f, 1f);
    static readonly Color Muted = new Color(.49f, .58f, .70f, 1f);

    RectTransform canvasRect;
    RectTransform safeRoot;
    RectTransform abilityPreviewRoot;
    Rect lastSafeArea;
    Vector2 lastCanvasSize;
    int lastScreenWidth = -1;
    int lastScreenHeight = -1;
    RectTransform backgroundArtworkViewport;
    Image backgroundArtwork;
    MenuGradientGraphic backgroundGradient;
    Text showcaseName;
    Text showcaseRole;
    Text showcaseDescription;
    HeroDefinition selectedDefinition;
    Sprite selectedArtwork;
    Sprite panelSprite;
    Sprite ornateFrameSprite;
    Sprite circleSprite;
    Sprite battlemageBackdrop;
    Button playButton;

    readonly Dictionary<HlwHeroSelection, ChamferedFrameGraphic> heroCards =
        new Dictionary<HlwHeroSelection, ChamferedFrameGraphic>();
    readonly Dictionary<HlwHeroSelection, Text> heroCardStates =
        new Dictionary<HlwHeroSelection, Text>();

    void Awake()
    {
        HlwGameSpeed.ResetToNormal();
        LoadArt();
        EnsureMenuCamera();
        EnsureEventSystem();
        BuildInterface();
        Canvas.ForceUpdateCanvases();
        ApplySafeArea(true);
        Canvas.ForceUpdateCanvases();
        RefreshArtworkCrop(true);
        ConfigureLayoutValidator();
    }

    void LateUpdate()
    {
        ApplySafeArea(false);
        RefreshArtworkCrop(false);
    }

    void OnDisable() => HlwGameSpeed.ResetToNormal();
    void OnApplicationQuit() => HlwGameSpeed.ResetToNormal();

    void LoadArt()
    {
        panelSprite = Resources.Load<Sprite>("HlwFantasyUI/Frames/Container1_default");
        ornateFrameSprite = Resources.Load<Sprite>("HlwFantasyUI/Buttons/FantasyButton1_ultramarine");
        circleSprite = Resources.Load<Sprite>("HlwFantasyUI/Buttons/Button6_ultramarine");
        battlemageBackdrop = Resources.Load<Sprite>("Heroes/Art/Battlemage Menu Background");
    }

    void EnsureMenuCamera()
    {
        Camera menuCamera = Camera.main;
        if (!menuCamera) menuCamera = FindAnyObjectByType<Camera>();
        if (!menuCamera)
        {
            GameObject cameraObject = new GameObject("MainMenuCamera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            menuCamera = cameraObject.GetComponent<Camera>();
        }

        menuCamera.enabled = true;
        menuCamera.clearFlags = CameraClearFlags.SolidColor;
        menuCamera.backgroundColor = DeepNavy;
        menuCamera.cullingMask = 0;
        menuCamera.orthographic = true;
        menuCamera.depth = -100f;
    }

    void EnsureEventSystem()
    {
        EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
        if (!eventSystem)
        {
            GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem = eventObject.GetComponent<EventSystem>();
        }
        if (!eventSystem.GetComponent<BaseInputModule>())
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    void BuildInterface()
    {
        Transform oldCanvas = transform.Find("MainMenuCanvas");
        if (oldCanvas)
        {
            oldCanvas.gameObject.SetActive(false);
            Destroy(oldCanvas.gameObject);
        }

        GameObject canvasObject = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasRect = canvasObject.GetComponent<RectTransform>();
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        HlwResponsiveCanvas.Configure(canvas, scaler, 100);

        BuildBackdrop(canvasRect);

        safeRoot = CreateRect("SafeArea", canvasRect);
        Stretch(safeRoot);
        BuildHeader();
        BuildHeroSelection();
        BuildHeroShowcase();
        BuildModeSelection();
        BuildPlayButton();

        HlwHeroSelection selected = HlwGameSession.Current
            ? HlwGameSession.Current.SelectedHero
            : HlwHeroSelection.Battlemage;
        SelectHero(HeroCatalog.Find(selected), selected);
    }

    void BuildBackdrop(Transform parent)
    {
        backgroundGradient = CreateGraphic<MenuGradientGraphic>("ArcaneFallback", parent);
        Stretch(backgroundGradient.rectTransform);
        backgroundGradient.Configure(new Color(.006f, .014f, .034f, 1f), new Color(.035f, .11f, .17f, 1f));

        backgroundArtworkViewport = CreateRect("ArtworkViewport", parent);
        Stretch(backgroundArtworkViewport);
        backgroundArtworkViewport.gameObject.AddComponent<RectMask2D>();
        backgroundArtwork = CreateImage("SelectedHeroFullscreenArtwork", backgroundArtworkViewport, null, Color.clear);
        backgroundArtwork.type = Image.Type.Simple;
        backgroundArtwork.preserveAspect = true;
        backgroundArtwork.color = Color.clear;

        Image leftAtmosphere = CreateImage("LeftAtmosphere", parent, null, new Color(.004f, .013f, .034f, .56f));
        SetAnchored(leftAtmosphere.rectTransform, Vector2.zero, new Vector2(.30f, 1f), Vector2.zero, Vector2.zero);
        Image rightAtmosphere = CreateImage("RightAtmosphere", parent, null, new Color(.004f, .013f, .034f, .58f));
        SetAnchored(rightAtmosphere.rectTransform, new Vector2(.70f, 0f), Vector2.one, Vector2.zero, Vector2.zero);

        MenuGradientGraphic cinematicWash = CreateGraphic<MenuGradientGraphic>("CinematicWash", parent);
        Stretch(cinematicWash.rectTransform);
        cinematicWash.Configure(new Color(.003f, .010f, .028f, .86f), new Color(.003f, .012f, .032f, .34f));

        MenuVignetteGraphic vignette = CreateGraphic<MenuVignetteGraphic>("Vignette", parent);
        Stretch(vignette.rectTransform);
        vignette.edgeColor = new Color(0f, .006f, .022f, .86f);
        vignette.centerColor = new Color(0f, 0f, 0f, 0f);
        vignette.raycastTarget = false;
    }

    void BuildHeader()
    {
        Text title = CreateText("Title", safeRoot, "HERO LINE WARS", 43, Color.white, TextAnchor.MiddleCenter);
        SetAnchored(title.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(0f, -18f), new Vector2(720f, 58f), new Vector2(.5f, 1f));
        title.fontStyle = FontStyle.Bold;
        AddShadow(title, new Color(0f, 0f, 0f, .9f), new Vector2(2f, -3f));

        Text subtitle = CreateText("Subtitle", safeRoot, "ARCANE BATTLEGROUNDS", 15,
            new Color(.53f, .84f, 1f), TextAnchor.MiddleCenter);
        SetAnchored(subtitle.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(0f, -90f), new Vector2(420f, 26f), new Vector2(.5f, 1f));

        CreateHeaderRule("LeftRule", new Vector2(.29f, 1f), new Vector2(-95f, -67f));
        CreateHeaderRule("RightRule", new Vector2(.71f, 1f), new Vector2(95f, -67f));
    }

    void CreateHeaderRule(string name, Vector2 anchor, Vector2 position)
    {
        Image rule = CreateImage(name, safeRoot, null, new Color(.30f, .65f, .88f, .48f));
        SetAnchored(rule.rectTransform, anchor, anchor, position, new Vector2(170f, 1.5f));
        Image jewel = CreateImage(name + "Jewel", safeRoot, null, Gold);
        SetAnchored(jewel.rectTransform, anchor, anchor,
            new Vector2(position.x + (position.x < 0f ? 92f : -92f), position.y), new Vector2(7f, 7f));
        jewel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    void BuildHeroSelection()
    {
        RectTransform panel = CreatePanel("HeroSelection", safeRoot, new Vector2(.018f, .17f), new Vector2(.225f, .88f));
        AddPanelHeading(panel, "SELECT HERO", "CHOOSE YOUR CHAMPION");

        HeroDefinition battlemage = HeroCatalog.Find(HlwHeroSelection.Battlemage);
        HeroDefinition geomancer = HeroCatalog.Find(HlwHeroSelection.Geomancer);
        CreateHeroCard(panel, battlemage, HlwHeroSelection.Battlemage, .67f);
        CreateHeroCard(panel, geomancer, HlwHeroSelection.Geomancer, .43f);

        Text hint = CreateText("HeroHint", panel, "NEW CHAMPIONS WILL ARRIVE", 12,
            new Color(.42f, .52f, .64f), TextAnchor.MiddleCenter);
        SetAnchored(hint.rectTransform, new Vector2(.08f, .07f), new Vector2(.92f, .14f), Vector2.zero, Vector2.zero);
    }

    void CreateHeroCard(Transform parent, HeroDefinition definition, HlwHeroSelection id, float centerY)
    {
        Button card = CreateChamferedButton(id + "Card", parent, CardNavy, BlueMetal, 12f, 2f);
        SetAnchored(card.GetComponent<RectTransform>(), new Vector2(.075f, centerY - .105f),
            new Vector2(.925f, centerY + .105f), Vector2.zero, Vector2.zero);
        ChamferedFrameGraphic frame = card.GetComponent<ChamferedFrameGraphic>();
        heroCards[id] = frame;

        if (ornateFrameSprite)
        {
            Image inlay = CreateImage("OrnamentalInlay", card.transform, ornateFrameSprite,
                new Color(.34f, .58f, .78f, .20f));
            Stretch(inlay.rectTransform);
            inlay.type = Image.Type.Sliced;
        }

        Image portraitBack = CreateImage("PortraitBack", card.transform, circleSprite,
            new Color(.07f, .17f, .27f, .96f));
        SetAnchored(portraitBack.rectTransform, new Vector2(.04f, .13f), new Vector2(.36f, .87f),
            Vector2.zero, Vector2.zero);
        portraitBack.preserveAspect = true;

        Image portrait = CreateImage("Portrait", card.transform, definition ? definition.portrait : null, Color.white);
        SetAnchored(portrait.rectTransform, new Vector2(.055f, .15f), new Vector2(.345f, .85f),
            Vector2.zero, Vector2.zero);
        portrait.preserveAspect = true;
        if (!portrait.sprite)
        {
            portrait.color = Color.clear;
            Text glyph = CreateText("PortraitGlyph", card.transform,
                id == HlwHeroSelection.Battlemage ? "B" : "G", 42, PaleGold, TextAnchor.MiddleCenter);
            SetAnchored(glyph.rectTransform, new Vector2(.055f, .15f), new Vector2(.345f, .85f),
                Vector2.zero, Vector2.zero);
            glyph.fontStyle = FontStyle.Bold;
        }

        Text heroName = CreateText("HeroName", card.transform,
            (definition ? definition.displayName : id.ToString()).ToUpperInvariant(), 20,
            Color.white, TextAnchor.MiddleLeft);
        SetAnchored(heroName.rectTransform, new Vector2(.40f, .5f), new Vector2(.94f, .5f),
            new Vector2(0f, 26f), new Vector2(0f, 28f), new Vector2(.5f, 1f));
        heroName.fontStyle = FontStyle.Bold;

        Text role = CreateText("Role", card.transform,
            definition ? definition.classSubtitle.ToUpperInvariant() : "HERO", 11,
            new Color(.55f, .72f, .84f), TextAnchor.MiddleLeft);
        SetAnchored(role.rectTransform, new Vector2(.40f, .5f), new Vector2(.94f, .5f),
            new Vector2(0f, -10f), new Vector2(0f, 18f), new Vector2(.5f, 1f));

        Text state = CreateText("State", card.transform, "SELECT", 11, Cyan, TextAnchor.LowerLeft);
        SetAnchored(state.rectTransform, new Vector2(.40f, .08f), new Vector2(.94f, .27f),
            Vector2.zero, Vector2.zero);
        heroCardStates[id] = state;
        card.onClick.AddListener(() => SelectHero(definition, id));
    }

    void BuildHeroShowcase()
    {
        MenuGradientGraphic infoWash = CreateGraphic<MenuGradientGraphic>("HeroInformationWash", safeRoot);
        SetAnchored(infoWash.rectTransform, new Vector2(.245f, .115f), new Vector2(.755f, .41f),
            Vector2.zero, Vector2.zero);
        infoWash.Configure(new Color(.004f, .014f, .035f, .93f), new Color(.004f, .014f, .035f, 0f));

        showcaseName = CreateText("SelectedHeroName", safeRoot, string.Empty, 48, Color.white, TextAnchor.MiddleCenter);
        SetAnchored(showcaseName.rectTransform, new Vector2(.27f, .38f), new Vector2(.73f, .38f),
            Vector2.zero, new Vector2(0f, 62f), new Vector2(.5f, 1f));
        showcaseName.fontStyle = FontStyle.Bold;
        AddShadow(showcaseName, new Color(0f, 0f, 0f, .9f), new Vector2(2f, -3f));

        showcaseRole = CreateText("HeroRole", safeRoot, string.Empty, 15, Cyan, TextAnchor.MiddleCenter);
        SetAnchored(showcaseRole.rectTransform, new Vector2(.29f, .38f), new Vector2(.71f, .38f),
            new Vector2(0f, -74f), new Vector2(0f, 24f), new Vector2(.5f, 1f));

        showcaseDescription = CreateText("HeroDescription", safeRoot, string.Empty, 17,
            new Color(.77f, .82f, .88f), TextAnchor.UpperCenter);
        SetAnchored(showcaseDescription.rectTransform, new Vector2(.30f, .38f), new Vector2(.70f, .38f),
            new Vector2(0f, -116f), new Vector2(0f, 70f), new Vector2(.5f, 1f));

        abilityPreviewRoot = CreateRect("AbilityPreviews", safeRoot);
        SetAnchored(abilityPreviewRoot, new Vector2(.32f, .115f), new Vector2(.68f, .18f),
            Vector2.zero, Vector2.zero);
    }

    void BuildAbilityPreviews(HeroDefinition definition)
    {
        for (int i = abilityPreviewRoot.childCount - 1; i >= 0; i--)
            Destroy(abilityPreviewRoot.GetChild(i).gameObject);

        for (int i = 0; i < 3; i++)
        {
            float x0 = i / 3f + .012f;
            float x1 = (i + 1f) / 3f - .012f;
            ChamferedFrameGraphic chip = CreateGraphic<ChamferedFrameGraphic>("Ability" + (i + 1), abilityPreviewRoot);
            SetAnchored(chip.rectTransform, new Vector2(x0, .05f), new Vector2(x1, .95f), Vector2.zero, Vector2.zero);
            bool exists = definition && definition.abilities != null && i < definition.abilities.Length;
            chip.Configure(new Color(.025f, .052f, .086f, .90f),
                exists ? new Color(.26f, .62f, .83f, .72f) : new Color(.26f, .34f, .43f, .52f), 8f, 1.5f);

            Text label = CreateText("Label", chip.transform,
                exists ? definition.abilities[i].displayName.ToUpperInvariant() : "LOCKED",
                11, exists ? new Color(.81f, .91f, .98f) : Muted, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
        }
    }

    void BuildModeSelection()
    {
        RectTransform panel = CreatePanel("ModeSelection", safeRoot, new Vector2(.775f, .17f), new Vector2(.982f, .88f));
        AddPanelHeading(panel, "GAME MODE", "CHOOSE YOUR BATTLE");
        CreateModeButton(panel, "SINGLE PLAYER", "SURVIVAL", .69f, true, SelectSinglePlayer);
        CreateModeButton(panel, "MULTI PLAYER", "COMING SOON", .49f, false, null);
        CreateModeButton(panel, "CAMPAIGN", "COMING SOON", .29f, false, null);
    }

    void CreateModeButton(Transform parent, string title, string state, float centerY, bool enabled,
        UnityEngine.Events.UnityAction action)
    {
        Color border = enabled ? Cyan : new Color(.27f, .38f, .51f, .65f);
        Button button = CreateChamferedButton(title.Replace(" ", string.Empty), parent,
            enabled ? new Color(.025f, .08f, .13f, .96f) : new Color(.022f, .039f, .066f, .90f),
            border, 12f, enabled ? 2.5f : 1.5f);
        SetAnchored(button.GetComponent<RectTransform>(), new Vector2(.075f, centerY - .075f),
            new Vector2(.925f, centerY + .075f), Vector2.zero, Vector2.zero);

        if (ornateFrameSprite)
        {
            Image inlay = CreateImage("OrnamentalInlay", button.transform, ornateFrameSprite,
                enabled ? new Color(.28f, .67f, .88f, .24f) : new Color(.34f, .42f, .52f, .14f));
            Stretch(inlay.rectTransform);
            inlay.type = Image.Type.Sliced;
        }

        Image accent = CreateImage("Accent", button.transform, null, enabled ? Cyan : Muted);
        SetAnchored(accent.rectTransform, new Vector2(.035f, .20f), new Vector2(.047f, .80f),
            Vector2.zero, Vector2.zero);
        Text label = CreateText("ModeName", button.transform, title, 18,
            enabled ? Color.white : new Color(.52f, .58f, .66f), TextAnchor.MiddleLeft);
        SetAnchored(label.rectTransform, new Vector2(.10f, .5f), new Vector2(.92f, .5f),
            new Vector2(0f, 29f), new Vector2(0f, 25f), new Vector2(.5f, 1f));
        label.fontStyle = FontStyle.Bold;
        Text stateLabel = CreateText("State", button.transform, state, 11,
            enabled ? Cyan : Muted, TextAnchor.MiddleLeft);
        SetAnchored(stateLabel.rectTransform, new Vector2(.10f, .5f), new Vector2(.92f, .5f),
            new Vector2(0f, -4f), new Vector2(0f, 18f), new Vector2(.5f, 1f));
        button.interactable = enabled;
        if (enabled && action != null) button.onClick.AddListener(action);
    }

    void BuildPlayButton()
    {
        ChamferedFrameGraphic glow = CreateGraphic<ChamferedFrameGraphic>("PlayGlow", safeRoot);
        SetAnchored(glow.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
            new Vector2(0f, 18f), new Vector2(456f, 92f), new Vector2(.5f, 0f));
        glow.Configure(new Color(.31f, .16f, .025f, .60f), new Color(1f, .72f, .17f, .34f), 18f, 6f);
        AddShadow(glow, new Color(0f, 0f, 0f, .9f), new Vector2(0f, -8f));

        playButton = CreateChamferedButton("PlayButton", glow.transform,
            new Color(.70f, .39f, .055f, 1f), new Color(1f, .88f, .48f, 1f), 14f, 3f);
        SetAnchored(playButton.GetComponent<RectTransform>(), new Vector2(.018f, .10f), new Vector2(.982f, .90f),
            Vector2.zero, Vector2.zero);
        Text label = CreateText("Label", playButton.transform, "PLAY", 29,
            new Color(.10f, .055f, .012f), TextAnchor.MiddleCenter);
        Stretch(label.rectTransform);
        label.fontStyle = FontStyle.Bold;
        AddShadow(label, new Color(1f, .86f, .38f, .36f), new Vector2(0f, 1f));
        playButton.onClick.AddListener(Play);
    }

    RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        ChamferedFrameGraphic panel = CreateGraphic<ChamferedFrameGraphic>(name, parent);
        SetAnchored(panel.rectTransform, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        panel.Configure(PanelNavy, new Color(.22f, .43f, .63f, .82f), 20f, 2.5f);
        AddShadow(panel, new Color(0f, 0f, .015f, .86f), new Vector2(0f, -8f));

        if (panelSprite)
        {
            Image ornament = CreateImage("OrnamentalFrame", panel.transform, panelSprite,
                new Color(.29f, .48f, .69f, .24f));
            Stretch(ornament.rectTransform);
            ornament.type = Image.Type.Sliced;
        }

        Image topLine = CreateImage("TopGoldInlay", panel.transform, null, new Color(1f, .70f, .22f, .58f));
        SetAnchored(topLine.rectTransform, new Vector2(.12f, .978f), new Vector2(.88f, .981f),
            Vector2.zero, Vector2.zero);
        return panel.rectTransform;
    }

    void AddPanelHeading(Transform panel, string title, string subtitle)
    {
        Text heading = CreateText("Heading", panel, title, 23, Color.white, TextAnchor.MiddleCenter);
        SetAnchored(heading.rectTransform, new Vector2(.05f, 1f), new Vector2(.95f, 1f),
            new Vector2(0f, -38f), new Vector2(0f, 34f), new Vector2(.5f, 1f));
        heading.fontStyle = FontStyle.Bold;
        Text caption = CreateText("Caption", panel, subtitle, 11, Muted, TextAnchor.MiddleCenter);
        SetAnchored(caption.rectTransform, new Vector2(.05f, 1f), new Vector2(.95f, 1f),
            new Vector2(0f, -90f), new Vector2(0f, 18f), new Vector2(.5f, 1f));
        Image divider = CreateImage("HeadingDivider", panel, null, new Color(.26f, .57f, .78f, .46f));
        SetAnchored(divider.rectTransform, new Vector2(.12f, .79f), new Vector2(.88f, .793f),
            Vector2.zero, Vector2.zero);
    }

    void SelectHero(HeroDefinition definition, HlwHeroSelection id)
    {
        selectedDefinition = definition;
        selectedArtwork = definition && definition.showcaseArtwork
            ? definition.showcaseArtwork
            : (id == HlwHeroSelection.Battlemage ? battlemageBackdrop : null);
        if (HlwGameSession.Current) HlwGameSession.Current.SelectHero(id);

        foreach (KeyValuePair<HlwHeroSelection, ChamferedFrameGraphic> pair in heroCards)
        {
            if (!pair.Value) continue;
            bool selected = pair.Key == id;
            pair.Value.Configure(selected ? new Color(.075f, .065f, .045f, .96f) : CardNavy,
                selected ? Gold : BlueMetal, 12f, selected ? 3.5f : 1.75f);
        }
        foreach (KeyValuePair<HlwHeroSelection, Text> pair in heroCardStates)
        {
            if (!pair.Value) continue;
            bool selected = pair.Key == id;
            pair.Value.text = selected ? "◆  SELECTED" : "SELECT";
            pair.Value.color = selected ? PaleGold : Cyan;
        }

        backgroundArtwork.sprite = selectedArtwork;
        backgroundArtwork.color = selectedArtwork ? Color.white : Color.clear;
        backgroundGradient.Configure(
            id == HlwHeroSelection.Battlemage ? new Color(.006f, .014f, .034f, 1f) : new Color(.008f, .025f, .026f, 1f),
            id == HlwHeroSelection.Battlemage ? new Color(.035f, .11f, .17f, 1f) : new Color(.09f, .15f, .08f, 1f));

        showcaseName.text = (definition ? definition.displayName : id.ToString()).ToUpperInvariant();
        showcaseRole.text = definition ? definition.classSubtitle.ToUpperInvariant() : "HERO";
        showcaseDescription.text = definition ? definition.description : "Hero artwork and data are not available.";
        BuildAbilityPreviews(definition);
        RefreshArtworkCrop(true);
    }

    void RefreshArtworkCrop(bool force)
    {
        if (!canvasRect || !backgroundArtwork || !selectedArtwork) return;
        Vector2 canvasSize = canvasRect.rect.size;
        if (canvasSize.x < 1f || canvasSize.y < 1f) return;
        if (!force && (canvasSize - lastCanvasSize).sqrMagnitude < .1f) return;
        lastCanvasSize = canvasSize;

        Rect spriteRect = selectedArtwork.textureRect;
        float targetAspect = canvasSize.x / canvasSize.y;
        float sourceAspect = spriteRect.width / spriteRect.height;
        Vector2 artworkSize;
        Vector2 artworkPosition = Vector2.zero;
        if (targetAspect > sourceAspect)
        {
            artworkSize = new Vector2(canvasSize.x, canvasSize.x / sourceAspect);
            float focusY = .61f;
            artworkPosition.y = (.5f - focusY) * artworkSize.y;
        }
        else
        {
            artworkSize = new Vector2(canvasSize.y * sourceAspect, canvasSize.y);
        }
        SetAnchored(backgroundArtwork.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            artworkPosition, artworkSize);
    }

    void SelectSinglePlayer()
    {
        if (HlwGameSession.Current) HlwGameSession.Current.SelectMode(HlwGameMode.SinglePlayer);
    }

    void Play()
    {
        if (!HlwGameSession.Current) return;
        if (HlwGameSession.Current.SelectMode(HlwGameMode.SinglePlayer))
            HlwGameSession.Current.StartSelectedMatch();
    }

    void ApplySafeArea(bool force)
    {
        if (!safeRoot || !canvasRect || Screen.width < 1 || Screen.height < 1) return;
        Rect safe = HlwResponsiveCanvas.ValidSafeArea();
        if (!force && Screen.width == lastScreenWidth && Screen.height == lastScreenHeight &&
            HlwResponsiveCanvas.Approximately(safe, lastSafeArea)) return;
        lastSafeArea = safe;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        HlwResponsiveCanvas.ApplySafeArea(safeRoot, safe);
        Canvas.ForceUpdateCanvases();
    }

    void ConfigureLayoutValidator()
    {
        if (!canvasRect || !safeRoot) return;
        Canvas canvas = canvasRect.GetComponent<Canvas>();
        HlwUiLayoutValidator validator = canvasRect.GetComponent<HlwUiLayoutValidator>();
        if (!validator) validator = canvasRect.gameObject.AddComponent<HlwUiLayoutValidator>();
        validator.Configure(canvas, safeRoot,
            "Title", "HeroSelection", "ModeSelection", "HeroInformationWash", "PlayGlow");
    }

    Button CreateChamferedButton(string name, Transform parent, Color fill, Color border,
        float corner, float borderWidth)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(ChamferedFrameGraphic), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        ChamferedFrameGraphic frame = buttonObject.GetComponent<ChamferedFrameGraphic>();
        frame.Configure(fill, border, corner, borderWidth);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = frame;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(.83f, .90f, 1f, 1f);
        colors.disabledColor = new Color(.48f, .51f, .57f, .78f);
        colors.fadeDuration = .07f;
        button.colors = colors;
        AddShadow(frame, new Color(0f, 0f, .012f, .72f), new Vector2(0f, -4f));
        return button;
    }

    static T CreateGraphic<T>(string name, Transform parent) where T : Graphic
    {
        GameObject graphicObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        graphicObject.transform.SetParent(parent, false);
        T graphic = graphicObject.GetComponent<T>();
        graphic.raycastTarget = false;
        return graphic;
    }

    static Image CreateImage(string name, Transform parent, Sprite sprite, Color tint)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = tint;
        image.raycastTarget = false;
        return image;
    }

    static Text CreateText(string name, Transform parent, string value, int size, Color color, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static void SetAnchored(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot ?? new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static void AddShadow(Graphic graphic, Color color, Vector2 distance)
    {
        Shadow shadow = graphic.gameObject.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }
}
