using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Procedurally builds the first Hero Line Wars battle lane.</summary>
[ExecuteAlways]
public partial class LaneGenerator : MonoBehaviour
{
    public const string GenerationVersion = "ClassicTwoLaneV2";
    [Header("Lane")]
    [Min(64)] public int laneLength = 96;
    [Min(6)] public int laneWidth = 15;
    [Min(0.25f)] public float cellSize = 1f;
    [Header("Classic two-lane battlefield")]
    [HideInInspector] public int battleLaneCount = 2;
    [Range(.12f, .35f)] public float mergeStartNormalized = .18f;
    [Tooltip("Clear world-space distance between adjacent physical roads.")]
    [Min(8f)] public float laneGap = 18f;
    [Min(0.05f)] public float floorThickness = 0.15f;
    [Header("Walls and pillars")]
    [Min(0.1f)] public float wallHeight = 0.8f;
    [Min(0.1f)] public float wallThickness = 0.35f;
    [Min(1)] public int pillarSpacing = 4;
    [Header("Visuals")]
    public Material floorMaterial;
    public Material wallMaterial;
    public Material accentMaterial;
    public bool createScalePlaceholders = false;
    public bool generateOnEnable = true;
    [Header("Kenney environment assets")]
    public GameObject arenaFloor;
    public GameObject arenaFloorDetail;
    public GameObject arenaWall;
    public GameObject arenaWallCorner;
    public GameObject arenaWallGate;
    public GameObject arenaColumn;
    public GameObject arenaBanner;
    public GameObject castleWallPillar;
    public GameObject castleFlagBannerLong;
    public GameObject castleFlagBannerShort;
    public GameObject castleTowerBase;
    public GameObject castleTowerArch;
    [Tooltip("Raised floor details are used on the outside apron only; playable paving stays flat.")]
    public bool useFloorDetails = false;
    [Tooltip("Diagnostic only: replaces generated floor materials with one plain URP/Unlit light-gray material.")]
    public bool useDebugFloorMaterial = false;
    [Header("Modern fantasy styling")]
    [Range(0f, 1f)] public float tileVariation = 0.12f;
    public bool createEdgeDetails = true;

    [SerializeField, HideInInspector] Material floorVariantMaterial;
    [SerializeField, HideInInspector] Material wallAccentMaterial;
    [SerializeField, HideInInspector] Material grassMaterial;
    [SerializeField, HideInInspector] Material debugFloorMaterial;

    public float LengthWorld => laneLength * cellSize;
    public float RoadWidth => laneWidth * cellSize;
    public float WidthWorld => (battleLaneCount * laneWidth + (battleLaneCount - 1) * Mathf.Max(2f, laneGap)) * cellSize;
    public float BranchStartWorld => 0f;
    public float BranchEndWorld => 0f;
    public float MergeStartWorld => BranchEndWorld;
    public float FinalRoadStartWorld => BranchStartWorld;
    public float LaneRoadWidth => RoadWidth;
    public float RoadStartX(int laneIndex) => laneIndex * (laneWidth + Mathf.Max(2f, laneGap)) * cellSize;
    public float RoadCenterX(int laneIndex) => RoadStartX(Mathf.Clamp(laneIndex, 0, battleLaneCount - 1)) + RoadWidth * .5f;
    public int PlayerDefenseLaneIndex => 0;
    public int EnemyDefenseLaneIndex => 1;

    public float LaneCenterX(int laneIndex, float z)
    {
        return RoadCenterX(laneIndex);
    }

    public bool IsInMergeZone(float z) => false;
    public bool IsConnectorRow(int row) => false;

    public int LaneIndexForX(float x)
    {
        int best = 0;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < battleLaneCount; i++)
        {
            float distance = Mathf.Abs(x - RoadCenterX(i));
            if (distance < bestDistance) { best = i; bestDistance = distance; }
        }
        return best;
    }

    public bool IsPointInRoad(int laneIndex, float x, float z, float inset = 0f)
    {
        if (laneIndex < 0 || laneIndex >= battleLaneCount || z < 0f || z >= LengthWorld) return false;
        float halfWidth = Mathf.Max(.05f, RoadWidth * .5f - Mathf.Max(0f, inset));
        return Mathf.Abs(x - RoadCenterX(laneIndex)) < halfWidth;
    }

    public bool IsWalkable(float x, float z)
    {
        int row = Mathf.FloorToInt(z / cellSize);
        int column = Mathf.FloorToInt(x / cellSize);
        return IsWalkableCell(column, row);
    }

    bool IsWalkableCell(int column, int row)
    {
        if (row < 0 || row >= laneLength || column < 0 || column >= Mathf.RoundToInt(WidthWorld / cellSize)) return false;
        float x = (column + .5f) * cellSize;
        float z = (row + .5f) * cellSize;
        for (int laneIndex = 0; laneIndex < battleLaneCount; laneIndex++)
            if (IsPointInRoad(laneIndex, x, z)) return true;
        return false;
    }

    // Edit-mode authoring is owned by LaneGeneratorEditor.GenerateLane. Running
    // Generate from OnEnable caused an unlogged, unsaved second regeneration on
    // scene load/script reload. Runtime generation remains in Start.
    void Start() { if (Application.isPlaying) Generate(); }

    public void Generate()
    {
        // Existing prototype scenes may already be open when the new script
        // compiles, so upgrade their in-memory values before saving the layout.
        if (!transform.Find(GenerationVersion))
        {
            // ClassicTwoLaneV2 is the requested 1.5x wider / .75x shorter
            // revision of the serialized 10 x 128 battlefield.
            laneLength = 96;
            laneWidth = 15;
        }
        battleLaneCount = 2;
        laneGap = Mathf.Max(18f, laneGap);
        laneLength = Mathf.Max(64, laneLength); laneWidth = Mathf.Max(6, laneWidth);
        ConfigureLighting();
        ClearGenerated();
        EnsureMaterials();
        if (arenaFloor) EnsureFortressMaterials();
        Transform floor = Group("LaneFloor");
        Transform walls = Group("LaneWalls");
        Transform pillars = Group("DecorativePillars");
        Transform spawn = Group("SpawnArea");
        Transform goal = Group("GoalArea");
        Transform decor = Group("Decorations");
        Transform baseGround = Group("BattlefieldBaseGround");
        Group("ClassicTwoLaneLayout");
        Group("ArcaneRecoveryV3");
        Group("StructuredOutskirtsV4");
        Group(GenerationVersion);
        HlwCameraController gameplayCamera = FindAnyObjectByType<HlwCameraController>();
        // One quad is cheap even at overview zoom. Leave enough dark outskirts
        // for the angled 40/75 camera at both map endpoints and maximum zoom.
        float overviewPadding = 4f * (gameplayCamera ? gameplayCamera.maximumOrthographicSize : 40f);
        float outskirtsMargin = Mathf.Max(WidthWorld + 16f * cellSize, overviewPadding);
        BuildBattlefieldBaseGround(baseGround, outskirtsMargin, Mathf.Max(20f * cellSize, overviewPadding));

        bool usingKenney = arenaFloor;
        float roadSurfaceY = 0f;
        for (int z = 0; z < laneLength; z++)
            for (int x = 0; x < Mathf.RoundToInt(WidthWorld / cellSize); x++)
            {
                if (!IsWalkableCell(x, z)) continue;
                Vector3 pos = new Vector3((x + .5f) * cellSize, 0, (z + .5f) * cellSize);
                GameObject tile;
                if (usingKenney)
                {
                    tile = CreateAsset($"Floor_{x:00}_{z:00}", floor, arenaFloor, pos, cellSize, cellSize, 0);
                    SetSharedMaterial(tile, pavingMaterial);
                }
                else tile = CreateCube($"Tile_{x:00}_{z:00}", floor, pos + Vector3.down * floorThickness * .5f, new Vector3(cellSize, floorThickness, cellSize), (x + z) % 4 == 0 ? floorVariantMaterial : floorMaterial);

                // Align the visible mesh rather than assuming its FBX pivot is centered.
                Bounds tileBounds = PrefabBounds(tile);
                tile.transform.position += new Vector3(pos.x - tileBounds.center.x, 0, pos.z - tileBounds.center.z);
                if (z == 0 && roadSurfaceY == 0f) roadSurfaceY = PrefabBounds(tile).max.y;
                if (useDebugFloorMaterial)
                {
                    Material debug = DebugFloorMaterial();
                    foreach (Renderer renderer in tile.GetComponentsInChildren<Renderer>(true))
                    {
                        Material[] slots = renderer.sharedMaterials;
                        for (int i = 0; i < slots.Length; i++) slots[i] = debug;
                        renderer.sharedMaterials = slots;
                    }
                }
            }

        BuildPhysicalRoadWalls(walls, pillars, usingKenney);
        if (usingKenney) BuildClassicTwoLaneEnds(spawn, goal);
        else
        {
            CreatePortal(spawn, new Vector3(RoadCenterX(PlayerDefenseLaneIndex), .35f, .8f),
                new Color(.1f, .65f, 1f), "PlayerHeroSpawn");
            CreatePortal(spawn, new Vector3(RoadCenterX(EnemyDefenseLaneIndex), .35f, .8f),
                new Color(.15f, .85f, .45f), "AlliedUnitSpawn");
            CreateCrystal(goal, new Vector3(RoadCenterX(PlayerDefenseLaneIndex), 1f, LengthWorld - .8f),
                new Color(1f, .08f, .05f), "EnemyMobOrigin");
            CreateCrystal(goal, new Vector3(RoadCenterX(EnemyDefenseLaneIndex), 1f, LengthWorld - .8f),
                new Color(1f, .35f, .08f), "EnemyHeroSanctum");
        }
        CreateDecorations(decor);
        if (createScalePlaceholders) CreatePlaceholders(decor);
    }

    // The movement grid stays deterministic; only the visible paving in the
    // funnel is clipped to the actual sloping road boundaries. Each cell owns
    // disjoint polygons, so there are no coplanar overlapping road surfaces.
    void BuildSmoothBranchPaving(Transform parent, float y, Material material)
    {
        int firstRow = Mathf.RoundToInt(BranchStartWorld / cellSize);
        int endRow = Mathf.RoundToInt(BranchEndWorld / cellSize);
        int columns = Mathf.RoundToInt(WidthWorld / cellSize);
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uv = new List<Vector2>();
        for (int row = firstRow; row < endRow; row++)
            for (int column = 0; column < columns; column++)
            {
                float x0 = column * cellSize, x1 = x0 + cellSize;
                float z0 = row * cellSize, z1 = z0 + cellSize;
                for (int laneIndex = 0; laneIndex < battleLaneCount; laneIndex++)
                {
                    List<Vector2> polygon = new List<Vector2> {
                        new Vector2(x0, z0), new Vector2(x0, z1),
                        new Vector2(x1, z1), new Vector2(x1, z0) };
                    polygon = ClipRoadEdge(polygon, laneIndex, -1);
                    polygon = ClipRoadEdge(polygon, laneIndex, 1);
                    for (int i = polygon.Count - 1; i >= 0 && polygon.Count > 1; i--)
                        if ((polygon[i] - polygon[(i + 1) % polygon.Count]).sqrMagnitude < .00000001f)
                            polygon.RemoveAt(i);
                    if (polygon.Count < 3) continue;
                    float twiceArea = 0f;
                    for (int i = 0; i < polygon.Count; i++)
                    {
                        Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                        twiceArea += a.x * b.y - b.x * a.y;
                    }
                    if (Mathf.Abs(twiceArea) < .00001f) continue;
                    int start = vertices.Count;
                    foreach (Vector2 point in polygon)
                    {
                        vertices.Add(new Vector3(point.x, y, point.y));
                        uv.Add(new Vector2(point.x / cellSize, point.y / cellSize));
                    }
                    for (int i = 1; i < polygon.Count - 1; i++)
                    { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
                }
            }
        if (triangles.Count == 0) return;
        GameObject paving = new GameObject("SmoothMergeJunction", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        paving.transform.SetParent(parent, false);
        Mesh mesh = new Mesh { name = "Smooth Merge Junction" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        paving.GetComponent<MeshFilter>().sharedMesh = mesh;
        paving.GetComponent<MeshCollider>().sharedMesh = mesh;
        paving.GetComponent<MeshRenderer>().sharedMaterial = useDebugFloorMaterial ? DebugFloorMaterial() : material;
    }

    List<Vector2> ClipRoadEdge(List<Vector2> polygon, int laneIndex, int side)
    {
        if (polygon.Count == 0) return polygon;
        List<Vector2> clipped = new List<Vector2>();
        Vector2 previous = polygon[polygon.Count - 1];
        float previousDistance = RoadEdgeDistance(previous, laneIndex, side);
        foreach (Vector2 current in polygon)
        {
            float distance = RoadEdgeDistance(current, laneIndex, side);
            if ((distance >= 0f) != (previousDistance >= 0f))
            {
                float t = previousDistance / (previousDistance - distance);
                clipped.Add(Vector2.Lerp(previous, current, t));
            }
            if (distance >= 0f) clipped.Add(current);
            previous = current; previousDistance = distance;
        }
        return clipped;
    }

    float RoadEdgeDistance(Vector2 point, int laneIndex, int side)
    {
        float center = LaneCenterX(laneIndex, point.y);
        if (side < 0)
        {
            float left = center - RoadWidth * .5f;
            if (laneIndex > 0) left = Mathf.Max(left, (center + LaneCenterX(laneIndex - 1, point.y)) * .5f);
            return point.x - left;
        }
        float right = center + RoadWidth * .5f;
        if (laneIndex < battleLaneCount - 1)
            right = Mathf.Min(right, (center + LaneCenterX(laneIndex + 1, point.y)) * .5f);
        return right - point.x;
    }

    GameObject CreateAsset(string name, Transform parent, GameObject prefab, Vector3 position, float targetX, float targetZ, float yRotation, Material materialOverride = null)
    {
        GameObject go = Instantiate(prefab, parent);
        go.name = name;
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0, yRotation, 0);
        Bounds bounds = PrefabBounds(go);
        if (bounds.size.x > .001f && bounds.size.z > .001f)
            go.transform.localScale = new Vector3(go.transform.localScale.x * targetX / bounds.size.x, go.transform.localScale.y, go.transform.localScale.z * targetZ / bounds.size.z);
        if (materialOverride)
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = materialOverride;
        // Re-read bounds after scaling. Using the pre-scale bounds here can leave
        // imported FBX pieces below the lane or intersecting adjacent surfaces.
        Bounds placedBounds = PrefabBounds(go);
        go.transform.position += Vector3.up * (position.y - placedBounds.min.y);
        return go;
    }

    Bounds PrefabBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    void CreateDecorations(Transform parent)
    {
        if (!createEdgeDetails) return;
        if (arenaFloor)
        {
            BuildFortressDressing(parent);
            return;
        }
        for (int z = 3; z < laneLength; z += 8)
        {
            CreateCylinder("Ruin", parent, new Vector3(-2.2f, .35f, z * cellSize), .35f, .7f, wallMaterial);
            CreateCylinder("Ruin", parent, new Vector3(WidthWorld + 2.2f, .25f, (z + 3) * cellSize), .25f, .5f, wallMaterial);
            CreateCube("GrassEdge", parent, new Vector3(-1.35f, .025f, (z + 1) * cellSize), new Vector3(.9f, .05f, 1.4f), grassMaterial);
            CreateCube("GrassEdge", parent, new Vector3(WidthWorld + 1.35f, .025f, (z + 4) * cellSize), new Vector3(.9f, .05f, 1.4f), grassMaterial);
            CreateTorch(parent, new Vector3(-.65f, 1.0f, (z + 2) * cellSize), new Color(1f, .55f, .16f));
            CreateTorch(parent, new Vector3(WidthWorld + .65f, 1.0f, (z + 6) * cellSize), new Color(1f, .55f, .16f));
        }
    }

    void CreateWallDetails(Transform parent)
    {
        if (arenaFloor) return;
        for (int z = 2; z < laneLength; z += pillarSpacing)
        {
            CreateCube("WallCap_L", parent, new Vector3(0, wallHeight + .08f, z * cellSize), new Vector3(wallThickness + .16f, .16f, .7f), wallAccentMaterial);
            CreateCube("WallCap_R", parent, new Vector3(WidthWorld, wallHeight + .08f, z * cellSize), new Vector3(wallThickness + .16f, .16f, .7f), wallAccentMaterial);
        }
    }

    void CreateTorch(Transform parent, Vector3 pos, Color color)
    {
        CreateCylinder("TorchStand", parent, pos - Vector3.up * .35f, .08f, .7f, wallAccentMaterial);
        Material glow = MakeEmissionMaterial("Torch Glow", color, 3f);
        CreateSphere("TorchFlame", parent, pos, .16f, glow);
    }

    void CreatePlaceholders(Transform parent)
    {
        CreateCapsule("HeroScale", parent, new Vector3(WidthWorld * .5f, 1f, LengthWorld * .35f), .45f, 1.8f, accentMaterial);
        for (int i = 0; i < 5; i++) CreateCapsule("MobScale", parent, new Vector3(1.5f + i * .8f, .45f, LengthWorld * .42f), .25f, .8f, accentMaterial);
    }

    void CreatePortal(Transform p, Vector3 pos, Color color, string name)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color }; m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 2.5f);
        CreateCylinder(name, p, pos, .9f, .12f, m);
        CreateCylinder(name + "Glow", p, pos + Vector3.up * .05f, .62f, .08f, m);
    }
    void CreateCrystal(Transform p, Vector3 pos, Color color, string name)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color }; m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 2.2f);
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(p); go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, 45, 45); go.transform.localScale = new Vector3(.7f, 1.8f, .7f); go.GetComponent<Renderer>().sharedMaterial = m;
    }
    GameObject CreateCube(string n, Transform p, Vector3 pos, Vector3 scale, Material m) => CreatePrimitive(PrimitiveType.Cube, n, p, pos, scale, m);
    GameObject CreateCylinder(string n, Transform p, Vector3 pos, float radius, float height, Material m) => CreatePrimitive(PrimitiveType.Cylinder, n, p, pos, new Vector3(radius * 2, height * .5f, radius * 2), m);
    GameObject CreateCapsule(string n, Transform p, Vector3 pos, float radius, float height, Material m) => CreatePrimitive(PrimitiveType.Capsule, n, p, pos, new Vector3(radius * 2, height * .5f, radius * 2), m);
    GameObject CreateSphere(string n, Transform p, Vector3 pos, float radius, Material m) => CreatePrimitive(PrimitiveType.Sphere, n, p, pos, Vector3.one * radius * 2f, m);
    GameObject CreatePrimitive(PrimitiveType t, string n, Transform p, Vector3 pos, Vector3 scale, Material m) { GameObject g = GameObject.CreatePrimitive(t); g.name = n; g.transform.SetParent(p); g.transform.position = pos; g.transform.localScale = scale; g.GetComponent<Renderer>().sharedMaterial = m; return g; }
    Transform Group(string n) { Transform t = transform.Find(n); if (!t) { t = new GameObject(n).transform; t.SetParent(transform); } return t; }
    void ClearGenerated()
    {
        // Only replace the six groups owned by this generator. User-authored
        // children (including cameras and lights) must survive regeneration.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            switch (child.name)
            {
                case "LaneFloor": case "LaneWalls": case "DecorativePillars":
                case "SpawnArea": case "GoalArea": case "Decorations": case "BattlefieldBaseGround":
                case "LaneGuides": case "ThreeRoadLayoutV2": case "ArcaneRecoveryV3":
                case "StructuredOutskirtsV4":
                case "OutskirtsVisibilityV5":
                case "ArcaneCityV6":
                case "WiderArcaneDistrictsV7":
                case "TwinArcaneRunesV8":
                case "ClassicTwoLaneLayout":
                case GenerationVersion:
                    DestroyImmediate(child.gameObject);
                    break;
            }
        }
    }
    void EnsureMaterials()
    {
        if (!floorMaterial) floorMaterial = MakeMaterial("Lane Stone", new Color(.34f, .37f, .42f));
        if (!wallMaterial) wallMaterial = MakeMaterial("Fortress Stone", new Color(.23f, .26f, .32f));
        if (!accentMaterial) accentMaterial = MakeEmissionMaterial("Magic Accent", new Color(.08f, .35f, .65f), 1.3f);
        if (!floorVariantMaterial) floorVariantMaterial = MakeMaterial("Lane Stone Variant", Color.Lerp(floorMaterial.color, new Color(.5f, .52f, .56f), tileVariation));
        if (!wallAccentMaterial) wallAccentMaterial = MakeMaterial("Fortress Trim", new Color(.42f, .38f, .3f));
        if (!grassMaterial) grassMaterial = MakeMaterial("Grass Edge", new Color(.16f, .3f, .18f));
    }
    Material MakeMaterial(string n, Color c) { Material m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = n, color = c }; return m; }
    Material MakeEmissionMaterial(string n, Color c, float intensity) { Material m = MakeMaterial(n, c); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * intensity); return m; }

    Material DebugFloorMaterial()
    {
        if (debugFloorMaterial) return debugFloorMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader) throw new System.InvalidOperationException("URP/Unlit shader is missing; cannot apply the diagnostic floor material.");
        debugFloorMaterial = new Material(shader) { name = "DEBUG Floor Unlit Light Gray" };
        debugFloorMaterial.SetColor("_BaseColor", new Color(.72f, .72f, .72f, 1f));
        debugFloorMaterial.SetTexture("_BaseMap", null);
        return debugFloorMaterial;
    }

    void ConfigureLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.58f, .67f, .78f);
        RenderSettings.ambientEquatorColor = new Color(.37f, .43f, .52f);
        RenderSettings.ambientGroundColor = new Color(.2f, .25f, .32f);
        RenderSettings.ambientIntensity = .9f;
        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include);
        Light sun = RenderSettings.sun;
        if (sun && (sun.gameObject.scene != gameObject.scene || sun.type != LightType.Directional)) sun = null;
        // Prefer an active key light; the saved prototype's original light root
        // was inactive and unnamed, so handle that existing root as well.
        foreach (Light light in lights)
        {
            if (light.gameObject.scene != gameObject.scene || light.type != LightType.Directional) continue;
            if (!sun && light.isActiveAndEnabled) sun = light;
        }
        if (!sun)
            foreach (Light light in lights)
                if (light.gameObject.scene == gameObject.scene && light.type == LightType.Directional &&
                    !light.transform.parent && (light.name == "Lighting" || string.IsNullOrEmpty(light.name)))
                { sun = light; break; }
        foreach (Light light in lights)
        {
            if (light != sun && light.gameObject.scene == gameObject.scene && light.type == LightType.Directional)
                light.enabled = false;
        }
        if (sun != null)
        {
            sun.gameObject.SetActive(true);
            sun.enabled = true;
            sun.color = new Color(1f, .98f, .95f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .62f;
            sun.transform.rotation = Quaternion.Euler(48f, -40f, 0f);
            RenderSettings.sun = sun;
        }
    }
}
