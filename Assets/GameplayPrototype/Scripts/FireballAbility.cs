using UnityEngine;

/// <summary>Mobile-friendly tap-to-place area ability with a live ground targeting ring.</summary>
public sealed class FireballAbility : MonoBehaviour
{
    public LaneGenerator lane;
    [Min(1)] public int damage = 25;
    [Min(.25f)] public float areaRadius = 2f;
    [Min(.5f)] public float abilityRange = 9f;
    [Min(.5f)] public float cooldown = 8f;
    [Min(.1f)] public float projectileSpeed = 10f;
    [Min(.1f)] public float projectileSize = .75f;

    HeroLaneController movement;
    Transform previewRing;
    Renderer previewRenderer;
    float nextReadyTime;
    bool isTargeting;
    bool rangeValid;
    HeroProgression progression;

    public bool IsTargeting => isTargeting;
    public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);
    public bool IsReady => CooldownRemaining <= 0f;

    void Awake()
    {
        movement = GetComponent<HeroLaneController>();
        progression = GetComponent<HeroProgression>();
        if (!lane && movement) lane = movement.lane;
        if (!lane) lane = FindAnyObjectByType<LaneGenerator>();
        CreatePreview();
    }

    void OnEnable() { GameplayInputRouter.TargetingTap += OnTap; }
    void OnDisable() { GameplayInputRouter.TargetingTap -= OnTap; }

    void Update()
    {
        if (!isTargeting) return;
        if (HealingWell.Current && HealingWell.Current.Contains(transform))
        {
            CancelTargeting();
            return;
        }
        rangeValid = false;
        if (lane && GameplayInputRouter.TryGetPointerLanePoint(lane, out Vector3 point))
        {
            Vector3 hero = lane.transform.InverseTransformPoint(transform.position);
            Vector3 target = lane.transform.InverseTransformPoint(point);
            float dx = target.x - hero.x;
            float dz = target.z - hero.z;
            rangeValid = dx * dx + dz * dz <= abilityRange * abilityRange;
            point += lane.transform.up * .035f;
            previewRing.position = point;
            previewRing.rotation = lane.transform.rotation;
            previewRing.localScale = new Vector3(areaRadius, 1f, areaRadius);
            previewRing.gameObject.SetActive(true);
            previewRenderer.sharedMaterial = GameplayVisuals.GetMaterial(
                rangeValid ? "HLW Ability Valid" : "HLW Ability Invalid",
                rangeValid ? new Color(1f, .42f, .08f) : new Color(.9f, .1f, .12f));
        }
        else if (previewRing) previewRing.gameObject.SetActive(false);
    }

    public bool BeginTargeting()
    {
        if (!IsReady || !lane || (HealingWell.Current && HealingWell.Current.Contains(transform))) return false;
        isTargeting = true;
        return true;
    }

    public void CancelTargeting()
    {
        isTargeting = false;
        if (previewRing) previewRing.gameObject.SetActive(false);
    }

    void OnTap(Vector2 screenPosition)
    {
        if (!isTargeting) return;
        GameplayInputRouter.ConsumeCurrentTap();
        if (HealingWell.Current && HealingWell.Current.Contains(transform))
        {
            CancelTargeting();
            return;
        }
        if (!lane || !GameplayInputRouter.TryGetLanePoint(screenPosition, lane, out Vector3 point)) return;
        Vector3 hero = lane.transform.InverseTransformPoint(transform.position);
        Vector3 target = lane.transform.InverseTransformPoint(point);
        float dx = target.x - hero.x;
        float dz = target.z - hero.z;
        if (dx * dx + dz * dz > abilityRange * abilityRange) return;

        isTargeting = false;
        if (previewRing) previewRing.gameObject.SetActive(false);
        nextReadyTime = Time.time + cooldown;
        if (!progression) progression = GetComponent<HeroProgression>();
        if (progression) progression.RecordCombatAction();
        Vector3 launch = transform.position + Vector3.up * .85f;
        Vector3 destination = point + lane.transform.up * .2f;
        UnitFaction faction = GetComponent<UnitFaction>();
        CombatProjectile.Launch(launch, destination, projectileSpeed, projectileSize,
            damage, null, areaRadius, faction ? faction.team : UnitTeam.Friendly,
            lane, new Color(1f, .25f, .03f));
    }

    void CreatePreview()
    {
        previewRing = GameplayVisuals.CreateGroundRing("FireballTargetingRing", .06f);
        previewRing.SetParent(transform, true);
        previewRenderer = previewRing.GetComponent<Renderer>();
        previewRing.gameObject.SetActive(false);
    }
}
