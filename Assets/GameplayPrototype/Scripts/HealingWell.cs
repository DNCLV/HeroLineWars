using UnityEngine;

/// <summary>Player recovery zone. It heals out of combat and suppresses outgoing attacks while occupied.</summary>
public sealed class HealingWell : MonoBehaviour
{
    [Min(.5f)] public float healingRadius = 4f;
    [Range(.01f, .25f)] public float maximumHealthPerSecond = .06f;
    [Min(0f)] public float outOfCombatDelay = 5f;

    public bool IsRegenerating { get; private set; }
    public bool ContainsPlayer { get; private set; }
    public static HealingWell Current { get; private set; }

    HeroProgression player;
    Transform glowRing;
    Renderer glowRenderer;
    Material idleMaterial;
    Material activeMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Current = null; }

    void Awake()
    {
        Current = this;
        BuildVisuals();
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    public void Configure(HeroProgression playerHero, float radius, float healingFraction, float delay)
    {
        player = playerHero;
        healingRadius = Mathf.Max(.5f, radius);
        maximumHealthPerSecond = Mathf.Clamp(healingFraction, .01f, .25f);
        outOfCombatDelay = Mathf.Max(0f, delay);
        if (glowRing) glowRing.localScale = Vector3.one * healingRadius;
    }

    void Update()
    {
        if (!player) player = HeroProgression.PlayerHero;
        if (!player || !player.Health || player.Health.IsDead)
        {
            SetState(false, false);
            return;
        }
        Vector3 delta = player.transform.position - transform.position;
        delta.y = 0f;
        bool inside = delta.sqrMagnitude <= healingRadius * healingRadius;
        bool regenerating = inside && player.HasBeenOutOfCombat(outOfCombatDelay) &&
            player.Health.CurrentHitPoints < player.Health.MaximumHitPoints;
        if (regenerating)
            player.Health.Heal(player.Health.MaximumHitPoints * maximumHealthPerSecond * Time.deltaTime);
        SetState(inside, regenerating);
    }

    public bool Contains(Transform target)
    {
        if (!target) return false;
        Vector3 delta = target.position - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= healingRadius * healingRadius;
    }

    void SetState(bool inside, bool regenerating)
    {
        ContainsPlayer = inside;
        if (IsRegenerating == regenerating) return;
        IsRegenerating = regenerating;
        if (glowRenderer) glowRenderer.sharedMaterial = regenerating ? activeMaterial : idleMaterial;
    }

    void BuildVisuals()
    {
        idleMaterial = GameplayVisuals.GetMaterial("HLW Healing Well Idle", new Color(.05f, .28f, .38f));
        activeMaterial = GameplayVisuals.GetMaterial("HLW Healing Well Active", new Color(.08f, .75f, .65f));
        glowRing = GameplayVisuals.CreateGroundRing("HealingRadius", .09f);
        glowRing.SetParent(transform, false);
        glowRing.localPosition = new Vector3(0f, .025f, 0f);
        glowRing.localScale = Vector3.one * healingRadius;
        glowRenderer = glowRing.GetComponent<Renderer>();
        if (glowRenderer) glowRenderer.sharedMaterial = idleMaterial;

        GameObject basin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        basin.name = "HealingWellBasin";
        basin.transform.SetParent(transform, false);
        basin.transform.localPosition = new Vector3(0f, .12f, 0f);
        basin.transform.localScale = new Vector3(1.4f, .12f, 1.4f);
        Collider collider = basin.GetComponent<Collider>();
        if (collider) Destroy(collider);
        basin.GetComponent<Renderer>().sharedMaterial = idleMaterial;
    }
}
