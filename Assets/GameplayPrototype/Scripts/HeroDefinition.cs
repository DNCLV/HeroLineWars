using System;
using UnityEngine;

public enum HeroAbilityKind { None, Fireball, Faultbreaker, EarthSpikes, FireSlash }

[Serializable]
public sealed class HeroAbilityDefinition
{
    public HeroAbilityKind kind;
    public string displayName;
    public Sprite icon;
    [Min(1)] public int requiredLevel = 1;
    [Min(1)] public int maximumRank = 3;
    [Min(0f)] public float cooldown;
}

/// <summary>Data-only playable hero definition shared by menu, spawning, progression and HUD.</summary>
[CreateAssetMenu(menuName = "Hero Line Wars/Hero Definition")]
public sealed class HeroDefinition : ScriptableObject
{
    public HlwHeroSelection heroId;
    public string displayName;
    public string classSubtitle;
    [TextArea(2, 4)] public string description;
    public Sprite portrait;
    public Sprite showcaseArtwork;
    public GameObject modelPrefab;

    [Header("Initial attributes")]
    public int strength = 12;
    public int agility = 10;
    public int intelligence = 16;
    public int stamina = 14;

    [Header("Hero base combat values (before attribute bonuses)")]
    [Min(0f)] public float basePhysicalDamage = 13f;
    [Min(1)] public int baseMaximumHealth = 306;
    [Min(0f)] public float bonusHealthPerLevel = 17.5f;
    [Min(0f)] public float baseSpellDamage = 15.25f;

    [Header("Automatic growth per level")]
    public int strengthGrowth = 2;
    public int agilityGrowth = 1;
    public int intelligenceGrowth = 2;
    public int staminaGrowth = 2;

    [Header("Animation clips")]
    public string idleClip = "Walking";
    public string runClip = "Running";
    public string basicAttackClip = "Boxing_Practice";
    public string faultbreakerClip = "Skill_03";
    public string earthSpikesClip = "Skill_01";

    public HeroAbilityDefinition[] abilities = Array.Empty<HeroAbilityDefinition>();
}

public static class HeroCatalog
{
    static HeroDefinition[] cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => cached = null;

    public static HeroDefinition Find(HlwHeroSelection id)
    {
        if (cached == null || cached.Length == 0)
            cached = Resources.LoadAll<HeroDefinition>("Heroes/Definitions");
        for (int i = 0; i < cached.Length; i++)
            if (cached[i] && cached[i].heroId == id) return cached[i];
        return null;
    }

    public static HeroDefinition[] All()
    {
        if (cached == null || cached.Length == 0)
            cached = Resources.LoadAll<HeroDefinition>("Heroes/Definitions");
        return cached;
    }
}
