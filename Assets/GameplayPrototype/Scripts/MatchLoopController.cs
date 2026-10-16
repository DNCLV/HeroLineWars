using System.Collections;
using UnityEngine;

public enum MatchResult { None, Victory, Defeat }

/// <summary>Owns match-only currency, payout timing, base health, and match lifecycle.</summary>
[DefaultExecutionOrder(-100)]
public sealed class MatchLoopController : MonoBehaviour
{
    [Header("Match economy")]
    [Min(0)] public int startingGold = 5;
    [Min(0)] public int startingIncome = 1;
    [Min(.1f)] public float payoutInterval = 10f;

    [Header("Bases")]
    [Min(1)] public int startingBaseHealth = 100;

    public int Gold { get; private set; }
    public int Income { get; private set; }
    public int BlueBaseHealth { get; private set; }
    public int RedBaseHealth { get; private set; }
    public float PayoutCountdown => Mathf.Max(0f, payoutCountdown);
    public MatchResult Result { get; private set; }
    public bool IsRunning => Result == MatchResult.None && isActiveAndEnabled;
    public int MatchNumber { get; private set; }
    public static MatchLoopController Current { get; private set; }

    GoblinWaveSpawner spawner;
    FriendlyUnitSender sender;
    float payoutCountdown;
    bool restarting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState() { Current = null; }

    void Awake()
    {
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

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Update()
    {
        if (!IsRunning) return;
        payoutCountdown -= Time.deltaTime;
        if (payoutCountdown > 0f) return;
        Gold = SafeAdd(Gold, Income);
        payoutCountdown = Mathf.Max(.1f, payoutInterval);
    }

    public bool CanAfford(int cost) => IsRunning && cost >= 0 && Gold >= cost;

    public bool TrySpend(int cost)
    {
        if (!CanAfford(cost)) return false;
        Gold -= cost;
        return true;
    }

    public void Refund(int amount)
    {
        if (amount > 0) Gold = SafeAdd(Gold, amount);
    }

    public void AddIncome(int amount)
    {
        if (IsRunning && amount > 0) Income = SafeAdd(Income, amount);
    }

    public void RewardEnemyKill(UnitTeam victimTeam, UnitTeam attackerTeam)
    {
        if (IsRunning && victimTeam == UnitTeam.Enemy && attackerTeam == UnitTeam.Friendly)
            Gold = SafeAdd(Gold, 1);
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

    public void RestartMatch()
    {
        if (IsRunning || restarting) return;
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
        Gold = Mathf.Max(0, startingGold);
        Income = Mathf.Max(0, startingIncome);
        BlueBaseHealth = Mathf.Max(1, startingBaseHealth);
        RedBaseHealth = Mathf.Max(1, startingBaseHealth);
        payoutCountdown = Mathf.Max(.1f, payoutInterval);
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
}
