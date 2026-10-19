using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared straight-lane movement, target engagement, melee attacks, and test stats.</summary>
public sealed class LaneUnitCombat : MonoBehaviour
{
    public LaneGenerator lane;
    [Min(1)] public int maximumHitPoints = 100;
    [Min(1)] public int attackDamage = 10;
    [Min(.1f)] public float attackInterval = 1f;
    [Min(.1f)] public float attackRange = 1.5f;
    [Min(.1f)] public float movementSpeed = 2f;
    [Min(.1f)] public float engagementRange = 4f;
    [Min(1)] public int targetsPerAttack = 1;
    [Min(0f)] public float multiTargetRadius = 0f;
    [Min(.1f)] public float targetRefreshInterval = .2f;
    [Min(0f)] public float baseStoppingInset = .8f;
    [Min(.1f)] public float modelScale = 1f;
    [Min(0f)] public float collisionRadiusOverride;
    public float modelYawOffset;
    [Range(0, 1)] public int assignedLane;
    [Tooltip("Deterministic lateral formation offset inside the assigned physical road.")]
    public float laneOffsetX;
    [Tooltip("Stationary defenders can fight but never advance toward a base.")]
    public bool stationaryDefender;
    [Min(0f)] public float defenderLeashRadius;
    public bool enableHeroCombatDiagnostics;
    [Tooltip("Disable for the player hero, whose existing HeroAutoAttack owns attacks.")]
    public bool performAttacks = true;
    public bool Retreating { get; private set; }

    public UnitFaction Faction { get; private set; }
    public GoblinHealth Health { get; private set; }
    public bool IsDead => !Health || Health.IsDead;
    public float CollisionRadius { get; private set; } = .45f;

    LaneUnitCombat target;
    Transform visualRoot;
    Transform ring;
    float nextTargetSearch;
    float nextAttack;
    bool scaled;
    bool reachedBase;
    float crowdControlEnd;
    float knockUpStart;
    float knockUpDuration;
    float knockUpHeight;
    Vector3 defenderHome;
    float nextCombatDiagnostic;
    int attackAttempts;
    int successfulHits;
    int damageDealt;
    int damageReceived;
    readonly List<LaneUnitCombat> nearbyUnits = new List<LaneUnitCombat>(24);

    void Awake()
    {
        Faction = GetComponent<UnitFaction>();
        Health = GetComponent<GoblinHealth>();
        if (!lane) lane = FindAnyObjectByType<LaneGenerator>();
        if (transform.childCount > 0) visualRoot = transform.GetChild(0);
        if (Health) Health.ConfigureHitPoints(maximumHitPoints);
    }

    void OnEnable() { UnitCombatRegistry.Register(this); }
    void OnDisable() { UnitCombatRegistry.Unregister(this); }

    void Start()
    {
        if (visualRoot && !scaled)
        {
            visualRoot.localScale *= modelScale;
            scaled = true;
        }
        Collider bodyCollider = GetComponent<Collider>();
        if (collisionRadiusOverride > 0f) CollisionRadius = collisionRadiusOverride;
        else if (bodyCollider && !bodyCollider.isTrigger)
            CollisionRadius = Mathf.Max(.3f, Mathf.Max(bodyCollider.bounds.extents.x,
                bodyCollider.bounds.extents.z));
        else if (visualRoot && Faction)
            CollisionRadius = Mathf.Max(.3f,
                GameplayVisuals.GetGroundFootprintRadius(visualRoot, Faction.archetype));
        if (Faction && Faction.showFactionRing) CreateFactionRing();
    }

    public void Configure(LaneGenerator laneGenerator, UnitTeam team, UnitArchetype archetype)
    {
        UnitCombatRegistry.ReleaseAttackSlot(this);
        target = null;
        nextTargetSearch = nextAttack = 0f;
        reachedBase = false;
        crowdControlEnd = knockUpDuration = 0f;
        lane = laneGenerator;
        defenderHome = transform.position;
        nextCombatDiagnostic = Time.time + 5f;
        attackAttempts = successfulHits = damageDealt = damageReceived = 0;
        if (!Faction) Faction = GetComponent<UnitFaction>() ?? gameObject.AddComponent<UnitFaction>();
        Faction.Set(team, archetype);
        if (!Health) Health = GetComponent<GoblinHealth>() ?? gameObject.AddComponent<GoblinHealth>();
        Health.ConfigureHitPoints(maximumHitPoints);
    }

    public void ConfigureLane(int laneIndex) => assignedLane = Mathf.Clamp(laneIndex, 0, Mathf.Max(1, lane ? lane.battleLaneCount - 1 : 1));

    public void ConfigureLaneOffset(float offset)
    {
        float inset = lane ? Mathf.Max(.65f, lane.cellSize * .65f) : .65f;
        float maximum = lane ? Mathf.Max(0f, lane.RoadWidth * .5f - inset) : 0f;
        laneOffsetX = Mathf.Clamp(offset, -maximum, maximum);
    }

    void Update()
    {
        if (!lane || !Faction || IsDead) return;
        if (Retreating)
        {
            UnitCombatRegistry.ReleaseAttackSlot(this);
            target = null;
            ReturnToDefenderHome();
            return;
        }
        LogHeroCombatIfDue();
        if (Time.time < crowdControlEnd)
        {
            if (knockUpDuration > 0f)
            {
                float t = Mathf.Clamp01((Time.time - knockUpStart) / knockUpDuration);
                Vector3 local = lane.transform.InverseTransformPoint(transform.position);
                local.y = Mathf.Sin(t * Mathf.PI) * knockUpHeight;
                transform.position = lane.transform.TransformPoint(local);
            }
            return;
        }
        if (knockUpDuration > 0f)
        {
            Vector3 grounded = lane.transform.InverseTransformPoint(transform.position);
            grounded.y = 0f;
            transform.position = lane.transform.TransformPoint(grounded);
            knockUpDuration = 0f;
        }
        bool separatedTarget = target && target.lane == lane && target.assignedLane != assignedLane;
        bool escapedDefender = stationaryDefender && target && defenderLeashRadius > 0f &&
            (target.transform.position - defenderHome).sqrMagnitude >
            (engagementRange + defenderLeashRadius) * (engagementRange + defenderLeashRadius);
        if (separatedTarget || escapedDefender)
        {
            UnitCombatRegistry.ReleaseAttackSlot(this);
            target = null;
        }
        if (performAttacks && (!target || target.IsDead || !target.Faction || target.Faction.team == Faction.team || target.lane != lane || separatedTarget) &&
            Time.time >= nextTargetSearch)
        {
            LaneUnitCombat nextTarget = UnitCombatRegistry.FindNearestOpponent(Faction.team, lane,
                transform.position, engagementRange);
            if (nextTarget != target) UnitCombatRegistry.ReleaseAttackSlot(this);
            target = nextTarget;
            nextTargetSearch = Time.time + targetRefreshInterval;
        }

        if (performAttacks && target && !target.IsDead && target.Faction && target.Health && target.Faction.team != Faction.team)
        {
            EngageTarget();
            return;
        }
        if (stationaryDefender) ReturnToDefenderHome();
        else AdvanceToOpposingBase();
    }

    void LateUpdate()
    {
        if (!lane || !Faction || IsDead || Faction.archetype == UnitArchetype.Battlemage) return;
        UnitCombatRegistry.CopyNearby(nearbyUnits, this);
        Vector3 local = lane.transform.InverseTransformPoint(transform.position);
        for (int pass = 0; pass < 2; pass++)
        for (int i = 0; i < nearbyUnits.Count; i++)
        {
            LaneUnitCombat other = nearbyUnits[i];
            if (!other || other.IsDead || other.lane != lane || other.assignedLane != assignedLane) continue;
            Vector3 otherLocal = lane.transform.InverseTransformPoint(other.transform.position);
            Vector2 delta = new Vector2(local.x - otherLocal.x, local.z - otherLocal.z);
            float minimum = CollisionRadius + other.CollisionRadius + .04f;
            if (delta.sqrMagnitude >= minimum * minimum) continue;
            Vector2 away = delta.sqrMagnitude > .0001f ? delta.normalized :
                (GetHashCode() < other.GetHashCode() ? Vector2.left : Vector2.right);
            float overlap = minimum - Mathf.Sqrt(delta.sqrMagnitude);
            Vector3 proposed = local + new Vector3(away.x, 0f, away.y) * (overlap * .55f);
            if (!lane.IsPointInRoad(assignedLane, proposed.x, proposed.z, CollisionRadius * .5f))
            {
                float splitDirection = GetHashCode() < other.GetHashCode() ? -1f : 1f;
                proposed = local + new Vector3(0f, 0f, splitDirection * overlap * .55f);
                if (!lane.IsPointInRoad(assignedLane, proposed.x, proposed.z, CollisionRadius * .5f))
                    proposed = local - new Vector3(0f, 0f, splitDirection * overlap * .55f);
            }
            if (lane.IsPointInRoad(assignedLane, proposed.x, proposed.z, CollisionRadius * .5f))
                local = proposed;
        }
        local.y = lane.transform.InverseTransformPoint(transform.position).y;
        transform.position = lane.transform.TransformPoint(local);
    }

    public void RecordIncomingDamage(int amount) { damageReceived += Mathf.Max(0, amount); }

    public void SetRetreating(bool retreating)
    {
        if (Retreating == retreating) return;
        Retreating = retreating;
        target = null;
        UnitCombatRegistry.ReleaseAttackSlot(this);
        nextTargetSearch = 0f;
    }

    public void SetPreferredTarget(LaneUnitCombat preferred)
    {
        if (!preferred || preferred.IsDead || !preferred.Faction || !Faction ||
            preferred.Faction.team == Faction.team || preferred.lane != lane ||
            preferred.assignedLane != assignedLane || Retreating) return;
        if ((preferred.transform.position - transform.position).sqrMagnitude >
            engagementRange * engagementRange) return;
        if (target == preferred) return;
        UnitCombatRegistry.ReleaseAttackSlot(this);
        target = preferred;
        nextTargetSearch = Time.time + targetRefreshInterval;
    }

    void LogHeroCombatIfDue()
    {
        if (!Debug.isDebugBuild || !enableHeroCombatDiagnostics || Time.time < nextCombatDiagnostic) return;
        nextCombatDiagnostic = Time.time + 5f;
        float distance = target ? Vector3.Distance(transform.position, target.transform.position) : -1f;
        Debug.Log($"[Enemy Hero Combat] target={(target ? target.name : "none")}, distance={distance:F1}, " +
            $"range={attackRange:F1}, search={engagementRange:F1}, attacks={attackAttempts}, " +
            $"hits={successfulHits}, dealt={damageDealt}, received={damageReceived}, " +
            $"HP={(Health ? Health.CurrentHitPoints : 0)}.", this);
    }

    public void ApplyKnockback(Vector3 worldDirection, float distance)
    {
        if (!lane || distance <= 0f || IsDead) return;
        Vector3 local = lane.transform.InverseTransformPoint(transform.position);
        Vector3 localDirection = lane.transform.InverseTransformDirection(worldDirection);
        Vector2 direction = new Vector2(localDirection.x, localDirection.z).normalized;
        float step = Mathf.Max(.1f, lane.cellSize * .2f);
        Vector3 lastValid = local;
        for (float travelled = step; travelled <= distance + .001f; travelled += step)
        {
            Vector3 candidate = local + new Vector3(direction.x, 0f, direction.y) * Mathf.Min(distance, travelled);
            if (!lane.IsPointInRoad(assignedLane, candidate.x, candidate.z, .35f)) break;
            lastValid = candidate;
        }
        lastValid.y = 0f;
        transform.position = lane.transform.TransformPoint(lastValid);
        target = null;
        UnitCombatRegistry.ReleaseAttackSlot(this);
        nextTargetSearch = 0f;
    }

    public void ApplyKnockUp(float duration, float height = 1.15f)
    {
        if (IsDead) return;
        knockUpStart = Time.time;
        knockUpDuration = Mathf.Max(.05f, duration);
        knockUpHeight = Mathf.Max(.1f, height);
        crowdControlEnd = Mathf.Max(crowdControlEnd, Time.time + knockUpDuration);
        target = null;
        UnitCombatRegistry.ReleaseAttackSlot(this);
    }

    void EngageTarget()
    {
        Vector3 local = lane.transform.InverseTransformPoint(transform.position);
        Vector3 targetLocal = lane.transform.InverseTransformPoint(target.transform.position);
        bool meleeMob = Faction.archetype != UnitArchetype.Battlemage && attackRange <= 2f;
        if (meleeMob)
        {
            if (!UnitCombatRegistry.TryGetAttackSlot(this, target, out Vector3 slotWorld))
            {
                float approach = Faction.team == UnitTeam.Friendly ? -1f : 1f;
                Vector3 queue = targetLocal + new Vector3(0f, 0f,
                    approach * (target.CollisionRadius + CollisionRadius + 1f));
                MoveTowardLocal(local, queue, .35f);
                return;
            }
            Vector3 slot = lane.transform.InverseTransformPoint(slotWorld);
            if (Vector2.Distance(new Vector2(local.x, local.z), new Vector2(slot.x, slot.z)) > .3f)
            {
                MoveTowardLocal(local, slot, .3f);
                return;
            }
        }
        Vector2 delta = new Vector2(targetLocal.x - local.x, targetLocal.z - local.z);
        float distance = delta.magnitude;
        Vector2 direction = distance > .001f ? delta / distance : Vector2.up;
        Face(direction);
        if (!meleeMob && distance > attackRange)
        {
            float step = Mathf.Min(movementSpeed * Time.deltaTime, distance - attackRange);
            Vector3 candidate = local + new Vector3(direction.x * step, 0f, direction.y * step);
            if (stationaryDefender &&
                (defenderLeashRadius <= 0f ||
                 (lane.transform.TransformPoint(candidate) - defenderHome).sqrMagnitude >
                 defenderLeashRadius * defenderLeashRadius)) return;
            if (!lane.IsPointInRoad(assignedLane, candidate.x, candidate.z, .3f))
            {
                if (!stationaryDefender) AdvanceToOpposingBase();
                return;
            }
            local = candidate;
            local.y = 0f;
            transform.position = lane.transform.TransformPoint(local);
            return;
        }
        if (Time.time < nextAttack) return;
        attackAttempts++;
        if (Faction.team == UnitTeam.Enemy && Faction.archetype == UnitArchetype.Battlemage && MatchLoopController.Current)
            MatchLoopController.Current.RecordEnemyHeroBasicAttempt();
        nextAttack = Time.time + attackInterval;
        int targetHpBeforeHit = target.Health.CurrentHitPoints;
        GameplayVisuals.Impact(target.transform.position + lane.transform.up * .22f,
            .26f, new Color(.9f, .78f, .48f), .12f);
        if (targetsPerAttack > 1 && multiTargetRadius > 0f)
            UnitCombatRegistry.DamageNearestOpponents(target.transform.position, multiTargetRadius,
                attackDamage, Faction.team, lane, targetsPerAttack, transform);
        else target.Health.TakeDamage(attackDamage, Faction.team, transform);
        successfulHits++;
        if (Faction.team == UnitTeam.Enemy && Faction.archetype == UnitArchetype.Battlemage)
        {
            GeomancerAbilityController geomancer = GetComponent<GeomancerAbilityController>();
            if (geomancer) geomancer.RecordAiBasicAttack(target, attackDamage);
        }
        damageDealt += Mathf.Min(attackDamage, targetHpBeforeHit);
        if (Faction.team == UnitTeam.Enemy && Faction.archetype == UnitArchetype.Battlemage && MatchLoopController.Current)
            MatchLoopController.Current.RecordEnemyHeroBasicHit();
    }

    void MoveTowardLocal(Vector3 current, Vector3 destination, float tolerance)
    {
        Vector2 delta = new Vector2(destination.x - current.x, destination.z - current.z);
        if (delta.sqrMagnitude <= tolerance * tolerance) return;
        float step = Mathf.Min(movementSpeed * Time.deltaTime, delta.magnitude - tolerance);
        Vector2 direction = delta.normalized;
        Vector3 candidate = current + new Vector3(direction.x * step, 0f, direction.y * step);
        if (!lane.IsPointInRoad(assignedLane, candidate.x, candidate.z, CollisionRadius * .5f)) return;
        candidate.y = 0f;
        transform.position = lane.transform.TransformPoint(candidate);
        Face(direction);
    }

    void ReturnToDefenderHome()
    {
        if (defenderLeashRadius <= 0f) return;
        Vector3 current = lane.transform.InverseTransformPoint(transform.position);
        Vector3 home = lane.transform.InverseTransformPoint(defenderHome);
        Vector2 delta = new Vector2(home.x - current.x, home.z - current.z);
        if (delta.sqrMagnitude < .01f) return;
        Vector2 direction = delta.normalized;
        float step = Mathf.Min(movementSpeed * Time.deltaTime, delta.magnitude);
        current.x += direction.x * step;
        current.z += direction.y * step;
        current.y = 0f;
        transform.position = lane.transform.TransformPoint(current);
        Face(direction);
    }

    void AdvanceToOpposingBase()
    {
        Vector3 local = lane.transform.InverseTransformPoint(transform.position);
        float direction = Faction.team == UnitTeam.Friendly ? 1f : -1f;
        float destinationZ = Faction.team == UnitTeam.Friendly
            ? lane.LengthWorld - baseStoppingInset : baseStoppingInset;
        float remaining = (destinationZ - local.z) * direction;
        if (remaining <= .02f)
        {
            if (reachedBase) return;
            reachedBase = true;
            if (Faction.team == UnitTeam.Friendly)
            {
                FriendlyUnitSender sender = GetComponentInParent<FriendlyUnitSender>();
                if (sender) sender.RecordFriendlyUnitReachedEnemyBase();
            }
            MatchLoopController match = MatchLoopController.Current;
            if (match) match.ReportBaseHit(Faction.team);
            Destroy(gameObject);
            return;
        }
        float step = Mathf.Min(remaining, movementSpeed * Time.deltaTime);
        float oldX = local.x;
        local.z += direction * step;
        // Classic lanes stay physically separate for their full length.
        float routeX = lane.RoadCenterX(assignedLane) + laneOffsetX;
        if (Mathf.Abs(local.x - routeX) > 1.5f)
            local.x = Mathf.MoveTowards(local.x, routeX, step * .25f);
        local.y = 0f;
        transform.position = lane.transform.TransformPoint(local);
        Face(new Vector2(routeX - oldX, direction * step).normalized);
    }

    void Face(Vector2 localDirection)
    {
        Vector3 world = lane.transform.TransformDirection(new Vector3(localDirection.x, 0f, localDirection.y));
        transform.rotation = Quaternion.LookRotation(world, lane.transform.up) *
                             Quaternion.Euler(0f, modelYawOffset, 0f);
    }

    void CreateFactionRing()
    {
        if (!Faction || ring) return;
        float radius = GameplayVisuals.GetGroundFootprintRadius(visualRoot, Faction.archetype);
        ring = GameplayVisuals.CreateGroundRing("FactionRing", .035f);
        ring.SetParent(transform, false);
        ring.localPosition = new Vector3(0f, .025f, 0f);
        ring.localScale = new Vector3(radius, 1f, radius);
        ring.GetComponent<Renderer>().sharedMaterial = UnitFaction.RingMaterial(Faction.team);
    }
}
