using UnityEngine;

public enum HeroAttribute { Strength, Agility, Intelligence, Stamina }

/// <summary>Reusable hero health, attributes, experience, levels, and derived-stat recalculation.</summary>
public sealed class HeroProgression : MonoBehaviour
{
    [Header("Identity")]
    public bool isPlayerHero = true;
    public bool enableExperience = true;

    [Header("Initial attributes")]
    [Min(0)] public int startingStrength = 12;
    [Min(0)] public int startingAgility = 10;
    [Min(0)] public int startingIntelligence = 16;
    [Min(0)] public int startingStamina = 14;

    [Header("Derived-stat formulas")]
    [Min(1)] public int baseMaximumHealth = 240;
    [Min(0f)] public float healthPerStamina = 20f;
    [Min(0f)] public float basePhysicalDamage = 6f;
    [Min(0f)] public float damagePerStrength = 1f;
    [Min(.1f)] public float baseAttackInterval = 1.35f;
    [Min(0f)] public float attackSpeedPerAgility = .025f;
    [Min(0f)] public float baseSpellDamage = 9f;
    [Min(0f)] public float spellDamagePerIntelligence = 1f;
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
    public GoblinHealth Health { get; private set; }
    public static HeroProgression PlayerHero { get; private set; }

    HeroAutoAttack autoAttack;
    FireballAbility fireball;
    HeroAbilityLoadout abilityLoadout;

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
        enableExperience = playerControlled;
        if (playerControlled) PlayerHero = this;
        RecalculateDerivedStats(false);
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
            RecalculateDerivedStats(true);
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

    public static void AwardNearbyExperience(Vector3 defeatedPosition, UnitArchetype archetype)
    {
        HeroProgression hero = PlayerHero;
        if (!hero || !hero.enableExperience || !hero.Health || hero.Health.IsDead) return;
        Vector3 delta = defeatedPosition - hero.transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude > hero.experienceRange * hero.experienceRange) return;
        hero.AddExperience(ExperienceReward(archetype));
    }

    public void RecalculateDerivedStats(bool preserveHealthRatio)
    {
        if (!Health) Health = GetComponent<GoblinHealth>();
        int maximumHealth = Mathf.Max(1, Mathf.RoundToInt(baseMaximumHealth + Stamina * healthPerStamina));
        if (Health)
        {
            Health.showHealthBarAlways = true;
            Health.SetMaximumHitPoints(maximumHealth, preserveHealthRatio);
        }
        if (!autoAttack) autoAttack = GetComponent<HeroAutoAttack>();
        if (autoAttack)
        {
            autoAttack.damage = Mathf.Max(1, Mathf.RoundToInt(basePhysicalDamage + Strength * damagePerStrength));
            autoAttack.attackInterval = Mathf.Max(.15f,
                baseAttackInterval / (1f + Agility * attackSpeedPerAgility));
        }
        if (!fireball) fireball = GetComponent<FireballAbility>();
        if (!abilityLoadout) abilityLoadout = GetComponent<HeroAbilityLoadout>();
        float spellDamage = baseSpellDamage *
            (abilityLoadout ? abilityLoadout.FireballDamageMultiplier : 1f) +
            Intelligence * spellDamagePerIntelligence;
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
        return archetype == UnitArchetype.Goblin ? 12 : 0;
    }
}
