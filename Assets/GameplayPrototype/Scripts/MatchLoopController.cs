using System.Collections;
using UnityEngine;

public enum MatchResult { None, Victory, Defeat, SurvivalVictory }

/// <summary>Owns match-only currency, payout timing, base health, and match lifecycle.</summary>
[DefaultExecutionOrder(-100)]
public sealed class MatchLoopController : MonoBehaviour
{
    [Header("Match economy")]
    [Min(0)] public int startingGold = 14;
    [Min(0)] public int startingIncome = 1;
    [Min(.1f)] public float payoutInterval = 10f;
    [Tooltip("Each cumulative block of Gold spent on successful allied purchases grants +1 Income.")]
    [Min(1)] public int goldSpentPerIncome = 5;
    [Header("Kill rewards (XP is unaffected)")]
    [Min(0)] public int goblinKillGold;
    [Min(0)] public int ghoulKillGold;
    [Min(0)] public int skeletonKingKillGold;
    [Min(0)] public int undeadDragonKillGold;
    [Tooltip("Editor/development-build event logs for payouts, purchases, and kill rewards.")]
    public bool enableEconomyDiagnostics;

    [Header("Bases")]
    [Min(1)] public int startingBaseHealth = 100;

    [Header("Survival mode")]
    [Range(30f, 420f)] public float survivalDuration = 420f;

    [Header("Combat balance V1")]
    [Min(0f)] public float playerBasicAttackMultiplier = 1f;
    [Min(0f)] public float playerAreaDamageMultiplier = 1f;

    [Header("Development match report")]
    public bool enableMatchReport;

    public int Gold { get; private set; }
    public int Income { get; private set; }
    public int BlueBaseHealth { get; private set; }
    public int RedBaseHealth { get; private set; }
    public float PayoutCountdown => Mathf.Max(0f, payoutCountdown);
    public float ElapsedTime => elapsedTime;
    public float SurvivalDuration => Mathf.Clamp(survivalDuration, 30f, 420f);
    public float RemainingSurvivalTime => Mathf.Max(0f, SurvivalDuration - elapsedTime);
    public MatchResult Result { get; private set; }
    public bool IsRunning => Result == MatchResult.None && isActiveAndEnabled;
    public int MatchNumber { get; private set; }
    public int TotalGoldSpent { get; private set; }
    public int TotalItemGoldSpent { get; private set; }
    public int IncomeBonusesGranted { get; private set; }
    public int AiGold { get; private set; }
    public int AiIncome { get; private set; }
    public int AiGoldSpent { get; private set; }
    public int AiPurchases { get; private set; }
    public int AiKillGoldEarned { get; private set; }
    public int AiIncomeGoldEarned { get; private set; }
    public int IncomeAt60Seconds { get; private set; } = -1;
    public int IncomeAt120Seconds { get; private set; } = -1;
    public int IncomeAt300Seconds { get; private set; } = -1;
    public int FriendlyMobDeaths { get; private set; }
    public int EnemyMobDeaths { get; private set; }
    public int TotalIncomeGoldEarned { get; private set; }
    public int TotalKillGoldEarned { get; private set; }
    public int PlayerHeroKills { get; private set; }
    public int PlayerHeroDeaths { get; private set; }
    public int PlayerHeroDamageDealt { get; private set; }
    public int EnemyHeroBasicHits { get; private set; }
    public int EnemyHeroBasicAttempts { get; private set; }
    public int EnemyHeroDamageDealt { get; private set; }
    public int PlayerHeroDamageReceived { get; private set; }
    public int EnemyHeroDamageReceived { get; private set; }
    public string PlayerHeroDeathCause { get; private set; }
    public string EnemyHeroDeathCause { get; private set; }
    public float EnemyHeroDeathTime { get; private set; }
    public float EnemyHeroTimeToKill { get; private set; }
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
        if (economyBalanceRevision >= 3) return;
        startingGold = 14;
        startingIncome = 1;
        payoutInterval = 10f;
        goldSpentPerIncome = 5;
        goblinKillGold = ghoulKillGold = skeletonKingKillGold = undeadDragonKillGold = 0;
        survivalDuration = 420f;
        economyBalanceRevision = 3;
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
            AiGold = SafeAdd(AiGold, AiIncome);
            AiIncomeGoldEarned = SafeAdd(AiIncomeGoldEarned, AiIncome);
            TotalIncomeGoldEarned = SafeAdd(TotalIncomeGoldEarned, Income);
            payoutCountdown += interval;
            LogEconomy($"Income +{Income}; Gold={Gold}, Income={Income}, elapsed={elapsedTime:F1}s.");
        }
        if (IncomeAt60Seconds < 0 && elapsedTime >= 60f) IncomeAt60Seconds = Income;
        if (IncomeAt120Seconds < 0 && elapsedTime >= 120f) IncomeAt120Seconds = Income;
        if (IncomeAt300Seconds < 0 && elapsedTime >= 300f) IncomeAt300Seconds = Income;
    }

    public bool CanAfford(int cost) => IsRunning && cost >= 0 && Gold >= cost;

    public bool TrySpendGoldForItem(int cost)
    {
        if (cost <= 0 || !CanAfford(cost)) return false;
        Gold -= cost;
        TotalItemGoldSpent = SafeAdd(TotalItemGoldSpent, cost);
        LogEconomy($"Item purchase -{cost}; Gold={Gold}, Income={Income}.");
        return true;
    }
    public bool CanAiAfford(int cost) => IsRunning && cost >= 0 && AiGold >= cost;

    public bool TryCommitAiPurchase(int cost, UnitArchetype archetype)
    {
        if (!CanAiAfford(cost)) return false;
        AiGold -= cost;
        AiGoldSpent = SafeAdd(AiGoldSpent, cost);
        AiIncome = SafeAdd(Mathf.Max(0, startingIncome), AiGoldSpent / Mathf.Max(1, goldSpentPerIncome));
        AiPurchases++;
        LogEconomy($"AI purchased {archetype} for {cost}; Gold={AiGold}, Income={AiIncome}, spent={AiGoldSpent}.");
        return true;
    }

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
        if (!IsRunning || victimTeam == attackerTeam) return;
        int reward = KillReward(archetype);
        if (reward > 0)
        {
            if (attackerTeam == UnitTeam.Friendly)
            {
                Gold = SafeAdd(Gold, reward);
                TotalKillGoldEarned = SafeAdd(TotalKillGoldEarned, reward);
            }
            else
            {
                AiGold = SafeAdd(AiGold, reward);
                AiKillGoldEarned = SafeAdd(AiKillGoldEarned, reward);
            }
        }
        LogEconomy($"{attackerTeam} kill {archetype} +{reward}; player/AI Gold={Gold}/{AiGold}.");
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
        if (playerHero) PlayerHeroDeaths = Mathf.Max(1, PlayerHeroDeaths);
        else if (string.IsNullOrEmpty(EnemyHeroDeathCause))
        {
            EnemyHeroDeathCause = "unattributed damage";
            EnemyHeroDeathTime = elapsedTime;
        }
        EndMatch(playerHero ? MatchResult.Defeat : MatchResult.Victory);
    }

    public int ScalePlayerBasicDamage(int baseDamage) =>
        Mathf.Max(1, Mathf.RoundToInt(baseDamage * Mathf.Max(0f, playerBasicAttackMultiplier)));

    public int ScalePlayerAreaDamage(int baseDamage) =>
        Mathf.Max(1, Mathf.RoundToInt(baseDamage * Mathf.Max(0f, playerAreaDamageMultiplier)));

    public void RecordCombatDamage(Transform attacker, GoblinHealth victim, int damage, bool lethal)
    {
        if (!IsRunning || !victim || damage <= 0) return;
        HeroProgression attackerHero = attacker ? attacker.GetComponent<HeroProgression>() : null;
        HeroProgression victimHero = victim.GetComponent<HeroProgression>();
        if (victimHero)
        {
            if (victimHero.IsPlayerHero) PlayerHeroDamageReceived = SafeAdd(PlayerHeroDamageReceived, damage);
            else EnemyHeroDamageReceived = SafeAdd(EnemyHeroDamageReceived, damage);
        }
        if (attackerHero && attackerHero.IsPlayerHero)
        {
            PlayerHeroDamageDealt = SafeAdd(PlayerHeroDamageDealt, damage);
            if (lethal) PlayerHeroKills++;
        }
        else if (attackerHero && !attackerHero.IsPlayerHero)
            EnemyHeroDamageDealt = SafeAdd(EnemyHeroDamageDealt, damage);
        if (!lethal || !victimHero) return;
        if (victimHero.IsPlayerHero)
        {
            PlayerHeroDeaths++;
            PlayerHeroDeathCause = attacker ? attacker.name : "unattributed damage";
        }
        else
        {
            EnemyHeroDeathCause = attacker ? attacker.name : "unattributed damage";
            EnemyHeroDeathTime = elapsedTime;
            EnemyHeroTimeToKill = float.IsNegativeInfinity(victim.FirstDamageTime)
                ? -1f : Time.time - victim.FirstDamageTime;
        }
    }

    public void RecordEnemyHeroBasicHit() { if (IsRunning) EnemyHeroBasicHits++; }
    public void RecordEnemyHeroBasicAttempt() { if (IsRunning) EnemyHeroBasicAttempts++; }
    public void RecordMobDeath(UnitTeam victimTeam)
    {
        if (!IsRunning) return;
        if (victimTeam == UnitTeam.Friendly) FriendlyMobDeaths++;
        else EnemyMobDeaths++;
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
        TotalItemGoldSpent = 0;
        IncomeBonusesGranted = 0;
        TotalIncomeGoldEarned = 0;
        TotalKillGoldEarned = 0;
        PlayerHeroKills = PlayerHeroDeaths = PlayerHeroDamageDealt = 0;
        EnemyHeroBasicAttempts = EnemyHeroBasicHits = EnemyHeroDamageDealt = 0;
        PlayerHeroDamageReceived = EnemyHeroDamageReceived = 0;
        PlayerHeroDeathCause = string.Empty;
        AiGold = Mathf.Max(0, startingGold);
        AiIncome = Mathf.Max(0, startingIncome);
        AiGoldSpent = AiPurchases = 0;
        AiKillGoldEarned = 0;
        AiIncomeGoldEarned = 0;
        IncomeAt60Seconds = IncomeAt120Seconds = IncomeAt300Seconds = -1;
        FriendlyMobDeaths = EnemyMobDeaths = 0;
        EnemyHeroDeathCause = string.Empty;
        EnemyHeroDeathTime = -1f;
        EnemyHeroTimeToKill = -1f;
        Result = MatchResult.None;
    }

    void EndMatch(MatchResult result)
    {
        if (!IsRunning) return;
        Result = result;
        LogMatchReport();
        if (spawner) spawner.StopWaveSpawning();
        LaneUnitCombat[] units = GetComponentsInChildren<LaneUnitCombat>(true);
        foreach (LaneUnitCombat unit in units) if (unit) unit.enabled = false;
        HeroAutoAttack[] attacks = GetComponentsInChildren<HeroAutoAttack>(true);
        foreach (HeroAutoAttack attack in attacks) if (attack) attack.enabled = false;
        FireballAbility[] abilities = GetComponentsInChildren<FireballAbility>(true);
        foreach (FireballAbility ability in abilities) if (ability) ability.enabled = false;
        GeomancerAbilityController[] geomancerAbilities = GetComponentsInChildren<GeomancerAbilityController>(true);
        foreach (GeomancerAbilityController ability in geomancerAbilities) if (ability) ability.enabled = false;
        HeroLaneController[] heroes = GetComponentsInChildren<HeroLaneController>(true);
        foreach (HeroLaneController hero in heroes) if (hero) hero.enabled = false;
    }

    static int SafeAdd(int current, int amount) => (int)System.Math.Min(int.MaxValue, (long)current + amount);

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEBUG")]
    void LogMatchReport()
    {
        if (!enableMatchReport) return;
        EnemyHeroAiController ai = GetComponentInChildren<EnemyHeroAiController>();
        Debug.Log($"[Match V3] duration={elapsedTime:F1}s winner={Result}, player/AI purchases={(sender ? sender.TotalSuccessfulPurchases : 0)}/{AiPurchases}, " +
            $"player Gold earned={TotalIncomeGoldEarned + TotalKillGoldEarned}, units/items spent={TotalGoldSpent}/{TotalItemGoldSpent}, Income={Income}; " +
            $"AI Gold={AiGold}, earned={AiIncomeGoldEarned + AiKillGoldEarned}, spent={AiGoldSpent}, Income={AiIncome}; " +
            $"Income at 60/120/300s={IncomeAt60Seconds}/{IncomeAt120Seconds}/{IncomeAt300Seconds}, " +
            $"mob deaths friendly/enemy={FriendlyMobDeaths}/{EnemyMobDeaths}; " +
            $"player kills/deaths={PlayerHeroKills}/{PlayerHeroDeaths}, damage dealt/received={PlayerHeroDamageDealt}/{PlayerHeroDamageReceived}, death={PlayerHeroDeathCause}; " +
            $"AI basic attempts/hits={EnemyHeroBasicAttempts}/{EnemyHeroBasicHits}, damage dealt/received={EnemyHeroDamageDealt}/{EnemyHeroDamageReceived}, " +
            $"abilities/hits/retreats/stat upgrades/ability upgrades={(ai ? ai.AbilitiesCast : 0)}/{(ai ? ai.AbilityHits : 0)}/{(ai ? ai.Retreats : 0)}/{(ai ? ai.StatUpgrades : 0)}/{(ai ? ai.AbilityUpgrades : 0)}, " +
            $"enemy hero death={EnemyHeroDeathCause} at {EnemyHeroDeathTime:F1}s, " +
            $"time-to-kill={EnemyHeroTimeToKill:F1}s.", this);
    }

    int KillReward(UnitArchetype archetype)
    {
        if (archetype == UnitArchetype.Battlemage) return 0;
        if (archetype == UnitArchetype.Ghoul) return ghoulKillGold;
        if (archetype == UnitArchetype.SkeletonKing) return skeletonKingKillGold;
        if (archetype == UnitArchetype.UndeadDragon) return undeadDragonKillGold;
        return goblinKillGold;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEBUG")]
    void LogEconomy(string message)
    {
        if (enableEconomyDiagnostics) Debug.Log($"[Economy v2] {message}", this);
    }
}
