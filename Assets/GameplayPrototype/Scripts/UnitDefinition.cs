using System.Collections.Generic;
using UnityEngine;

public enum UnitTier { Starter, Nature, Goblins, Undead }
public enum UnitAttackType { Melee, Ranged }

/// <summary>One purchasable unit. The prefab and portrait are generated from canonical GLBs in the Editor.</summary>
[CreateAssetMenu(menuName = "Hero Line Wars/Unit Definition")]
public sealed class UnitDefinition : ScriptableObject
{
    public UnitArchetype archetype;
    public UnitTier tier;
    [Min(0)] public int order;
    public string displayName;
    [Min(0)] public int cost;
    [Min(1)] public int maxHP;
    [Min(1)] public int baseDamage;
    [Min(0)] public int experienceReward = 12;
    public UnitAttackType attackType;
    public GameObject modelPrefab;
    public Sprite portrait;
    [Header("Existing combat cadence and movement")]
    [Min(.1f)] public float attackInterval = 1f;
    [Min(.1f)] public float attackRange = 1.5f;
    [Min(.1f)] public float movementSpeed = 2f;
    [Min(1)] public int targetsPerAttack = 1;
    [Min(0f)] public float multiTargetRadius;
    [Min(0f)] public float collisionRadius;
}

/// <summary>Cached Resources catalog; no per-frame searches or HUD-specific unit table.</summary>
public static class UnitRoster
{
    static UnitDefinition[] entries;
    static readonly Dictionary<UnitArchetype, UnitDefinition> byType = new Dictionary<UnitArchetype, UnitDefinition>();

    public static IReadOnlyList<UnitDefinition> All
    {
        get { EnsureLoaded(); return entries; }
    }

    public static UnitDefinition Find(UnitArchetype archetype)
    {
        EnsureLoaded();
        byType.TryGetValue(archetype, out UnitDefinition definition);
        return definition;
    }

    public static void Reload() { entries = null; byType.Clear(); }

    static void EnsureLoaded()
    {
        if (entries != null) return;
        entries = Resources.LoadAll<UnitDefinition>("HlwUnits");
        System.Array.Sort(entries, (a, b) => a.order.CompareTo(b.order));
        byType.Clear();
        foreach (UnitDefinition definition in entries)
            if (definition && !byType.ContainsKey(definition.archetype)) byType.Add(definition.archetype, definition);
    }
}
