using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Swappable presentation-only artwork for the runtime gameplay HUD. Assign transparent PNG
/// sprites here; gameplay components, labels, icons and input targets remain independent.
/// </summary>
[CreateAssetMenu(fileName = "HLW_PremiumHudSkin", menuName = "Hero Line Wars/Premium HUD Skin")]
public sealed class HlwHudSkin : ScriptableObject
{
    [Header("Circular controls")]
    public Sprite primaryCircularButtonFrame;
    public Sprite secondaryCircularButtonFrame;
    public Sprite unitsButtonFrame;
    public Sprite itemsButtonFrame;
    public Sprite autoAttackButtonFrame;
    public Sprite abilityFrame;
    [Tooltip("Complete Battlemage Fireball button artwork, including icon and frame.")]
    public Sprite fireballButtonArtwork;
    public Sprite lockedAbilityOverlay;

    [Header("Panels")]
    [Tooltip("Use a 9-sliced sprite for the coordinated Gold/Income/Next/Base panels.")]
    public Sprite topHudBackground;
    [Tooltip("Three-cell connected frame for Gold, Income and Next.")]
    public Sprite topbarEconomy;
    public Sprite topbarRedBase;
    public Sprite topbarBlueBase;
    [Tooltip("Transparent ornamental border. The live minimap remains a separate layer below it.")]
    public Sprite minimapFrame;
    [Tooltip("Use a 9-sliced sprite so hero cards can remain responsive.")]
    public Sprite heroNavigationCard;
    [Tooltip("Use a 9-sliced sprite.")]
    public Sprite statsFrame;
    [Tooltip("Optional shared backing behind Units and Items.")]
    public Sprite controlClusterBackground;
    [Tooltip("Optional shared backing for the Stats/Level/XP strip.")]
    public Sprite progressionStripBackground;
    [Tooltip("Wide decorative flow used behind the bottom-right control cluster.")]
    public Sprite bottomHudFlowBackground;

    [Header("Progression")]
    public Sprite levelBadge;
    [Tooltip("Use a 9-sliced sprite.")]
    public Sprite xpBarBackground;
    [Tooltip("Complete runtime-clean level and XP frame; live labels/fill remain separate.")]
    public Sprite progressionBarArtwork;
    public Sprite xpBarFill;

    [Header("Optional state overlays")]
    public Sprite selectedOverlay;
    public Sprite pressedOverlay;
    public Sprite disabledOverlay;
    public Sprite cooldownOverlay;

}

/// <summary>Optional sprite state overlays; decorative Images never receive raycasts.</summary>
[DisallowMultipleComponent]
public sealed class HlwHudSpriteState : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IPointerExitHandler
{
    Button button;
    Image pressedOverlay;
    Image disabledOverlay;
    bool pressed;

    public void Configure(Button owner, Image pressedImage, Image disabledImage)
    {
        button = owner;
        pressedOverlay = pressedImage;
        disabledOverlay = disabledImage;
        if (pressedOverlay) { pressedOverlay.raycastTarget = false; pressedOverlay.gameObject.SetActive(false); }
        if (disabledOverlay) { disabledOverlay.raycastTarget = false; disabledOverlay.gameObject.SetActive(false); }
        Refresh();
    }

    void LateUpdate() => Refresh();

    void Refresh()
    {
        bool usable = button && button.interactable;
        if (pressedOverlay) pressedOverlay.gameObject.SetActive(usable && pressed);
        if (disabledOverlay) disabledOverlay.gameObject.SetActive(!usable);
    }

    public void OnPointerDown(PointerEventData eventData) { pressed = true; Refresh(); }
    public void OnPointerUp(PointerEventData eventData) { pressed = false; Refresh(); }
    public void OnPointerExit(PointerEventData eventData) { pressed = false; Refresh(); }
    void OnDisable() { pressed = false; }
}
