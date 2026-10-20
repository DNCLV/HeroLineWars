using System.Collections.Generic;
using UnityEngine;

/// <summary>Single-player decisions; combat, legal purchases and level progression remain in existing systems.</summary>
public sealed class EnemyHeroAiController : MonoBehaviour
{
    [System.Serializable]
    public struct DifficultySettings
    {
        [Min(.2f)] public float decisionInterval;
        [Range(.1f, 1f)] public float retreatHealthFraction;
        [Range(.2f, 1f)] public float resumeHealthFraction;
        [Min(0)] public int minimumAreaTargets;
        [Min(0)] public int reserveGold;
        [Min(0f)] public float purchaseInterval;
        public DifficultySettings(float interval, float retreat, float resume, int area, int reserve, float purchase)
        { decisionInterval = interval; retreatHealthFraction = retreat; resumeHealthFraction = resume;
          minimumAreaTargets = area; reserveGold = reserve; purchaseInterval = purchase; }
    }

    public HlwAiDifficulty difficulty = HlwAiDifficulty.Normal;
    public DifficultySettings easy = new DifficultySettings(1.25f, .22f, .65f, 4, 5, 4f);
    public DifficultySettings normal = new DifficultySettings(.6f, .30f, .72f, 3, 2, 1.5f);
    public DifficultySettings hard = new DifficultySettings(.35f, .37f, .78f, 2, 1, 1.1f);
    public DifficultySettings nightmare = new DifficultySettings(.22f, .43f, .85f, 2, 0, .8f);
    [Header("Legal purchase strategy")]
    [Min(1f)] public float maximumSavingSeconds = 24f;
    [Min(1)] public int emergencyThreatCount = 4;
    [Min(1f)] public float emergencyThreatRadius = 12f;
    public bool enableDiagnostics;

    public HeroProgression Progression { get; private set; }
    public int Retreats { get; private set; }
    public int StatUpgrades { get; private set; }
    public int AbilityUpgrades { get; private set; }
    public int AbilitiesCast { get; private set; }
    public int AbilityHits { get; private set; }
    public void RecordAbilityHits(int hits) { AbilityHits += Mathf.Max(0, hits); }

    readonly List<LaneUnitCombat> opponents = new List<LaneUnitCombat>(48);
    MatchLoopController match;
    GoblinWaveSpawner spawner;
    FriendlyUnitSender sender;
    HlwBattleDefinition battle;
    LaneUnitCombat combat;
    GoblinHealth health;
    HeroAbilityLoadout loadout;
    FireballAbility fireball;
    FireSlashAbility fireSlash;
    GeomancerAbilityController geomancer;
    float nextDecision;
    float nextPurchase;
    bool retreating;
    int purchaseSequence;
    UnitDefinition savingFor;
    float savingDeadline;
    float nextPurchaseDiagnostic;
    int nextTelemetryIndex;
    int mostExpensivePurchase;
    readonly int[] purchasedByTier = new int[4];
    float initialBasicMultiplier;
    float initialAttackSpeedMultiplier;
    int initialCombatDamage;
    float initialCombatInterval;

    DifficultySettings Settings => difficulty == HlwAiDifficulty.Easy ? easy :
        difficulty == HlwAiDifficulty.Hard ? hard :
        difficulty == HlwAiDifficulty.Nightmare ? nightmare : normal;

    public void Configure(GoblinWaveSpawner owner, MatchLoopController game, HlwBattleDefinition definition)
    {
        spawner = owner;
        match = game;
        battle = definition;
        sender = owner ? owner.GetComponent<FriendlyUnitSender>() : null;
        difficulty = definition ? definition.difficulty : HlwAiDifficulty.Normal;
        if (definition)
        {
            easy = definition.easy;
            normal = definition.normal;
            hard = definition.hard;
            nightmare = definition.nightmare;
        }
        Progression = GetComponent<HeroProgression>();
        combat = GetComponent<LaneUnitCombat>();
        health = GetComponent<GoblinHealth>();
        loadout = GetComponent<HeroAbilityLoadout>();
        fireball = GetComponent<FireballAbility>();
        fireSlash = GetComponent<FireSlashAbility>();
        geomancer = GetComponent<GeomancerAbilityController>();
        if (Progression && combat)
        {
            initialBasicMultiplier = Progression.BasicAttackDamageMultiplier;
            initialAttackSpeedMultiplier = Progression.AttackSpeedMultiplier;
            initialCombatDamage = combat.attackDamage;
            initialCombatInterval = combat.attackInterval;
        }
    }

    void Update()
    {
        if (!match || !match.IsRunning || !combat || combat.IsDead || !health) return;
        LogMilestoneIfDue();
        if (Time.time < nextDecision) return;
        DifficultySettings settings = Settings;
        nextDecision = Time.time + Mathf.Max(.2f, settings.decisionInterval);
        if (retreating)
        {
            if (health.CurrentHitPoints >= health.MaximumHitPoints * settings.resumeHealthFraction)
            {
                retreating = false;
                combat.SetRetreating(false);
            }
        }
        else if (health.CurrentHitPoints <= health.MaximumHitPoints * settings.retreatHealthFraction)
        {
            retreating = true;
            Retreats++;
            combat.SetRetreating(true);
            Log("Retreating to well");
        }
        SpendSkillPoints();
        UpdateCombatStats();
        if (!retreating)
        {
            SelectThreat(settings);
            TryAbility(settings);
        }
        if (Time.time >= nextPurchase) TryPurchase(settings);
    }

    void SelectThreat(DifficultySettings settings)
    {
        if (!spawner || !spawner.lane) return;
        UnitCombatRegistry.CopyOpponents(opponents, UnitTeam.Enemy, spawner.lane);
        LaneUnitCombat best = null;
        float bestScore = float.NegativeInfinity;
        foreach (LaneUnitCombat unit in opponents)
        {
            if (!unit || unit.IsDead || unit.assignedLane != combat.assignedLane) continue;
            float distance = Vector3.Distance(transform.position, unit.transform.position);
            if (distance > combat.engagementRange) continue;
            float score = -distance;
            if (difficulty != HlwAiDifficulty.Easy)
            {
                // Use the definition's real damage and range instead of naming only starter mobs.
                UnitDefinition definition = unit.Faction ? UnitRoster.Find(unit.Faction.archetype) : null;
                if (definition)
                    score += Mathf.Min(4f, definition.baseDamage /
                        Mathf.Max(1f, definition.attackInterval * 30f));
                if (unit.attackRange > 2f) score += 1f;
                Vector3 local = spawner.lane.transform.InverseTransformPoint(unit.transform.position);
                score += local.z / Mathf.Max(1f, spawner.lane.LengthWorld) * 2f;
            }
            if (score > bestScore) { bestScore = score; best = unit; }
        }
        if (best) combat.SetPreferredTarget(best);
    }

    void UpdateCombatStats()
    {
        if (!Progression || !combat || initialBasicMultiplier <= 0f || initialAttackSpeedMultiplier <= 0f) return;
        combat.attackDamage = Mathf.Max(1, Mathf.RoundToInt(initialCombatDamage *
            Progression.BasicAttackDamageMultiplier / initialBasicMultiplier));
        combat.attackInterval = Mathf.Max(.15f, initialCombatInterval *
            initialAttackSpeedMultiplier / Progression.AttackSpeedMultiplier);
    }

    void SpendSkillPoints()
    {
        if (!Progression) return;
        while (Progression.UnspentSkillPoints > 0)
        {
            bool upgraded = false;
            if (loadout)
            {
                int[] priority = geomancer ? new[] { 2, 1 } : new[] { 1, 2 };
                foreach (int slot in priority)
                    if (loadout.TryUpgrade(slot, Progression)) { AbilityUpgrades++; upgraded = true; break; }
            }
            if (!upgraded)
            {
                HeroAttribute attribute = geomancer ? HeroAttribute.Stamina : HeroAttribute.Intelligence;
                if (!Progression.TryUpgradeAttribute(attribute)) break;
                StatUpgrades++;
            }
        }
    }

    void TryAbility(DifficultySettings settings)
    {
        if (!spawner || !spawner.lane) return;
        UnitCombatRegistry.CopyOpponents(opponents, UnitTeam.Enemy, spawner.lane);
        LaneUnitCombat best = null;
        float radius = geomancer ? geomancer.earthSpikesRange :
            Mathf.Max(fireball ? fireball.abilityRange : 0f, fireSlash ? fireSlash.aiTargetingRange : 0f);
        float areaRadius = geomancer ? 2.5f : fireball ? fireball.areaRadius : 0f;
        int nearby = 0;
        float bestScore = float.NegativeInfinity;
        foreach (LaneUnitCombat unit in opponents)
        {
            if (!unit || unit.IsDead || unit.assignedLane != combat.assignedLane) continue;
            float distance = Vector3.Distance(transform.position, unit.transform.position);
            if (distance > radius) continue;
            int groupSize = 0;
            foreach (LaneUnitCombat other in opponents)
                if (other && !other.IsDead && other.assignedLane == combat.assignedLane &&
                    (other.transform.position - unit.transform.position).sqrMagnitude <= areaRadius * areaRadius)
                    groupSize++;
            float score = groupSize * 10f - distance;
            if (score > bestScore) { best = unit; bestScore = score; nearby = groupSize; }
        }
        bool dangerous = best && (best.attackDamage >= combat.attackDamage ||
            best.maximumHitPoints >= combat.maximumHitPoints);
        if (!best || (nearby < Mathf.Max(1, settings.minimumAreaTargets) &&
            !(dangerous && geomancer))) return;
        bool cast = false;
        if (geomancer) cast = geomancer.TryCastAiAt(best.transform.position, nearby);
        else
        {
            if (fireSlash && nearby >= Mathf.Max(2, settings.minimumAreaTargets))
                cast = fireSlash.TryCastAt(best.transform.position);
            if (!cast && fireball) cast = fireball.TryCastAt(best.transform.position);
        }
        if (cast) { AbilitiesCast++; Log($"Ability cast at {best.name}, targets in range={nearby}"); }
    }

    void TryPurchase(DifficultySettings settings)
    {
        nextPurchase = Time.time + Mathf.Max(.2f, settings.purchaseInterval);
        if (!sender || !spawner || !match) return;
        if (UnitRoster.All.Count == 0)
        {
            LogPurchaseState("roster missing; run Import 16 Unit Roster", 0);
            return;
        }
        int budget = match.AiGold - settings.reserveGold;
        int preferredTier = match.AiIncome < 20 ? 0 : match.AiIncome < 50 ? 1 :
            match.AiIncome < 100 ? 2 : 3;
        bool emergency = HasDefensiveEmergency();
        UnitDefinition selected = null;
        UnitDefinition upgrade = null;
        int available = 0;
        foreach (UnitDefinition unit in UnitRoster.All)
        {
            if (!unit || !Allowed(unit.archetype) || !sender.PrefabFor(unit.archetype)) continue;
            available++;
            int tier = (int)unit.tier;
            if (!emergency && tier == preferredTier && unit.cost > budget &&
                unit.cost <= match.AiGold + Mathf.Max(1, match.AiIncome) *
                    (difficulty == HlwAiDifficulty.Easy ? 1 : 2) &&
                (!upgrade || unit.cost < upgrade.cost)) upgrade = unit;
            if (unit.cost > budget)
                continue;
            if (emergency && tier > 1) continue;
            if (!emergency && tier > preferredTier) continue;
            if (!selected || PurchaseScore(unit, preferredTier, emergency) >
                PurchaseScore(selected, preferredTier, emergency)) selected = unit;
        }
        if (upgrade && selected && difficulty != HlwAiDifficulty.Easy &&
            (int)selected.tier < preferredTier && purchaseSequence % 3 != 0)
        {
            if (savingFor != upgrade)
            {
                savingFor = upgrade;
                savingDeadline = Time.time + Mathf.Min(maximumSavingSeconds,
                    10f * Mathf.Ceil((upgrade.cost - match.AiGold) /
                        (float)Mathf.Max(1, match.AiIncome)) + 2f);
            }
            if (Time.time < savingDeadline)
            {
                LogPurchaseState($"saving for {upgrade.displayName} ({upgrade.cost}), affordable={selected.displayName}", available);
                return;
            }
        }
        savingFor = null;
        if (!selected)
        {
            LogPurchaseState($"no affordable allowed unit; preferred tier={preferredTier}, emergency={emergency}", available);
            return;
        }
        int cost = selected.cost;
        if (!match.CanAiAfford(cost + settings.reserveGold))
        {
            LogPurchaseState($"insufficient Gold for {selected.displayName}", available);
            return;
        }
        GameObject prefab = sender.PrefabFor(selected.archetype);
        GameObject spawned = spawner.SpawnEnemyPurchase(prefab, selected.archetype, purchaseSequence + 1);
        if (!spawned) { LogPurchaseState($"spawn failed for {selected.displayName}", available); return; }
        if (!match.TryCommitAiPurchase(cost, selected.archetype))
        { Destroy(spawned); LogPurchaseState($"commit failed for {selected.displayName}", available); return; }
        purchaseSequence++;
        purchasedByTier[(int)selected.tier]++;
        mostExpensivePurchase = Mathf.Max(mostExpensivePurchase, cost);
        Log($"Purchased {selected.displayName} for {cost}; Gold={match.AiGold}, Income={match.AiIncome}, roster={available}");
    }

    float PurchaseScore(UnitDefinition unit, int preferredTier, bool emergency)
    {
        if (emergency) return -unit.cost + (unit.attackType == UnitAttackType.Ranged ? 1f : 0f);
        float tier = difficulty == HlwAiDifficulty.Easy ? -(int)unit.tier * 8f :
            (int)unit.tier * (difficulty == HlwAiDifficulty.Normal ? 30f : 40f);
        float role = unit.attackType == UnitAttackType.Ranged && purchaseSequence % 3 == 1 ? 4f : 0f;
        float value = (unit.baseDamage + Mathf.Sqrt(unit.maxHP)) / Mathf.Max(1, unit.cost);
        float costPreference = purchaseSequence % 3 == 0 ? -unit.cost * .1f : unit.cost * .12f;
        int tierStart = unit.tier == UnitTier.Starter ? 0 : unit.tier == UnitTier.Nature ? 3 :
            unit.tier == UnitTier.Goblins ? 7 : 11;
        int tierCount = unit.tier == UnitTier.Starter ? 3 : unit.tier == UnitTier.Undead ? 5 : 4;
        float variety = (unit.order - tierStart) == purchaseSequence % tierCount ? 12f : 0f;
        return tier + role + value + costPreference + variety -
            Mathf.Abs(preferredTier - (int)unit.tier) * 3f;
    }

    bool HasDefensiveEmergency()
    {
        if (!spawner || !spawner.lane) return false;
        UnitCombatRegistry.CopyOpponents(opponents, UnitTeam.Enemy, spawner.lane);
        int nearby = 0;
        float radiusSquared = emergencyThreatRadius * emergencyThreatRadius;
        foreach (LaneUnitCombat unit in opponents)
            if (unit && !unit.IsDead && unit.assignedLane == combat.assignedLane &&
                (unit.transform.position - transform.position).sqrMagnitude <= radiusSquared &&
                ++nearby >= emergencyThreatCount) return true;
        return false;
    }

    void LogPurchaseState(string reason, int available)
    {
        if (!enableDiagnostics || Time.time < nextPurchaseDiagnostic) return;
        nextPurchaseDiagnostic = Time.time + 10f;
        Log($"Gold={match.AiGold}, Income={match.AiIncome}, roster={UnitRoster.All.Count}, available={available}: {reason}");
    }

    void LogMilestoneIfDue()
    {
        if (!enableDiagnostics || nextTelemetryIndex >= 4) return;
        int[] marks = { 60, 120, 180, 300 };
        if (match.ElapsedTime < marks[nextTelemetryIndex]) return;
        Log($"t={marks[nextTelemetryIndex]}s Gold={match.AiGold} Income={match.AiIncome} " +
            $"purchases Starter/Nature/Goblins/Undead={purchasedByTier[0]}/{purchasedByTier[1]}/{purchasedByTier[2]}/{purchasedByTier[3]} " +
            $"maxCost={mostExpensivePurchase} heroLv={(Progression ? Progression.Level : 0)} " +
            $"upgrades stat/ability={StatUpgrades}/{AbilityUpgrades} AA hits={match.EnemyHeroBasicHits} " +
            $"casts={AbilitiesCast} retreats={Retreats}");
        nextTelemetryIndex++;
    }

    bool Allowed(UnitArchetype archetype)
    {
        if (!battle || battle.allowedUnits == null) return true;
        foreach (UnitArchetype allowed in battle.allowedUnits) if (allowed == archetype) return true;
        return false;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEBUG")]
    void Log(string message) { if (enableDiagnostics) Debug.Log($"[Enemy AI] {message}", this); }
}
