using System.Collections.Generic;
using UnityEngine;

/// <summary>Small active-unit registry; combat never performs scene-wide searches.</summary>
public sealed class UnitCombatRegistry : MonoBehaviour
{
    static readonly List<LaneUnitCombat> activeUnits = new List<LaneUnitCombat>(64);
    static readonly LaneUnitCombat[] nearestAttackTargets = new LaneUnitCombat[8];
    static readonly float[] nearestAttackDistances = new float[8];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() { activeUnits.Clear(); }

    public static void Register(LaneUnitCombat unit)
    {
        if (unit && !activeUnits.Contains(unit)) activeUnits.Add(unit);
    }

    public static void Unregister(LaneUnitCombat unit) { if (unit) activeUnits.Remove(unit); }

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

    public static void DamageOpponentsInRadius(Vector3 center, float radius, int damage,
        UnitTeam attacker, LaneGenerator lane)
    {
        if (!lane) return;
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
            if (dx * dx + dz * dz <= radiusSqr) candidate.Health.TakeDamage(damage, attacker);
        }
    }

    /// <summary>Damages at most maxTargets closest opponents without per-attack allocations.</summary>
    public static void DamageNearestOpponents(Vector3 center, float radius, int damage,
        UnitTeam attacker, LaneGenerator lane, int maxTargets)
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
            target.Health.TakeDamage(damage, attacker);
            if (i > 0) GameplayVisuals.Impact(target.transform.position + lane.transform.up * .2f,
                .2f, new Color(.45f, .68f, 1f), .1f);
        }
    }
}
