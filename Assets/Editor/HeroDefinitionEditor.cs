using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HeroDefinitionEditor
{
    const string GeomancerFolder = "Assets/Heroes/Geomancer";
    const string DefinitionFolder = "Assets/Resources/Heroes/Definitions";
    const string PrefabFolder = "Assets/Resources/Heroes/Models";

    static HeroDefinitionEditor() => EditorApplication.delayCall += EnsureDefinitionsWhenReady;

    [MenuItem("Tools/Hero Line Wars/Refresh Hero Definitions")]
    public static void RefreshDefinitions()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EnsureDefinitionsWhenReady();
    }

    static void EnsureDefinitionsWhenReady()
    {
        EnsureFolder(DefinitionFolder);
        EnsureFolder(PrefabFolder);
        ConfigureGlb(GeomancerFolder + "/Animated Geomancer.glb");
        ConfigureSprite(GeomancerFolder + "/Geomancer Profile.png");
        ConfigureSprite(GeomancerFolder + "/Geomancer Art picture.png");

        GameObject importedGeomancer = AssetDatabase.LoadAssetAtPath<GameObject>(
            GeomancerFolder + "/Animated Geomancer.glb");
        if (!importedGeomancer) return;

        GameObject geomancerPrefab = BuildGeomancerPrefab(importedGeomancer);
        HeroDefinition battlemage = LoadOrCreate(DefinitionFolder + "/Battlemage.asset");
        battlemage.heroId = HlwHeroSelection.Battlemage;
        battlemage.displayName = "Battlemage";
        battlemage.classSubtitle = "Arcane Vanguard • Ranged";
        battlemage.description = "Control the battlefield with fire and arcane power. Defend your lane and overwhelm the enemy hero.";
        battlemage.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameplayPrototype/Prefabs/Battlemage.prefab");
        battlemage.strength = 12; battlemage.agility = 10; battlemage.intelligence = 16; battlemage.stamina = 14;
        battlemage.basePhysicalDamage = 36f; battlemage.baseMaximumHealth = 176;
        battlemage.bonusHealthPerLevel = 17.5f; battlemage.baseSpellDamage = 42f;
        battlemage.strengthGrowth = 2; battlemage.agilityGrowth = 1; battlemage.intelligenceGrowth = 2; battlemage.staminaGrowth = 2;
        battlemage.abilities = new[]
        {
            new HeroAbilityDefinition { kind = HeroAbilityKind.Fireball, displayName = "Fireball", requiredLevel = 1, maximumRank = 3, cooldown = 8f }
        };

        HeroDefinition geomancer = LoadOrCreate(DefinitionFolder + "/Geomancer.asset");
        geomancer.heroId = HlwHeroSelection.Geomancer;
        geomancer.displayName = "Geomancer";
        geomancer.classSubtitle = "Earthbreaker • Melee";
        geomancer.description = "Shatter enemy formations with seismic punches, crushing shockwaves and erupting stone.";
        geomancer.portrait = AssetDatabase.LoadAssetAtPath<Sprite>(GeomancerFolder + "/Geomancer Profile.png");
        geomancer.showcaseArtwork = AssetDatabase.LoadAssetAtPath<Sprite>(GeomancerFolder + "/Geomancer Art picture.png");
        geomancer.modelPrefab = geomancerPrefab;
        geomancer.strength = 18; geomancer.agility = 10; geomancer.intelligence = 7; geomancer.stamina = 17;
        geomancer.basePhysicalDamage = 32.5f; geomancer.baseMaximumHealth = 167;
        geomancer.bonusHealthPerLevel = 17.5f; geomancer.baseSpellDamage = 15.25f;
        geomancer.strengthGrowth = 2; geomancer.agilityGrowth = 1; geomancer.intelligenceGrowth = 1; geomancer.staminaGrowth = 2;
        geomancer.idleClip = "Walking";
        geomancer.runClip = "Running";
        geomancer.basicAttackClip = "Boxing_Practice";
        geomancer.faultbreakerClip = "Skill_03";
        geomancer.earthSpikesClip = "Skill_01";
        geomancer.abilities = new[]
        {
            new HeroAbilityDefinition { kind = HeroAbilityKind.Faultbreaker, displayName = "Faultbreaker", requiredLevel = 1, maximumRank = 3, cooldown = 5f },
            new HeroAbilityDefinition { kind = HeroAbilityKind.EarthSpikes, displayName = "Earth Spikes", requiredLevel = 2, maximumRank = 3, cooldown = 20f }
        };
        EditorUtility.SetDirty(battlemage);
        EditorUtility.SetDirty(geomancer);
        AssetDatabase.SaveAssets();
    }

    static GameObject BuildGeomancerPrefab(GameObject imported)
    {
        const string path = PrefabFolder + "/Geomancer.prefab";
        GameObject root = new GameObject("Geomancer");
        try
        {
            GameObject visual = Object.Instantiate(imported, root.transform, false);
            visual.name = "Geomancer_Visual";
            Bounds bounds = RendererBounds(visual);
            if (bounds.size.y > .001f)
            {
                visual.transform.localScale *= 1.75f / bounds.size.y;
                bounds = RendererBounds(visual);
                visual.transform.localPosition += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    static void ConfigureSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer || importer.textureType == TextureImporterType.Sprite) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    static void ConfigureGlb(string path)
    {
        AssetImporter importer = AssetImporter.GetAtPath(path);
        if (!importer) return;
        SerializedObject serialized = new SerializedObject(importer);
        SerializedProperty animationMethod = serialized.FindProperty("importSettings.animationMethod");
        if (animationMethod == null || animationMethod.intValue == 1) return;
        animationMethod.intValue = 1; // glTFast Legacy: exposes named clips through Animation.CrossFade.
        serialized.ApplyModifiedPropertiesWithoutUndo();
        importer.SaveAndReimport();
    }

    static HeroDefinition LoadOrCreate(string path)
    {
        HeroDefinition definition = AssetDatabase.LoadAssetAtPath<HeroDefinition>(path);
        if (definition) return definition;
        definition = ScriptableObject.CreateInstance<HeroDefinition>();
        AssetDatabase.CreateAsset(definition, path);
        return definition;
    }

    static Bounds RendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
