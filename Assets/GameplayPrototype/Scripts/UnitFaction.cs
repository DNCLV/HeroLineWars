using UnityEngine;

public enum UnitTeam { Friendly, Enemy }
// Append only: existing serialized enum values in scenes and prefabs must remain stable.
public enum UnitArchetype
{
    Battlemage, GreenArcher, HolyKnight, BlueWizard, SkeletonKing, UndeadDragon, Ghoul, Goblin,
    Treant, Seer, Druid, BarkTitan, GoblinBrute, GoblinRider, GiantGoblin, Wraith, SkeletalCommander
}

/// <summary>Faction and visual identity shared by all lane units.</summary>
public sealed class UnitFaction : MonoBehaviour
{
    public UnitTeam team = UnitTeam.Friendly;
    public UnitArchetype archetype;
    public bool showFactionRing = true;

    public void Set(UnitTeam newTeam, UnitArchetype unitType)
    {
        team = newTeam;
        archetype = unitType;
        Renderer ring = transform.Find("FactionRing")?.GetComponent<Renderer>();
        if (ring) ring.sharedMaterial = RingMaterial(team);
    }

    public static Material RingMaterial(UnitTeam team) => team == UnitTeam.Friendly
        ? GameplayVisuals.GetMaterial("HLW Friendly Unit Ring", new Color(.07f, .32f, .52f))
        : GameplayVisuals.GetMaterial("HLW Enemy Unit Ring", new Color(.48f, .09f, .12f));
}
