using UnityEngine;

/// <summary>Immediate-use player consumable; item spending never grants unit-purchase Income.</summary>
public sealed class HealingPotionItem : MonoBehaviour
{
    [Min(1)] public int cost = 10;
    [Min(1)] public int healing = 225;
    [Min(0f)] public float cooldown = 12f;
    public bool enablePotionDiagnostics;

    public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);
    public bool CanUse
    {
        get
        {
            MatchLoopController match = MatchLoopController.Current;
            HeroProgression player = HeroProgression.PlayerHero;
            GoblinHealth health = player ? player.Health : null;
            return match && match.CanAfford(cost) && health && !health.IsDead &&
                health.CurrentHitPoints < health.MaximumHitPoints && CooldownRemaining <= 0f;
        }
    }

    float nextReadyTime;
    int matchNumber = -1;

    void Update()
    {
        MatchLoopController match = MatchLoopController.Current;
        if (match && match.MatchNumber != matchNumber)
        {
            matchNumber = match.MatchNumber;
            nextReadyTime = 0f;
        }
    }

    public bool TryUse()
    {
        MatchLoopController match = MatchLoopController.Current;
        if (match && match.MatchNumber != matchNumber)
        {
            matchNumber = match.MatchNumber;
            nextReadyTime = 0f;
        }
        if (!CanUse || !match.TrySpendGoldForItem(cost)) return false;
        if (match.enableMatchReport) enablePotionDiagnostics = true;
        GoblinHealth health = HeroProgression.PlayerHero.Health;
        int hpBefore = health.CurrentHitPoints;
        int maximum = health.MaximumHitPoints;
        int goldAfterPurchase = match.Gold;
        health.Heal(healing);
        LogUse(hpBefore, maximum, healing, health.CurrentHitPoints - hpBefore,
            health.CurrentHitPoints, cost, goldAfterPurchase);
        nextReadyTime = Time.time + cooldown;
        return true;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEBUG")]
    void LogUse(int before, int maximum, int configured, int restored, int after, int goldDeducted, int goldRemaining)
    {
        if (enablePotionDiagnostics)
            Debug.Log($"[Potion] HP {before}/{maximum} +{configured} configured, +{restored} actual, " +
                $"after={after}/{maximum}; Gold -{goldDeducted}, remaining={goldRemaining}; " +
                $"cooldown={cooldown:F1}s.", this);
    }
}
