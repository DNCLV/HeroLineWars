using System.Collections;
using UnityEngine;

public enum MatchResult { None, Victory, Defeat, SurvivalVictory }

/// <summary>Owns match-only currency, payout timing, base health, and match lifecycle.</summary>
[DefaultExecutionOrder(-100)]
public sealed class MatchLoopController : MonoBehaviour
{
    [Header("Match economy")]
    [Min(0)] public int startingGold = 8;
    [Min(0)] public int startingIncome = 1;
    [Min(.1f)] public float payoutInterval = 10f;
    [Tooltip("Each cumulative block of Gold spent on successful allied purchases grants +1 Income.")]
    [Min(1)] public int goldSpentPerIncome = 2;
    [Tooltip("Editor/development-build event logs for payouts, purchases, and kill rewards.")]
    public bool enableEconomyDiagnostics;

    [Header("Bases")]
    [Min(1)] public int startingBaseHealth = 100;

    [Header("Survival mode")]
    [Range(30f, 360f)] public float survivalDuration = 360f;

    public int Gold { get; private set; }
    public int Income { get; private set; }
    public int BlueBaseHealth { get; private set; }
    public int RedBaseHealth { get; private set; }
    public float PayoutCountdown => Mathf.Max(0f, payoutCountdown);
    public float ElapsedTime => elapsedTime;
    public float SurvivalDuration => Mathf.Clamp(survivalDuration, 30f, 360f);
    public float RemainingSurvivalTime => Mathf.Max(0f, SurvivalDuration - elapsedTime);
    public MatchResult Result { get; private set; }
    public bool IsRunning => Result == MatchResult.None && isActiveAndEnabled;
    public int MatchNumber { get; private set; }
    public int TotalGoldSpent { get; private set; }
    public int IncomeBonusesGranted { get; private set; }
    public int TotalIncomeGoldEarned { get; private set; }
    public int TotalKillGoldEarned { get; private set; }
    public static MatchLoopController Current { get; private set; }

    GoblinWaveSpawner spawner;
    FriendlyUnitSender sender;
    float payoutCountdown;
    float elapsedTime;
    bool restarting;
    [SerializeField, HideInInspector] int economyBalanceRevision;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState() { Current = null; }

    void Awake()
    {
        UpgradeEconomyBalance();
        if (Current && Current != this)
        {
            Debug.LogError("Multiple MatchLoopControllers are active. Keep one under GameplayPrototype.", this);
            enabled = false;
            return;
        }
        Current = this;
        spawner = GetComponent<GoblinWaveSpawner>();
        sender = GetComponent<FriendlyUnitSender>();
        MatchNumber = 1;
        ResetMatchValues();
    }

    public void UpgradeEconomyBalance()
    {
        if (economyBalanceRevision >= 1) return;
        startingGold = 8;
        startingIncome = 1;
        payoutInterval = 10f;
        goldSpentPerIncome = 2;
        economyBalanceRevision = 1;
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Update()
    {
        if (!IsRunning) return;
        elapsedTime = Mathf.Min(SurvivalDuration, elapsedTime + Time.deltaTime);
        if (elapsedTime >= SurvivalDuration)
        {
            EndMatch(MatchResult.SurvivalVictory);
            return;
        }
        payoutCountdown -= Time.deltaTime;
        float interval = Mathf.Max(.1f, payoutInterval);
        while (payoutCountdown <= 0f && IsRunning)
        {
            Gold = SafeAdd(Gold, Income);
            TotalIncomeGoldEarned = SafeAdd(TotalIncomeGoldEarned, Income);
            payoutCountdown += interval;
            LogEconomy($"Income +{Income}; Gold={Gold}, Income={Income}, elapsed={elapsedTime:F1}s.");
        }
    }

    public bool CanAfford(int cost) => IsRunning && cost >= 0 && Gold >= cost;

    public bool TryCommitUnitPurchase(int cost, UnitArchetype archetype)
    {
        if (!CanAfford(cost)) return false;
        Gold -= cost;
        TotalGoldSpent = SafeAdd(TotalGoldSpent, cost);
        int earnedBonuses = TotalGoldSpent / Mathf.Max(1, goldSpentPerIncome);
        int newBonuses = Mathf.Max(0, earnedBonuses - IncomeBonusesGranted);
        if (newBonuses > 0)
        {
            Income = SafeAdd(Income, newBonuses);
            IncomeBonusesGranted = earnedBonuses;
        }
        LogEconomy($"Purchased {archetype} for {cost}; Gold={Gold}, Income={Income}, " +
            $"spent={TotalGoldSpent}, income bonuses={IncomeBonusesGranted}.");
        return true;
    }

    public void RewardEnemyKill(UnitTeam victimTeam, UnitTeam attackerTeam, UnitArchetype archetype)
    {
        if (!IsRunning || victimTeam != UnitTeam.Enemy || attackerTeam != UnitTeam.Friendly) return;
        int reward = KillReward(archetype);
        if (reward > 0)
        {
            Gold = SafeAdd(Gold, reward);
            TotalKillGoldEarned = SafeAdd(TotalKillGoldEarned, reward);
        }
        LogEconomy($"Kill {archetype} +{reward}; Gold={Gold}, total kill Gold={TotalKillGoldEarned}.");
    }

    [ContextMenu("Log Economy v2 Diagnostics")]
    public void LogEconomyDiagnostics()
    {
        Debug.Log($"[Economy v2] Gold={Gold}, Income={Income}, spent={TotalGoldSpent}, " +
            $"income bonuses={IncomeBonusesGranted}, payout Gold={TotalIncomeGoldEarned}, " +
            $"kill Gold={TotalKillGoldEarned}, next payout={PayoutCountdown:F2}s.", this);
    }

    public void ReportBaseHit(UnitTeam attackingTeam)
    {
        if (!IsRunning) return;
        if (attackingTeam == UnitTeam.Friendly)
        {
            RedBaseHealth = Mathf.Max(0, RedBaseHealth - 1);
            if (RedBaseHealth == 0) EndMatch(MatchResult.Victory);
        }
        else
        {
            BlueBaseHealth = Mathf.Max(0, BlueBaseHealth - 1);
            if (BlueBaseHealth == 0) EndMatch(MatchResult.Defeat);
        }
    }

    public void ReportHeroDefeated(bool playerHero)
    {
        if (!IsRunning) return;
        EndMatch(playerHero ? MatchResult.Defeat : MatchResult.Victory);
    }

    public void RestartMatch()
    {
        if (IsRunning || restarting) return;
        HlwGameSpeed.ResetToNormal();
        restarting = true;
        if (spawner)
        {
            spawner.StopWaveSpawning();
            spawner.ClearSpawnedUnits();
        }
        StartCoroutine(RestartAfterCleanup());
    }

    IEnumerator RestartAfterCleanup()
    {
        yield return null;
        MatchNumber++;
        ResetMatchValues();
        if (sender) sender.ResetForMatch();
        if (spawner) spawner.StartMatchSpawns();
        restarting = false;
    }

    void ResetMatchValues()
    {
        HlwGameSpeed.ResetToNormal();
        Gold = Mathf.Max(0, startingGold);
        Income = Mathf.Max(0, startingIncome);
        BlueBaseHealth = Mathf.Max(1, startingBaseHealth);
        RedBaseHealth = Mathf.Max(1, startingBaseHealth);
        payoutCountdown = Mathf.Max(.1f, payoutInterval);
        elapsedTime = 0f;
        TotalGoldSpent = 0;
        IncomeBonusesGranted = 0;
        TotalIncomeGoldEarned = 0;
        TotalKillGoldEarned = 0;
        Result = MatchResult.None;
    }

    void EndMatch(MatchResult result)
    {
        if (!IsRunning) return;
        Result = result;
        if (spawner) spawner.StopWaveSpawning();
        LaneUnitCombat[] units = GetComponentsInChildren<LaneUnitCombat>(true);
        foreach (LaneUnitCombat unit in units) if (unit) unit.enabled = false;
        HeroAutoAttack[] attacks = GetComponentsInChildren<HeroAutoAttack>(true);
        foreach (HeroAutoAttack attack in attacks) if (attack) attack.enabled = false;
        FireballAbility[] abilities = GetComponentsInChildren<FireballAbility>(true);
        foreach (FireballAbility ability in abilities) if (ability) ability.enabled = false;
        HeroLaneController[] heroes = GetComponentsInChildren<HeroLaneController>(true);
        foreach (HeroLaneController hero in heroes) if (hero) hero.enabled = false;
    }

    static int SafeAdd(int current, int amount) => (int)System.Math.Min(int.MaxValue, (long)current + amount);

    static int KillReward(UnitArchetype archetype)
    {
        if (archetype == UnitArchetype.Ghoul) return 1;
        if (archetype == UnitArchetype.SkeletonKing) return 2;
        if (archetype == UnitArchetype.UndeadDragon) return 3;
        return 0;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEBUG")]
    void LogEconomy(string message)
    {
        if (enableEconomyDiagnostics) Debug.Log($"[Economy v2] {message}", this);
    }
}
