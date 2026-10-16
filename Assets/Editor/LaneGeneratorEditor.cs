using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;

public static class LaneGeneratorEditor
{
    [InitializeOnLoadMethod]
    static void UpgradeOpenPrototypeRoads()
    {
        EditorApplication.delayCall += UpgradeSavedPrototypeIfNeeded;
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += UpgradeSavedPrototypeIfNeeded;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += UpgradeSavedPrototypeIfNeeded;
        };
    }

    static void UpgradeSavedPrototypeIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/HLW_LanePrototype.unity") return;
        string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", scene.path));
        if (!File.Exists(absolutePath)) return;
        string savedScene = File.ReadAllText(absolutePath);
        if (savedScene.Contains("m_Name: ContinuousArcaneDistrictGround") &&
            savedScene.Contains("m_Name: ArcaneRecoveryV3") &&
            savedScene.Contains("m_Name: StructuredOutskirtsV4") &&
            savedScene.Contains("m_Name: ArcaneRunes") &&
            savedScene.Contains($"m_Name: {LaneGenerator.GenerationVersion}"))
        {
            const string sessionKey = "HLW.ClassicTwoLane.RuntimeConfigurationV1";
            if (!SessionState.GetBool(sessionKey, false))
            {
                GameplayPrototypeEditor.NormalizeClassicGameplayConfiguration(
                    Object.FindAnyObjectByType<LaneGenerator>());
                SessionState.SetBool(sessionKey, true);
            }
            return;
        }
        HlwCameraController cameraController = Object.FindAnyObjectByType<HlwCameraController>();
        if (cameraController)
        {
            cameraController.orthographicSize = 12f;
            cameraController.maximumOrthographicSize = 40f;
            Camera camera = cameraController.GetComponent<Camera>();
            if (camera) { camera.orthographicSize = 12f; EditorUtility.SetDirty(camera); }
            EditorUtility.SetDirty(cameraController);
        }
        GenerateLane();
    }
    static readonly string Arena = "Assets/Kenney Arena/Models/FBX format/";
    static readonly string Castle = "Assets/Kenney Castle/Models/FBX format/";
    static readonly string ArenaFloorPath = "Assets/Kenney Arena/Models/FBX format/floor.fbx";
    static readonly string Arcane = "Assets/Environment/Arcane/";

    [MenuItem("Tools/Hero Line Wars/Create Floor Diagnostics Scene")]
    public static void CreateFloorDiagnosticsScene()
    {
        GameObject floorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaFloorPath);
        if (!floorAsset) { Debug.LogError("Could not load " + ArenaFloorPath); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject root = new GameObject("HLW_FloorDiagnostics");
        GameObject single = Object.Instantiate(floorAsset, root.transform);
        single.name = "Single_UnmodifiedFloor";
        single.transform.position = new Vector3(-2.5f, 0, 0);
        Bounds sourceBounds = single.GetComponentInChildren<Renderer>().bounds;
        for (int z = 0; z < 2; z++)
            for (int x = 0; x < 2; x++)
            {
                GameObject tile = Object.Instantiate(floorAsset, root.transform);
                tile.name = $"Tile2x2_{x}_{z}";
                tile.transform.position = new Vector3(2.5f + x * sourceBounds.size.x, 0, z * sourceBounds.size.z);
            }
        GameObject lightObject = new GameObject("Diagnostic Light");
        Light light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f; lightObject.transform.rotation = Quaternion.Euler(45, -30, 0);
        GameObject cameraObject = new GameObject("Diagnostic Camera");
        Camera camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5.5f; camera.transform.position = new Vector3(0, 10, 2); camera.transform.rotation = Quaternion.Euler(90, 0, 0); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .12f, .12f); cameraObject.tag = "MainCamera";
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/HLW_FloorDiagnostics.unity");
        Selection.activeGameObject = root;
        Debug.Log("Created Assets/Scenes/HLW_FloorDiagnostics.unity with one unmodified floor and a separated 2x2 arrangement.");
        ReportFloorInstances(root.transform);
    }

    [MenuItem("Tools/Hero Line Wars/Report Floor Mesh Geometry")]
    public static void ReportFloorMeshGeometry()
    {
        GameObject floorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaFloorPath);
        if (!floorAsset) { Debug.LogError("Could not load " + ArenaFloorPath); return; }
        Debug.Log("--- Kenney Arena floor asset geometry ---");
        ReportObject(floorAsset, "Unmodified asset");
        List<Renderer> activeFloors = new List<Renderer>();
        List<Renderer> activeSceneRenderers = new List<Renderer>();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            activeSceneRenderers.AddRange(root.GetComponentsInChildren<Renderer>(false));
            foreach (LaneGenerator generator in root.GetComponentsInChildren<LaneGenerator>(true))
            {
                Debug.Log($"Generator {HierarchyPath(generator.transform)}: active={generator.isActiveAndEnabled}, KenneyFloor={generator.arenaFloor}, debug={generator.useDebugFloorMaterial}");
                Transform floor = generator.transform.Find("LaneFloor");
                if (!floor) continue;
                ReportFloorInstances(floor);
                if (!generator.isActiveAndEnabled) continue;
                foreach (Renderer renderer in floor.GetComponentsInChildren<Renderer>(false))
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    activeFloors.Add(renderer);
                    if (generator.useDebugFloorMaterial)
                        foreach (Material material in renderer.sharedMaterials)
                            if (!material || material.shader.name != "Universal Render Pipeline/Unlit")
                                Debug.LogError($"Debug floor override missing: {HierarchyPath(renderer.transform)}", renderer);
                }
            }
        }
        // Include floors owned by OTHER generators and stale geometry outside this
        // generator, which the original 256-tile diagnostic did not inspect.
        int surfaceOverlaps = 0;
        foreach (Renderer floor in activeFloors)
            foreach (Renderer other in activeSceneRenderers)
            {
                if (other == floor || !other.enabled || !other.gameObject.activeInHierarchy) continue;
                if (activeFloors.Contains(other) && activeFloors.IndexOf(other) < activeFloors.IndexOf(floor)) continue;
                Bounds a = floor.bounds, b = other.bounds;
                float x = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
                float z = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
                // A positive area and a matching top surface distinguish a stacked
                // floor from adjacent tiles merely touching at their edges.
                if (x <= .0001f || z <= .0001f || Mathf.Abs(a.max.y - b.max.y) > .0001f) continue;
                if (surfaceOverlaps++ < 8)
                    Debug.LogWarning($"Possible coplanar surfaces: {HierarchyPath(floor.transform)} / {HierarchyPath(other.transform)} at Y={a.max.y:F5}");
            }
        Debug.Log($"Scene floor audit: active floor renderers={activeFloors.Count}, possible coplanar surface pairs={surfaceOverlaps}. Bounds candidates require mesh inspection to confirm.");
    }

    static void ReportFloorInstances(Transform parent)
    {
        Debug.Log($"--- Floor instance report: {parent.name} ---");
        Renderer[] all = parent.GetComponentsInChildren<Renderer>(true);
        Debug.Log($"{HierarchyPath(parent)}: tile roots={parent.childCount}, renderers={all.Length}");
        if (all.Length > 0) ReportRenderer(all[0], 0);
        if (all.Length > 0)
        {
            Bounds total = all[0].bounds;
            foreach (Renderer renderer in all) total.Encapsulate(renderer.bounds);
            Debug.Log($"All floor renderers: world bounds={total}, Y={total.min.y:F6}..{total.max.y:F6}; group position={parent.position}, rotation={parent.eulerAngles}, scale={parent.lossyScale}");
        }
        int overlaps = 0;
        for (int i = 0; i < all.Length; i++)
            for (int j = i + 1; j < all.Length; j++)
            {
                Vector3 extent = Vector3.Min(all[i].bounds.max, all[j].bounds.max) - Vector3.Max(all[i].bounds.min, all[j].bounds.min);
                if (extent.x > .0001f && extent.z > .0001f && extent.y >= -.0001f) overlaps++;
            }
        Debug.Log($"Positive-area bounds overlaps within {HierarchyPath(parent)}: {overlaps}");
    }

    static void ReportObject(GameObject root, string label)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Debug.Log($"{label}: renderers={renderers.Length}, position={root.transform.position}, rotation={root.transform.eulerAngles}, scale={root.transform.lossyScale}");
        foreach (Renderer renderer in renderers) ReportRenderer(renderer, 0);
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (!mesh) continue;
            try
            {
                // Unity allows Editor inspection outside Play Mode even when the
                // player's Read/Write flag is off. Never change the importer here.
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                Vector3[] normals = mesh.normals;
                Vector2[] uv = mesh.uv;
                Debug.Log($"Mesh {mesh.name}: readable={mesh.isReadable}, triangles={triangles.Length / 3}, transform={HierarchyPath(filter.transform)}");
                float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 world = filter.transform.TransformPoint(vertices[i]);
                    minY = Mathf.Min(minY, world.y); maxY = Mathf.Max(maxY, world.y);
                    if (i < 16) Debug.Log($"vertex[{i}] local={vertices[i].ToString("F5")} world={world.ToString("F5")} normal={(i < normals.Length ? normals[i].ToString() : "missing")} uv={(i < uv.Length ? uv[i].ToString() : "missing")}");
                }
                int duplicates = 0;
                HashSet<string> faces = new HashSet<string>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int[] sorted = { a, b, c }; System.Array.Sort(sorted);
                    if (!faces.Add(string.Join(",", sorted))) duplicates++;
                    if (i < 18) Debug.Log($"triangle {a},{b},{c}: local winding normal={Vector3.Cross(vertices[b]-vertices[a], vertices[c]-vertices[a]).normalized}");
                }
                Debug.Log($"Actual vertex world Y={minY:F6}..{maxY:F6}, repeated index triangles={duplicates}");
            }
            catch (System.Exception e) { Debug.LogWarning($"Mesh data inspection unavailable for {mesh.name}: {e.Message}. Importer was not changed."); }
        }
    }

    static void ReportRenderer(Renderer renderer, int index)
    {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        Mesh mesh = filter ? filter.sharedMesh : null;
        string meshInfo = mesh ? $"mesh={mesh.name}, localBounds={mesh.bounds}, subMeshes={mesh.subMeshCount}, vertices={mesh.vertexCount}" : "mesh=not MeshFilter-backed";
        string materials = string.Join(", ", System.Array.ConvertAll(renderer.sharedMaterials, m => m ? $"{m.name} [{m.shader?.name}]" : "null"));
        Debug.Log($"[{index}] {renderer.name}: worldBounds={renderer.bounds}, worldY={renderer.bounds.min.y:F5}..{renderer.bounds.max.y:F5}, transformPos={renderer.transform.position}, rotation={renderer.transform.eulerAngles}, scale={renderer.transform.lossyScale}, {meshInfo}, materials={materials}");
        foreach (Material material in renderer.sharedMaterials)
            if (material)
                Debug.Log($"Material {material.name}: baseMap={(material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null)}, baseColor={(material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor").ToString() : "n/a")}, queue={material.renderQueue}, surface={(material.HasProperty("_Surface") ? material.GetFloat("_Surface").ToString() : "n/a")}, zWrite={(material.HasProperty("_ZWrite") ? material.GetFloat("_ZWrite").ToString() : "n/a")}");
    }

    static string HierarchyPath(Transform t) => t.parent ? HierarchyPath(t.parent) + "/" + t.name : t.name;

    static LaneGenerator GetLaneGenerator()
    {
        List<LaneGenerator> generators = new List<LaneGenerator>();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (LaneGenerator generator in root.GetComponentsInChildren<LaneGenerator>(false))
                if (generator.isActiveAndEnabled) generators.Add(generator);
        if (generators.Count == 1) return generators[0];
        Debug.LogError($"Expected one active LaneGenerator in the active scene; found {generators.Count}. Disable redundant generator hierarchies before regenerating. Use Report Floor Mesh Geometry to locate them.");
        return null;
    }

    [MenuItem("Tools/Hero Line Wars/Setup Kenney Environment Assets")]
    public static void SetupKenneyAssets()
    {
        LaneGenerator g = GetLaneGenerator();
        if (!g) { Debug.LogError("No LaneGenerator found in the open scene."); return; }
        g.arenaFloor = Load(Arena + "floor.fbx");
        g.arenaFloorDetail = Load(Arena + "floor-detail.fbx");
        g.arenaWall = Load(Arena + "wall.fbx");
        g.arenaWallCorner = Load(Arena + "wall-corner.fbx");
        g.arenaWallGate = Load(Arena + "wall-gate.fbx");
        g.arenaColumn = Load(Arena + "column.fbx");
        g.arenaBanner = Load(Arena + "banner.fbx");
        g.castleWallPillar = Load(Castle + "wall-pillar.fbx");
        g.castleFlagBannerLong = Load(Castle + "flag-banner-long.fbx");
        g.castleFlagBannerShort = Load(Castle + "flag-banner-short.fbx");
        g.castleTowerBase = Load(Castle + "tower-square-base.fbx");
        g.castleTowerArch = Load(Castle + "tower-square-arch.fbx");
        g.castleWallDoorway = Load(Castle + "wall-doorway.fbx");
        g.castleTowerRoof = Load(Castle + "tower-square-roof.fbx");
        g.castleTowerHexBase = Load(Castle + "tower-hexagon-base.fbx");
        g.castleTowerHexMid = Load(Castle + "tower-hexagon-mid.fbx");
        g.castleTowerHexRoof = Load(Castle + "tower-hexagon-roof.fbx");
        AssignPolishAssets(g);
        EditorUtility.SetDirty(g);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(g.gameObject.scene);
        Debug.Log("Kenney Arena/Castle environment assets assigned to LaneGenerator.");
    }

    [MenuItem("Tools/Hero Line Wars/Generate Lane")]
    public static void GenerateLane()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play Mode before generating the lane. GameplayPrototype, its runtime HUD, and spawned hero are left untouched.");
            return;
        }
        LaneGenerator g = GetLaneGenerator();
        if (!g) { Debug.LogError("No LaneGenerator found in the open scene."); return; }
        Scene activeScene = SceneManager.GetActiveScene();
        Debug.Log($"[HLW Generate Lane] generator={HierarchyPath(g.transform)}, entityId={g.GetEntityId()}, " +
            $"active={g.isActiveAndEnabled}, generatorScene={g.gameObject.scene.path}, " +
            $"activeScene={activeScene.path}, version={LaneGenerator.GenerationVersion}.", g);
        if (g.gameObject.scene != activeScene)
        {
            Debug.LogError("Generate Lane resolved a generator outside the active scene; generation aborted.", g);
            return;
        }
        GameplayHud existingHud = Object.FindAnyObjectByType<GameplayHud>(FindObjectsInactive.Include);
        if (!g.arenaFloor) SetupKenneyAssets();
        AssignPolishAssets(g);
        g.Generate();
        GameplayPrototypeEditor.NormalizeClassicGameplayConfiguration(g);
        float generatedGap = g.RoadStartX(1) - (g.RoadStartX(0) + g.RoadWidth);
        Debug.Log($"[HLW Generate Lane] dimensions: roadWidth={g.RoadWidth:F2}, " +
            $"interRoadGap={generatedGap:F2}, totalWidth={g.WidthWorld:F2}, length={g.LengthWorld:F2}; " +
            $"centers=({g.RoadCenterX(0):F2}, {g.RoadCenterX(1):F2}).", g);
        if (Mathf.Abs(generatedGap - g.laneGap * g.cellSize) > .01f)
            Debug.LogError("Generated inter-road spacing does not match the configured classic lane gap.", g);
        bool routesConnected = true;
        for (int lane = 0; lane < g.battleLaneCount; lane++)
            routesConnected &= g.IsWalkable(g.RoadCenterX(lane), .5f * g.cellSize) &&
                g.IsWalkable(g.RoadCenterX(lane), g.LengthWorld - .5f * g.cellSize);
        float gapCenter = g.RoadStartX(0) + g.RoadWidth + generatedGap * .5f;
        bool districtIsNonWalkable = !g.IsWalkable(gapCenter, g.LengthWorld * .5f);
        Debug.Log($"[HLW Generate Lane] route validation: bothRoadsContinuous={routesConnected}, " +
            $"interLaneDistrictSeparated={districtIsNonWalkable}.", g);
        if (!routesConnected || !districtIsNonWalkable)
            Debug.LogError("Classic two-lane generation produced a disconnected road or walkable district shortcut.", g);
        Transform generatedGround = g.transform.Find("BattlefieldBaseGround/ContinuousArcaneDistrictGround");
        Transform generatedEdgeStrip = g.transform.Find("Decorations/EdgeStrip");
        Transform generatedRunes = g.transform.Find("Decorations/ArcaneRunes");
        Transform generatedLandmarks = g.transform.Find("Decorations/MajorLandmarks");
        Transform obsoleteDistricts = g.transform.Find("Decorations/ArcaneDistricts");
        if (!generatedGround)
            Debug.LogError("Lane generation did not create the expected continuous outskirts ground.", g);
        if (!generatedEdgeStrip || !generatedRunes || !generatedLandmarks)
            Debug.LogError("Lane generation did not create all three structured outskirts layers.", g);
        else
        {
            int decorationCount = generatedEdgeStrip.childCount + generatedRunes.childCount +
                generatedLandmarks.childCount;
            Debug.Log($"Structured outskirts generated: {generatedEdgeStrip.childCount} edge details, " +
                $"{generatedRunes.childCount} inter-lane rune markers, " +
                $"{generatedLandmarks.childCount} outer landmark objects ({decorationCount} total).", g);
            if (generatedRunes.childCount != 1)
                Debug.LogError($"Expected one central Arcane seal between the two roads; generated {generatedRunes.childCount}.", g);
            foreach (Transform rune in generatedRunes)
                Debug.Log($"{rune.name}: worldPosition={rune.position}, radius={g.interLaneRuneRadius:F2}, " +
                    $"brightness={g.interLaneRuneBrightness:F2}.", rune);
            foreach (Collider collider in g.transform.Find("Decorations").GetComponentsInChildren<Collider>(true))
                if (collider.enabled)
                    Debug.LogError($"Decorative collider remained enabled on {collider.name}.", collider);
        }
        if (obsoleteDistricts)
            Debug.LogError("Obsolete Decorations/ArcaneDistricts survived regeneration.", obsoleteDistricts);
        LaneOutskirtsDiagnostics.Log(g);
        EditorUtility.SetDirty(g);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(g.gameObject.scene);
        // Generate Lane is the authoring workflow for the prototype scene; save
        // the generated hierarchy so a successful run cannot be lost when the
        // scene is closed or opened by the gameplay camera.
        bool sceneSaved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(g.gameObject.scene);
        string absoluteScenePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", g.gameObject.scene.path));
        bool persisted = sceneSaved && File.Exists(absoluteScenePath) &&
            File.ReadAllText(absoluteScenePath).Contains($"m_Name: {LaneGenerator.GenerationVersion}");
        Debug.Log($"[HLW Generate Lane] saveSucceeded={sceneSaved}, versionPersisted={persisted}, " +
            $"scene={g.gameObject.scene.path}.", g);
        if (!persisted)
            Debug.LogError("Generate Lane did not persist the generation version to the expected scene file.", g);
        if (existingHud && Object.FindAnyObjectByType<GameplayHud>(FindObjectsInactive.Include) != existingHud)
            Debug.LogError("Gameplay HUD changed during lane generation; inspect the scene hierarchy before entering Play Mode.");
        Selection.activeGameObject = g.gameObject;
        Debug.Log($"Hero Line Wars lane generated and scene saved ({LaneGenerator.GenerationVersion}).", g);
    }

    [MenuItem("Tools/Hero Line Wars/Diagnose Arcane Outskirts Visibility")]
    public static void DiagnoseArcaneOutskirtsVisibility()
    {
        LaneGenerator g = GetLaneGenerator();
        if (!g) return;
        LaneOutskirtsDiagnostics.Log(g);
        Selection.activeGameObject = g.gameObject;
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Hero Line Wars/Diagnose Kenney Floor")]
    public static void DiagnoseKenneyFloor()
    {
        LaneGenerator g = GetLaneGenerator();
        if (!g) { Debug.LogError("No LaneGenerator found in the open scene."); return; }
        Transform floor = g.transform.Find("LaneFloor");
        if (!floor) { Debug.LogWarning("LaneFloor has not been generated yet."); return; }
        int count = 0; float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        Bounds total = new Bounds(); bool hasBounds = false;
        foreach (Transform tile in floor)
        {
            Renderer[] renderers = tile.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) continue;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            count++; minY = Mathf.Min(minY, b.min.y); maxY = Mathf.Max(maxY, b.max.y);
            if (!hasBounds) { total = b; hasBounds = true; } else total.Encapsulate(b);
        }
        Debug.Log($"Kenney floor diagnostic: tiles={count}, expected={g.laneLength * g.laneWidth}, yRange={minY:F4}..{maxY:F4}, totalBounds={total.size}, detailsEnabled={g.useFloorDetails}");
    }

    static GameObject Load(string path)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!asset) Debug.LogWarning("Kenney asset not found: " + path);
        return asset;
    }

    static void AssignPolishAssets(LaneGenerator g)
    {
        // Fill new references without replacing the user's existing assignments.
        if (!g.arenaBorder) g.arenaBorder = Load(Arena + "border-straight.fbx");
        if (!g.castleGround) g.castleGround = Load(Castle + "ground.fbx");
        if (!g.castleSmallTree) g.castleSmallTree = Load(Castle + "tree-small.fbx");
        if (!g.castleSmallRocks) g.castleSmallRocks = Load(Castle + "rocks-small.fbx");
        if (!g.arenaDamagedColumn) g.arenaDamagedColumn = Load(Arena + "column-damaged.fbx");
        if (!g.arenaBricks) g.arenaBricks = Load(Arena + "bricks.fbx");
        if (!g.castleWallDoorway) g.castleWallDoorway = Load(Castle + "wall-doorway.fbx");
        if (!g.castleTowerRoof) g.castleTowerRoof = Load(Castle + "tower-square-roof.fbx");
        if (!g.castleTowerHexBase) g.castleTowerHexBase = Load(Castle + "tower-hexagon-base.fbx");
        if (!g.castleTowerHexMid) g.castleTowerHexMid = Load(Castle + "tower-hexagon-mid.fbx");
        if (!g.castleTowerHexRoof) g.castleTowerHexRoof = Load(Castle + "tower-hexagon-roof.fbx");
        if (!g.arcaneCrystalSmall) g.arcaneCrystalSmall = Load(Arcane + "Cenji/CrystalFormations/PROP_01_SmallCrystalCluster.fbx");
        if (!g.arcaneCrystalMedium) g.arcaneCrystalMedium = Load(Arcane + "Cenji/CrystalFormations/PROP_02_MediumCrystalCluster.fbx");
        if (!g.arcaneCrystalLarge) g.arcaneCrystalLarge = Load(Arcane + "Cenji/CrystalFormations/PROP_03_LargeCrystalCluster.fbx");
        if (!g.arcaneCrystalTall) g.arcaneCrystalTall = Load(Arcane + "Cenji/CrystalFormations/PROP_04_TallCrystalFormation.fbx");
        if (!g.arcaneCrystalBroken) g.arcaneCrystalBroken = Load(Arcane + "Cenji/CrystalFormations/PROP_08_BrokenCrystalFormation.fbx");
        if (!g.arcaneRuneCrystal) g.arcaneRuneCrystal = Load(Arcane + "Cenji/CrystalFormations/PROP_17_RuneCrystal.fbx");
        if (!g.arcaneMegalith) g.arcaneMegalith = Load(Arcane + "Cenji/AncientRocks/ROCK_Ancient_01_LargeMegalith.fbx");
        if (!g.arcaneRuinedPillar) g.arcaneRuinedPillar = Load(Arcane + "Cenji/AncientRocks/ROCK_Ancient_02_RuinedPillar.fbx");
        if (!g.arcanePlinth) g.arcanePlinth = Load(Arcane + "Cenji/AncientRocks/ROCK_Ancient_03_PlinthMedium.fbx");
        if (!g.arcaneRubble) g.arcaneRubble = Load(Arcane + "Cenji/AncientRocks/ROCK_Ancient_05_CarvedRubble.fbx");
        if (!g.arcaneArch) g.arcaneArch = Load(Arcane + "Quaternius/Ruins/Arch_Gothic_RoundColumn.fbx");
        if (!g.arcaneRoundArch) g.arcaneRoundArch = Load(Arcane + "Quaternius/Ruins/Arch_Round_RoundColumn.fbx");
        if (!g.arcaneColumn) g.arcaneColumn = Load(Arcane + "Quaternius/Ruins/Column_Round.fbx");
        if (!g.arcaneShortColumn) g.arcaneShortColumn = Load(Arcane + "Quaternius/Ruins/Column_Round_Short.fbx");
        if (!g.arcaneSquareColumn) g.arcaneSquareColumn = Load(Arcane + "Quaternius/Ruins/Column_Square.fbx");
        if (!g.arcaneBrokenWall) g.arcaneBrokenWall = Load(Arcane + "Quaternius/Ruins/Wall_ArchRound_Broken.fbx");
        if (!g.arcaneBrokenWallDouble) g.arcaneBrokenWallDouble = Load(Arcane + "Quaternius/Ruins/Wall_Double_Broken.fbx");
        if (!g.arcaneWallBroken) g.arcaneWallBroken = Load(Arcane + "Quaternius/Ruins/Wall_Broken.fbx");
        if (!g.arcaneStairs) g.arcaneStairs = Load(Arcane + "Quaternius/Ruins/Stairs_2.fbx");
        if (!g.arcaneBricks) g.arcaneBricks = Load(Arcane + "Quaternius/Ruins/Bricks.fbx");
        if (!g.fortressShader)
            g.fortressShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/HLW_FortressSurface.shader");
    }
}

// Editor-only scene diagnostics. Selecting the LaneGenerator draws one labeled
// wire bound per authored station/cluster; no debug objects are saved or built.
static class LaneOutskirtsDiagnostics
{
    public static void Log(LaneGenerator generator)
    {
        Transform decorations = generator.transform.Find("Decorations");
        HlwCameraController cameraController = Object.FindAnyObjectByType<HlwCameraController>();
        Camera camera = cameraController ? cameraController.GetComponent<Camera>() : null;
        if (!decorations || !camera || !TryGameplayFrustum(generator, cameraController, camera,
            out Plane[] planes, out Vector3 cameraPosition, out Vector3 cameraForward))
        {
            Debug.LogError("Outskirts diagnostics require Decorations and the gameplay camera.", generator);
            return;
        }

        Debug.Log($"Outskirts gameplay camera: expectedPosition={cameraPosition}, forward={cameraForward}, " +
            $"orthographic=True, size={cameraController.orthographicSize}, aspect={camera.aspect:F3}, " +
            $"pitch={cameraController.pitch}, yaw={cameraController.yaw}.", camera);
        string[] layers = { "EdgeStrip", "MajorLandmarks" };
        foreach (string layerName in layers)
        {
            Transform layer = decorations.Find(layerName);
            if (!layer) { Debug.LogError($"Missing Decorations/{layerName}.", generator); continue; }
            int valid = 0;
            foreach (Transform item in layer)
            {
                if (!TryRendererBounds(item, out Bounds bounds, out int enabledRenderers,
                    out int validMeshes, out int validMaterials)) continue;
                valid++;
                if (layerName == "MajorLandmarks")
                    Debug.Log($"{item.name}: active={item.gameObject.activeInHierarchy}, position={item.position}, " +
                        $"scale={item.lossyScale}, boundsCenter={bounds.center}, boundsSize={bounds.size}, " +
                        $"renderers={enabledRenderers}, meshes={validMeshes}, materials={validMaterials}, " +
                        $"inCameraFrustum={GeometryUtility.TestPlanesAABB(planes, bounds)}, groundMinY={bounds.min.y:F3}.", item);
            }
            Debug.Log($"Decorations/{layerName}: {layer.childCount} serialized objects, {valid} with renderer bounds.", layer);
        }

        Transform landmarks = decorations.Find("MajorLandmarks");
        Dictionary<string, Bounds> clusters = CollectClusterBounds(landmarks);
        int visibleClusters = 0;
        foreach (KeyValuePair<string, Bounds> cluster in clusters)
        {
            bool visible = GeometryUtility.TestPlanesAABB(planes, cluster.Value);
            if (visible) visibleClusters++;
            Debug.Log($"Major landmark {cluster.Key}: position={cluster.Value.center}, " +
                $"boundsSize={cluster.Value.size}, intersectsGameplayCamera={visible}.", landmarks);
        }
        Debug.Log($"Major landmarks intersecting gameplay camera frustum: {visibleClusters}/{clusters.Count}.", camera);

        Transform runes = decorations.Find("ArcaneRunes");
        Dictionary<string, Bounds> runeBounds = new Dictionary<string, Bounds>();
        if (!runes || runes.childCount != 1)
            Debug.LogError($"Expected Decorations/ArcaneRunes with one central marker; found " +
                $"{(runes ? runes.childCount : 0)}.", generator);
        else
        {
            foreach (Transform rune in runes)
            {
                Bounds bounds = new Bounds(rune.position,
                    new Vector3(generator.interLaneRuneRadius * 2f, .1f,
                        generator.interLaneRuneRadius * 2f));
                runeBounds.Add(rune.name, bounds);
                Debug.Log($"{rune.name}: position={rune.position}, radius={generator.interLaneRuneRadius:F2}, " +
                    $"intersectsGameplayCamera={GeometryUtility.TestPlanesAABB(planes, bounds)}.", rune);
            }
        }
        float cityLength = generator.LengthWorld-generator.BranchEndWorld;
        float[] representativeZ =
        {
            generator.BranchEndWorld+cityLength*.16f,
            generator.BranchEndWorld+cityLength*.47f,
            generator.BranchEndWorld+cityLength*.79f
        };
        string[] regionNames = { "beginning", "middle", "end" };
        for (int region = 0; region < representativeZ.Length; region++)
        {
            Vector3 focus = generator.transform.TransformPoint(new Vector3(generator.WidthWorld*.5f,
                cameraController.framingOffset.y, representativeZ[region]));
            Plane[] regionPlanes = CreateOrthographicPlanes(cameraController, camera, focus,
                out Vector3 regionCameraPosition, out _);
            int runesVisible = CountIntersecting(regionPlanes, runeBounds);
            int landmarkVisible = CountIntersecting(regionPlanes, clusters);
            Debug.Log($"Arcane city {regionNames[region]} view: focus={focus}, camera={regionCameraPosition}, " +
                $"interLaneRunesVisible={runesVisible}/{runeBounds.Count}, " +
                $"majorLandmarksVisible={landmarkVisible}/{clusters.Count}.", camera);
        }
    }

    static bool TryGameplayFrustum(LaneGenerator generator, HlwCameraController controller, Camera camera,
        out Plane[] planes, out Vector3 position, out Vector3 forward)
    {
        planes = null; position = forward = default;
        Transform floor = generator.transform.Find("LaneFloor");
        if (!floor) return false;
        bool found = false; Bounds mapBounds = default;
        foreach (Renderer renderer in floor.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) { mapBounds = renderer.bounds; found = true; }
            else mapBounds.Encapsulate(renderer.bounds);
        }
        if (!found) return false;

        Vector3 focus = new Vector3(mapBounds.center.x, generator.transform.position.y, mapBounds.center.z) +
            generator.transform.TransformVector(controller.framingOffset);
        planes = CreateOrthographicPlanes(controller, camera, focus, out position, out forward);
        return true;
    }

    static Plane[] CreateOrthographicPlanes(HlwCameraController controller, Camera camera, Vector3 focus,
        out Vector3 position, out Vector3 forward)
    {
        Quaternion rotation = Quaternion.Euler(controller.pitch, controller.yaw, 0f);
        forward = rotation * Vector3.forward;
        Vector3 right = rotation * Vector3.right;
        Vector3 up = rotation * Vector3.up;
        position = focus - forward * Mathf.Max(10f, controller.cameraDistance);
        float size = controller.orthographicSize;
        float halfWidth = size * camera.aspect;
        return new[]
        {
            new Plane(right, position - right * halfWidth),
            new Plane(-right, position + right * halfWidth),
            new Plane(up, position - up * size),
            new Plane(-up, position + up * size),
            new Plane(forward, position + forward * camera.nearClipPlane),
            new Plane(-forward, position + forward * camera.farClipPlane)
        };
    }

    static int CountIntersecting(Plane[] planes, Dictionary<string, Bounds> clusters)
    {
        int count = 0;
        foreach (Bounds bounds in clusters.Values)
            if (GeometryUtility.TestPlanesAABB(planes, bounds)) count++;
        return count;
    }

    [DrawGizmo(GizmoType.Selected)]
    static void DrawOutskirtsBounds(LaneGenerator generator, GizmoType gizmoType)
    {
        Transform decorations = generator.transform.Find("Decorations");
        if (!decorations) return;
        DrawLayer(decorations.Find("EdgeStrip"), new Color(.25f, .8f, 1f), "Edge");
        DrawRuneMarkers(decorations.Find("ArcaneRunes"), generator.interLaneRuneRadius);
        DrawLayer(decorations.Find("MajorLandmarks"), new Color(1f, .7f, .15f), "Landmark");
    }

    static void DrawRuneMarkers(Transform runes, float radius)
    {
        if (!runes) return;
        Color color = new Color(.15f, .8f, 1f);
        Handles.color = color;
        foreach (Transform rune in runes)
        {
            Handles.DrawWireDisc(rune.position, Vector3.up, radius, 2f);
            Handles.Label(rune.position + Vector3.up * .25f,
                $"{rune.name}  R={radius:F1}");
        }
    }

    static void DrawLayer(Transform layer, Color color, string label)
    {
        if (!layer) return;
        Dictionary<string, Bounds> clusters = CollectClusterBounds(layer);
        Gizmos.color = color;
        foreach (KeyValuePair<string, Bounds> cluster in clusters)
        {
            Gizmos.DrawWireCube(cluster.Value.center, cluster.Value.size);
            Handles.color = color;
            Handles.Label(cluster.Value.center + Vector3.up * (cluster.Value.extents.y + .2f),
                $"{label} {cluster.Key}");
        }
    }

    static string ClusterKey(string name)
    {
        if (name == "LandmarkWitheredTree") return "1_1";
        string[] parts = name.Split('_');
        if (name.Contains("Trunk") || parts.Length < 3) return name;
        return parts[parts.Length - 2] + "_" + parts[parts.Length - 1];
    }

    static Dictionary<string, Bounds> CollectClusterBounds(Transform layer)
    {
        Dictionary<string, Bounds> clusters = new Dictionary<string, Bounds>();
        if (!layer) return clusters;
        foreach (Transform item in layer)
        {
            if (!TryRendererBounds(item, out Bounds bounds, out _, out _, out _)) continue;
            string key = ClusterKey(item.name);
            if (clusters.TryGetValue(key, out Bounds combined))
            {
                combined.Encapsulate(bounds);
                clusters[key] = combined;
            }
            else clusters.Add(key, bounds);
        }
        return clusters;
    }

    static bool TryRendererBounds(Transform root, out Bounds bounds, out int enabledRenderers,
        out int validMeshes, out int validMaterials)
    {
        bounds = default;
        enabledRenderers = validMeshes = validMaterials = 0;
        bool found = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            enabledRenderers++;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if ((filter && filter.sharedMesh) || (skinned && skinned.sharedMesh)) validMeshes++;
            foreach (Material material in renderer.sharedMaterials) if (material && material.shader) validMaterials++;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found;
    }
}
