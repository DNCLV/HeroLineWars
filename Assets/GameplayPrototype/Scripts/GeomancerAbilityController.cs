using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Geomancer passive and two directional active abilities.</summary>
public sealed class GeomancerAbilityController : MonoBehaviour
{
    [Header("Seismic Fury")]
    [Min(1)] public int attacksToActivate = 5;
    [Min(.1f)] public float furyDuration = 5f;
    [Range(0f, 2f)] public float furyAttackSpeedBonus = .35f;
    [Range(0f, 1f)] public float splashDamageFraction = .5f;
    [Min(.1f)] public float splashRadius = 2.5f;

    [Header("Faultbreaker")]
    [Min(.1f)] public float faultbreakerCooldown = 5f;
    [Min(0f)] public float faultbreakerBaseDamage = 54.2f;
    [Min(.1f)] public float faultbreakerRange = 8f;
    [Min(.1f)] public float faultbreakerWidth = 2.5f;
    [Min(0f)] public float faultbreakerKnockback = 2.5f;

    [Header("Earth Spikes")]
    [Min(.1f)] public float earthSpikesCooldown = 20f;
    [Min(0f)] public float earthSpikesBaseDamage = 113.5f;
    [Min(.1f)] public float earthSpikesRange = 9f;
    [Range(10f, 170f)] public float earthSpikesAngle = 90f;
    [Min(.1f)] public float knockUpDuration = .8f;

    readonly List<LaneUnitCombat> targets = new List<LaneUnitCombat>(64);
    HeroProgression progression;
    HeroLaneController movement;
    HeroAbilityLoadout loadout;
    HeroAnimationDriver animationDriver;
    UnitFaction faction;
    LaneGenerator lane;
    int successfulAttacks;
    float furyEnd;
    float faultbreakerReady;
    float earthSpikesReady;
    HeroAbilityKind targeting;

    public int FuryProgress => IsFuryActive ? attacksToActivate : successfulAttacks;
    public bool IsFuryActive => Time.time < furyEnd;
    public float FuryRemaining => Mathf.Max(0f, furyEnd - Time.time);
    public float AttackSpeedMultiplier => IsFuryActive ? 1f + furyAttackSpeedBonus : 1f;
    public float FaultbreakerRemaining => Mathf.Max(0f, faultbreakerReady - Time.time);
    public float EarthSpikesRemaining => Mathf.Max(0f, earthSpikesReady - Time.time);
    public bool IsTargeting => targeting != HeroAbilityKind.None;

    void Awake()
    {
        progression = GetComponent<HeroProgression>();
        movement = GetComponent<HeroLaneController>();
        loadout = GetComponent<HeroAbilityLoadout>();
        animationDriver = GetComponent<HeroAnimationDriver>();
        faction = GetComponent<UnitFaction>();
        lane = movement ? movement.lane : FindAnyObjectByType<LaneGenerator>();
    }

    void OnEnable() => GameplayInputRouter.TargetingTap += OnTargetingTap;
    void OnDisable() => GameplayInputRouter.TargetingTap -= OnTargetingTap;

    void Start()
    {
        if (!progression) progression = GetComponent<HeroProgression>();
        if (!loadout) loadout = GetComponent<HeroAbilityLoadout>();
        if (!animationDriver) animationDriver = GetComponent<HeroAnimationDriver>();
        if (!movement) movement = GetComponent<HeroLaneController>();
        if (!lane) lane = movement ? movement.lane : FindAnyObjectByType<LaneGenerator>();
    }

    void Update()
    {
        if (furyEnd > 0f && Time.time >= furyEnd)
        {
            furyEnd = 0f;
            successfulAttacks = 0;
        }
    }

    public bool BeginFaultbreaker()
    {
        if (Time.time < faultbreakerReady || !CanCast(1)) return false;
        targeting = HeroAbilityKind.Faultbreaker;
        return true;
    }

    public bool BeginEarthSpikes()
    {
        if (Time.time < earthSpikesReady || !CanCast(2)) return false;
        targeting = HeroAbilityKind.EarthSpikes;
        return true;
    }

    public void CancelTargeting() => targeting = HeroAbilityKind.None;

    public bool TryCastAiAt(Vector3 point, int nearbyTargetCount)
    {
        if (!enabled || !lane || !loadout) return false;
        Vector3 direction = Vector3.ProjectOnPlane(point - transform.position, lane.transform.up);
        float distance = direction.magnitude;
        if (distance < .1f) return false;
        direction /= distance;
        // Reserve the large cone for groups; a single threat gets the narrower strike.
        if (nearbyTargetCount >= 3 && distance <= earthSpikesRange &&
            Time.time >= earthSpikesReady && loadout.IsUnlocked(2))
        {
            earthSpikesReady = Time.time + earthSpikesCooldown;
            transform.rotation = Quaternion.LookRotation(direction, lane.transform.up);
            StartCoroutine(ResolveEarthSpikes(direction, .25f));
            if (progression) progression.RecordCombatAction();
            return true;
        }
        if (distance > faultbreakerRange || Time.time < faultbreakerReady || !loadout.IsUnlocked(1)) return false;
        faultbreakerReady = Time.time + faultbreakerCooldown;
        transform.rotation = Quaternion.LookRotation(direction, lane.transform.up);
        StartCoroutine(ResolveFaultbreaker(direction, .2f));
        if (progression) progression.RecordCombatAction();
        return true;
    }

    public void RecordAiBasicAttack(LaneUnitCombat primary, int damage)
    {
        if (!enabled || !primary) return;
        if (IsFuryActive)
            SplashAround(primary, Mathf.Max(1, Mathf.RoundToInt(damage * splashDamageFraction)),
                faction ? faction.team : UnitTeam.Enemy);
        else if (++successfulAttacks >= attacksToActivate)
            furyEnd = Time.time + furyDuration;
    }

    public void ExecuteBasicAttack(LaneUnitCombat target, int damage)
    {
        if (!target || target.IsDead) return;
        if (movement) movement.CancelMovement();
        if (!animationDriver) animationDriver = GetComponent<HeroAnimationDriver>();
        float duration = animationDriver ? animationDriver.PlayBasicAttack() : .42f;
        StartCoroutine(ResolveBasicAttack(target, damage, Mathf.Min(.32f, duration * .45f)));
    }

    IEnumerator ResolveBasicAttack(LaneUnitCombat target, int damage, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!target || target.IsDead || !target.Health || !movement) yield break;
        Vector3 delta = target.transform.position - transform.position;
        if (delta.sqrMagnitude > GetComponent<HeroAutoAttack>().attackRange * GetComponent<HeroAutoAttack>().attackRange) yield break;
        UnitTeam team = faction ? faction.team : UnitTeam.Friendly;
        target.Health.TakeDamage(damage, team, transform);
        GameplayVisuals.Impact(target.transform.position + Vector3.up * .25f,
            IsFuryActive ? .52f : .3f, IsFuryActive ? new Color(.18f, .72f, 1f) : new Color(.5f, .38f, .24f), .16f);
        if (IsFuryActive)
            SplashAround(target, Mathf.Max(1, Mathf.RoundToInt(damage * splashDamageFraction)), team);
        else
        {
            successfulAttacks++;
            if (successfulAttacks >= attacksToActivate)
                furyEnd = Time.time + furyDuration;
        }
    }

    void OnTargetingTap(Vector2 screenPosition)
    {
        if (targeting == HeroAbilityKind.None || !lane) return;
        GameplayInputRouter.ConsumeCurrentTap();
        if (!GameplayInputRouter.TryGetLanePoint(screenPosition, lane, out Vector3 point)) return;
        Vector3 direction = Vector3.ProjectOnPlane(point - transform.position, lane.transform.up).normalized;
        if (direction.sqrMagnitude < .1f) return;
        HeroAbilityKind cast = targeting;
        targeting = HeroAbilityKind.None;
        if (movement) movement.CancelMovement();
        transform.rotation = Quaternion.LookRotation(direction, lane.transform.up);
        if (cast == HeroAbilityKind.Faultbreaker)
        {
            faultbreakerReady = Time.time + faultbreakerCooldown;
            float duration = animationDriver ? animationDriver.PlayFaultbreaker() : .7f;
            StartCoroutine(ResolveFaultbreaker(direction, Mathf.Min(.48f, duration * .55f)));
        }
        else
        {
            earthSpikesReady = Time.time + earthSpikesCooldown;
            float duration = animationDriver ? animationDriver.PlayEarthSpikes() : .9f;
            StartCoroutine(ResolveEarthSpikes(direction, Mathf.Min(.62f, duration * .58f)));
        }
        if (progression) progression.RecordCombatAction();
    }

    IEnumerator ResolveFaultbreaker(Vector3 direction, float delay)
    {
        yield return new WaitForSeconds(delay);
        int hits = 0;
        int damage = ScaledDamage(faultbreakerBaseDamage, 1);
        UnitTeam team = faction ? faction.team : UnitTeam.Friendly;
        UnitCombatRegistry.CopyOpponents(targets, team, lane);
        for (int i = 0; i < targets.Count; i++)
        {
            LaneUnitCombat target = targets[i];
            if (movement && target.assignedLane != movement.allowedLaneIndex) continue;
            Vector3 offset = Vector3.ProjectOnPlane(target.transform.position - transform.position, lane.transform.up);
            float forward = Vector3.Dot(offset, direction);
            float lateral = (offset - direction * forward).magnitude;
            if (forward < 0f || forward > faultbreakerRange || lateral > faultbreakerWidth * .5f) continue;
            target.Health.TakeDamage(damage, team, transform);
            hits++;
            target.ApplyKnockback(direction, faultbreakerKnockback);
            GameplayVisuals.Impact(target.transform.position + Vector3.up * .2f, .6f, new Color(.2f, .7f, 1f), .24f);
        }
        for (float d = 1f; d <= faultbreakerRange; d += 1.25f)
            GameplayVisuals.Impact(transform.position + direction * d + lane.transform.up * .05f,
                .42f, new Color(.18f, .54f, .78f), .2f);
        EnemyHeroAiController ai = GetComponent<EnemyHeroAiController>();
        if (ai) ai.RecordAbilityHits(hits);
    }

    IEnumerator ResolveEarthSpikes(Vector3 direction, float delay)
    {
        yield return new WaitForSeconds(delay);
        int hits = 0;
        int damage = ScaledDamage(earthSpikesBaseDamage, 2);
        UnitTeam team = faction ? faction.team : UnitTeam.Friendly;
        float minimumDot = Mathf.Cos(earthSpikesAngle * .5f * Mathf.Deg2Rad);
        UnitCombatRegistry.CopyOpponents(targets, team, lane);
        for (int i = 0; i < targets.Count; i++)
        {
            LaneUnitCombat target = targets[i];
            if (movement && target.assignedLane != movement.allowedLaneIndex) continue;
            Vector3 offset = Vector3.ProjectOnPlane(target.transform.position - transform.position, lane.transform.up);
            float distance = offset.magnitude;
            if (distance <= .01f || distance > earthSpikesRange || Vector3.Dot(offset / distance, direction) < minimumDot) continue;
            target.Health.TakeDamage(damage, team, transform);
            hits++;
            target.ApplyKnockUp(knockUpDuration, target.Faction && target.Faction.archetype == UnitArchetype.UndeadDragon ? .65f : 1.25f);
            GameplayVisuals.Impact(target.transform.position, .85f, new Color(.22f, .72f, 1f), .35f);
        }
        for (float d = 1.5f; d <= earthSpikesRange; d += 1.5f)
            GameplayVisuals.Impact(transform.position + direction * d, .7f + d * .04f, new Color(.2f, .62f, .9f), .3f);
        EnemyHeroAiController ai = GetComponent<EnemyHeroAiController>();
        if (ai) ai.RecordAbilityHits(hits);
    }

    int ScaledDamage(float baseDamage, int slot)
    {
        int raw = Mathf.Max(1, Mathf.RoundToInt(
            baseDamage * (progression ? progression.ActiveAbilityDamageMultiplier : 1f) *
            (loadout ? loadout.DamageMultiplier(slot) : 1f)));
        MatchLoopController match = MatchLoopController.Current;
        return match && progression && progression.IsPlayerHero ? match.ScalePlayerAreaDamage(raw) : raw;
    }

    bool CanCast(int slot) => !IsTargeting && loadout && loadout.IsUnlocked(slot) &&
        !(HealingWell.Current && HealingWell.Current.Contains(transform));

    void SplashAround(LaneUnitCombat primary, int damage, UnitTeam team)
    {
        UnitCombatRegistry.CopyOpponents(targets, team, lane);
        float radiusSquared = splashRadius * splashRadius;
        for (int i = 0; i < targets.Count; i++)
        {
            LaneUnitCombat secondary = targets[i];
            if (!secondary || secondary == primary || secondary.IsDead) continue;
            if (movement && secondary.assignedLane != movement.allowedLaneIndex) continue;
            if ((secondary.transform.position - primary.transform.position).sqrMagnitude > radiusSquared) continue;
            secondary.Health.TakeDamage(damage, team, transform);
            GameplayVisuals.Impact(secondary.transform.position + Vector3.up * .2f, .28f, new Color(.2f, .68f, 1f), .12f);
        }
    }
}
