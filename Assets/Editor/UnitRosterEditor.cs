using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Idempotent glTFast prefab and ScriptableObject import for the canonical 16-unit roster.</summary>
public static class UnitRosterEditor
{
    const string Models = "Assets/GameplayPrototype/Models";
    const string Definitions = "Assets/Resources/HlwUnits";
    const string Portraits = "Assets/Resources/HlwUnitProfiles";
    const string ImportedPortraits = "Assets/Art/UI/UnitPortraits";
    static int automaticAttempts;

    [InitializeOnLoadMethod]
    static void QueueInitialImport()
    {
        automaticAttempts = 0;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += ImportWhenReady;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += ImportWhenReady;
    }

    static void ImportWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            if (++automaticAttempts < 20) EditorApplication.delayCall += ImportWhenReady;
            return;
        }
        if (AssetDatabase.FindAssets("t:UnitDefinition", new[] { Definitions }).Length >= 16)
            AssignThreePortraits();
        else SyncRosterAssets();
    }

    struct Seed
    {
        public UnitArchetype type;
        public UnitTier tier;
        public string name, model;
        public int cost, hp, damage;
        public UnitAttackType attack;
        public float cadence, range, speed, height;
        public int targets;
        public float radius;
        public Seed(UnitArchetype t, UnitTier group, string label, string glb,
            int price, int health, int hit, UnitAttackType style,
            float interval, float reach, float move, float modelHeight,
            int count = 1, float splash = 0f)
        {
            type = t; tier = group; name = label; model = glb; cost = price;
            hp = health; damage = hit; attack = style; cadence = interval;
            range = reach; speed = move; height = modelHeight; targets = count; radius = splash;
        }
    }

    // Cadence/range/movement on the first three preserve the saved scene profiles.
    // New-unit cadence/range/movement are configurable V1 defaults, not invented animations.
    static readonly Seed[] Catalog =
    {
        new Seed(UnitArchetype.HolyKnight, UnitTier.Starter, "Knight", "Holy Knight", 2, 100, 9, UnitAttackType.Melee, 1.3f, 1.5f, 2f, 1.4f),
        new Seed(UnitArchetype.GreenArcher, UnitTier.Starter, "Archer", "Green Archer", 4, 75, 15, UnitAttackType.Ranged, 1f, 5f, 2.6f, 1.4f),
        new Seed(UnitArchetype.BlueWizard, UnitTier.Starter, "Wizard", "Blue Wizard", 6, 65, 22, UnitAttackType.Ranged, 1.8f, 4.5f, 2.35f, 1.4f, 3, 2.6f),
        new Seed(UnitArchetype.Treant, UnitTier.Nature, "Treant", "Treant", 12, 240, 24, UnitAttackType.Melee, 1.35f, 1.7f, 1.85f, 1.75f),
        new Seed(UnitArchetype.Seer, UnitTier.Nature, "Seer", "Seer", 18, 170, 38, UnitAttackType.Ranged, 1.65f, 4.5f, 2.15f, 1.5f),
        new Seed(UnitArchetype.Druid, UnitTier.Nature, "Druid", "Druid", 25, 310, 35, UnitAttackType.Ranged, 1.55f, 4f, 2f, 1.6f),
        new Seed(UnitArchetype.BarkTitan, UnitTier.Nature, "Bark Titan", "Bark Titan", 35, 800, 45, UnitAttackType.Melee, 1.9f, 2f, 1.55f, 2.35f),
        new Seed(UnitArchetype.Goblin, UnitTier.Goblins, "Goblin", "Goblin", 50, 650, 75, UnitAttackType.Melee, 1f, 1.5f, 2.2f, 1.35f),
        new Seed(UnitArchetype.GoblinBrute, UnitTier.Goblins, "Goblin Brute", "Goblin Brute", 70, 1150, 95, UnitAttackType.Melee, 1.5f, 1.8f, 1.8f, 1.9f),
        new Seed(UnitArchetype.GoblinRider, UnitTier.Goblins, "Goblin Rider", "Goblin Rider", 100, 900, 155, UnitAttackType.Melee, 1.25f, 1.8f, 2.55f, 2f),
        new Seed(UnitArchetype.GiantGoblin, UnitTier.Goblins, "Giant Goblin", "Giant Goblin", 140, 2200, 150, UnitAttackType.Melee, 1.8f, 2f, 1.55f, 2.5f),
        new Seed(UnitArchetype.Ghoul, UnitTier.Undead, "Ghoul", "Ghoul", 190, 1750, 210, UnitAttackType.Melee, 1.2f, 1.5f, 2f, 1.4f),
        new Seed(UnitArchetype.Wraith, UnitTier.Undead, "Wraith", "Wraith", 240, 1450, 300, UnitAttackType.Ranged, 1.8f, 4.5f, 2.1f, 1.65f),
        new Seed(UnitArchetype.UndeadDragon, UnitTier.Undead, "Undead Dragon", "Undead Dragon", 310, 2900, 380, UnitAttackType.Ranged, 2f, 4f, 1.8f, 2.65f),
        new Seed(UnitArchetype.SkeletalCommander, UnitTier.Undead, "Skeletal Commander", "Skeletal Commander Flying", 380, 4000, 400, UnitAttackType.Ranged, 1.8f, 4f, 1.8f, 2.1f),
        new Seed(UnitArchetype.SkeletonKing, UnitTier.Undead, "Skeleton King", "Skeleton King", 450, 6000, 460, UnitAttackType.Melee, 1.5f, 2f, 1.65f, 2.4f)
    };

    [MenuItem("Tools/Hero Line Wars/Import 16 Unit Roster")]
    public static void SyncRosterAssets()
    {
        EnsureFolder(Definitions);
        EnsureFolder(Portraits);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureImportedPortraits();
        int ready = 0;
        for (int i = 0; i < Catalog.Length; i++)
        {
            Seed seed = Catalog[i];
            string modelPath = Models + "/" + seed.model + ".glb";
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(modelPath))
            {
                Debug.LogError($"Roster model not imported: {modelPath}");
                continue;
            }
            string legacyGoblin = "Assets/GameplayPrototype/Prefabs/Goblin.prefab";
            string prefabPath = seed.type == UnitArchetype.Goblin &&
                AssetDatabase.LoadAssetAtPath<GameObject>(legacyGoblin) ? legacyGoblin :
                "Assets/GameplayPrototype/Prefabs/Units/" + seed.type + ".prefab";
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
                GameplayPrototypeEditor.EnsureUnitPrefab(seed.model, UnitTeam.Friendly, seed.type, seed.height);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab) { Debug.LogError($"Roster prefab missing: {prefabPath}"); continue; }

            string definitionPath = Definitions + "/" + seed.type + ".asset";
            UnitDefinition definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(definitionPath);
            bool isNew = !definition;
            if (isNew) definition = ScriptableObject.CreateInstance<UnitDefinition>();
            if (isNew)
            {
                definition.archetype = seed.type;
                definition.tier = seed.tier;
                definition.order = i;
                definition.displayName = seed.name;
                definition.cost = seed.cost;
                definition.maxHP = seed.hp;
                definition.baseDamage = seed.damage;
                definition.experienceReward = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(seed.cost) * 4f), 12, 90);
                definition.attackType = seed.attack;
                definition.attackInterval = seed.cadence;
                definition.attackRange = seed.range;
                definition.movementSpeed = seed.speed;
                definition.targetsPerAttack = seed.targets;
                definition.multiTargetRadius = seed.radius;
                definition.collisionRadius = Mathf.Clamp(seed.height * .4f, .45f, 1.1f);
            }
            definition.modelPrefab = prefab;
            if (isNew) AssetDatabase.CreateAsset(definition, definitionPath);
            Sprite portrait = FindPortrait(seed.type);
            if (!portrait) portrait = GeneratePortrait(prefab, seed.type);
            if (portrait) definition.portrait = portrait;
            EditorUtility.SetDirty(definition);
            ready++;
        }
        AssetDatabase.SaveAssets();
        ReportPortraitCoverage();
        UnitRoster.Reload();
        Debug.Log($"[HLW Roster] {ready}/{Catalog.Length} definitions ready. No gameplay scene objects were replaced.");
    }

    static Sprite FindPortrait(UnitArchetype type)
    {
        Sprite imported = FindImportedPortrait(type);
        if (imported) return imported;
        const string starterPortraits = "Assets/UI/UnitProfiles/Resources/HlwUnitProfiles";
        string path = type == UnitArchetype.HolyKnight ? starterPortraits + "/HolyKnightProfile.png" :
            type == UnitArchetype.GreenArcher ? starterPortraits + "/GreenArcherProfile.png" :
            type == UnitArchetype.BlueWizard ? starterPortraits + "/BlueWizardProfile.png" :
            Portraits + "/" + type + ".png";
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    [MenuItem("Tools/Hero Line Wars/Assign Imported Unit Portraits")]
    public static void SyncImportedPortraits()
    {
        ConfigureImportedPortraits();
        foreach (Seed seed in Catalog)
        {
            UnitDefinition definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                Definitions + "/" + seed.type + ".asset");
            if (!definition) continue;
            Sprite portrait = FindPortrait(seed.type);
            if (!portrait || definition.portrait == portrait) continue;
            definition.portrait = portrait;
            EditorUtility.SetDirty(definition);
        }
        AssetDatabase.SaveAssets();
        ReportPortraitCoverage();
        UnitRoster.Reload();
    }

    static void ConfigureImportedPortraits()
    {
        foreach (string path in ImportedPortraitPaths())
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) continue;
            if (importer.textureType == TextureImporterType.Sprite && !importer.mipmapEnabled &&
                importer.filterMode == FilterMode.Bilinear && importer.alphaIsTransparency) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }

    static IEnumerable<string> ImportedPortraitPaths()
    {
        if (!AssetDatabase.IsValidFolder(ImportedPortraits)) yield break;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ImportedPortraits }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".png" || extension == ".jpg" || extension == ".jpeg") yield return path;
        }
    }

    static Sprite FindImportedPortrait(UnitArchetype type)
    {
        // These three mappings are deliberately exact; Goblin must never match Goblin Rider.
        string explicitPath = type == UnitArchetype.GoblinRider ? ImportedPortraits + "/Goblin Rider.png" :
            type == UnitArchetype.Wraith ? ImportedPortraits + "/Wraith.png" :
            type == UnitArchetype.SkeletonKing ? ImportedPortraits + "/SKeletal King.png" : null;
        if (explicitPath != null) return AssetDatabase.LoadAssetAtPath<Sprite>(explicitPath);
        Seed seed = Catalog.FirstOrDefault(entry => entry.type == type);
        string key = NormalizePortraitName(seed.name);
        string modelKey = NormalizePortraitName(seed.model);
        string alias = type == UnitArchetype.UndeadDragon ? "skeletaldragon" : string.Empty;
        Sprite match = null;
        foreach (string path in ImportedPortraitPaths())
        {
            string fileKey = NormalizePortraitName(Path.GetFileNameWithoutExtension(path));
            if (fileKey != key && fileKey != modelKey && fileKey != alias) continue;
            Sprite candidate = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!candidate) continue;
            if (match)
            {
                Debug.LogError($"Ambiguous portrait for {seed.name}: {AssetDatabase.GetAssetPath(match)} and {path}");
                return null;
            }
            match = candidate;
        }
        return match;
    }

    [MenuItem("Tools/Hero Line Wars/Assign Three Unit Portraits")]
    public static void AssignThreePortraits()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureImportedPortraits();
        UnitArchetype[] targets = { UnitArchetype.GoblinRider, UnitArchetype.Wraith,
            UnitArchetype.SkeletonKing };
        foreach (UnitArchetype type in targets)
        {
            UnitDefinition definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                Definitions + "/" + type + ".asset");
            Sprite sprite = FindImportedPortrait(type);
            if (!definition || !sprite)
            {
                Debug.LogError($"[HLW Portraits] Missing definition or exact portrait for {type}.");
                continue;
            }
            if (definition.portrait == sprite) continue;
            definition.portrait = sprite;
            EditorUtility.SetDirty(definition);
            Debug.Log($"[HLW Portraits] {type} -> {AssetDatabase.GetAssetPath(sprite)}");
        }
        AssetDatabase.SaveAssets();
        UnitRoster.Reload();
    }

    static string NormalizePortraitName(string filename)
    {
        string name = new string(filename.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        bool stripped;
        do
        {
            stripped = false;
            foreach (string suffix in new[] { "profilepicture", "portrait", "profile" })
                if (name.EndsWith(suffix, StringComparison.Ordinal))
                { name = name.Substring(0, name.Length - suffix.Length); stripped = true; break; }
        } while (stripped);
        return name;
    }

    static void ReportPortraitCoverage()
    {
        HashSet<string> matched = new HashSet<string>();
        foreach (Seed seed in Catalog)
        {
            Sprite sprite = FindImportedPortrait(seed.type);
            if (sprite) matched.Add(AssetDatabase.GetAssetPath(sprite));
            else Debug.Log($"[HLW Portraits] No supplied portrait for {seed.name}; existing artwork remains as fallback.");
        }
        foreach (string path in ImportedPortraitPaths())
            if (!matched.Contains(path)) Debug.LogWarning($"[HLW Portraits] Unmatched supplied image: {path}");
        Debug.Log($"[HLW Portraits] Matched {matched.Count} supplied images to {Catalog.Length} units.");
    }

    static Sprite GeneratePortrait(GameObject prefab, UnitArchetype type)
    {
        string path = Portraits + "/" + type + ".png";
        if (File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        PreviewRenderUtility preview = new PreviewRenderUtility();
        Texture2D image = null;
        try
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            preview.AddSingleGO(instance);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer || renderer is ParticleSystemRenderer) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!found) return null;
            preview.cameraFieldOfView = 30f;
            preview.camera.transform.position = bounds.center + new Vector3(bounds.extents.x * .8f,
                bounds.extents.y * .15f, -Mathf.Max(2f, bounds.extents.magnitude * 2.7f));
            preview.camera.transform.LookAt(bounds.center);
            preview.camera.nearClipPlane = .01f;
            preview.camera.farClipPlane = 100f;
            preview.lights[0].intensity = 1.4f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, -35f, 0f);
            preview.BeginStaticPreview(new Rect(0f, 0f, 256f, 256f));
            preview.Render(true);
            image = preview.EndStaticPreview();
            if (!image) return null;
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        catch (Exception e) { Debug.LogWarning($"Portrait render failed for {type}: {e.Message}"); return null; }
        finally { if (image) UnityEngine.Object.DestroyImmediate(image); preview.Cleanup(); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
