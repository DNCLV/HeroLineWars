using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Imports canonical Units models, creates reusable prefabs, and wires the lane prototype.</summary>
public static class GameplayPrototypeEditor
{
    const string CanonicalUnitsFolder = @"D:\App\Hero Line Wars\Units";
    const string ModelFolder = "Assets/GameplayPrototype/Models";
    const string PrefabFolder = "Assets/GameplayPrototype/Prefabs";
    const string UnitPrefabFolder = "Assets/GameplayPrototype/Prefabs/Units";
    const string PrototypeScenePath = "Assets/Scenes/HLW_LanePrototype.unity";
    const string HeroModel = "Battlemage.glb";
    const string GoblinModel = "Goblin.glb";

    [MenuItem("Tools/Hero Line Wars/Setup Gameplay Prototype")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != PrototypeScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(PrototypeScenePath);
        }
        LaneGenerator[] generators = FindActiveLaneGenerators(scene);
        LaneGenerator lane = SelectCurrentLaneGenerator(generators);
        if (!lane) return;

        SyncCanonicalModels();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string heroPath = AssetPath(HeroModel);
        string goblinPath = AssetPath(GoblinModel);
        GameObject heroPrefab = EnsureModelPrefab(heroPath, PrefabFolder + "/Battlemage.prefab",
            "Battlemage", 1.25f, UnitTeam.Friendly, UnitArchetype.Battlemage, true);
        GameObject goblinPrefab = EnsureModelPrefab(goblinPath, PrefabFolder + "/Goblin.prefab",
            "Goblin", 1.35f, UnitTeam.Enemy, UnitArchetype.Goblin, false);
        if (!heroPrefab || !goblinPrefab) return;

        string archerPath = EnsureUnitPrefab("Green Archer", UnitTeam.Friendly, UnitArchetype.GreenArcher, 1.4f);
        string knightPath = EnsureUnitPrefab("Holy Knight", UnitTeam.Friendly, UnitArchetype.HolyKnight, 1.4f);
        string wizardPath = EnsureUnitPrefab("Blue Wizard", UnitTeam.Friendly, UnitArchetype.BlueWizard, 1.4f);
        string skeletonPath = EnsureUnitPrefab("Skeleton King", UnitTeam.Enemy, UnitArchetype.SkeletonKing, 1.4f);
        string dragonPath = EnsureUnitPrefab("Undead Dragon", UnitTeam.Enemy, UnitArchetype.UndeadDragon, 1.65f);
        string ghoulPath = EnsureUnitPrefab("Ghoul", UnitTeam.Enemy, UnitArchetype.Ghoul, 1.4f);
        if (new[] { archerPath, knightPath, wizardPath, skeletonPath, dragonPath, ghoulPath }.Any(string.IsNullOrEmpty)) return;

        EnsurePrefabComponents(heroPrefab, UnitTeam.Friendly, UnitArchetype.Battlemage, false, false);
        EnsurePrefabComponents(goblinPrefab, UnitTeam.Enemy, UnitArchetype.Goblin, true, true);

        GameObject group = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "GameplayPrototype");
        if (!group) group = new GameObject("GameplayPrototype");
        group.SetActive(true);
        if (group.scene != scene) SceneManager.MoveGameObjectToScene(group, scene);
        GoblinWaveSpawner spawner = group.GetComponent<GoblinWaveSpawner>();
        if (!spawner) spawner = Undo.AddComponent<GoblinWaveSpawner>(group);
        if (!group.GetComponent<GameplayInputRouter>()) Undo.AddComponent<GameplayInputRouter>(group);
        GameplayHud hud = group.GetComponent<GameplayHud>();
        if (!hud) hud = Undo.AddComponent<GameplayHud>(group);
        hud.enabled = true;
        if (!group.GetComponent<UnitCombatRegistry>()) Undo.AddComponent<UnitCombatRegistry>(group);
        FriendlyUnitSender sender = group.GetComponent<FriendlyUnitSender>();
        if (!sender) sender = Undo.AddComponent<FriendlyUnitSender>(group);
        MatchLoopController matchLoop = group.GetComponent<MatchLoopController>();
        if (!matchLoop) matchLoop = Undo.AddComponent<MatchLoopController>(group);

        spawner.lane = lane;
        spawner.matchLoop = matchLoop;
        spawner.heroPrefab = heroPrefab;
        spawner.goblinPrefab = goblinPrefab;
        spawner.enemyPrefabs = new[]
        {
            Enemy(UnitArchetype.SkeletonKing, skeletonPath),
            Enemy(UnitArchetype.UndeadDragon, dragonPath),
            Enemy(UnitArchetype.Ghoul, ghoulPath),
            Enemy(UnitArchetype.Goblin, PrefabFolder + "/Goblin.prefab")
        };
        sender.spawner = spawner;
        sender.matchLoop = matchLoop;
        sender.greenArcherPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(archerPath);
        sender.holyKnightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(knightPath);
        sender.blueWizardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(wizardPath);
        // Normalize Survival v2 economy and director values on every setup run.
        // Combat profiles and unrelated Inspector settings are deliberately preserved.
        sender.archerCost = 4;
        sender.knightCost = 2;
        sender.wizardCost = 6;
        sender.UpgradePurchaseBalance();
        matchLoop.startingGold = 8;
        matchLoop.startingIncome = 1;
        matchLoop.payoutInterval = 10f;
        matchLoop.goldSpentPerIncome = 2;
        matchLoop.UpgradeEconomyBalance();
        spawner.UpgradeAlliedBalance();
        matchLoop.survivalDuration = 360f;
        spawner.minuteOneSpawnCount = 12;
        spawner.minuteTwoSpawnCount = 18;
        spawner.minuteThreeSpawnCount = 24;
        spawner.minuteFourSpawnCount = 30;
        spawner.minuteFiveSpawnCount = 36;
        spawner.minuteSixSpawnCount = 42;
        spawner.openingGhoulWeight = .12f;
        spawner.finalGoblinWeight = .22f;
        spawner.finalGhoulWeight = .38f;
        spawner.finalSkeletonWeight = .24f;
        spawner.finalDragonWeight = .16f;
        hud.matchLoop = matchLoop;

        EditorUtility.SetDirty(spawner);
        EditorUtility.SetDirty(sender);
        EditorUtility.SetDirty(hud);
        EditorUtility.SetDirty(matchLoop);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = group;
        Debug.Log("Two-sided GameplayPrototype with economy and match loop is ready. Save the scene, then press Play.");
    }

    public static void NormalizeClassicGameplayConfiguration(LaneGenerator lane)
    {
        if (!lane) return;
        Scene scene = lane.gameObject.scene;
        GameObject group = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "GameplayPrototype");
        if (!group) group = new GameObject("GameplayPrototype");
        if (group.scene != scene) SceneManager.MoveGameObjectToScene(group, scene);
        group.SetActive(true);

        GoblinWaveSpawner spawner = group.GetComponent<GoblinWaveSpawner>();
        if (!spawner) spawner = Undo.AddComponent<GoblinWaveSpawner>(group);
        FriendlyUnitSender sender = group.GetComponent<FriendlyUnitSender>();
        if (!sender) sender = Undo.AddComponent<FriendlyUnitSender>(group);
        MatchLoopController matchLoop = group.GetComponent<MatchLoopController>();
        if (!matchLoop) matchLoop = Undo.AddComponent<MatchLoopController>(group);
        GameplayHud hud = group.GetComponent<GameplayHud>();
        if (!hud) hud = Undo.AddComponent<GameplayHud>(group);
        if (!group.GetComponent<UnitCombatRegistry>()) Undo.AddComponent<UnitCombatRegistry>(group);
        if (!group.GetComponent<GameplayInputRouter>()) Undo.AddComponent<GameplayInputRouter>(group);

        spawner.lane = lane;
        spawner.matchLoop = matchLoop;
        sender.spawner = spawner;
        sender.matchLoop = matchLoop;
        hud.matchLoop = matchLoop;
        sender.archerCost = 4;
        sender.knightCost = 2;
        sender.wizardCost = 6;
        sender.UpgradePurchaseBalance();
        matchLoop.startingGold = 8;
        matchLoop.startingIncome = 1;
        matchLoop.payoutInterval = 10f;
        matchLoop.goldSpentPerIncome = 2;
        matchLoop.UpgradeEconomyBalance();
        spawner.UpgradeAlliedBalance();
        matchLoop.survivalDuration = 360f;
        spawner.minuteOneSpawnCount = 12;
        spawner.minuteTwoSpawnCount = 18;
        spawner.minuteThreeSpawnCount = 24;
        spawner.minuteFourSpawnCount = 30;
        spawner.minuteFiveSpawnCount = 36;
        spawner.minuteSixSpawnCount = 42;

        EditorUtility.SetDirty(group);
        EditorUtility.SetDirty(spawner);
        EditorUtility.SetDirty(sender);
        EditorUtility.SetDirty(matchLoop);
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    static GoblinWaveSpawner.EnemyPrefab Enemy(UnitArchetype type, string path) =>
        new GoblinWaveSpawner.EnemyPrefab { archetype = type, prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) };

    static string EnsureUnitPrefab(string modelName, UnitTeam team, UnitArchetype archetype, float targetHeight)
    {
        string modelPath = AssetPath(modelName + ".glb");
        string prefabPath = UnitPrefabFolder + "/" + archetype + ".prefab";
        GameObject prefab = EnsureModelPrefab(modelPath, prefabPath, modelName, targetHeight, team, archetype, false);
        if (!prefab) return null;
        EnsurePrefabComponents(prefab, team, archetype, true, false);
        return prefabPath;
    }

    static GameObject EnsureModelPrefab(string modelPath, string prefabPath, string displayName,
        float targetHeight, UnitTeam team, UnitArchetype archetype, bool hero)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab) return prefab;
        GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) ??
            AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<GameObject>().FirstOrDefault();
        if (!imported)
        {
            Debug.LogError($"Could not import {modelPath}. Resolve glTFast and wait for Unity's GLB import, then rerun Setup Gameplay Prototype.");
            return null;
        }

        EnsureAssetFolder(UnitPrefabFolder);
        GameObject root = new GameObject(displayName);
        try
        {
            GameObject visual = Object.Instantiate(imported, root.transform, false);
            visual.name = displayName + "_Visual";
            Bounds bounds = RendererBounds(visual);
            if (bounds.size.y <= .001f)
            {
                Debug.LogError($"Imported {displayName} has no measurable renderer bounds.");
                return null;
            }
            visual.transform.localScale *= targetHeight / bounds.size.y;
            bounds = RendererBounds(visual);
            visual.transform.localPosition += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);

            if (hero)
            {
                root.AddComponent<HeroLaneController>();
                root.AddComponent<HeroAutoAttack>();
                root.AddComponent<FireballAbility>();
            }
            else
            {
                root.AddComponent<GoblinHealth>();
                root.AddComponent<UnitFaction>();
                root.AddComponent<LaneUnitCombat>();
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { Object.DestroyImmediate(root); }
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    static void EnsurePrefabComponents(GameObject prefab, UnitTeam team, UnitArchetype archetype,
        bool needsUnitCombat, bool scaleLegacyGoblin)
    {
        if (!prefab) return;
        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        bool changed = false;
        try
        {
            if (needsUnitCombat && !contents.GetComponent<GoblinHealth>())
            {
                GoblinHealth health = contents.AddComponent<GoblinHealth>();
                health.maximumHitPoints = 100;
                changed = true;
            }
            UnitFaction faction = contents.GetComponent<UnitFaction>();
            if (!faction) { faction = contents.AddComponent<UnitFaction>(); changed = true; }
            faction.team = team;
            faction.archetype = archetype;
            faction.showFactionRing = needsUnitCombat;
            changed = true;

            if (needsUnitCombat)
            {
                LaneUnitCombat combat = contents.GetComponent<LaneUnitCombat>();
                bool wasMissing = !combat;
                if (!combat) { combat = contents.AddComponent<LaneUnitCombat>(); changed = true; }
                if (wasMissing && scaleLegacyGoblin) combat.modelScale = 1.35f / .78f;
                if (wasMissing) contents.GetComponent<GoblinHealth>().maximumHitPoints = combat.maximumHitPoints;
                GoblinLaneMover oldMover = contents.GetComponent<GoblinLaneMover>();
                if (oldMover && oldMover.enabled) { oldMover.enabled = false; changed = true; }
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    static void SyncCanonicalModels()
    {
        string destinationFolder = Path.Combine(Application.dataPath, "GameplayPrototype", "Models");
        Directory.CreateDirectory(destinationFolder);
        string[] models = { HeroModel, "Blue Wizard.glb", "Ghoul.glb", GoblinModel,
            "Green Archer.glb", "Holy Knight.glb", "Skeleton King.glb", "Undead Dragon.glb" };
        foreach (string filename in models)
        {
            string source = Path.Combine(CanonicalUnitsFolder, filename);
            string destination = Path.Combine(destinationFolder, filename);
            if (!File.Exists(source))
            {
                if (!File.Exists(destination)) Debug.LogError($"Missing model in canonical Units folder and project Assets: {filename}");
                continue;
            }
            if (!File.Exists(destination) || !FilesEqual(source, destination)) File.Copy(source, destination, true);
        }
    }

    static bool FilesEqual(string first, string second)
    {
        FileInfo a = new FileInfo(first);
        FileInfo b = new FileInfo(second);
        if (a.Length != b.Length) return false;
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(first))
        {
            byte[] hashA = sha.ComputeHash(stream);
            sha.Initialize();
            using (FileStream other = File.OpenRead(second))
            {
                byte[] hashB = sha.ComputeHash(other);
                return hashA.SequenceEqual(hashB);
            }
        }
    }

    static void EnsureAssetFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string folder = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }

    static string AssetPath(string filename) => ModelFolder + "/" + filename;

    static LaneGenerator[] FindActiveLaneGenerators(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<LaneGenerator>(true))
        .Where(generator => generator && generator.gameObject.scene == scene &&
                            generator.gameObject.activeInHierarchy && generator.isActiveAndEnabled).ToArray();

    static LaneGenerator SelectCurrentLaneGenerator(LaneGenerator[] generators)
    {
        LaneGenerator[] owners = generators.Where(generator =>
        {
            Transform root = generator.transform;
            return root.Find("LaneFloor") && root.Find("LaneWalls") && root.Find("SpawnArea") && root.Find("GoalArea");
        }).ToArray();
        LaneGenerator[] candidates = owners.Length > 0 ? owners : generators;
        if (candidates.Length == 1) return candidates[0];
        string paths = string.Join("\n", candidates.Select(item => "  - " + HierarchyPath(item.transform)));
        Debug.LogError(candidates.Length == 0
            ? "No active LaneGenerator was found in HLW_LanePrototype."
            : "Multiple active LaneGenerators remain; setup was not changed. Candidate paths:\n" + paths);
        return null;
    }

    static string HierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent) { transform = transform.parent; path = transform.name + "/" + path; }
        return path;
    }

    static Bounds RendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}
