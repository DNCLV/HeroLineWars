using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Battlemage slot-two sweep. The visual and damage share one clock.</summary>
public sealed class FireSlashAbility : MonoBehaviour
{
    [Header("Sweep footprint")]
    [Min(.1f)] public float slashStartOffset = .25f;
    [Min(.5f)] public float slashTravelDistance = 8f;
    [Min(.5f)] public float slashWidth = 7.5f;
    [Min(1)] public int baseDamage = 62;
    [Min(.1f)] public float cooldown = 12f;
    [Min(.1f)] public float aiTargetingRange = 9f;

    [Header("Fire Slash presentation")]
    [Min(.01f)] public float castBuildupDuration = .12f;
    [Min(.05f)] public float slashTravelDuration = .50f;
    [Min(0f)] public float endpointHoldDuration = .04f;
    [Min(.01f)] public float slashFadeOutDuration = .18f;
    [Header("Temporary play-mode preview")]
    [Tooltip("Fire Slash only. 1 = 0.82 seconds total; 2 = 1.64 seconds. Cooldown is unchanged.")]
    [Min(.25f)] public float debugDurationMultiplier = 1f;

    readonly List<LaneUnitCombat> targets = new List<LaneUnitCombat>(64);
    HeroProgression progression;
    HeroAbilityLoadout loadout;
    HeroLaneController movement;
    LaneGenerator lane;
    UnitFaction faction;
    float nextReadyTime;
    public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);

    void Awake()
    {
        progression = GetComponent<HeroProgression>();
        loadout = GetComponent<HeroAbilityLoadout>();
        movement = GetComponent<HeroLaneController>();
        faction = GetComponent<UnitFaction>();
        lane = movement ? movement.lane : null;
    }

    public bool TryCastFacing()
    {
        if (!CanCast()) return false;
        return Cast(Vector3.ProjectOnPlane(transform.forward, lane.transform.up));
    }

    public bool TryCastAt(Vector3 point)
    {
        if (!CanCast()) return false;
        Vector3 direction = Vector3.ProjectOnPlane(point - transform.position, lane.transform.up);
        if (direction.sqrMagnitude < .01f || direction.sqrMagnitude > aiTargetingRange * aiTargetingRange) return false;
        return Cast(direction);
    }

    bool Cast(Vector3 direction)
    {
        if (direction.sqrMagnitude < .01f) return false;
        HlwCombatVfxCatalog catalog = HlwCombatVfxCatalog.Current;
        if (!catalog || !catalog.fireSlash) { Debug.LogError("Fire Slash prefab missing from VFX catalog.", this); return false; }
        direction.Normalize();
        Vector3 castOrigin = transform.position;
        Vector3 laneUp = lane.transform.up.normalized;
        float worldWidth = Mathf.Min(Mathf.Max(.5f, slashWidth), Mathf.Max(.5f, lane.WidthWorld - 1f));
        Vector3 slashOrigin = castOrigin + direction * slashStartOffset + laneUp * .12f;
        GameObject effect = Instantiate(catalog.fireSlash, slashOrigin, Quaternion.LookRotation(laneUp, direction));
        BattlemageFireSlashV2Visual visual = effect.GetComponent<BattlemageFireSlashV2Visual>();
        if (!visual || !visual.slashMaterial)
        {
            Debug.LogError("Fire Slash prefab needs its textured material and visual component.", effect);
            Destroy(effect);
            return false;
        }
        visual.Initialize(worldWidth, slashTravelDistance, slashOrigin, direction);
        nextReadyTime = Time.time + cooldown;
        if (progression && progression.IsPlayerHero)
        {
            FireballAbility fireball = GetComponent<FireballAbility>();
            if (fireball) fireball.CancelTargeting();
        }
        else transform.rotation = Quaternion.LookRotation(direction, laneUp);
        if (progression) progression.RecordCombatAction();
        StartCoroutine(ResolveSlash(castOrigin, direction, worldWidth, visual,
            MatchLoopController.Current ? MatchLoopController.Current.MatchNumber : 0));
        return true;
    }

    bool CanCast()
    {
        if (!enabled || Time.time < nextReadyTime || !loadout || !loadout.IsUnlocked(2)) return false;
        if (!lane && movement) lane = movement.lane;
        if (!lane) return false;
        MatchLoopController match = MatchLoopController.Current;
        if (match && !match.IsRunning) return false;
        return !(progression && progression.IsPlayerHero && HealingWell.Current && HealingWell.Current.Contains(transform));
    }

    IEnumerator ResolveSlash(Vector3 castOrigin, Vector3 direction, float worldWidth,
        BattlemageFireSlashV2Visual visual, int matchNumber)
    {
        UnitTeam team = faction ? faction.team : UnitTeam.Friendly;
        UnitCombatRegistry.CopyOpponents(targets, team, lane);
        bool[] hit = new bool[targets.Count];
        int hits = 0;
        int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage *
            (progression ? progression.ActiveAbilityDamageMultiplier : 1f) * loadout.DamageMultiplier(2)));
        MatchLoopController match = MatchLoopController.Current;
        if (match && progression && progression.IsPlayerHero) damage = match.ScalePlayerAreaDamage(damage);
        float scale = Mathf.Max(.25f, debugDurationMultiplier);
        float build = castBuildupDuration * scale;
        float travel = slashTravelDuration * scale;
        float hold = endpointHoldDuration * scale;
        float fade = slashFadeOutDuration * scale;
        float elapsed = 0f;
        while (elapsed < build)
        {
            if (!StillActive(matchNumber, visual)) yield break;
            elapsed = Mathf.Min(build, elapsed + Time.deltaTime);
            float t = elapsed / build;
            visual.RenderAt(0f, Mathf.Lerp(.35f, 1f, t), Mathf.Lerp(.25f, 1f, t));
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < travel)
        {
            if (!StillActive(matchNumber, visual)) yield break;
            elapsed = Mathf.Min(travel, elapsed + Time.deltaTime);
            float t = elapsed / travel;
            float size = Mathf.Lerp(.25f, 1f, Mathf.Clamp01(t / .25f));
            visual.RenderAt(t, 1f, size);
            hits += DamageAtFront(castOrigin, direction, hit, damage, team, visual);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < hold + fade)
        {
            if (!StillActive(matchNumber, visual)) yield break;
            elapsed += Time.deltaTime;
            float opacity = elapsed <= hold ? 1f : 1f - Mathf.Clamp01((elapsed - hold) / fade);
            visual.RenderAt(1f, opacity);
            yield return null;
        }
        if (visual) Destroy(visual.gameObject);
        EnemyHeroAiController ai = GetComponent<EnemyHeroAiController>();
        if (ai) ai.RecordAbilityHits(hits);
    }

    bool StillActive(int matchNumber, BattlemageFireSlashV2Visual visual)
    {
        MatchLoopController match = MatchLoopController.Current;
        if (visual && (!match || (match.IsRunning && match.MatchNumber == matchNumber)) &&
            !(progression && progression.Health && progression.Health.IsDead)) return true;
        if (visual) Destroy(visual.gameObject);
        return false;
    }

    int DamageAtFront(Vector3 origin, Vector3 direction,
        bool[] hit, int damage, UnitTeam team, BattlemageFireSlashV2Visual visual)
    {
        float front = slashStartOffset + visual.FrontDistance;
        Vector3 lateralAxis = Vector3.Cross(direction, lane.transform.up);
        int count = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            if (hit[i]) continue;
            LaneUnitCombat target = targets[i];
            if (!target || target.IsDead || !target.Health) { hit[i] = true; continue; }
            Vector3 delta = Vector3.ProjectOnPlane(target.transform.position - origin, lane.transform.up);
            float forward = Vector3.Dot(delta, direction);
            float lateral = Vector3.Dot(delta, lateralAxis);
            float radius = target.CollisionRadius;
            if (forward + radius < slashStartOffset || forward - radius > front) continue;
            if (!visual.TouchesVisibleFire(forward - slashStartOffset, lateral, radius)) continue;
            target.Health.TakeDamage(damage, team, transform);
            hit[i] = true;
            count++;
        }
        return count;
    }
}
