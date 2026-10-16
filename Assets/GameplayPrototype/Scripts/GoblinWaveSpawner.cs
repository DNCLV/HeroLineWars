using System;
using System.Collections;
using UnityEngine;

/// <summary>Deterministic continuous survival director retained under its original component name.</summary>
[DefaultExecutionOrder(100)]
public sealed class GoblinWaveSpawner : MonoBehaviour
{
    [Serializable] public struct EnemyPrefab
    {
        public UnitArchetype archetype;
        public GameObject prefab;
    }

    [Serializable]
    public struct CombatProfile
    {
        [Min(1)] public int hitPoints;
        [Min(1)] public int damage;
        [Min(.1f)] public float attackInterval;
        [Tooltip("Zero preserves the prefab's existing enemy range.")]
        [Min(0f)] public float attackRange;
        [Min(.1f)] public float movementSpeed;
        [Min(1)] public int targetsPerAttack;
        [Min(0f)] public float multiTargetRadius;

        public CombatProfile(int hp, int attackDamage, float interval, float range, float speed,
            int targets = 1, float radius = 0f)
        {
            hitPoints = hp;
            damage = attackDamage;
            attackInterval = interval;
            attackRange = range;
            movementSpeed = speed;
            targetsPerAttack = targets;
            multiTargetRadius = radius;
        }
    }

    public LaneGenerator lane;
    public MatchLoopController matchLoop;
    public GameObject heroPrefab;
    [Tooltip("Available enemy models. Selection is controlled by the survival director weights.")]
    public EnemyPrefab[] enemyPrefabs;
    [Tooltip("Legacy fallback when the enemy prefab list has not yet been assigned.")]
    public GameObject goblinPrefab;

    [Header("Friendly hero")]
    [Min(.1f)] public float heroMovementSpeed = 3.5f;
    [Min(.1f)] public float heroSpawnInset = 3.5f;
    [Min(.1f)] public float heroModelScale = 1.12f;
    [Min(.1f)] public float selectionRingSize = 1.15f;

    [Header("Allied combat profiles")]
    public CombatProfile archerStats = new CombatProfile(95, 16, 1f, 5f, 2.6f);
    public CombatProfile knightStats = new CombatProfile(150, 10, 1.2f, 1.5f, 2f);
    public CombatProfile wizardStats = new CombatProfile(80, 14, 1.6f, 4.5f, 2.35f, 3, 2.6f);

    [Header("Enemy base combat profiles")]
    public CombatProfile goblinStats = new CombatProfile(65, 8, 1f, 0f, 2.2f);
    public CombatProfile ghoulStats = new CombatProfile(130, 13, 1.2f, 0f, 2f);
    public CombatProfile skeletonKingStats = new CombatProfile(330, 24, 1.5f, 0f, 1.65f);
    public CombatProfile undeadDragonStats = new CombatProfile(450, 20, 2f, 0f, 1.8f);
    public CombatProfile playerHeroStats = new CombatProfile(520, 18, 1.1f, 6f, 3.5f);
    public CombatProfile enemyHeroStats = new CombatProfile(620, 16, 1.15f, 5.5f, 0.1f);

    [Header("Enemy Director v2 - total spawns per simulated minute")]
    [Min(0)] public int minuteOneSpawnCount = 12;
    [Min(0)] public int minuteTwoSpawnCount = 18;
    [Min(0)] public int minuteThreeSpawnCount = 24;
    [Min(0)] public int minuteFourSpawnCount = 30;
    [Min(0)] public int minuteFiveSpawnCount = 36;
    [Min(0)] public int minuteSixSpawnCount = 42;

    [Header("Enemy Director v2 - composition and scaling")]
    [Min(0f)] public float skeletonIntroductionTime = 120f;
    [Min(0f)] public float dragonIntroductionTime = 180f;
    [Min(1f)] public float archetypeRampDuration = 60f;
    [Range(0f, 1f)] public float openingGhoulWeight = .12f;
    [Range(0f, 1f)] public float finalGoblinWeight = .22f;
    [Range(0f, 1f)] public float finalGhoulWeight = .38f;
    [Range(0f, 1f)] public float finalSkeletonWeight = .24f;
    [Range(0f, 1f)] public float finalDragonWeight = .16f;
    [Min(0f)] public float enemyHitPointBonusPerMinute = .08f;
    [Min(0f)] public float enemyDamageBonusPerMinute = .05f;
    [Min(0f)] public float enemySpawnInset = 1.5f;
    [Min(.1f)] public float laneEdgePadding = 1f;
    [Min(.1f)] public float alternatingRowOffset = .7f;

    [Header("Development diagnostics")]
    [Tooltip("Editor/development-build summaries for scheduled interval, type totals, and living enemies.")]
    public bool enableSpawnDiagnostics;
    [Min(1f)] public float diagnosticLogInterval = 15f;

    int spawnSequence;
    float actualSpawnInterval;
    float nextDiagnosticTime;
    int goblinsSpawned;
    int ghoulsSpawned;
    int skeletonKingsSpawned;
    int undeadDragonsSpawned;
    static GoblinWaveSpawner activeDirector;
    [SerializeField, HideInInspector] int alliedBalanceRevision;

    public int TotalEnemiesSpawned => spawnSequence;
    public int GoblinsSpawned => goblinsSpawned;
    public int GhoulsSpawned => ghoulsSpawned;
    public int SkeletonKingsSpawned => skeletonKingsSpawned;
    public int UndeadDragonsSpawned => undeadDragonsSpawned;
    public float ActualSpawnInterval => actualSpawnInterval;
    public int PlayerDefenseLaneIndex => lane ? lane.PlayerDefenseLaneIndex : 0;
    public int AlliedDefenseLaneIndex => lane ? lane.EnemyDefenseLaneIndex : 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState() { activeDirector = null; }

    void Awake()
    {
        if (activeDirector && activeDirector != this)
        {
            Debug.LogError("Multiple enemy directors are active. Keep one GoblinWaveSpawner under GameplayPrototype.", this);
            enabled = false;
            return;
        }
        activeDirector = this;
    }

    void OnDestroy()
    {
        if (activeDirector == this) activeDirector = null;
    }

    void Start()
    {
        UpgradeAlliedBalance();
        EnsureProfileDefaults();
        if (!lane) lane = FindAnyObjectByType<LaneGenerator>();
        if (!matchLoop) matchLoop = GetComponent<MatchLoopController>();
        if (!lane || !heroPrefab || GetEnemyPrefabs().Length == 0)
        {
            Debug.LogError("GameplayPrototype needs the active lane, Battlemage prefab, and enemy prefabs.", this);
            enabled = false;
            return;
        }
        if (!matchLoop) matchLoop = gameObject.AddComponent<MatchLoopController>();
        if (!matchLoop || matchLoop.IsRunning) StartMatchSpawns();
    }

    public void UpgradeAlliedBalance()
    {
        if (alliedBalanceRevision >= 1) return;
        archerStats = new CombatProfile(95, 16, 1f, 5f, 2.6f);
        knightStats = new CombatProfile(150, 10, 1.2f, 1.5f, 2f);
        wizardStats = new CombatProfile(80, 14, 1.6f, 4.5f, 2.35f, 3, 2.6f);
        alliedBalanceRevision = 1;
    }

    public void StopWaveSpawning() { StopAllCoroutines(); }

    public void ClearSpawnedUnits()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponent<UnitFaction>() || child.GetComponent<HeroLaneController>() ||
                child.GetComponent<HealingWell>())
                Destroy(child.gameObject);
        }
    }

    public void StartMatchSpawns()
    {
        if (!lane || (matchLoop && !matchLoop.IsRunning)) return;
        spawnSequence = 0;
        goblinsSpawned = 0;
        ghoulsSpawned = 0;
        skeletonKingsSpawned = 0;
        undeadDragonsSpawned = 0;
        actualSpawnInterval = 0f;
        nextDiagnosticTime = 0f;
        SpawnPlayerHero();
        SpawnHealingWell();
        SpawnEnemyHero();
        StartCoroutine(RunContinuousDirector());
    }

    void SpawnPlayerHero()
    {
        int defenseLane = PlayerDefenseLaneIndex;
        Vector3 position = EndpointPosition(false, lane.RoadCenterX(defenseLane), heroSpawnInset);
        GameObject hero = Instantiate(heroPrefab, position, lane.transform.rotation, transform);
        hero.name = "Battlemage";
        UnitFaction faction = hero.GetComponent<UnitFaction>();
        if (!faction) faction = hero.AddComponent<UnitFaction>();
        faction.showFactionRing = false;
        faction.Set(UnitTeam.Friendly, UnitArchetype.Battlemage);
        HeroLaneController controller = hero.GetComponent<HeroLaneController>();
        if (!controller) controller = hero.AddComponent<HeroLaneController>();
        controller.lane = lane;
        controller.movementSpeed = heroMovementSpeed;
        controller.modelScale = heroModelScale;
        controller.selectionRingSize = selectionRingSize;
        controller.allowedLaneIndex = defenseLane;
        FireballAbility ability = hero.GetComponent<FireballAbility>();
        if (!ability) ability = hero.AddComponent<FireballAbility>();
        ability.lane = lane;
        if (!hero.GetComponent<HeroAutoAttack>()) hero.AddComponent<HeroAutoAttack>();
        GoblinHealth health = hero.GetComponent<GoblinHealth>();
        if (!health) health = hero.AddComponent<GoblinHealth>();
        LaneUnitCombat combat = hero.GetComponent<LaneUnitCombat>();
        if (!combat) combat = hero.AddComponent<LaneUnitCombat>();
        ApplyCombatProfile(combat, UnitArchetype.Battlemage, 0f, false);
        combat.stationaryDefender = true;
        combat.performAttacks = false;
        combat.Configure(lane, UnitTeam.Friendly, UnitArchetype.Battlemage);
        combat.ConfigureLane(defenseLane);
        HeroAbilityLoadout loadout = hero.GetComponent<HeroAbilityLoadout>();
        if (!loadout) loadout = hero.AddComponent<HeroAbilityLoadout>();
        HeroProgression progression = hero.GetComponent<HeroProgression>();
        if (!progression) progression = hero.AddComponent<HeroProgression>();
        progression.Configure(true);
    }

    void SpawnHealingWell()
    {
        Vector3 position = EndpointPosition(false, lane.RoadCenterX(PlayerDefenseLaneIndex), 1.35f);
        GameObject wellObject = new GameObject("PlayerHealingWell");
        wellObject.transform.SetParent(transform, false);
        wellObject.transform.position = position;
        wellObject.transform.rotation = lane.transform.rotation;
        HealingWell well = wellObject.AddComponent<HealingWell>();
        well.Configure(HeroProgression.PlayerHero, 4f, .06f, 5f);
    }

    void SpawnEnemyHero()
    {
        int defenseLane = AlliedDefenseLaneIndex;
        Vector3 position = EndpointPosition(true, lane.RoadCenterX(defenseLane), heroSpawnInset);
        GameObject hero = Instantiate(heroPrefab, position, lane.transform.rotation * Quaternion.Euler(0f, 180f, 0f), transform);
        hero.name = "EnemyBattlemage";
        HeroLaneController movement = hero.GetComponent<HeroLaneController>();
        if (movement) movement.enabled = false;
        HeroAutoAttack oldAutoAttack = hero.GetComponent<HeroAutoAttack>();
        if (oldAutoAttack) oldAutoAttack.enabled = false;
        FireballAbility oldAbility = hero.GetComponent<FireballAbility>();
        if (oldAbility) oldAbility.enabled = false;
        UnitFaction faction = hero.GetComponent<UnitFaction>();
        if (!faction) faction = hero.AddComponent<UnitFaction>();
        faction.showFactionRing = true;
        faction.Set(UnitTeam.Enemy, UnitArchetype.Battlemage);
        GoblinHealth health = hero.GetComponent<GoblinHealth>();
        if (!health) health = hero.AddComponent<GoblinHealth>();
        LaneUnitCombat combat = hero.GetComponent<LaneUnitCombat>();
        if (!combat) combat = hero.AddComponent<LaneUnitCombat>();
        combat.maximumHitPoints = enemyHeroStats.hitPoints;
        combat.attackDamage = enemyHeroStats.damage;
        combat.attackInterval = enemyHeroStats.attackInterval;
        combat.attackRange = enemyHeroStats.attackRange;
        combat.engagementRange = Mathf.Max(enemyHeroStats.attackRange + 2f, 7f);
        combat.movementSpeed = .1f;
        combat.stationaryDefender = true;
        combat.performAttacks = true;
        combat.Configure(lane, UnitTeam.Enemy, UnitArchetype.Battlemage);
        combat.ConfigureLane(defenseLane);
        health.showHealthBarAlways = true;
        HeroProgression progression = hero.GetComponent<HeroProgression>();
        if (!progression) progression = hero.AddComponent<HeroProgression>();
        progression.baseMaximumHealth = 340;
        progression.enableExperience = false;
        progression.Configure(false);
    }

    IEnumerator RunContinuousDirector()
    {
        EnemyPrefab[] available = GetEnemyPrefabs();
        for (int minuteIndex = 0; minuteIndex < 6; minuteIndex++)
        {
            int spawnCount = SpawnCountForMinute(minuteIndex);
            if (spawnCount <= 0) continue;
            actualSpawnInterval = 60f / spawnCount;
            for (int slot = 0; slot < spawnCount; slot++)
            {
                float scheduledTime = minuteIndex * 60f + (slot + .5f) * actualSpawnInterval;
                while (matchLoop && matchLoop.IsRunning && matchLoop.ElapsedTime < scheduledTime)
                    yield return null;
                if (matchLoop && !matchLoop.IsRunning) yield break;

                float elapsed = matchLoop ? matchLoop.ElapsedTime : scheduledTime;
                SpawnScheduledEnemy(available, elapsed);
                LogSpawnSummaryIfDue(elapsed);
            }
        }
        while (!matchLoop || matchLoop.IsRunning) yield return null;
    }

    void SpawnScheduledEnemy(EnemyPrefab[] available, float elapsed)
    {
        EnemyPrefab selection = SelectEnemy(available, elapsed, spawnSequence);
        int assignedLane = PlayerDefenseLaneIndex;
        float zInset = enemySpawnInset + (spawnSequence % 2) * alternatingRowOffset;
        float laneX = lane.RoadCenterX(assignedLane);
        SpawnUnit(selection.prefab, selection.archetype, UnitTeam.Enemy,
            EndpointPosition(true, laneX, zInset), $"Enemy_{selection.archetype}_{spawnSequence + 1}", assignedLane);
        CountSpawn(selection.archetype);
        spawnSequence++;
    }

    int SpawnCountForMinute(int minuteIndex)
    {
        switch (minuteIndex)
        {
            case 0: return Mathf.Max(0, minuteOneSpawnCount);
            case 1: return Mathf.Max(0, minuteTwoSpawnCount);
            case 2: return Mathf.Max(0, minuteThreeSpawnCount);
            case 3: return Mathf.Max(0, minuteFourSpawnCount);
            case 4: return Mathf.Max(0, minuteFiveSpawnCount);
            default: return Mathf.Max(0, minuteSixSpawnCount);
        }
    }

    void CountSpawn(UnitArchetype archetype)
    {
        if (archetype == UnitArchetype.Ghoul) ghoulsSpawned++;
        else if (archetype == UnitArchetype.SkeletonKing) skeletonKingsSpawned++;
        else if (archetype == UnitArchetype.UndeadDragon) undeadDragonsSpawned++;
        else goblinsSpawned++;
    }

    void LogSpawnSummaryIfDue(float elapsed)
    {
        if (elapsed < nextDiagnosticTime) return;
        nextDiagnosticTime = elapsed + Mathf.Max(1f, diagnosticLogInterval);
        LogSpawn($"elapsed={elapsed:F1}s, interval={actualSpawnInterval:F3}s, total={TotalEnemiesSpawned}, " +
            $"Goblin={goblinsSpawned}, Ghoul={ghoulsSpawned}, SkeletonKing={skeletonKingsSpawned}, " +
            $"UndeadDragon={undeadDragonsSpawned}, alive={UnitCombatRegistry.Count(UnitTeam.Enemy)}.");
    }

    [ContextMenu("Log Enemy Director v2 Diagnostics")]
    public void LogDirectorDiagnostics()
    {
        Debug.Log($"[Enemy Director v2] targets={minuteOneSpawnCount}/{minuteTwoSpawnCount}/" +
            $"{minuteThreeSpawnCount}/{minuteFourSpawnCount}/{minuteFiveSpawnCount}/{minuteSixSpawnCount}, " +
            $"spawned={TotalEnemiesSpawned} (Goblin={goblinsSpawned}, Ghoul={ghoulsSpawned}, " +
            $"SkeletonKing={skeletonKingsSpawned}, UndeadDragon={undeadDragonsSpawned}), " +
            $"alive={UnitCombatRegistry.Count(UnitTeam.Enemy)}, interval={actualSpawnInterval:F3}s.", this);
    }

    GameObject SpawnUnit(GameObject prefab, UnitArchetype archetype, UnitTeam team,
        Vector3 position, string objectName, int assignedLane = 1)
    {
        GameObject unit = Instantiate(prefab, position, lane.transform.rotation, transform);
        unit.name = objectName;
        GoblinLaneMover legacyMover = unit.GetComponent<GoblinLaneMover>();
        if (legacyMover) legacyMover.enabled = false;
        UnitFaction faction = unit.GetComponent<UnitFaction>();
        if (!faction) faction = unit.AddComponent<UnitFaction>();
        faction.Set(team, archetype);
        GoblinHealth health = unit.GetComponent<GoblinHealth>();
        if (!health) health = unit.AddComponent<GoblinHealth>();
        health.showHealthBarAlways = true;
        LaneUnitCombat combat = unit.GetComponent<LaneUnitCombat>();
        if (!combat) combat = unit.AddComponent<LaneUnitCombat>();
        ApplyCombatProfile(combat, archetype,
            team == UnitTeam.Enemy && matchLoop ? matchLoop.ElapsedTime : 0f, team == UnitTeam.Enemy);
        combat.Configure(lane, team, archetype);
        combat.ConfigureLane(assignedLane);
        return unit;
    }

    public GameObject SpawnFriendly(GameObject prefab, UnitArchetype archetype, int assignedLane, int purchaseNumber,
        float lateralOffset = 0f, float longitudinalOffset = 0f)
    {
        if (!lane || !prefab || (matchLoop && !matchLoop.IsRunning)) return null;
        assignedLane = AlliedDefenseLaneIndex;
        float edgeInset = Mathf.Max(.65f, lane.cellSize * .65f);
        float maximumOffset = Mathf.Max(0f, lane.RoadWidth * .5f - edgeInset);
        lateralOffset = Mathf.Clamp(lateralOffset, -maximumOffset, maximumOffset);
        Vector3 localPosition = new Vector3(lane.RoadCenterX(assignedLane) + lateralOffset, 0f,
            Mathf.Clamp(heroSpawnInset + .8f + Mathf.Max(0f, longitudinalOffset),
                edgeInset, lane.LengthWorld - edgeInset));
        GameObject spawned = SpawnUnit(prefab, archetype, UnitTeam.Friendly,
            lane.transform.TransformPoint(localPosition),
            $"Friendly_{archetype}_{purchaseNumber}_Lane{assignedLane + 1}", assignedLane);
        if (!spawned) return null;
        LaneUnitCombat combat = spawned.GetComponent<LaneUnitCombat>();
        if (combat) combat.ConfigureLaneOffset(lateralOffset);
        Renderer[] renderers = spawned.GetComponentsInChildren<Renderer>(true);
        bool hasVisibleModel = false;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] && renderers[i].enabled && renderers[i].gameObject.activeInHierarchy &&
                !(renderers[i].transform.name.Contains("Ring") || renderers[i] is ParticleSystemRenderer))
            {
                hasVisibleModel = true;
                break;
            }
        if (!hasVisibleModel)
            Debug.LogError($"Spawned {archetype} has no active model renderer. Check its imported prefab.", spawned);
        return spawned;
    }

    EnemyPrefab[] GetEnemyPrefabs()
    {
        if (enemyPrefabs != null)
        {
            int count = 0;
            foreach (EnemyPrefab entry in enemyPrefabs) if (entry.prefab) count++;
            if (count > 0)
            {
                EnemyPrefab[] result = new EnemyPrefab[count];
                int index = 0;
                foreach (EnemyPrefab entry in enemyPrefabs) if (entry.prefab) result[index++] = entry;
                return result;
            }
        }
        return goblinPrefab ? new[] { new EnemyPrefab { archetype = UnitArchetype.Goblin, prefab = goblinPrefab } }
                            : Array.Empty<EnemyPrefab>();
    }

    EnemyPrefab SelectEnemy(EnemyPrefab[] available, float elapsed, int sequence)
    {
        float duration = matchLoop ? matchLoop.SurvivalDuration : 360f;
        float progress = Mathf.Clamp01(elapsed / duration);
        float goblin = Mathf.Lerp(1f - Mathf.Clamp01(openingGhoulWeight), finalGoblinWeight, progress);
        float ghoul = Mathf.Lerp(Mathf.Clamp01(openingGhoulWeight), finalGhoulWeight, progress);
        float skeletonT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(skeletonIntroductionTime,
            skeletonIntroductionTime + Mathf.Max(1f, archetypeRampDuration), elapsed));
        float dragonT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dragonIntroductionTime,
            dragonIntroductionTime + Mathf.Max(1f, archetypeRampDuration), elapsed));
        float skeleton = finalSkeletonWeight * skeletonT;
        float dragon = finalDragonWeight * dragonT;
        float total = Mathf.Max(.0001f, goblin + ghoul + skeleton + dragon);
        float roll = Mathf.Repeat((sequence + 1) * .61803398875f, 1f) * total;
        UnitArchetype selected = roll < goblin ? UnitArchetype.Goblin :
            roll < goblin + ghoul ? UnitArchetype.Ghoul :
            roll < goblin + ghoul + skeleton ? UnitArchetype.SkeletonKing : UnitArchetype.UndeadDragon;
        for (int i = 0; i < available.Length; i++)
            if (available[i].archetype == selected && available[i].prefab) return available[i];
        for (int i = 0; i < available.Length; i++)
            if (available[i].archetype == UnitArchetype.Goblin && available[i].prefab) return available[i];
        return available[sequence % available.Length];
    }

    void ApplyCombatProfile(LaneUnitCombat combat, UnitArchetype archetype, float elapsed, bool scaleEnemy)
    {
        CombatProfile profile = Profile(archetype);
        float elapsedMinutes = Mathf.Max(0f, elapsed) / 60f;
        float hpScale = scaleEnemy ? 1f + enemyHitPointBonusPerMinute * elapsedMinutes : 1f;
        float damageScale = scaleEnemy ? 1f + enemyDamageBonusPerMinute * elapsedMinutes : 1f;
        combat.maximumHitPoints = Mathf.Max(1, Mathf.RoundToInt(profile.hitPoints * hpScale));
        combat.attackDamage = Mathf.Max(1, Mathf.RoundToInt(profile.damage * damageScale));
        combat.attackInterval = Mathf.Max(.1f, profile.attackInterval);
        if (profile.attackRange > 0f)
        {
            combat.attackRange = profile.attackRange;
            combat.engagementRange = Mathf.Max(combat.engagementRange, profile.attackRange + 1f);
        }
        combat.movementSpeed = Mathf.Max(.1f, profile.movementSpeed);
        combat.targetsPerAttack = Mathf.Max(1, profile.targetsPerAttack);
        combat.multiTargetRadius = Mathf.Max(0f, profile.multiTargetRadius);
    }

    CombatProfile Profile(UnitArchetype archetype)
    {
        if (archetype == UnitArchetype.Battlemage) return playerHeroStats;
        if (archetype == UnitArchetype.GreenArcher) return archerStats;
        if (archetype == UnitArchetype.HolyKnight) return knightStats;
        if (archetype == UnitArchetype.BlueWizard) return wizardStats;
        if (archetype == UnitArchetype.Ghoul) return ghoulStats;
        if (archetype == UnitArchetype.SkeletonKing) return skeletonKingStats;
        if (archetype == UnitArchetype.UndeadDragon) return undeadDragonStats;
        return goblinStats;
    }

    void EnsureProfileDefaults()
    {
        if (archerStats.hitPoints <= 0) archerStats = new CombatProfile(95, 16, 1f, 5f, 2.6f);
        if (knightStats.hitPoints <= 0) knightStats = new CombatProfile(150, 10, 1.2f, 1.5f, 2f);
        if (wizardStats.hitPoints <= 0) wizardStats = new CombatProfile(80, 14, 1.6f, 4.5f, 2.35f, 3, 2.6f);
        if (goblinStats.hitPoints <= 0) goblinStats = new CombatProfile(65, 8, 1f, 0f, 2.2f);
        if (ghoulStats.hitPoints <= 0) ghoulStats = new CombatProfile(130, 13, 1.2f, 0f, 2f);
        if (skeletonKingStats.hitPoints <= 0) skeletonKingStats = new CombatProfile(330, 24, 1.5f, 0f, 1.65f);
        if (undeadDragonStats.hitPoints <= 0) undeadDragonStats = new CombatProfile(450, 20, 2f, 0f, 1.8f);
        if (playerHeroStats.hitPoints <= 0) playerHeroStats = new CombatProfile(520, 18, 1.1f, 6f, 3.5f);
        if (enemyHeroStats.hitPoints <= 0) enemyHeroStats = new CombatProfile(620, 16, 1.15f, 5.5f, .1f);
        if (minuteOneSpawnCount <= 0) minuteOneSpawnCount = 12;
        if (minuteTwoSpawnCount <= 0) minuteTwoSpawnCount = 18;
        if (minuteThreeSpawnCount <= 0) minuteThreeSpawnCount = 24;
        if (minuteFourSpawnCount <= 0) minuteFourSpawnCount = 30;
        if (minuteFiveSpawnCount <= 0) minuteFiveSpawnCount = 36;
        if (minuteSixSpawnCount <= 0) minuteSixSpawnCount = 42;
        if (skeletonIntroductionTime <= 0f) skeletonIntroductionTime = 120f;
        if (dragonIntroductionTime <= 0f) dragonIntroductionTime = 180f;
        if (archetypeRampDuration <= 0f) archetypeRampDuration = 60f;
        if (openingGhoulWeight <= 0f) openingGhoulWeight = .12f;
        if (finalGoblinWeight + finalGhoulWeight + finalSkeletonWeight + finalDragonWeight <= .001f)
        {
            finalGoblinWeight = .22f;
            finalGhoulWeight = .38f;
            finalSkeletonWeight = .24f;
            finalDragonWeight = .16f;
        }
        if (enemyHitPointBonusPerMinute <= 0f) enemyHitPointBonusPerMinute = .08f;
        if (enemyDamageBonusPerMinute <= 0f) enemyDamageBonusPerMinute = .05f;
        if (diagnosticLogInterval <= 0f) diagnosticLogInterval = 15f;
    }

    Vector3 EndpointPosition(bool goalSide, float x, float inset)
    {
        Transform anchor = lane.transform.Find(goalSide ? "GoalArea" : "SpawnArea");
        float anchorZ = anchor ? lane.transform.InverseTransformPoint(anchor.position).z : 0f;
        float z = goalSide
            ? (anchorZ > lane.LengthWorld * .5f ? anchorZ - inset : lane.LengthWorld - inset + anchorZ)
            : (anchorZ < lane.LengthWorld * .5f ? anchorZ + inset : inset);
        z = Mathf.Clamp(z, .15f, lane.LengthWorld - .15f);
        return lane.transform.TransformPoint(new Vector3(Mathf.Clamp(x, 0f, lane.WidthWorld), 0f, z));
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEBUG")]
    void LogSpawn(string message)
    {
        if (enableSpawnDiagnostics) Debug.Log($"[Enemy Director v2] {message}", this);
    }
}
