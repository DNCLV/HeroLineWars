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
    public float modelYawOffset;
    [Range(0, 1)] public int assignedLane;
    [Tooltip("Stationary defenders can fight but never advance toward a base.")]
    public bool stationaryDefender;
    [Tooltip("Disable for the player hero, whose existing HeroAutoAttack owns attacks.")]
    public bool performAttacks = true;

    public UnitFaction Faction { get; private set; }
    public GoblinHealth Health { get; private set; }
    public bool IsDead => !Health || Health.IsDead;

    LaneUnitCombat target;
    Transform visualRoot;
    Transform ring;
    float nextTargetSearch;
    float nextAttack;
    bool scaled;
    bool reachedBase;

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
        if (Faction && Faction.showFactionRing) CreateFactionRing();
    }

    public void Configure(LaneGenerator laneGenerator, UnitTeam team, UnitArchetype archetype)
    {
        lane = laneGenerator;
        if (!Faction) Faction = GetComponent<UnitFaction>() ?? gameObject.AddComponent<UnitFaction>();
        Faction.Set(team, archetype);
        if (!Health) Health = GetComponent<GoblinHealth>() ?? gameObject.AddComponent<GoblinHealth>();
        Health.ConfigureHitPoints(maximumHitPoints);
    }

    public void ConfigureLane(int laneIndex) => assignedLane = Mathf.Clamp(laneIndex, 0, Mathf.Max(1, lane ? lane.battleLaneCount - 1 : 1));

    void Update()
    {
        if (!lane || !Faction || IsDead) return;
        bool separatedTarget = target && target.lane == lane && target.assignedLane != assignedLane;
        if (separatedTarget) target = null;
        if (performAttacks && (!target || target.IsDead || !target.Faction || target.Faction.team == Faction.team || target.lane != lane || separatedTarget) &&
            Time.time >= nextTargetSearch)
        {
            target = UnitCombatRegistry.FindNearestOpponent(Faction.team, lane, transform.position, engagementRange);
            nextTargetSearch = Time.time + targetRefreshInterval;
        }

        if (performAttacks && target && !target.IsDead && target.Faction && target.Health && target.Faction.team != Faction.team)
        {
            EngageTarget();
            return;
        }
        if (!stationaryDefender) AdvanceToOpposingBase();
    }

    void EngageTarget()
    {
        Vector3 local = lane.transform.InverseTransformPoint(transform.position);
        Vector3 targetLocal = lane.transform.InverseTransformPoint(target.transform.position);
        Vector2 delta = new Vector2(targetLocal.x - local.x, targetLocal.z - local.z);
        float distance = delta.magnitude;
        if (distance < .001f) return;
        Vector2 direction = delta / distance;
        Face(direction);
        if (distance > attackRange)
        {
            if (stationaryDefender) return;
            float step = Mathf.Min(movementSpeed * Time.deltaTime, distance - attackRange);
            local.x += direction.x * step;
            local.z += direction.y * step;
            if (!lane.IsWalkable(local.x, local.z))
            {
                AdvanceToOpposingBase();
                return;
            }
            local.y = 0f;
            transform.position = lane.transform.TransformPoint(local);
            return;
        }
        if (Time.time < nextAttack) return;
        nextAttack = Time.time + attackInterval;
        GameplayVisuals.Impact(target.transform.position + lane.transform.up * .22f,
            .26f, new Color(.9f, .78f, .48f), .12f);
        if (targetsPerAttack > 1 && multiTargetRadius > 0f)
            UnitCombatRegistry.DamageNearestOpponents(target.transform.position, multiTargetRadius,
                attackDamage, Faction.team, lane, targetsPerAttack);
        else target.Health.TakeDamage(attackDamage, Faction.team);
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
        float routeX = lane.RoadCenterX(assignedLane);
        local.x = routeX;
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
