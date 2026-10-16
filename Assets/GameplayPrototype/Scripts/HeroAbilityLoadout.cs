using System;
using UnityEngine;

[Serializable]
public sealed class HeroAbilitySlot
{
    [Range(1, 4)] public int slot = 1;
    [Min(1)] public int requiredHeroLevel = 1;
    public string displayName = "Locked";
    public Sprite icon;
    [Min(0f)] public float cooldown;
    [Min(0)] public int rank;
    [Min(0)] public int maximumRank;
    public bool implemented;
    [NonSerialized] public bool unlocked;
}

/// <summary>Level-gated four-slot shell; only the existing Fireball behavior is assigned.</summary>
public sealed class HeroAbilityLoadout : MonoBehaviour
{
    public HeroAbilitySlot[] slots =
    {
        new HeroAbilitySlot { slot = 1, requiredHeroLevel = 1, displayName = "Fireball", rank = 1, maximumRank = 3, implemented = true },
        new HeroAbilitySlot { slot = 2, requiredHeroLevel = 2, displayName = "Locked" },
        new HeroAbilitySlot { slot = 3, requiredHeroLevel = 4, displayName = "Locked" },
        new HeroAbilitySlot { slot = 4, requiredHeroLevel = 6, displayName = "Locked" }
    };

    public FireballAbility fireball;
    [Range(0f, 1f)] public float fireballDamageBonusPerRank = .15f;
    public int FireballRank => slots != null && slots.Length > 0 ? slots[0].rank : 1;
    public float FireballDamageMultiplier => 1f + Mathf.Max(0, FireballRank - 1) * fireballDamageBonusPerRank;

    void Awake()
    {
        if (!fireball) fireball = GetComponent<FireballAbility>();
        EnsureSlots();
        RefreshUnlocks(GetComponent<HeroProgression>() ? GetComponent<HeroProgression>().Level : 1);
    }

    public void RefreshUnlocks(int heroLevel)
    {
        EnsureSlots();
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].unlocked = heroLevel >= slots[i].requiredHeroLevel;
            if (i == 0 && fireball) slots[i].cooldown = fireball.cooldown;
        }
    }

    public bool IsUnlocked(int slotIndex)
    {
        EnsureSlots();
        return slotIndex >= 1 && slotIndex <= slots.Length && slots[slotIndex - 1].unlocked;
    }

    public bool CanUpgrade(int slotIndex, HeroProgression progression)
    {
        EnsureSlots();
        if (!progression || progression.UnspentSkillPoints <= 0 || slotIndex < 1 || slotIndex > slots.Length)
            return false;
        HeroAbilitySlot ability = slots[slotIndex - 1];
        return ability.implemented && ability.unlocked && ability.rank < ability.maximumRank;
    }

    public bool TryUpgrade(int slotIndex, HeroProgression progression)
    {
        if (!CanUpgrade(slotIndex, progression) || !progression.TrySpendSkillPoint()) return false;
        slots[slotIndex - 1].rank++;
        progression.RecalculateDerivedStats(true);
        return true;
    }

    public void ResetRanks()
    {
        EnsureSlots();
        for (int i = 0; i < slots.Length; i++) slots[i].rank = i == 0 ? 1 : 0;
        RefreshUnlocks(1);
    }

    void EnsureSlots()
    {
        if (slots == null || slots.Length != 4)
            slots = new[]
        {
            new HeroAbilitySlot { slot = 1, requiredHeroLevel = 1, displayName = "Fireball", rank = 1, maximumRank = 3, implemented = true },
            new HeroAbilitySlot { slot = 2, requiredHeroLevel = 2, displayName = "Locked" },
            new HeroAbilitySlot { slot = 3, requiredHeroLevel = 4, displayName = "Locked" },
            new HeroAbilitySlot { slot = 4, requiredHeroLevel = 6, displayName = "Locked" }
        };
        slots[0].slot = 1;
        slots[0].requiredHeroLevel = 1;
        slots[0].displayName = "Fireball";
        slots[0].implemented = true;
        slots[0].maximumRank = 3;
        slots[0].rank = Mathf.Clamp(slots[0].rank <= 0 ? 1 : slots[0].rank, 1, 3);
        for (int i = 1; i < slots.Length; i++)
        {
            slots[i].slot = i + 1;
            slots[i].requiredHeroLevel = i == 1 ? 2 : i == 2 ? 4 : 6;
            slots[i].implemented = false;
            slots[i].maximumRank = 0;
            slots[i].rank = 0;
        }
    }
}
