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
    [NonSerialized] public bool unlocked;
}

/// <summary>Level-gated four-slot shell; only the existing Fireball behavior is assigned.</summary>
public sealed class HeroAbilityLoadout : MonoBehaviour
{
    public HeroAbilitySlot[] slots =
    {
        new HeroAbilitySlot { slot = 1, requiredHeroLevel = 1, displayName = "Fireball" },
        new HeroAbilitySlot { slot = 2, requiredHeroLevel = 2, displayName = "Locked" },
        new HeroAbilitySlot { slot = 3, requiredHeroLevel = 4, displayName = "Locked" },
        new HeroAbilitySlot { slot = 4, requiredHeroLevel = 6, displayName = "Locked" }
    };

    public FireballAbility fireball;

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

    void EnsureSlots()
    {
        if (slots != null && slots.Length == 4) return;
        slots = new[]
        {
            new HeroAbilitySlot { slot = 1, requiredHeroLevel = 1, displayName = "Fireball" },
            new HeroAbilitySlot { slot = 2, requiredHeroLevel = 2, displayName = "Locked" },
            new HeroAbilitySlot { slot = 3, requiredHeroLevel = 4, displayName = "Locked" },
            new HeroAbilitySlot { slot = 4, requiredHeroLevel = 6, displayName = "Locked" }
        };
    }
}
