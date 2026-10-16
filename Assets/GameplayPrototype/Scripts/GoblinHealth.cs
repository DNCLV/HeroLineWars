using UnityEngine;

/// <summary>Lightweight damage/death component for each Goblin.</summary>
public sealed class GoblinHealth : MonoBehaviour
{
    static int BaseColorId;
    static int ColorId;
    static MaterialPropertyBlock flashProperties;
    static bool flashResourcesInitialized;

    [Min(1)] public int maximumHitPoints = 100;
    public int CurrentHitPoints { get; private set; }
    public bool IsDead { get; private set; }

    Renderer[] modelRenderers;
    UnitHealthBar healthBar;
    bool flashing;
    float flashEndTime;
    float deathEndTime;
    bool enemyKillRewardGranted;

    static void EnsureFlashResources()
    {
        // The shared block can be reset when entering/exiting Play Mode (including
        // with domain reload disabled). Do not rely solely on each unit's Awake.
        if (flashResourcesInitialized && flashProperties != null) return;
        BaseColorId = Shader.PropertyToID("_BaseColor");
        ColorId = Shader.PropertyToID("_Color");
        flashProperties = new MaterialPropertyBlock();
        flashResourcesInitialized = true;
    }

    internal static void ResetFlashResources()
    {
        if (flashProperties != null) flashProperties.Clear();
        flashProperties = null;
        flashResourcesInitialized = false;
        BaseColorId = 0;
        ColorId = 0;
    }

    void Awake()
    {
        EnsureFlashResources();
        CurrentHitPoints = Mathf.Max(1, maximumHitPoints);
        modelRenderers = GetComponentsInChildren<Renderer>(true);
    }

    void Update()
    {
        if (flashing && Time.time >= flashEndTime) ClearHitFlash();
        if (IsDead && Time.time >= deathEndTime) Destroy(gameObject);
    }

    public void ConfigureHitPoints(int value)
    {
        maximumHitPoints = Mathf.Max(1, value);
        CurrentHitPoints = maximumHitPoints;
    }

    public void TakeDamage(int amount, UnitTeam attackerTeam)
    {
        if (IsDead || amount <= 0) return;
        CurrentHitPoints = Mathf.Max(0, CurrentHitPoints - amount);
        ApplyHitFlash();
        if (CurrentHitPoints > 0)
        {
            if (!healthBar) healthBar = GetComponent<UnitHealthBar>() ?? gameObject.AddComponent<UnitHealthBar>();
            healthBar.Show(this, CurrentHitPoints, maximumHitPoints);
            return;
        }
        IsDead = true;
        UnitFaction faction = GetComponent<UnitFaction>();
        if (faction && faction.team == UnitTeam.Friendly)
        {
            FriendlyUnitSender sender = GetComponentInParent<FriendlyUnitSender>();
            if (sender) sender.RecordFriendlyDeath();
        }
        if (!enemyKillRewardGranted)
        {
            enemyKillRewardGranted = true;
            if (faction)
            {
                MatchLoopController match = MatchLoopController.Current;
                if (match) match.RewardEnemyKill(faction.team, attackerTeam, faction.archetype);
            }
        }
        LaneUnitCombat combat = GetComponent<LaneUnitCombat>();
        if (combat) combat.enabled = false;
        Color deathColor = faction && faction.team == UnitTeam.Friendly
            ? new Color(.18f, .62f, 1f) : new Color(1f, .2f, .18f);
        GameplayVisuals.Impact(transform.position + Vector3.up * .35f, .72f, deathColor, .28f);
        deathEndTime = Time.time + .28f;
    }

    void ApplyHitFlash()
    {
        EnsureFlashResources();
        if (modelRenderers == null) modelRenderers = GetComponentsInChildren<Renderer>(true);
        flashProperties.Clear();
        Color flash = new Color(1f, .72f, .56f, 1f);
        flashProperties.SetColor(BaseColorId, flash);
        flashProperties.SetColor(ColorId, flash);
        for (int i = 0; modelRenderers != null && i < modelRenderers.Length; i++)
        {
            Renderer renderer = modelRenderers[i];
            if (renderer && !renderer.name.Contains("Ring")) renderer.SetPropertyBlock(flashProperties);
        }
        flashing = true;
        flashEndTime = Time.time + .09f;
    }

    void ClearHitFlash()
    {
        if (modelRenderers == null) modelRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; modelRenderers != null && i < modelRenderers.Length; i++)
        {
            Renderer renderer = modelRenderers[i];
            if (renderer && !renderer.name.Contains("Ring")) renderer.SetPropertyBlock(null);
        }
        flashing = false;
    }
}
