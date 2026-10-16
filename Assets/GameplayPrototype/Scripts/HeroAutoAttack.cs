using UnityEngine;

/// <summary>Fires a simple projectile at the closest in-range Goblin while the hero is idle.</summary>
public sealed class HeroAutoAttack : MonoBehaviour
{
    [Min(.1f)] public float attackRange = 6f;
    [Min(1)] public int damage = 10;
    [Min(.1f)] public float projectileSpeed = 12f;
    [Min(.1f)] public float attackInterval = 1.1f;
    [Min(.05f)] public float targetSearchInterval = .2f;
    [Min(.05f)] public float projectileSize = .2f;

    HeroLaneController movement;
    float nextAttackTime;
    float nextTargetSearchTime;
    LaneUnitCombat cachedTarget;
    UnitFaction faction;
    HeroProgression progression;
    FireballAbility fireball;

    void Awake()
    {
        movement = GetComponent<HeroLaneController>();
        faction = GetComponent<UnitFaction>();
        progression = GetComponent<HeroProgression>();
        fireball = GetComponent<FireballAbility>();
    }

    void Update()
    {
        if (!movement || movement.IsMoving) return;
        TryAttackInRange(false);
    }

    public bool TryAttackInRange(bool forceTargetSearch = true)
    {
        if (!isActiveAndEnabled || Time.time < nextAttackTime) return false;
        if (!progression) progression = GetComponent<HeroProgression>();
        if (!fireball) fireball = GetComponent<FireballAbility>();
        if (fireball && fireball.IsTargeting) return false;
        if (HealingWell.Current && HealingWell.Current.Contains(transform)) return false;
        bool invalidTarget = !cachedTarget || cachedTarget.IsDead || !cachedTarget.Faction ||
            cachedTarget.Faction.team == (faction ? faction.team : UnitTeam.Friendly) ||
            (cachedTarget.transform.position - transform.position).sqrMagnitude > attackRange * attackRange;
        if (invalidTarget && (forceTargetSearch || Time.time >= nextTargetSearchTime))
        {
            cachedTarget = FindClosestTarget();
            nextTargetSearchTime = Time.time + targetSearchInterval;
        }
        LaneUnitCombat target = cachedTarget;
        if (!target || target.IsDead || !target.Health || !target.Faction ||
            target.Faction.team == (faction ? faction.team : UnitTeam.Friendly) ||
            (target.transform.position - transform.position).sqrMagnitude > attackRange * attackRange) return false;
        nextAttackTime = Time.time + attackInterval;
        if (progression) progression.RecordCombatAction();
        Vector3 launch = transform.position + Vector3.up * .8f;
        CombatProjectile.Launch(launch, target.transform.position + Vector3.up * .45f,
            projectileSpeed, projectileSize, damage, target.Health, 0f,
            faction ? faction.team : UnitTeam.Friendly, movement.lane, new Color(1f, .38f, .06f));
        return true;
    }

    LaneUnitCombat FindClosestTarget()
    {
        if (!movement || !movement.lane) return null;
        return UnitCombatRegistry.FindNearestOpponent(
            faction ? faction.team : UnitTeam.Friendly, movement.lane, transform.position, attackRange);
    }
}
