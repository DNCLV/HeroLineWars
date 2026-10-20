using UnityEngine;

/// <summary>Small straight-flight projectile supporting single-target or impact-radius damage.</summary>
public sealed class CombatProjectile : MonoBehaviour
{
    float speed;
    float size;
    int damage;
    float radius;
    Vector3 destination;
    GoblinHealth target;
    UnitTeam attackerTeam;
    Transform attacker;
    LaneGenerator lane;
    int matchNumber;
    bool initialized;
    bool importedVisual;

    public static CombatProjectile Launch(Vector3 start, Vector3 end, float speed, float size,
        int damage, GoblinHealth target, float radius, UnitTeam attackerTeam, LaneGenerator lane, Color color,
        Transform source = null, GameObject visualPrefab = null, float visualScale = 1f)
    {
        Vector3 direction = end - start;
        Quaternion rotation = direction.sqrMagnitude > .0001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up) : Quaternion.identity;
        GameObject projectile = visualPrefab
            ? Instantiate(visualPrefab, start, rotation) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = radius > 0f ? "FireballProjectile" : "FireboltProjectile";
        Collider[] colliders = projectile.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++) Destroy(colliders[i]);
        projectile.transform.position = start;
        projectile.transform.localScale = Vector3.one * (visualPrefab ? visualScale : size);
        if (!visualPrefab)
        {
            Renderer renderer = projectile.GetComponent<Renderer>();
            renderer.sharedMaterial = GameplayVisuals.GetMaterial(radius > 0f ? "HLW Fireball" : "HLW Firebolt", color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        CombatProjectile flight = projectile.AddComponent<CombatProjectile>();
        flight.speed = speed;
        flight.size = size;
        flight.damage = damage;
        flight.target = target;
        flight.radius = radius;
        flight.attackerTeam = attackerTeam;
        flight.attacker = source;
        flight.lane = lane;
        flight.matchNumber = MatchLoopController.Current ? MatchLoopController.Current.MatchNumber : 0;
        flight.destination = end;
        flight.initialized = true;
        flight.importedVisual = visualPrefab;
        return flight;
    }

    void Update()
    {
        if (!initialized) return;
        MatchLoopController match = MatchLoopController.Current;
        if (match && (!match.IsRunning || match.MatchNumber != matchNumber))
        {
            Destroy(gameObject);
            return;
        }
        if (target)
        {
            if (target.IsDead) { Destroy(gameObject); return; }
            destination = target.transform.position + Vector3.up * .45f;
        }
        float step = speed * Time.deltaTime;
        if ((destination - transform.position).sqrMagnitude > step * step)
        {
            Vector3 flightDirection = destination - transform.position;
            if (importedVisual && flightDirection.sqrMagnitude > .0001f)
                transform.rotation = Quaternion.LookRotation(flightDirection.normalized, Vector3.up);
            transform.position = Vector3.MoveTowards(transform.position, destination, step);
            return;
        }
        transform.position = destination;
        if (radius > 0f) Detonate();
        else if (target)
        {
            UnitFaction targetFaction = target.GetComponent<UnitFaction>();
            if (targetFaction && targetFaction.team != attackerTeam) target.TakeDamage(damage, attackerTeam, attacker);
        }
        if (radius <= 0f)
            GameplayVisuals.Impact(new Vector3(destination.x, .25f, destination.z),
                Mathf.Max(.3f, size * 1.8f), new Color(1f, .48f, .12f, .9f));
        Destroy(gameObject);
    }

    void Detonate()
    {
        int hits = UnitCombatRegistry.DamageOpponentsInRadius(destination, radius, damage, attackerTeam, lane, attacker);
        EnemyHeroAiController ai = attacker ? attacker.GetComponent<EnemyHeroAiController>() : null;
        if (ai) ai.RecordAbilityHits(hits);
        GameplayVisuals.Impact(new Vector3(destination.x, .2f, destination.z), radius * 1.4f,
            new Color(1f, .22f, .04f, .72f), .22f);
    }
}
