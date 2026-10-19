using UnityEngine;
using System;

public enum HeroAttribute { Strength, Agility, Intelligence, Stamina }

/// <summary>Reusable hero health, attributes, experience, levels, and derived-stat recalculation.</summary>
public sealed class HeroProgression : MonoBehaviour
{
    public event Action<int> LevelledUp;
    [Header("Identity")]
    public bool isPlayerHero = true;
    public bool enableExperience = true;

    [Header("Initial attributes")]
    [Min(0)] public int startingStrength = 12;
    [Min(0)] public int startingAgility = 10;
    [Min(0)] public int startingIntelligence = 16;
    [Min(0)] public int startingStamina = 14;

    [Header("Derived-stat formulas")]
    [Min(1)] public int baseMaximumHealth = 176;
    [Min(0f)] public float bonusHealthPerLevel = 17.5f;
    [Min(0f)] public float staminaHealthPercentPerPoint = .05f;
    [Min(0f)] public float basePhysicalDamage = 13f;
    [Min(0f)] public float strengthDamagePercentPerPoint = .03f;
    [Min(.1f)] public float baseAttackInterval = 1.35f;
    [Min(0f)] public float attackSpeedPerAgility = .025f;
    [Min(0f)] public float baseSpellDamage = 15.25f;
    [Min(0f)] public float intelligenceAbilityPercentPerPoint = .04f;
    [Min(0f)] public float baseRegenerationPerSecond;
    [Min(0f)] public float regenerationPerStamina = .05f;
    [Min(0f)] public float passiveRegenerationDelay = 8f;

    [Header("Experience")]
    [Min(1)] public int baseExperienceToLevel = 70;
    [Min(1f)] public float experienceRequirementGrowth = 1.22f;
    [Min(.5f)] public float experienceRange = 12f;

    public int Level { get; private set; } = 1;
    public int Experience { get; private set; }
    public int Strength { get; private set; }
    public int Agility { get; private set; }
    public int Intelligence { get; private set; }
    public int Stamina { get; private set; }
    public int UnspentSkillPoints { get; private set; }
    public int ExperienceToNextLevel => ExperienceRequiredForLevel(Level);
    public bool IsPlayerHero => isPlayerHero;
    public float LastCombatTime { get; private set; } = float.NegativeInfinity;
    public float RegenerationPerSecond => baseRegenerationPerSecond + Stamina * regenerationPerStamina;
    public float BasicAttackDamageMultiplier => 1f + Strength * strengthDamagePercentPerPoint;
    public float AttackSpeedMultiplier => 1f + Agility * attackSpeedPerAgility;
    public float ActiveAbilityDamageMultiplier => 1f + Intelligence * intelligenceAbilityPercentPerPoint;
    public float MaximumHealthMultiplier => 1f + Stamina * staminaHealthPercentPerPoint;
    public GoblinHealth Health { get; private set; }
    public static HeroProgression PlayerHero { get; private set; }

    HeroAutoAttack autoAttack;
    FireballAbility fireball;
    HeroAbilityLoadout abilityLoadout;
    int strengthGrowth;
    int agilityGrowth;
    int intelligenceGrowth;
    int staminaGrowth;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { PlayerHero = null; }

    void Awake()
    {
        Health = GetComponent<GoblinHealth>();
        if (!Health) Health = gameObject.AddComponent<GoblinHealth>();
        Health.showHealthBarAlways = true;
        autoAttack = GetComponent<HeroAutoAttack>();
        fireball = GetComponent<FireballAbility>();
        abilityLoadout = GetComponent<HeroAbilityLoadout>();
        ResetProgression();
    }

    void OnDestroy()
    {
        if (PlayerHero == this) PlayerHero = null;
    }

    void Update()
    {
        if (!Health || Health.IsDead || Health.CurrentHitPoints >= Health.MaximumHitPoints) return;
        if (!HasBeenOutOfCombat(passiveRegenerationDelay)) return;
        if (HealingWell.Current && HealingWell.Current.Contains(transform)) return;
        Health.Heal(RegenerationPerSecond * Time.deltaTime);
    }

    public void Configure(bool playerControlled)
    {
        isPlayerHero = playerControlled;
        enableExperience = true;
        if (playerControlled) PlayerHero = this;
        RecalculateDerivedStats(false);
    }

    public void ConfigureDefinition(HeroDefinition definition)
    {
        if (!definition) return;
        startingStrength = definition.strength;
        startingAgility = definition.agility;
        startingIntelligence = definition.intelligence;
        startingStamina = definition.stamina;
        basePhysicalDamage = definition.basePhysicalDamage;
        baseMaximumHealth = definition.baseMaximumHealth;
        bonusHealthPerLevel = definition.bonusHealthPerLevel;
        baseSpellDamage = definition.baseSpellDamage;
        strengthGrowth = definition.strengthGrowth;
        agilityGrowth = definition.agilityGrowth;
        intelligenceGrowth = definition.intelligenceGrowth;
        staminaGrowth = definition.staminaGrowth;
        if (!abilityLoadout) abilityLoadout = GetComponent<HeroAbilityLoadout>();
        if (abilityLoadout) abilityLoadout.Configure(definition);
        ResetProgression();
    }

    public void ResetProgression()
    {
        Level = 1;
        Experience = 0;
        Strength = startingStrength;
        Agility = startingAgility;
        Intelligence = startingIntelligence;
        Stamina = startingStamina;
        UnspentSkillPoints = 0;
        LastCombatTime = float.NegativeInfinity;
        if (!abilityLoadout) abilityLoadout = GetComponent<HeroAbilityLoadout>();
        if (abilityLoadout) abilityLoadout.ResetRanks();
        RecalculateDerivedStats(false);
    }

    public void RecordCombatAction() { LastCombatTime = Time.time; }
    public void RecordDamageReceived() { LastCombatTime = Time.time; }
    public bool HasBeenOutOfCombat(float seconds) => Time.time - LastCombatTime >= Mathf.Max(0f, seconds);

    public void AddExperience(int amount)
    {
        if (!enableExperience || amount <= 0 || !Health || Health.IsDead) return;
        Experience += amount;
        while (Experience >= ExperienceToNextLevel)
        {
            Experience -= ExperienceToNextLevel;
            Level++;
            UnspentSkillPoints++;
            Strength += strengthGrowth;
            Agility += agilityGrowth;
            Intelligence += intelligenceGrowth;
            Stamina += staminaGrowth;
            RecalculateDerivedStats(true);
            LevelledUp?.Invoke(Level);
        }
    }

    public bool TryUpgradeAttribute(HeroAttribute attribute)
    {
        if (!TrySpendSkillPoint()) return false;
        if (attribute == HeroAttribute.Strength) Strength++;
        else if (attribute == HeroAttribute.Agility) Agility++;
        else if (attribute == HeroAttribute.Intelligence) Intelligence++;
        else Stamina++;
        RecalculateDerivedStats(true);
        return true;
    }

    public bool TrySpendSkillPoint()
    {
        if (UnspentSkillPoints <= 0) return false;
        UnspentSkillPoints--;
        return true;
    }

    public void AwardExperienceIfNearby(Vector3 defeatedPosition, UnitArchetype archetype)
    {
        if (!enableExperience || !Health || Health.IsDead) return;
        Vector3 delta = defeatedPosition - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude > experienceRange * experienceRange) return;
        AddExperience(ExperienceReward(archetype));
    }

    public static void AwardNearbyExperience(Vector3 defeatedPosition, UnitArchetype archetype)
    {
        if (PlayerHero) PlayerHero.AwardExperienceIfNearby(defeatedPosition, archetype);
    }

    public void RecalculateDerivedStats(bool preserveHealthRatio)
    {
        if (!Health) Health = GetComponent<GoblinHealth>();
        int maximumHealth = Mathf.Max(1, Mathf.RoundToInt(baseMaximumHealth * MaximumHealthMultiplier +
            Mathf.Max(0, Level - 1) * bonusHealthPerLevel));
        if (Health)
        {
            Health.showHealthBarAlways = true;
            Health.SetMaximumHitPoints(maximumHealth, preserveHealthRatio);
        }
        if (!autoAttack) autoAttack = GetComponent<HeroAutoAttack>();
        if (autoAttack)
        {
            autoAttack.damage = Mathf.Max(1, Mathf.RoundToInt(basePhysicalDamage * BasicAttackDamageMultiplier));
            autoAttack.attackInterval = Mathf.Max(.15f,
                baseAttackInterval / AttackSpeedMultiplier);
        }
        if (!fireball) fireball = GetComponent<FireballAbility>();
        if (!abilityLoadout) abilityLoadout = GetComponent<HeroAbilityLoadout>();
        float spellDamage = baseSpellDamage * ActiveAbilityDamageMultiplier *
            (abilityLoadout ? abilityLoadout.FireballDamageMultiplier : 1f);
        if (fireball)
            fireball.damage = Mathf.Max(1, Mathf.RoundToInt(spellDamage));
        if (abilityLoadout) abilityLoadout.RefreshUnlocks(Level);
    }

    int ExperienceRequiredForLevel(int currentLevel) => Mathf.Max(1,
        Mathf.RoundToInt(baseExperienceToLevel * Mathf.Pow(experienceRequirementGrowth, Mathf.Max(0, currentLevel - 1))));

    static int ExperienceReward(UnitArchetype archetype)
    {
        if (archetype == UnitArchetype.Ghoul) return 20;
        if (archetype == UnitArchetype.SkeletonKing) return 45;
        if (archetype == UnitArchetype.UndeadDragon) return 60;
        if (archetype == UnitArchetype.HolyKnight) return 12;
        if (archetype == UnitArchetype.GreenArcher) return 20;
        if (archetype == UnitArchetype.BlueWizard) return 28;
        if (archetype == UnitArchetype.Goblin) return 12;
        UnitDefinition definition = UnitRoster.Find(archetype);
        return definition ? definition.experienceReward : 0;
    }
}
