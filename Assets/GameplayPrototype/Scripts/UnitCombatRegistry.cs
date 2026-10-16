using System.Collections.Generic;
using UnityEngine;

/// <summary>Small active-unit registry; combat never performs scene-wide searches.</summary>
public sealed class UnitCombatRegistry : MonoBehaviour
{
    static readonly List<LaneUnitCombat> activeUnits = new List<LaneUnitCombat>(64);

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
            if (localOrigin.z > lane.BranchEndWorld && localTarget.z > lane.BranchEndWorld &&
                candidate.assignedLane != laneIndexForPosition(lane, localOrigin.x)) continue;
            float dx = localTarget.x - localOrigin.x;
            float dz = localTarget.z - localOrigin.z;
            float distance = dx * dx + dz * dz;
            if (distance <= best) { best = distance; nearest = candidate; }
        }
        return nearest;
    }

    static int laneIndexForPosition(LaneGenerator lane, float x)
    {
        int best = 0;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < lane.battleLaneCount; i++)
        {
            float delta = Mathf.Abs(x - lane.RoadCenterX(i));
            if (delta < distance) { distance = delta; best = i; }
        }
        return best;
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
            float dx = local.x - localCenter.x;
            float dz = local.z - localCenter.z;
            if (dx * dx + dz * dz <= radiusSqr) candidate.Health.TakeDamage(damage, attacker);
        }
    }
}
