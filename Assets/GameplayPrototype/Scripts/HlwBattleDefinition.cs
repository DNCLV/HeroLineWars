using UnityEngine;

public enum HlwAiDifficulty { Easy, Normal, Hard, Nightmare }

[System.Serializable]
public struct HlwUnitAffix
{
    public UnitArchetype unit;
    public string affixId;
}

/// <summary>Reusable single-player/campaign encounter settings; no reward or UI behavior.</summary>
[CreateAssetMenu(menuName = "Hero Line Wars/Battle Definition")]
public sealed class HlwBattleDefinition : ScriptableObject
{
    public HlwHeroSelection enemyHero = HlwHeroSelection.Battlemage;
    public HlwAiDifficulty difficulty = HlwAiDifficulty.Normal;
    [Header("Decision quality profiles")]
    public EnemyHeroAiController.DifficultySettings easy = new EnemyHeroAiController.DifficultySettings(1.25f, .22f, .65f, 4, 5, 4f);
    public EnemyHeroAiController.DifficultySettings normal = new EnemyHeroAiController.DifficultySettings(.6f, .30f, .72f, 3, 2, 1.5f);
    public EnemyHeroAiController.DifficultySettings hard = new EnemyHeroAiController.DifficultySettings(.35f, .37f, .78f, 2, 1, 1.1f);
    public EnemyHeroAiController.DifficultySettings nightmare = new EnemyHeroAiController.DifficultySettings(.22f, .43f, .85f, 2, 0, .8f);
    public UnitArchetype[] allowedUnits =
    {
        UnitArchetype.HolyKnight, UnitArchetype.GreenArcher, UnitArchetype.BlueWizard,
        UnitArchetype.Treant, UnitArchetype.Seer, UnitArchetype.Druid, UnitArchetype.BarkTitan,
        UnitArchetype.Goblin, UnitArchetype.GoblinBrute, UnitArchetype.GoblinRider, UnitArchetype.GiantGoblin,
        UnitArchetype.Ghoul, UnitArchetype.Wraith, UnitArchetype.UndeadDragon,
        UnitArchetype.SkeletalCommander, UnitArchetype.SkeletonKing
    };
    [Tooltip("Future explicit encounter modifiers. Empty in ordinary Single Player.")]
    public HlwUnitAffix[] unitAffixes;
    [Tooltip("Future Campaign reward; no reward is granted by this definition.")]
    public string unlockRewardHeroId;
}
