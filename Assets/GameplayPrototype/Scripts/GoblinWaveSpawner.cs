using System;
using System.Collections;
using UnityEngine;

/// <summary>Deterministic, capped enemy wave spawner retained under its original component name.</summary>
[DefaultExecutionOrder(100)]
public sealed class GoblinWaveSpawner : MonoBehaviour
{
    [Serializable] public struct EnemyPrefab
    {
        public UnitArchetype archetype;
        public GameObject prefab;
    }

    public LaneGenerator lane;
    public MatchLoopController matchLoop;
    public GameObject heroPrefab;
    [Tooltip("Ordered deterministic selection; waves cycle through these enemy types.")]
    public EnemyPrefab[] enemyPrefabs;
    [Tooltip("Legacy fallback when the enemy prefab list has not yet been assigned.")]
    public GameObject goblinPrefab;

    [Header("Friendly hero")]
    [Min(.1f)] public float heroMovementSpeed = 3.5f;
    [Min(.1f)] public float heroSpawnInset = 3.5f;
    [Min(.1f)] public float heroModelScale = 1.12f;
    [Min(.1f)] public float selectionRingSize = 1.15f;

    [Header("Enemy waves")]
    [Range(1, 12)] public int unitsPerWave = 5;
    [Min(.05f)] public float delayBetweenUnits = .6f;
    [Min(.5f)] public float delayBetweenWaves = 18f;
    [Min(1)] public int maximumActiveEnemies = 10;
    [Min(0f)] public float enemySpawnInset = 1.5f;
    [Min(.1f)] public float laneEdgePadding = 1f;
    [Min(.1f)] public float alternatingRowOffset = .7f;

    int waveIndex;

    void Start()
    {
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

    public void StopWaveSpawning() { StopAllCoroutines(); }

    public void ClearSpawnedUnits()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponent<UnitFaction>() || child.GetComponent<HeroLaneController>())
                Destroy(child.gameObject);
        }
    }

    public void StartMatchSpawns()
    {
        if (!lane || (matchLoop && !matchLoop.IsRunning)) return;
        waveIndex = 0;
        SpawnHero();
        StartCoroutine(SpawnWaves());
    }

    void SpawnHero()
    {
        Vector3 position = EndpointPosition(false, lane.RoadCenterX(lane.battleLaneCount / 2), heroSpawnInset);
        GameObject hero = Instantiate(heroPrefab, position, lane.transform.rotation, transform);
        hero.name = "Battlemage";
        UnitFaction faction = hero.GetComponent<UnitFaction>();
        if (!faction) faction = hero.AddComponent<UnitFaction>();
        faction.showFactionRing = false; // HeroLaneController owns the cyan selection ring.
        faction.Set(UnitTeam.Friendly, UnitArchetype.Battlemage);
        HeroLaneController controller = hero.GetComponent<HeroLaneController>();
        if (!controller) controller = hero.AddComponent<HeroLaneController>();
        controller.lane = lane;
        controller.movementSpeed = heroMovementSpeed;
        controller.modelScale = heroModelScale;
        controller.selectionRingSize = selectionRingSize;
        FireballAbility ability = hero.GetComponent<FireballAbility>();
        if (!ability) ability = hero.AddComponent<FireballAbility>();
        ability.lane = lane;
        if (!hero.GetComponent<HeroAutoAttack>()) hero.AddComponent<HeroAutoAttack>();
    }

    IEnumerator SpawnWaves()
    {
        while (!matchLoop || matchLoop.IsRunning)
        {
            EnemyPrefab[] available = GetEnemyPrefabs();
            int capacity = Mathf.Max(0, maximumActiveEnemies - UnitCombatRegistry.Count(UnitTeam.Enemy));
            int count = Mathf.Min(Mathf.Max(1, unitsPerWave), capacity);
            for (int i = 0; i < count; i++)
            {
                EnemyPrefab selection = available[(waveIndex * Mathf.Max(1, unitsPerWave) + i) % available.Length];
                float zInset = enemySpawnInset + (i % 2) * alternatingRowOffset;
                int assignedLane = (waveIndex * Mathf.Max(1, unitsPerWave) + i) % Mathf.Max(2, lane.battleLaneCount);
                float laneX = lane.RoadCenterX(assignedLane);
                SpawnUnit(selection.prefab, selection.archetype, UnitTeam.Enemy,
                    EndpointPosition(true, laneX, zInset), $"Enemy_{selection.archetype}_{waveIndex + 1}_{i + 1}", assignedLane);
                if (i + 1 < count) yield return new WaitForSeconds(delayBetweenUnits);
            }
            waveIndex++;
            yield return new WaitForSeconds(delayBetweenWaves);
        }
    }

    GameObject SpawnUnit(GameObject prefab, UnitArchetype archetype, UnitTeam team, Vector3 position, string objectName, int assignedLane = 1)
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
        LaneUnitCombat combat = unit.GetComponent<LaneUnitCombat>();
        if (!combat) combat = unit.AddComponent<LaneUnitCombat>();
        combat.Configure(lane, team, archetype);
        combat.ConfigureLane(assignedLane);
        return unit;
    }

    public GameObject SpawnFriendly(GameObject prefab, UnitArchetype archetype, int assignedLane, int purchaseNumber)
    {
        if (!lane || !prefab || (matchLoop && !matchLoop.IsRunning)) return null;
        assignedLane = Mathf.Clamp(assignedLane, 0, lane.battleLaneCount - 1);
        // Every purchase starts at this exact shared blue-road point; only its
        // route assignment differs. The branch is reached through movement.
        Vector3 position = EndpointPosition(false, lane.RoadCenterX(lane.battleLaneCount / 2), heroSpawnInset + .8f);
        GameObject unit = SpawnUnit(prefab, archetype, UnitTeam.Friendly, position,
            $"Friendly_{archetype}_{purchaseNumber}_Lane{assignedLane + 1}", assignedLane);
        return unit;
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

    Vector3 EndpointPosition(bool goalSide, float x, float inset)
    {
        // SpawnArea/GoalArea are the lane generator's endpoint groups. Their local
        // transforms define a true endpoint when positioned there; the lane bounds
        // remain the source of truth when those generated groups share the origin.
        Transform anchor = lane.transform.Find(goalSide ? "GoalArea" : "SpawnArea");
        float anchorZ = anchor ? lane.transform.InverseTransformPoint(anchor.position).z : 0f;
        float z;
        if (goalSide)
            z = anchorZ > lane.LengthWorld * .5f ? anchorZ - inset : lane.LengthWorld - inset + anchorZ;
        else
            z = anchorZ < lane.LengthWorld * .5f ? anchorZ + inset : inset;
        z = Mathf.Clamp(z, .15f, lane.LengthWorld - .15f);
        return lane.transform.TransformPoint(new Vector3(Mathf.Clamp(x, 0f, lane.WidthWorld), 0f, z));
    }
}
