using UnityEngine;

/// <summary>Clip-name based legacy animation bridge for imported GLB heroes.</summary>
public sealed class HeroAnimationDriver : MonoBehaviour
{
    const string NeutralClipName = "__HLW_NeutralPose";

    HeroDefinition definition;
    HeroLaneController movement;
    Animation legacyAnimation;
    string currentClip;
    string neutralClip;
    float actionUntil;
    bool locomotionInitialized;
    bool lastMoving;

    public void Configure(HeroDefinition heroDefinition)
    {
        definition = heroDefinition;
        movement = GetComponent<HeroLaneController>();
        legacyAnimation = GetComponentInChildren<Animation>(true);
        if (!legacyAnimation)
            Debug.LogWarning($"{name}: imported hero has no Legacy Animation component; gameplay remains functional.", this);
        PrepareNeutralPose();
        PlayLocomotion(true);
    }

    void Update()
    {
        if (!definition || !legacyAnimation || Time.time < actionUntil) return;
        PlayLocomotion(false);
    }

    public float PlayBasicAttack(float fallbackDuration = .42f) =>
        PlayAction(definition ? definition.basicAttackClip : null, fallbackDuration);
    public float PlayFaultbreaker(float fallbackDuration = .7f) =>
        PlayAction(definition ? definition.faultbreakerClip : null, fallbackDuration);
    public float PlayEarthSpikes(float fallbackDuration = .9f) =>
        PlayAction(definition ? definition.earthSpikesClip : null, fallbackDuration);

    float PlayAction(string clip, float fallbackDuration)
    {
        float duration = fallbackDuration;
        if (legacyAnimation && !string.IsNullOrEmpty(clip) && legacyAnimation.GetClip(clip))
        {
            AnimationState state = legacyAnimation[clip];
            state.wrapMode = WrapMode.Once;
            state.speed = 1f;
            duration = Mathf.Max(.1f, state.length);
            legacyAnimation.CrossFade(clip, .08f);
            currentClip = clip;
        }
        else
        {
            // Action gameplay is timer-driven. Heroes with intentionally disabled attack clips
            // remain in a stable neutral pose while their damage/VFX coroutines continue normally.
            PlayNeutralPose(.1f);
        }
        actionUntil = Time.time + duration;
        return duration;
    }

    void PlayLocomotion(bool force)
    {
        if (!definition || !legacyAnimation) return;
        bool moving = movement && movement.IsMoving;
        string clip = moving ? definition.runClip :
            (!string.IsNullOrEmpty(neutralClip) ? neutralClip : definition.idleClip);
        if (!force && locomotionInitialized && currentClip == clip && lastMoving == moving) return;
        AnimationClip animationClip = legacyAnimation.GetClip(clip);
        if (!animationClip) return;
        AnimationState state = legacyAnimation[clip];
        state.wrapMode = WrapMode.Loop;
        state.speed = moving ? 1f : 0f;
        state.normalizedTime = moving ? state.normalizedTime : 0f;
        legacyAnimation.CrossFade(clip, .12f);
        currentClip = clip;
        lastMoving = moving;
        locomotionInitialized = true;
    }

    void PrepareNeutralPose()
    {
        neutralClip = null;
        if (!definition || !legacyAnimation || definition.heroId != HlwHeroSelection.Geomancer) return;
        AnimationClip source = legacyAnimation.GetClip(definition.idleClip);
        if (!source) return;
        if (!legacyAnimation.GetClip(NeutralClipName))
            legacyAnimation.AddClip(source, NeutralClipName);
        neutralClip = NeutralClipName;
    }

    void PlayNeutralPose(float fade)
    {
        if (!definition || !legacyAnimation) return;
        string clip = !string.IsNullOrEmpty(neutralClip) ? neutralClip : definition.idleClip;
        AnimationClip animationClip = legacyAnimation.GetClip(clip);
        if (!animationClip) return;
        AnimationState state = legacyAnimation[clip];
        state.wrapMode = WrapMode.Loop;
        state.normalizedTime = 0f;
        state.speed = 0f;
        legacyAnimation.CrossFade(clip, fade);
        currentClip = clip;
        lastMoving = false;
        locomotionInitialized = true;
    }
}
