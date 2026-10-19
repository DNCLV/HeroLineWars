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
    [Min(0)] public int archerCost = 4;
    [Min(0)] public int knightCost = 2;
    [Min(0)] public int wizardCost = 6;

    public int TotalSuccessfulPurchases { get; private set; }
    public int TotalFriendlyUnitsSpawned { get; private set; }
    public int TotalFriendlyDeaths { get; private set; }
    public int TotalFriendlyUnitsReachedEnemyBase { get; private set; }
    public int TotalGoldSpent => matchLoop ? matchLoop.TotalGoldSpent : 0;
    public int IncomeBonusesGranted => matchLoop ? matchLoop.IncomeBonusesGranted : 0;
    public int ActiveFriendlyCount => UnitCombatRegistry.Count(UnitTeam.Friendly);
    [SerializeField, HideInInspector] int purchaseBalanceRevision;

    void Awake()
    {
        UpgradePurchaseBalance();
        if (!spawner) spawner = GetComponent<GoblinWaveSpawner>();
        if (!matchLoop) matchLoop = GetComponent<MatchLoopController>();
    }

    public void UpgradePurchaseBalance()
    {
        if (purchaseBalanceRevision >= 1) return;
        knightCost = 2;
        archerCost = 4;
        wizardCost = 6;
        purchaseBalanceRevision = 1;
    }

    public void SendArcher() => TrySend(UnitArchetype.GreenArcher);
    public void SendKnight() => TrySend(UnitArchetype.HolyKnight);
    public void SendWizard() => TrySend(UnitArchetype.BlueWizard);

    public bool CanPurchase(UnitArchetype archetype)
    {
        if (!matchLoop) matchLoop = GetComponent<MatchLoopController>();
        if (!spawner) spawner = GetComponent<GoblinWaveSpawner>();
        if (!matchLoop || !matchLoop.IsRunning || !spawner || !PrefabFor(archetype)) return false;
        return matchLoop.CanAfford(Cost(archetype));
    }

    public int Cost(UnitArchetype archetype)
    {
        UnitDefinition definition = UnitRoster.Find(archetype);
        if (definition) return definition.cost;
        if (archetype == UnitArchetype.GreenArcher) return archerCost;
        if (archetype == UnitArchetype.HolyKnight) return knightCost;
        if (archetype == UnitArchetype.BlueWizard) return wizardCost;
        return int.MaxValue;
    }

    public void ResetForMatch()
    {
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

    public bool TrySend(UnitArchetype archetype)
    {
        GameObject prefab = PrefabFor(archetype);
        int cost = Cost(archetype);
        if (!matchLoop) matchLoop = GetComponent<MatchLoopController>();
        if (!spawner) spawner = GetComponent<GoblinWaveSpawner>();
        if (!prefab || !spawner || !matchLoop || !CanPurchase(archetype)) return false;
        if (!matchLoop.CanAfford(cost)) return false;

        int assignedLane = spawner.AlliedDefenseLaneIndex;
        // Keep rapidly purchased units physically readable. The previous
        // implementation spawned every model at one point and movement then
        // snapped every unit onto the exact same centre line.
        int formationIndex = TotalSuccessfulPurchases;
        int column = formationIndex % 5;
        int row = (formationIndex / 5) % 3;
        int[] columnOrder = { 0, -1, 1, -2, 2 };
        float lateralOffset = columnOrder[column] * spawnSpacing;
        float longitudinalOffset = row * spawnRowSpacing;
        GameObject spawned = spawner.SpawnFriendly(prefab, archetype, assignedLane,
            TotalSuccessfulPurchases + 1, lateralOffset, longitudinalOffset);
        if (!spawned)
        {
            Debug.LogWarning($"{archetype} spawn failed; purchase was rejected and no Gold was charged.", this);
            return false;
        }
        if (!matchLoop.TryCommitUnitPurchase(cost, archetype))
        {
            Destroy(spawned);
            Debug.LogWarning($"{archetype} purchase could not be committed; spawned unit was removed without charging.", this);
            return false;
        }

        TotalFriendlyUnitsSpawned++;
        TotalSuccessfulPurchases++;
        return true;
    }

    public GameObject PrefabFor(UnitArchetype archetype)
    {
        UnitDefinition definition = UnitRoster.Find(archetype);
        if (definition && definition.modelPrefab) return definition.modelPrefab;
        if (archetype == UnitArchetype.GreenArcher) return greenArcherPrefab;
        if (archetype == UnitArchetype.HolyKnight) return holyKnightPrefab;
        if (archetype == UnitArchetype.BlueWizard) return blueWizardPrefab;
        return null;
    }
}
