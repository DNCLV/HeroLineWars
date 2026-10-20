using UnityEngine;

/// <summary>Shared references to imported combat VFX; gameplay never depends on AssetDatabase.</summary>
[CreateAssetMenu(menuName = "Hero Line Wars/Combat VFX Catalog")]
public sealed class HlwCombatVfxCatalog : ScriptableObject
{
    public GameObject fireball;
    public GameObject fireSlash;

    static HlwCombatVfxCatalog cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache() => cached = null;

    public static HlwCombatVfxCatalog Current =>
        cached ? cached : cached = Resources.Load<HlwCombatVfxCatalog>("HlwCombatVfxCatalog");
}
