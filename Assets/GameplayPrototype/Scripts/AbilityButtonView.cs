using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable layered mobile ability button. Its frame, blue backing, icon, cooldown,
/// and selected state are separate UI graphics; replace iconSprite to reuse the style.
/// </summary>
[DisallowMultipleComponent]
public sealed class AbilityButtonView : MonoBehaviour
{
    [Header("Swappable art")]
    public Sprite frameSprite;
    public Sprite backgroundSprite;
    public Sprite iconSprite;
    public Sprite selectedSprite;

    [Header("Independent layer sizing")]
    [Range(.5f, 1.1f)] public float frameScale = 1f;
    [Range(.5f, 1.1f)] public float backgroundScale = 1f;
    [Range(.5f, 1.1f)] public float iconScale = .68f;

    [Header("State styling")]
    public Color backgroundTint = Color.white;
    public Color cooldownTint = new Color(.015f, .025f, .06f, .78f);
    public Color selectedTint = new Color(.2f, .88f, 1f, .48f);
    public Color selectedOutlineTint = new Color(.25f, .85f, 1f, .95f);
    public bool showCooldownText = true;
    public bool radialCooldown = true;
    public string abilityLabel;

    Button button;
    Image backgroundImage;
    Image frameImage;
    Image iconImage;
    Image cooldownImage;
    Image selectedImage;
    Image pressedImage;
    Image disabledImage;
    Text cooldownText;
    Text titleText;
    Outline backgroundOutline;
    AbilityButtonGlyph fallbackGlyph;
    ArcaneRingGraphic decorativeRing;
    bool built;
    bool compositeArtworkActive;
    int lastCooldownSeconds = -1;

    public Button Button => button;
    public Image IconImage => iconImage;

    /// <summary>Creates the layered view at a landscape HUD anchor.</summary>
    public static AbilityButtonView Create(Transform parent, string objectName, Vector2 size,
        Vector2 anchoredPosition, bool bottomRight, Sprite frame, Sprite blueBackground,
        Sprite icon, Sprite selected)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        AbilityButtonView view = root.AddComponent<AbilityButtonView>();
        view.frameSprite = frame;
        view.backgroundSprite = blueBackground;
        view.iconSprite = icon;
        view.selectedSprite = selected;
        view.Build(size, anchoredPosition, bottomRight);
        return view;
    }

    /// <summary>Builds the view's independent visual layers.</summary>
    public void Build(Vector2 size, Vector2 anchoredPosition, bool bottomRight)
    {
        if (built) return;
        built = true;
        RectTransform rootRect = (RectTransform)transform;
        rootRect.anchorMin = rootRect.anchorMax = bottomRight ? new Vector2(1f, 0f) : Vector2.zero;
        rootRect.pivot = bottomRight ? new Vector2(1f, 0f) : Vector2.zero;
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = size;

        Image hitTarget = GetComponent<Image>();
        hitTarget.color = Color.clear;
        hitTarget.raycastTarget = true;
        button = GetComponent<Button>();
        button.targetGraphic = null;
        button.transition = Selectable.Transition.None;

        Color backingTint = backgroundSprite ? backgroundTint : new Color(.035f, .09f, .18f, .96f);
        backgroundImage = CreateLayer("BlueBackground", backgroundSprite, backingTint, backgroundScale);
        backgroundImage.raycastTarget = false;
        button.targetGraphic = backgroundImage;

        selectedImage = CreateLayer("SelectedOverlay", selectedSprite ? selectedSprite : backgroundSprite,
            new Color(selectedTint.r, selectedTint.g, selectedTint.b, 0f));
        selectedImage.raycastTarget = false;
        selectedImage.gameObject.SetActive(false);

        GameObject glyphObject = new GameObject("AbilityGlyphFallback", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(AbilityButtonGlyph));
        glyphObject.transform.SetParent(transform, false);
        RectTransform glyphRect = (RectTransform)glyphObject.transform;
        Stretch(glyphRect, .62f);
        fallbackGlyph = glyphObject.GetComponent<AbilityButtonGlyph>();
        fallbackGlyph.raycastTarget = false;

        iconImage = CreateLayer("AbilityIcon", iconSprite, Color.white, iconScale);
        iconImage.raycastTarget = false;
        iconImage.preserveAspect = true;
        fallbackGlyph.gameObject.SetActive(!iconSprite);

        cooldownImage = CreateLayer("CooldownRadial", backgroundSprite, cooldownTint, backgroundScale);
        cooldownImage.type = radialCooldown ? Image.Type.Filled : Image.Type.Simple;
        cooldownImage.fillMethod = Image.FillMethod.Radial360;
        cooldownImage.fillOrigin = (int)Image.Origin360.Bottom;
        cooldownImage.fillClockwise = true;
        cooldownImage.fillAmount = 0f;
        cooldownImage.raycastTarget = false;
        cooldownImage.gameObject.SetActive(false);

        GameObject textObject = new GameObject("CooldownText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(transform, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        Stretch(textRect, .8f);
        cooldownText = textObject.GetComponent<Text>();
        cooldownText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cooldownText.fontSize = Mathf.Max(14, Mathf.RoundToInt(size.x * .14f));
        cooldownText.fontStyle = FontStyle.Bold;
        cooldownText.alignment = TextAnchor.MiddleCenter;
        cooldownText.color = Color.white;
        cooldownText.raycastTarget = false;
        cooldownText.gameObject.SetActive(false);

        GameObject titleObject = new GameObject("AbilityLabel", typeof(RectTransform), typeof(Text));
        titleObject.transform.SetParent(transform, false);
        RectTransform titleRect = (RectTransform)titleObject.transform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(.5f, .5f);
        titleRect.pivot = new Vector2(.5f, .5f);
        titleRect.anchoredPosition = new Vector2(0f, -size.y * .34f);
        titleRect.sizeDelta = new Vector2(size.x * .84f, size.y * .22f);
        titleText = titleObject.GetComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = Mathf.Max(10, Mathf.RoundToInt(size.x * .092f));
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.raycastTarget = false;
        titleText.text = abilityLabel;

        frameImage = CreateLayer("OuterFrame", frameSprite, Color.white, frameScale);
        frameImage.raycastTarget = false;
        frameImage.preserveAspect = true;

        GameObject ringObject = new GameObject("ArcaneAbilityRing", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(ArcaneRingGraphic));
        ringObject.transform.SetParent(transform, false);
        RectTransform ringRect = (RectTransform)ringObject.transform;
        ringRect.anchorMin = ringRect.anchorMax = ringRect.pivot = new Vector2(.5f, .5f);
        ringRect.anchoredPosition = Vector2.zero;
        ringRect.sizeDelta = new Vector2(size.x * .96f, size.y * .96f);
        decorativeRing = ringObject.GetComponent<ArcaneRingGraphic>();
        decorativeRing.Configure(new Color(.08f, .42f, .68f, .88f),
            new Color(.55f, .9f, 1f, .92f), new Color(1f, .68f, .22f, .72f), .055f);
        decorativeRing.accentTicks = 4;
        decorativeRing.raycastTarget = false;

        backgroundOutline = backgroundImage.gameObject.AddComponent<Outline>();
        backgroundOutline.effectColor = new Color(selectedOutlineTint.r, selectedOutlineTint.g,
            selectedOutlineTint.b, 0f);
        backgroundOutline.effectDistance = new Vector2(2f, 2f);
        backgroundOutline.useGraphicAlpha = true;
    }

    public void SetIcon(Sprite sprite)
    {
        if (iconSprite == sprite && iconImage && iconImage.enabled == (bool)sprite) return;
        iconSprite = sprite;
        if (!built) return;
        iconImage.sprite = sprite;
        iconImage.enabled = sprite;
        fallbackGlyph.gameObject.SetActive(!sprite);
    }

    /// <summary>Uses a complete button illustration while retaining cooldown and input layers.</summary>
    public void SetCompositeArtwork(Sprite artwork)
    {
        if (!built || !backgroundImage) return;
        bool active = artwork;
        if (compositeArtworkActive == active && (!active || backgroundImage.sprite == artwork)) return;
        compositeArtworkActive = active;
        backgroundImage.sprite = active ? artwork : backgroundSprite;
        backgroundImage.color = active || backgroundSprite ? Color.white : backgroundTint;
        backgroundImage.preserveAspect = active;
        if (frameImage) frameImage.gameObject.SetActive(!active && frameSprite);
        if (iconImage) iconImage.gameObject.SetActive(!active && iconSprite);
        if (fallbackGlyph) fallbackGlyph.gameObject.SetActive(!active && !iconSprite);
        if (titleText) titleText.gameObject.SetActive(!active);
        if (decorativeRing) decorativeRing.gameObject.SetActive(!active);
    }

    public void SetFallbackStyle(AbilityGlyphStyle style)
    {
        if (fallbackGlyph) fallbackGlyph.SetStyle(style);
    }

    /// <summary>Applies optional presentation-only kit overlays without replacing button logic.</summary>
    public void SetStateSprites(Sprite selected, Sprite pressed, Sprite disabled, Sprite cooldown)
    {
        if (selectedImage && selected) selectedImage.sprite = selected;
        if (cooldownImage && cooldown) cooldownImage.sprite = cooldown;
        if (pressed && !pressedImage)
        {
            pressedImage = CreateLayer("PressedArtwork", pressed, Color.white, .92f);
            pressedImage.gameObject.SetActive(false);
        }
        if (disabled && !disabledImage)
        {
            disabledImage = CreateLayer("DisabledArtwork", disabled, Color.white, .92f);
            disabledImage.gameObject.SetActive(false);
        }
        if ((pressedImage || disabledImage) && !GetComponent<HlwHudSpriteState>())
        {
            HlwHudSpriteState state = gameObject.AddComponent<HlwHudSpriteState>();
            state.Configure(button, pressedImage, disabledImage);
        }
    }

    /// <summary>Adjusts the three reusable art layers without changing their sprites or aspect ratios.</summary>
    public void SetLayerScales(float frame, float background, float icon)
    {
        frameScale = Mathf.Clamp(frame, .5f, 1.1f);
        backgroundScale = Mathf.Clamp(background, .5f, 1.1f);
        iconScale = Mathf.Clamp(icon, .5f, 1.1f);
        ResizeLayer(frameImage, frameScale);
        ResizeLayer(backgroundImage, backgroundScale);
        ResizeLayer(iconImage, iconScale);
        ResizeLayer(cooldownImage, backgroundScale);
    }

    /// <summary>Premium frame artwork already contains its own rim; hide the procedural micro-ornaments.</summary>
    public void SetDecorativeRingVisible(bool visible)
    {
        if (decorativeRing) decorativeRing.gameObject.SetActive(visible);
    }

    public void SetFrameTint(Color tint)
    {
        if (frameImage) frameImage.color = tint;
    }

    public void UseCleanMobileRing()
    {
        if (frameImage) frameImage.gameObject.SetActive(false);
        if (!decorativeRing) return;
        decorativeRing.gameObject.SetActive(true);
        decorativeRing.Configure(new Color(.08f, .38f, .62f, .94f),
            new Color(.58f, .9f, 1f, .98f), Color.clear, .075f);
        decorativeRing.accentTicks = 0;
    }

    public void SetCooldown(float remaining01, int secondsRemaining = 0)
    {
        if (!built || !cooldownImage || !cooldownText) return;
        remaining01 = Mathf.Clamp01(remaining01);
        cooldownImage.fillAmount = remaining01;
        bool cooling = remaining01 > .001f;
        cooldownImage.gameObject.SetActive(cooling);
        secondsRemaining = cooling ? Mathf.Max(0, secondsRemaining) : 0;
        bool showLabel = showCooldownText && secondsRemaining > 0;
        if (secondsRemaining != lastCooldownSeconds)
        {
            cooldownText.text = showLabel ? secondsRemaining.ToString() : string.Empty;
            lastCooldownSeconds = secondsRemaining;
        }
        cooldownText.gameObject.SetActive(showLabel);
    }

    public void SetLabel(string label)
    {
        abilityLabel = label;
        if (titleText)
        {
            titleText.text = label;
            titleText.gameObject.SetActive(!compositeArtworkActive);
        }
    }

    public void SetSelected(bool selected)
    {
        if (!built || !selectedImage || !backgroundOutline) return;
        if (selectedImage.gameObject.activeSelf == selected) return;
        selectedImage.gameObject.SetActive(selected);
        Color tint = selectedTint;
        tint.a = selected ? selectedTint.a : 0f;
        selectedImage.color = tint;
        Color outline = selectedOutlineTint;
        outline.a = selected ? selectedOutlineTint.a : 0f;
        backgroundOutline.effectColor = outline;
        transform.localScale = selected ? Vector3.one * 1.045f : Vector3.one;
    }

    public void SetInteractable(bool interactable)
    {
        if (button) button.interactable = interactable;
    }

    Image CreateLayer(string name, Sprite sprite, Color tint, float scale = 1f, float aspectHeight = 1f)
    {
        GameObject layer = new GameObject(name, typeof(RectTransform), typeof(Image));
        layer.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)layer.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        RectTransform rootRect = (RectTransform)transform;
        rect.sizeDelta = new Vector2(rootRect.sizeDelta.x * scale, rootRect.sizeDelta.y * scale * aspectHeight);
        Image image = layer.GetComponent<Image>();
        image.sprite = sprite;
        image.color = sprite || name == "BlueBackground" ? tint : Color.clear;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    void ResizeLayer(Image image, float scale)
    {
        if (!image) return;
        RectTransform rootRect = (RectTransform)transform;
        RectTransform layerRect = (RectTransform)image.transform;
        layerRect.sizeDelta = new Vector2(rootRect.sizeDelta.x * scale, rootRect.sizeDelta.y * scale);
    }

    static void Stretch(RectTransform rect, float fraction)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        RectTransform parent = (RectTransform)rect.parent;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(parent.sizeDelta.x * fraction, parent.sizeDelta.y * fraction);
    }
}

