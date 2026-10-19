using System.Collections.Generic;
using UnityEngine;

/// <summary>Small active-unit registry; combat never performs scene-wide searches.</summary>
public sealed class UnitCombatRegistry : MonoBehaviour
{
    const float SpatialCellSize = 4f;
    struct SpatialKey : System.IEquatable<SpatialKey>
    {
        public LaneGenerator lane;
        public int road, x, z;
        public SpatialKey(LaneGenerator laneRef, int roadIndex, int cellX, int cellZ)
        { lane = laneRef; road = roadIndex; x = cellX; z = cellZ; }
        public bool Equals(SpatialKey other) => lane == other.lane && road == other.road && x == other.x && z == other.z;
        public override bool Equals(object obj) => obj is SpatialKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return ((((lane ? lane.GetHashCode() : 0) * 397) ^ road) * 397 ^ x) * 397 ^ z; }
        }
    }

    sealed class AttackReservation
    {
        public LaneUnitCombat attacker;
        public float angle;
    }

    static readonly Dictionary<SpatialKey, List<LaneUnitCombat>> spatial = new Dictionary<SpatialKey, List<LaneUnitCombat>>();
    static readonly Stack<List<LaneUnitCombat>> spatialPool = new Stack<List<LaneUnitCombat>>();
    static readonly Dictionary<LaneUnitCombat, List<AttackReservation>> attackSlots =
        new Dictionary<LaneUnitCombat, List<AttackReservation>>();
    static readonly Dictionary<LaneUnitCombat, LaneUnitCombat> reservedTarget =
        new Dictionary<LaneUnitCombat, LaneUnitCombat>();
    static int indexedFrame = -1;
    static readonly List<LaneUnitCombat> activeUnits = new List<LaneUnitCombat>(64);
    static readonly LaneUnitCombat[] nearestAttackTargets = new LaneUnitCombat[8];
    static readonly float[] nearestAttackDistances = new float[8];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry()
    {
        activeUnits.Clear();
        foreach (List<LaneUnitCombat> bucket in spatial.Values) { bucket.Clear(); spatialPool.Push(bucket); }
        spatial.Clear();
        attackSlots.Clear();
        reservedTarget.Clear();
        indexedFrame = -1;
    }

    public static void Register(LaneUnitCombat unit)
    {
        if (unit && !activeUnits.Contains(unit)) { activeUnits.Add(unit); indexedFrame = -1; }
    }

    public static void Unregister(LaneUnitCombat unit)
    {
        if (!unit) return;
        activeUnits.Remove(unit);
        indexedFrame = -1;
        ReleaseAttackSlot(unit);
        if (attackSlots.TryGetValue(unit, out List<AttackReservation> targetSlots))
            for (int i = 0; i < targetSlots.Count; i++)
                if (targetSlots[i].attacker) reservedTarget.Remove(targetSlots[i].attacker);
        attackSlots.Remove(unit);
    }

    static void EnsureSpatialIndex()
    {
        if (indexedFrame == Time.frameCount) return;
        foreach (List<LaneUnitCombat> bucket in spatial.Values) { bucket.Clear(); spatialPool.Push(bucket); }
        spatial.Clear();
        for (int i = activeUnits.Count - 1; i >= 0; i--)
        {
            LaneUnitCombat unit = activeUnits[i];
            if (!unit) { activeUnits.RemoveAt(i); continue; }
            if (!unit.isActiveAndEnabled || unit.IsDead || !unit.lane) continue;
            Vector3 local = unit.lane.transform.InverseTransformPoint(unit.transform.position);
            SpatialKey key = new SpatialKey(unit.lane, unit.assignedLane,
                Mathf.FloorToInt(local.x / SpatialCellSize), Mathf.FloorToInt(local.z / SpatialCellSize));
            if (!spatial.TryGetValue(key, out List<LaneUnitCombat> bucket))
            {
                bucket = spatialPool.Count > 0 ? spatialPool.Pop() : new List<LaneUnitCombat>(8);
                spatial.Add(key, bucket);
            }
            bucket.Add(unit);
        }
        indexedFrame = Time.frameCount;
    }

    public static void CopyNearby(List<LaneUnitCombat> result, LaneUnitCombat origin)
    {
        result.Clear();
        if (!origin || !origin.lane) return;
        EnsureSpatialIndex();
        Vector3 local = origin.lane.transform.InverseTransformPoint(origin.transform.position);
        int x = Mathf.FloorToInt(local.x / SpatialCellSize);
        int z = Mathf.FloorToInt(local.z / SpatialCellSize);
        for (int dz = -2; dz <= 2; dz++)
        for (int dx = -2; dx <= 2; dx++)
        {
            if (!spatial.TryGetValue(new SpatialKey(origin.lane, origin.assignedLane, x + dx, z + dz),
                    out List<LaneUnitCombat> bucket)) continue;
            for (int i = 0; i < bucket.Count; i++)
                if (bucket[i] && bucket[i] != origin) result.Add(bucket[i]);
        }
    }

    public static void ReleaseAttackSlot(LaneUnitCombat attacker)
    {
        if (!attacker) return;
        if (!reservedTarget.TryGetValue(attacker, out LaneUnitCombat target)) return;
        reservedTarget.Remove(attacker);
        if (!target || !attackSlots.TryGetValue(target, out List<AttackReservation> slots)) return;
        for (int i = slots.Count - 1; i >= 0; i--)
            if (!slots[i].attacker || slots[i].attacker == attacker) slots.RemoveAt(i);
        if (slots.Count == 0) attackSlots.Remove(target);
    }

    public static bool TryGetAttackSlot(LaneUnitCombat attacker, LaneUnitCombat target, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        if (!attacker || !target || !attacker.lane || attacker.lane != target.lane) return false;
        if (!attackSlots.TryGetValue(target, out List<AttackReservation> slots))
        {
            slots = new List<AttackReservation>(8);
            attackSlots.Add(target, slots);
        }
        for (int i = slots.Count - 1; i >= 0; i--)
            if (!slots[i].attacker || slots[i].attacker.IsDead || !slots[i].attacker.isActiveAndEnabled)
            {
                if (slots[i].attacker) reservedTarget.Remove(slots[i].attacker);
                slots.RemoveAt(i);
            }
        Vector3 center = attacker.lane.transform.InverseTransformPoint(target.transform.position);
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].attacker == attacker)
            {
                if (AttackSlotPosition(attacker, center, target.CollisionRadius,
                    slots[i].angle, out worldPosition)) return true;
                slots.RemoveAt(i);
                reservedTarget.Remove(attacker);
                break;
            }
        float radius = target.CollisionRadius + attacker.CollisionRadius + .12f;
        int capacity = Mathf.Clamp(Mathf.FloorToInt(2f * Mathf.PI * radius /
            (2f * attacker.CollisionRadius + .12f)), 1, 16);
        float bestDistance = float.PositiveInfinity;
        float bestAngle = 0f;
        Vector3 bestPosition = Vector3.zero;
        bool found = false;
        for (int i = 0; i < capacity; i++)
        {
            float angle = i * (2f * Mathf.PI / capacity);
            if (!AttackSlotPosition(attacker, center, target.CollisionRadius,
                    angle, out Vector3 candidate)) continue;
            bool occupied = false;
            for (int j = 0; j < slots.Count; j++)
            {
                LaneUnitCombat other = slots[j].attacker;
                if (!other || !AttackSlotPosition(other, center, target.CollisionRadius,
                        slots[j].angle, out Vector3 otherPosition)) continue;
                Vector3 delta = candidate - otherPosition;
                delta.y = 0f;
                float minimum = attacker.CollisionRadius + other.CollisionRadius + .1f;
                if (delta.sqrMagnitude < minimum * minimum) { occupied = true; break; }
            }
            if (occupied) continue;
            float distance = (candidate - attacker.transform.position).sqrMagnitude;
            if (distance >= bestDistance) continue;
            found = true;
            bestDistance = distance;
            bestAngle = angle;
            bestPosition = candidate;
        }
        if (!found) return false;
        slots.Add(new AttackReservation { attacker = attacker, angle = bestAngle });
        reservedTarget[attacker] = target;
        worldPosition = bestPosition;
        return true;
    }

    static bool AttackSlotPosition(LaneUnitCombat attacker, Vector3 targetLocal, float targetRadius, float angle,
        out Vector3 worldPosition)
    {
        float distance = attacker.CollisionRadius + targetRadius + .12f;
        Vector3 local = targetLocal + new Vector3(Mathf.Cos(angle) * distance, 0f,
            Mathf.Sin(angle) * distance);
        worldPosition = attacker.lane.transform.TransformPoint(local);
        return attacker.lane.IsPointInRoad(attacker.assignedLane, local.x, local.z,
            attacker.CollisionRadius * .5f);
    }

    public static int Count(UnitTeam team)
    {
        int count = 0;
        for (int i = activeUnits.Count - 1; i >= 0; i--)
        {
            LaneUnitCombat unit = activeUnits[i];
            if (!unit) { activeUnits.RemoveAt(i); continue; }
            if (unit.isActiveAndEnabled && unit.Faction && unit.Faction.team == team) count++;
        }
        return count;
    }

    /// <summary>Copies active combat units into a caller-owned list without allocating.</summary>
    public static void CopyActiveUnits(List<LaneUnitCombat> destination)
    {
        if (destination == null) return;
        destination.Clear();
        for (int i = activeUnits.Count - 1; i >= 0; i--)
        {
            LaneUnitCombat unit = activeUnits[i];
            if (!unit) { activeUnits.RemoveAt(i); continue; }
            if (unit.isActiveAndEnabled && !unit.IsDead && unit.Faction) destination.Add(unit);
        }
    }

    public static void CopyOpponents(List<LaneUnitCombat> destination, UnitTeam team, LaneGenerator lane)
    {
        if (destination == null) return;
        destination.Clear();
        for (int i = activeUnits.Count - 1; i >= 0; i--)
        {
            LaneUnitCombat unit = activeUnits[i];
            if (!unit) { activeUnits.RemoveAt(i); continue; }
            if (unit.isActiveAndEnabled && !unit.IsDead && unit.Faction && unit.Health &&
                unit.Faction.team != team && unit.lane == lane) destination.Add(unit);
        }
    }

    public static LaneUnitCombat FindNearestOpponent(UnitTeam team, LaneGenerator lane,
        Vector3 worldPosition, float range)
    {
        if (!lane) return null;
        float best = range * range;
        LaneUnitCombat nearest = null;
        Vector3 localOrigin = lane.transform.InverseTransformPoint(worldPosition);
        for (int i = activeUnits.Count - 1; i >= 0; i--)
        {
            LaneUnitCombat candidate = activeUnits[i];
            if (!candidate) { activeUnits.RemoveAt(i); continue; }
            if (!candidate.isActiveAndEnabled || candidate.IsDead || !candidate.Faction ||
                candidate.Faction.team == team || candidate.lane != lane) continue;
            Vector3 localTarget = lane.transform.InverseTransformPoint(candidate.transform.position);
            if (candidate.assignedLane != lane.LaneIndexForX(localOrigin.x)) continue;
            float dx = localTarget.x - localOrigin.x;
            float dz = localTarget.z - localOrigin.z;
            float distance = dx * dx + dz * dz;
            if (distance <= best) { best = distance; nearest = candidate; }
        }
        return nearest;
    }

    public static int DamageOpponentsInRadius(Vector3 center, float radius, int damage,
        UnitTeam attacker, LaneGenerator lane, Transform source = null)
    {
        if (!lane) return 0;
        int hits = 0;
        float radiusSqr = radius * radius;
        Vector3 localCenter = lane.transform.InverseTransformPoint(center);
        for (int i = activeUnits.Count - 1; i >= 0; i--)
        {
            LaneUnitCombat candidate = activeUnits[i];
            if (!candidate) { activeUnits.RemoveAt(i); continue; }
            if (!candidate.isActiveAndEnabled || candidate.IsDead || !candidate.Faction ||
                !candidate.Health || candidate.Faction.team == attacker || candidate.lane != lane) continue;
            Vector3 local = lane.transform.InverseTransformPoint(candidate.transform.position);
            if (candidate.assignedLane != lane.LaneIndexForX(localCenter.x)) continue;
            float dx = local.x - localCenter.x;
            float dz = local.z - localCenter.z;
            if (dx * dx + dz * dz <= radiusSqr)
            {
                candidate.Health.TakeDamage(damage, attacker, source);
                hits++;
            }
        }
        return hits;
    }

    /// <summary>Damages at most maxTargets closest opponents without per-attack allocations.</summary>
    public static void DamageNearestOpponents(Vector3 center, float radius, int damage,
        UnitTeam attacker, LaneGenerator lane, int maxTargets, Transform source = null)
    {
        if (!lane || damage <= 0) return;
        int capacity = Mathf.Clamp(maxTargets, 1, nearestAttackTargets.Length);
        for (int i = 0; i < capacity; i++)
        {
            nearestAttackTargets[i] = null;
            nearestAttackDistances[i] = float.PositiveInfinity;
        }

        float radiusSqr = radius * radius;
        Vector3 localCenter = lane.transform.InverseTransformPoint(center);
        for (int i = activeUnits.Count - 1; i >= 0; i--)
        {
            LaneUnitCombat candidate = activeUnits[i];
            if (!candidate) { activeUnits.RemoveAt(i); continue; }
            if (!candidate.isActiveAndEnabled || candidate.IsDead || !candidate.Faction ||
                !candidate.Health || candidate.Faction.team == attacker || candidate.lane != lane) continue;
            Vector3 local = lane.transform.InverseTransformPoint(candidate.transform.position);
            if (candidate.assignedLane != lane.LaneIndexForX(localCenter.x)) continue;
            float dx = local.x - localCenter.x;
            float dz = local.z - localCenter.z;
            float distance = dx * dx + dz * dz;
            if (distance > radiusSqr || distance >= nearestAttackDistances[capacity - 1]) continue;
            int insert = capacity - 1;
            while (insert > 0 && distance < nearestAttackDistances[insert - 1])
            {
                nearestAttackDistances[insert] = nearestAttackDistances[insert - 1];
                nearestAttackTargets[insert] = nearestAttackTargets[insert - 1];
                insert--;
            }
            nearestAttackDistances[insert] = distance;
            nearestAttackTargets[insert] = candidate;
        }

        for (int i = 0; i < capacity; i++)
        {
            LaneUnitCombat target = nearestAttackTargets[i];
            if (!target || target.IsDead || !target.Health) continue;
            target.Health.TakeDamage(damage, attacker, source);
            if (i > 0) GameplayVisuals.Impact(target.transform.position + lane.transform.up * .2f,
                .2f, new Color(.45f, .68f, 1f), .1f);
        }
    }
}
