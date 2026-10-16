using UnityEngine;

/// <summary>Manual unit deployment invoked by the three HUD send buttons.</summary>
public sealed class FriendlyUnitSender : MonoBehaviour
{
    public GoblinWaveSpawner spawner;
    public GameObject greenArcherPrefab;
    public GameObject holyKnightPrefab;
    public GameObject blueWizardPrefab;
    public MatchLoopController matchLoop;
    [Min(.1f)] public float spawnSpacing = 1.25f;
    [Min(.1f)] public float spawnRowSpacing = 1f;

    [Header("Match purchase settings")]
    [Min(0)] public int archerCost = 1;
    [Min(0)] public int archerIncomeBonus = 1;
    [Min(0)] public int knightCost = 1;
    [Min(0)] public int knightIncomeBonus = 1;
    [Min(0)] public int wizardCost = 1;
    [Min(0)] public int wizardIncomeBonus = 1;

    int nextLaneIndex;
    public int NextLaneIndex => nextLaneIndex;

    public int TotalSuccessfulPurchases { get; private set; }
    public int TotalFriendlyUnitsSpawned { get; private set; }
    public int TotalFriendlyDeaths { get; private set; }
    public int TotalFriendlyUnitsReachedEnemyBase { get; private set; }
    public int ActiveFriendlyCount => UnitCombatRegistry.Count(UnitTeam.Friendly);

    void Awake()
    {
        if (!spawner) spawner = GetComponent<GoblinWaveSpawner>();
        if (!matchLoop) matchLoop = GetComponent<MatchLoopController>();
    }

    public void SendArcher() => Send(greenArcherPrefab, UnitArchetype.GreenArcher, archerCost, archerIncomeBonus);
    public void SendKnight() => Send(holyKnightPrefab, UnitArchetype.HolyKnight, knightCost, knightIncomeBonus);
    public void SendWizard() => Send(blueWizardPrefab, UnitArchetype.BlueWizard, wizardCost, wizardIncomeBonus);

    public bool CanPurchase(UnitArchetype archetype)
    {
        if (!matchLoop) matchLoop = GetComponent<MatchLoopController>();
        if (!spawner) spawner = GetComponent<GoblinWaveSpawner>();
        if (!matchLoop || !matchLoop.IsRunning || !spawner || !PrefabFor(archetype)) return false;
        return matchLoop.CanAfford(Cost(archetype));
    }

    public int Cost(UnitArchetype archetype) => archetype == UnitArchetype.GreenArcher ? archerCost :
        archetype == UnitArchetype.HolyKnight ? knightCost : wizardCost;

    public int IncomeBonus(UnitArchetype archetype) => archetype == UnitArchetype.GreenArcher ? archerIncomeBonus :
        archetype == UnitArchetype.HolyKnight ? knightIncomeBonus : wizardIncomeBonus;

    public void ResetForMatch()
    {
        nextLaneIndex = 0;
        TotalSuccessfulPurchases = 0;
        TotalFriendlyUnitsSpawned = 0;
        TotalFriendlyDeaths = 0;
        TotalFriendlyUnitsReachedEnemyBase = 0;
    }

    public void RecordFriendlyDeath() { TotalFriendlyDeaths++; }
    public void RecordFriendlyUnitReachedEnemyBase() { TotalFriendlyUnitsReachedEnemyBase++; }

    [ContextMenu("Log Friendly Unit Diagnostics")]
    public void LogDiagnostics()
    {
        Debug.Log($"Friendly diagnostics - successful purchases: {TotalSuccessfulPurchases}, " +
            $"spawned total: {TotalFriendlyUnitsSpawned}, active: {ActiveFriendlyCount}, " +
            $"deaths: {TotalFriendlyDeaths}, completed at enemy base: {TotalFriendlyUnitsReachedEnemyBase}.", this);
    }

    void Send(GameObject prefab, UnitArchetype archetype, int cost, int incomeBonus)
    {
        if (!matchLoop) matchLoop = GetComponent<MatchLoopController>();
        if (!spawner) spawner = GetComponent<GoblinWaveSpawner>();
        if (!prefab || !spawner || !matchLoop || !CanPurchase(archetype)) return;
        if (!matchLoop.CanAfford(cost)) return;

        int assignedLane = nextLaneIndex;
        GameObject spawned = spawner.SpawnFriendly(prefab, archetype, assignedLane,
            TotalSuccessfulPurchases + 1);
        if (!spawned)
        {
            Debug.LogWarning($"{archetype} spawn failed; purchase was rejected and no Gold was charged.", this);
            return;
        }
        if (!matchLoop.TrySpend(cost))
        {
            Destroy(spawned);
            Debug.LogWarning($"{archetype} purchase could not be committed; spawned unit was removed without charging.", this);
            return;
        }

        nextLaneIndex = (nextLaneIndex + 1) % 3;
        TotalFriendlyUnitsSpawned++;
        TotalSuccessfulPurchases++;
        matchLoop.AddIncome(incomeBonus);
    }

    GameObject PrefabFor(UnitArchetype archetype) => archetype == UnitArchetype.GreenArcher ? greenArcherPrefab :
        archetype == UnitArchetype.HolyKnight ? holyKnightPrefab : blueWizardPrefab;
}
