using UnityEngine;
using UnityEngine.Rendering;

// Presentation only. Generation lifecycle, lane coordinates and camera remain in
// the existing generator/controller. All parts reuse imported meshes/materials.
public partial class LaneGenerator
{
    [Header("Fortress presentation")]
    [Tooltip("Density of the optional outside ruins, standards, rocks and sparse trees. Zero keeps only the stone surround.")]
    [Range(0f, 1f)] public float decorationDensity = .5f;
    public bool useBanners = true;
    public bool useGreenery = true;
    public Color spawnAccentColor = new Color(.12f, .55f, 1f);
    public Color goalAccentColor = new Color(.9f, .14f, .22f);

    [Header("Arcane battleground")]
    [Tooltip("Brightness of the inlaid floor and stone runes. Zero removes their glow and markings.")]
    [Range(0f, 1.5f)] public float runeIntensity = .7f;
    [Tooltip("Subtle pulse amount for existing rune and arcane-stone emission. Set to zero to disable animation.")]
    [Range(0f, .5f)] public float magicPulse = .16f;
    public Color arcaneAccentColor = new Color(.12f, .72f, .8f);
    [Tooltip("Faction-color emission on the gate/core. Rune brightness uses Rune Intensity. No extra lights or bloom.")]
    [Range(0f, 1.5f)] public float endpointGlow = .65f;
    [Tooltip("Place a few imported runestones and broken masonry groups on the outside apron. Uses Decoration Density.")]
    public bool useArcaneProps = true;
    [Tooltip("Strength of the large, faded arcane floor motifs on the outer stone apron.")]
    [Range(0f, .8f)] public float outskirtsRuneStrength = .35f;
    [Tooltip("Radius in lane-cell units for the subtle rune circles on the outer apron.")]
    [Range(1f, 5f)] public float outskirtsRuneRadius = 2.4f;
    [Header("Inter-lane arcane rune")]
    [Tooltip("World-space radius of the primary rune circle in the district between the two roads.")]
    [Range(2.5f, 5.25f)] public float interLaneRuneRadius = 4.6f;
    [Tooltip("Emission multiplier for the primary inter-lane rune circle.")]
    [Range(0f, 2.5f)] public float interLaneRuneBrightness = 1.35f;
    public Color interLaneRuneAColor = new Color(.08f, .78f, 1f);
    public Color interLaneRuneBColor = new Color(.16f, .58f, 1f);
    public Color interLaneRuneAccentColor = new Color(.58f, .2f, .9f);
    [Tooltip("Subtle emission pulse. This does not create lights or particles.")]
    [Range(0f, .25f)] public float interLaneRunePulse = .1f;

    [Header("Additional Kenney assets")]
    public GameObject arenaBorder;
    public GameObject castleGround;
    public GameObject castleSmallTree;
    public GameObject castleSmallRocks;
    public GameObject arenaDamagedColumn;
    public GameObject arenaBricks;
    public GameObject castleWallDoorway;
    public GameObject castleTowerRoof;
    public GameObject castleTowerHexBase;
    public GameObject castleTowerHexMid;
    public GameObject castleTowerHexRoof;
    [Header("Imported arcane environment")]
    public GameObject arcaneCrystalSmall;
    public GameObject arcaneCrystalMedium;
    public GameObject arcaneCrystalLarge;
    public GameObject arcaneCrystalTall;
    public GameObject arcaneCrystalBroken;
    public GameObject arcaneRuneCrystal;
    public GameObject arcaneMegalith;
    public GameObject arcaneRuinedPillar;
    public GameObject arcanePlinth;
    public GameObject arcaneRubble;
    public GameObject arcaneArch;
    public GameObject arcaneRoundArch;
    public GameObject arcaneColumn;
    public GameObject arcaneShortColumn;
    public GameObject arcaneSquareColumn;
    public GameObject arcaneBrokenWall;
    public GameObject arcaneBrokenWallDouble;
    public GameObject arcaneWallBroken;
    public GameObject arcaneStairs;
    public GameObject arcaneBricks;
    [HideInInspector] public Shader fortressShader;

    [SerializeField, HideInInspector] Material pavingMaterial;
    [SerializeField, HideInInspector] Material arenaStoneMaterial;
    [SerializeField, HideInInspector] Material castleStoneMaterial;
    [SerializeField, HideInInspector] Material blueArenaMaterial;
    [SerializeField, HideInInspector] Material redArenaMaterial;
    [SerializeField, HideInInspector] Material redCastleMaterial;
    [SerializeField, HideInInspector] Material blueCastleMaterial;
    [SerializeField, HideInInspector] Material natureMaterial;
    [SerializeField, HideInInspector] Material gardenMaterial;
    [SerializeField, HideInInspector] Material arcaneStoneMaterial;
    [SerializeField, HideInInspector] Material blueRunestoneMaterial;
    [SerializeField, HideInInspector] Material redRunestoneMaterial;
    [SerializeField, HideInInspector] Material gatewayMaterial;
    [SerializeField, HideInInspector] Material blueGatewayArchMaterial;
    [SerializeField, HideInInspector] Material castleArcaneRoofMaterial;
    [SerializeField, HideInInspector] Material goalCoreMaterial;
    [SerializeField, HideInInspector] Material crystalCyanMaterial;
    [SerializeField, HideInInspector] Material crystalPurpleMaterial;

    void EnsureFortressMaterials()
    {
        if (!fortressShader) fortressShader = Shader.Find("Hero Line Wars/Fortress Surface");
        if (!fortressShader)
            throw new System.InvalidOperationException("Fortress Surface shader missing. Run Setup Kenney Environment Assets after Unity finishes importing.");

        PrepareMaterial(ref pavingMaterial, arenaFloor, "HLW Paving", .9f, Color.white, 0);
        pavingMaterial.SetColor("_BaseColor", new Color(.42f, .49f, .57f));
        pavingMaterial.SetFloat("_Smoothness", .12f);
        pavingMaterial.EnableKeyword("_PAVING");
        pavingMaterial.SetFloat("_Paving", 1);
        pavingMaterial.SetFloat("_Variation", tileVariation);
        pavingMaterial.SetFloat("_MagicPulse", magicPulse);
        pavingMaterial.SetColor("_SpawnColor", spawnAccentColor);
        pavingMaterial.SetColor("_GoalColor", goalAccentColor);
        PrepareMaterial(ref arenaStoneMaterial, arenaWall, "HLW Arena Limestone", .75f, Color.white, 0);
        PrepareMaterial(ref castleStoneMaterial, castleTowerBase, "HLW Castle Limestone", .75f, Color.white, 0);
        foreach (Material stone in new[] { arenaStoneMaterial, castleStoneMaterial })
        {
            stone.SetFloat("_StoneDefinition", 1);
            stone.SetFloat("_Smoothness", .16f);
        }
        arenaStoneMaterial.SetColor("_BaseColor", new Color(.4f, .46f, .53f));
        // A restrained warm limestone variant separates Castle caps and selected
        // wall stones from the cool Arena masonry without adding new textures.
        castleStoneMaterial.SetColor("_BaseColor", new Color(.47f, .46f, .44f));
        PrepareMaterial(ref blueArenaMaterial, arenaWallGate, "HLW Blue Gateway", .4f, spawnAccentColor, .8f);
        PrepareMaterial(ref redArenaMaterial, arenaBanner, "HLW Crimson Standards", .4f, goalAccentColor, .85f);
        PrepareMaterial(ref redCastleMaterial, castleFlagBannerShort, "HLW Crimson Fortress", .4f, goalAccentColor, .85f);
        PrepareMaterial(ref blueCastleMaterial, castleFlagBannerLong, "HLW Blue Banners", .4f, spawnAccentColor, .85f);
        PrepareMaterial(ref natureMaterial, castleGround, "HLW Perimeter Garden", 0, Color.white, 0);
        PrepareMaterial(ref gardenMaterial, castleGround, "HLW Arcane Stone Surround", .94f, Color.white, 0);
        gardenMaterial.EnableKeyword("_SURROUND");
        gardenMaterial.SetFloat("_Surround", 1);
        gardenMaterial.SetColor("_BaseColor", new Color(.145f, .19f, .235f));
        gardenMaterial.SetFloat("_Smoothness", .1f);
        gardenMaterial.SetColor("_RuneColor", arcaneAccentColor);
        gardenMaterial.SetFloat("_RuneIntensity", runeIntensity);
        gardenMaterial.SetFloat("_OutskirtsRuneStrength", outskirtsRuneStrength);
        gardenMaterial.SetFloat("_OutskirtsRuneRadius", outskirtsRuneRadius);
        float runeZ = LengthWorld * .43f;
        float gapA = RoadStartX(0) + RoadWidth + laneGap * cellSize * .5f;
        Vector3 runeAWorld = transform.TransformPoint(new Vector3(gapA, 0f, runeZ));
        float coordinateScale = Mathf.Max(.001f, cellSize);
        gardenMaterial.SetVector("_DistrictRuneA", new Vector4(runeAWorld.x / coordinateScale,
            runeAWorld.z / coordinateScale, interLaneRuneRadius / coordinateScale, -.14f));
        // The classic battlefield has one district. Keep the legacy second
        // shader input safely outside the generated footprint so an old
        // material cannot paint a phantom rune beyond the second road.
        gardenMaterial.SetVector("_DistrictRuneB", new Vector4(-10000f, -10000f,
            interLaneRuneRadius / coordinateScale, .21f));
        gardenMaterial.SetColor("_DistrictRuneAColor", interLaneRuneAColor);
        gardenMaterial.SetColor("_DistrictRuneBColor", interLaneRuneBColor);
        gardenMaterial.SetColor("_DistrictRuneAccentColor", interLaneRuneAccentColor);
        gardenMaterial.SetFloat("_DistrictRuneBrightness", interLaneRuneBrightness);
        gardenMaterial.SetFloat("_DistrictRunePulse", interLaneRunePulse);
        float cityStart = BranchEndWorld / cellSize;
        float cityLength = laneLength - cityStart;
        gardenMaterial.SetVector("_CityLayout", new Vector4(cityStart,
            cityStart + cityLength * .16f, cityStart + cityLength * .47f,
            cityStart + cityLength * .79f));
        PrepareMaterial(ref arcaneStoneMaterial, arenaColumn, "HLW Arcane Runestones", .8f, arcaneAccentColor, 0);
        PrepareMaterial(ref blueRunestoneMaterial, arenaColumn, "HLW Entry Runestones", .7f, spawnAccentColor, 0);
        PrepareMaterial(ref redRunestoneMaterial, arenaDamagedColumn, "HLW Crimson Runestones", .7f, goalAccentColor, 0);
        PrepareMaterial(ref gatewayMaterial, arenaWallGate, "HLW Arcane Gateway Stone", .8f, spawnAccentColor, .28f);
        gatewayMaterial.SetColor("_BaseColor", new Color(.42f, .5f, .6f));
        PrepareMaterial(ref blueGatewayArchMaterial, castleWallDoorway, "HLW Blue Castle Gateway", .8f, spawnAccentColor, .22f);
        blueGatewayArchMaterial.EnableKeyword("_ARCANE_STONE");
        blueGatewayArchMaterial.SetFloat("_ArcaneStone", 1);
        blueGatewayArchMaterial.SetFloat("_StoneDefinition", .75f);
        blueGatewayArchMaterial.SetColor("_BaseColor", new Color(.46f, .52f, .6f));
        blueGatewayArchMaterial.SetFloat("_EmissionStrength", endpointGlow * .18f);
        PrepareMaterial(ref goalCoreMaterial, castleTowerArch, "HLW Crimson Core Stone", .85f, goalAccentColor, .35f);
        goalCoreMaterial.SetColor("_BaseColor", new Color(.38f, .43f, .5f));
        PrepareMaterial(ref castleArcaneRoofMaterial, castleTowerHexRoof, "HLW Arcane Sanctum Roofs", .78f, arcaneAccentColor, 0);
        castleArcaneRoofMaterial.EnableKeyword("_ARCANE_STONE");
        castleArcaneRoofMaterial.SetFloat("_ArcaneStone", 1);
        castleArcaneRoofMaterial.SetColor("_BaseColor", new Color(.38f, .48f, .56f));
        castleArcaneRoofMaterial.SetFloat("_StoneDefinition", .8f);
        castleArcaneRoofMaterial.SetFloat("_MagicPulse", magicPulse);
        foreach (Material stone in new[] { arcaneStoneMaterial, blueRunestoneMaterial, redRunestoneMaterial })
        {
            stone.EnableKeyword("_ARCANE_STONE");
            stone.SetFloat("_ArcaneStone", 1);
            stone.SetColor("_BaseColor", new Color(.34f, .44f, .52f));
            stone.SetFloat("_StoneDefinition", .85f);
            stone.SetFloat("_Smoothness", .16f);
        }
        // Gate/core keep their own Kenney palette maps; the glyph overlay is
        // evaluated on those same opaque surfaces, never a second mesh layer.
        foreach (Material endpoint in new[] { gatewayMaterial, goalCoreMaterial })
        {
            endpoint.EnableKeyword("_ARCANE_STONE");
            endpoint.SetFloat("_ArcaneStone", 1);
            endpoint.SetFloat("_EmissionStrength", endpointGlow * .25f);
            endpoint.SetFloat("_StoneDefinition", .65f);
        }
        EnsureCrystalMaterial(ref crystalCyanMaterial, "HLW Crystal Cyan", new Color(.05f, .72f, .95f), 1.8f);
        EnsureCrystalMaterial(ref crystalPurpleMaterial, "HLW Crystal Violet", new Color(.48f, .16f, .95f), 1.35f);
    }

    static void EnsureCrystalMaterial(ref Material material, string name, Color color, float emission)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new System.InvalidOperationException("URP/Lit shader is missing; cannot create arcane crystal materials.");
        if (!material || material.shader != shader) material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color * .58f);
        material.SetColor("_EmissionColor", color * emission);
        material.EnableKeyword("_EMISSION");
        material.SetFloat("_Smoothness", .32f);
        material.SetFloat("_Metallic", .05f);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
    }

    void PrepareMaterial(ref Material material, GameObject source, string name, float stoneMix, Color team, float teamAmount)
    {
        Renderer renderer = source ? source.GetComponentInChildren<Renderer>(true) : null;
        Material original = renderer ? renderer.sharedMaterial : null;
        if (!material) material = new Material(fortressShader) { name = name };
        material.shader = fortressShader;
        material.DisableKeyword("_PAVING");
        material.DisableKeyword("_SURROUND");
        material.DisableKeyword("_ARCANE_STONE");
        material.SetFloat("_Paving", 0);
        material.SetFloat("_Surround", 0);
        material.SetFloat("_ArcaneStone", 0);
        material.SetFloat("_StoneDefinition", 0);
        material.SetTexture("_BaseMap", original ? original.mainTexture : null);
        material.SetColor("_BaseColor", new Color(.72f, .77f, .81f));
        material.SetFloat("_StoneMix", stoneMix);
        material.SetColor("_TeamColor", team);
        material.SetFloat("_TeamAmount", teamAmount);
        material.SetFloat("_Smoothness", .22f);
        material.SetFloat("_EmissionStrength", teamAmount > 0 ? .13f : 0);
        material.SetVector("_LaneSize", new Vector4(laneWidth, laneLength, cellSize,
            laneWidth + Mathf.Max(2f, laneGap)));
        material.SetColor("_RuneColor", arcaneAccentColor);
        material.SetFloat("_RuneIntensity", runeIntensity);
        material.SetFloat("_MagicPulse", magicPulse);
        material.enableInstancing = true;
    }

    // Bounds are measured after placement, not inferred from FBX pivots/axes.
    // Only accent meshes and the few supports need these per-renderer bounds;
    // ordinary wall modules retain their shared, batch-friendly material data.
    void SetGlyphBounds(GameObject root)
    {
        if (!root) return;
        Bounds b = PrefabBounds(root);
        var block = new MaterialPropertyBlock();
        block.SetVector("_GlyphBounds", new Vector4(b.center.x, b.min.y, b.center.z,
            Mathf.Max(.01f, Mathf.Max(b.size.x, b.size.z) * .5f)));
        block.SetFloat("_GlyphHeight", Mathf.Max(.01f, b.size.y));
        block.SetVector("_StoneExtents", new Vector4(b.extents.x, b.extents.y, b.extents.z, 0));
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.SetPropertyBlock(block);
    }

    static void SetSharedMaterial(GameObject root, Material material)
    {
        if (!material) return;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
        }
    }

    // Rotate the imported model INSIDE an identity wrapper, then fit that wrapper
    // to world X/Y/Z. This avoids scaling a rotated model along the wrong axes.
    GameObject ArtAsset(string name, Transform parent, GameObject asset, Vector3 centerAtBase,
        Vector3 size, Vector3 rotation, Material material)
    {
        if (!asset) return null;
        GameObject wrapper = new GameObject(name);
        wrapper.transform.SetParent(parent);
        GameObject model = Instantiate(asset, wrapper.transform);
        model.name = asset.name;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(rotation) * asset.transform.localRotation;
        Bounds bounds = PrefabBounds(wrapper);
        Vector3 scale = Vector3.one;
        if (bounds.size.x > .0001f) scale.x = size.x / bounds.size.x;
        if (bounds.size.y > .0001f && size.y > 0) scale.y = size.y / bounds.size.y;
        if (bounds.size.z > .0001f) scale.z = size.z / bounds.size.z;
        wrapper.transform.localScale = scale;
        bounds = PrefabBounds(wrapper);
        wrapper.transform.position += new Vector3(centerAtBase.x - bounds.center.x,
            centerAtBase.y - bounds.min.y, centerAtBase.z - bounds.center.z);
        SetSharedMaterial(wrapper, material);
        if (material && (material.IsKeywordEnabled("_ARCANE_STONE") ||
            name.StartsWith("Support_", System.StringComparison.Ordinal))) SetGlyphBounds(wrapper);
#if UNITY_EDITOR
        foreach (Transform child in wrapper.GetComponentsInChildren<Transform>(true))
            UnityEditor.GameObjectUtility.SetStaticEditorFlags(child.gameObject,
                UnityEditor.StaticEditorFlags.BatchingStatic | UnityEditor.StaticEditorFlags.OccluderStatic);
#endif
        return wrapper;
    }

    void BuildFortressWalls(Transform walls, Transform pillars)
    {
        float thickness = wallThickness;
        for (int row = 0; row < laneLength; row++)
        {
            float z = (row + .5f) * cellSize;
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -thickness * .5f : WidthWorld + thickness * .5f;
                ArtAsset($"Wall_{side}_{row:00}", walls, arenaWall, new Vector3(x, 0, z),
                    new Vector3(thickness, wallHeight, cellSize), new Vector3(0, side == 0 ? 90 : -90, 0),
                    row % Mathf.Max(2, pillarSpacing * 2) == 0 ? castleStoneMaterial : arenaStoneMaterial);
                // A low imported stone plinth gives the outside edge a clear profile.
                float trimWidth = .3f * cellSize;
                float trimX = side == 0 ? -thickness - trimWidth * .5f : WidthWorld + thickness + trimWidth * .5f;
                ArtAsset($"Plinth_{side}_{row:00}", walls, arenaBorder, new Vector3(trimX, -.02f, z),
                    new Vector3(trimWidth, .18f * cellSize, cellSize), new Vector3(0, 90, 0), arenaStoneMaterial);
            }
        }

        int spacing = Mathf.Max(1, pillarSpacing);
        for (int row = 0; row <= laneLength; row += spacing) BuildSupportPair(pillars, row);
        if (laneLength % spacing != 0) BuildSupportPair(pillars, laneLength);
        // Corner buttresses sit wholly outside the lane at its four extremities.
        foreach (float z in new[] { 0f, LengthWorld })
            for (int side = 0; side < 2; side++)
                ArtAsset($"CornerButtress_{side}_{z:F1}", walls, arenaWallCorner,
                    new Vector3(side == 0 ? -.95f * cellSize : WidthWorld + .95f * cellSize, 0, z),
                    new Vector3(.6f, wallHeight * .7f, .65f) * cellSize,
                    new Vector3(0, side == 0 ? 90 : -90, 0), arenaStoneMaterial);
    }

    void BuildPhysicalRoadWalls(Transform walls, Transform pillars, bool usingKenney)
    {
        int columns = Mathf.RoundToInt(WidthWorld / cellSize);
        for (int row = 0; row < laneLength; row++)
        {
            float z = (row + .5f) * cellSize;
            if (z >= BranchStartWorld && z < BranchEndWorld) continue;
            for (int column = 0; column < columns; column++)
            {
                if (!IsWalkableCell(column, row)) continue;
                for (int edge = -1; edge <= 1; edge += 2)
                {
                    if (IsWalkableCell(column + edge, row)) continue;
                    float x = (column + (edge > 0 ? 1f : 0f)) * cellSize;
                    float outsideX = x + edge * wallThickness * .5f;
                    if (usingKenney)
                    {
                        ArtAsset($"RoadWall_{row:00}_{column:00}_{edge}", walls, arenaWall,
                            new Vector3(outsideX, 0f, z), new Vector3(wallThickness, wallHeight, cellSize),
                            new Vector3(0f, edge > 0 ? -90f : 90f, 0f), arenaStoneMaterial);
                        if (row % Mathf.Max(2, pillarSpacing * 2) == 0 && row * cellSize > BranchEndWorld)
                            ArtAsset($"RoadButtress_{row:00}_{column:00}_{edge}", pillars, arenaColumn,
                                new Vector3(x + edge * .3f * cellSize, 0f, z),
                                new Vector3(.55f, wallHeight * 1.3f, .55f) * cellSize,
                                Vector3.zero, castleStoneMaterial);
                    }
                    else
                        CreateCube($"RoadWall_{row:00}_{column:00}_{edge}", walls,
                            new Vector3(outsideX, wallHeight * .5f, z),
                            new Vector3(wallThickness, wallHeight, cellSize), wallMaterial);
                }
            }
        }
        BuildSmoothBranchWalls(walls, usingKenney ? arenaStoneMaterial : wallMaterial);
    }

    void BuildSmoothBranchWalls(Transform walls, Material material)
    {
        for (int laneIndex = 0; laneIndex < battleLaneCount; laneIndex++)
            for (int side = -1; side <= 1; side += 2)
            {
                float start = BranchStartWorld;
                int neighbor = laneIndex + side;
                if (neighbor >= 0 && neighbor < battleLaneCount)
                {
                    // Internal walls begin only where the two roads separate.
                    float endSpacing = Mathf.Abs(RoadCenterX(laneIndex) - RoadCenterX(neighbor));
                    if (endSpacing <= RoadWidth) continue;
                    start = Mathf.Lerp(BranchStartWorld, BranchEndWorld, RoadWidth / endSpacing);
                }
                float end = BranchEndWorld;
                if (end - start < .01f) continue;
                Vector3 a = new Vector3(LaneCenterX(laneIndex, start) + side * RoadWidth * .5f,
                    wallHeight * .5f, start);
                Vector3 b = new Vector3(LaneCenterX(laneIndex, end) + side * RoadWidth * .5f,
                    wallHeight * .5f, end);
                Vector3 middle = (a + b) * .5f;
                Vector3 direction = b - a;
                GameObject wall = CreateCube($"SmoothBranchWall_{laneIndex}_{side}", walls,
                    middle, new Vector3(wallThickness, wallHeight, direction.magnitude), material);
                wall.transform.rotation = Quaternion.FromToRotation(Vector3.forward, direction.normalized);
            }
    }

    void BuildThreeRoadEnds(Transform spawn, Transform goal)
    {
        // The original blue gate is the single friendly origin/goal. Its red
        // core marks the middle enemy origin; add one red origin per outer road.
        BuildFortressEnds(spawn, goal);
        for (int laneIndex = 0; laneIndex < battleLaneCount; laneIndex++)
        {
            if (laneIndex == battleLaneCount / 2) continue;
            float x = RoadCenterX(laneIndex);
            ArtAsset($"RedMobOrigin_{laneIndex}", goal, arenaWallGate,
                new Vector3(x, 0f, LengthWorld - 1f * cellSize),
                new Vector3(Mathf.Min(4.4f * cellSize, RoadWidth * .7f), 1.65f * cellSize, .65f * cellSize),
                Vector3.zero, goalCoreMaterial);
        }
    }

    void BuildClassicTwoLaneEnds(Transform spawn, Transform goal)
    {
        float gateWidth = Mathf.Min(4.8f * cellSize, RoadWidth * .72f);
        float nearZ = .8f * cellSize;
        float farZ = LengthWorld - 1f * cellSize;
        for (int laneIndex = 0; laneIndex < battleLaneCount; laneIndex++)
        {
            float x = RoadCenterX(laneIndex);
            bool playerLane = laneIndex == PlayerDefenseLaneIndex;
            Material nearMaterial = playerLane ? blueGatewayArchMaterial : blueRunestoneMaterial;
            Material farMaterial = playerLane ? goalCoreMaterial : redRunestoneMaterial;
            string nearName = playerLane ? "PlayerHeroSpawnGate" : "AlliedUnitDispatchGate";
            string farName = playerLane ? "EnemyMobOriginGate" : "EnemyHeroSanctumGate";

            ArtAsset(nearName, spawn, arenaWallGate, new Vector3(x, 0f, nearZ),
                new Vector3(gateWidth, 1.65f * cellSize, .65f * cellSize), Vector3.zero, nearMaterial);
            ArtAsset(farName, goal, arenaWallGate, new Vector3(x, 0f, farZ),
                new Vector3(gateWidth, 1.65f * cellSize, .65f * cellSize), Vector3.zero, farMaterial);
            for (int side = -1; side <= 1; side += 2)
            {
                ArtAsset($"{nearName}_Column_{side}", spawn, arenaColumn,
                    new Vector3(x + side * (gateWidth * .5f + .28f * cellSize), 0f, nearZ),
                    new Vector3(.5f, 1.55f, .55f) * cellSize, Vector3.zero, nearMaterial);
                ArtAsset($"{farName}_Column_{side}", goal, arenaDamagedColumn,
                    new Vector3(x + side * (gateWidth * .5f + .28f * cellSize), 0f, farZ),
                    new Vector3(.5f, 1.45f, .55f) * cellSize, Vector3.zero, farMaterial);
            }
        }
    }

    void BuildSupportPair(Transform parent, int row)
    {
        bool castle = castleWallPillar && (row == 0 || row == laneLength || row % (Mathf.Max(1, pillarSpacing) * 2) == 0);
        float width = .72f * cellSize;
        for (int side = 0; side < 2; side++)
            ArtAsset($"Support_{side}_{row:00}", parent, castle ? castleWallPillar : arenaColumn,
                new Vector3(side == 0 ? -width * .5f : WidthWorld + width * .5f, 0, row * cellSize),
                new Vector3(width, wallHeight * (castle ? 1.55f : 1.35f), width), Vector3.zero,
                castle ? castleStoneMaterial : arenaStoneMaterial);
        // Slender rune sentinels beyond selected buttresses add depth without
        // making the side walls taller or intruding on the battlefield.
        if (row > 0 && row < laneLength && row % (Mathf.Max(1, pillarSpacing) * 2) == 0)
            for (int side = 0; side < 2; side++)
                ArtAsset($"ArcaneSentinel_{side}_{row:00}", parent, arenaColumn,
                    new Vector3(side == 0 ? -1.12f * cellSize : WidthWorld + 1.12f * cellSize, 0, row * cellSize),
                    new Vector3(.44f, 1.4f, .44f) * cellSize, Vector3.zero, arcaneStoneMaterial);
    }

    void BuildFortressEnds(Transform spawn, Transform goal)
    {
        // Preserve the existing Kenney spawn and goal center coordinates.
        Vector3 spawnCenter = new Vector3(WidthWorld * .5f, 0, .8f);
        Vector3 goalCenter = new Vector3(WidthWorld * .5f, 0, LengthWorld - 1f);
        float gateWidth = Mathf.Min(4.4f * cellSize, WidthWorld * .7f);
        ArtAsset("BlueSpawnGate", spawn, arenaWallGate, spawnCenter,
            new Vector3(gateWidth, 1.65f * cellSize, .65f * cellSize), Vector3.zero, gatewayMaterial);
        // A separate rear arch creates a readable portal depth; the two imported
        // structures have a deliberate gap and neither overlays the lane floor.
        Vector3 rearArchCenter = spawnCenter + Vector3.forward * (1.8f * cellSize);
        ArtAsset("BlueGatewayRearArch", spawn, castleWallDoorway, rearArchCenter,
            new Vector3(gateWidth * .9f, 1.55f * cellSize, .42f * cellSize), Vector3.zero, blueGatewayArchMaterial);
        for (int side = -1; side <= 1; side += 2)
            ArtAsset($"SpawnGateColumn_{side}", spawn, arenaColumn,
                spawnCenter + new Vector3(side * (gateWidth * .5f + .32f * cellSize), 0, 0),
                new Vector3(.52f, 1.85f, .62f) * cellSize, Vector3.zero, blueRunestoneMaterial);

        // Short imported column caps sit on the gate lintel, not on the floor.
        for (int i = -1; i <= 1; i++)
        {
            ArtAsset($"GatewayRuneCrown_{i}", spawn, arenaColumn,
                spawnCenter + new Vector3(i * 1.35f * cellSize, 1.65f * cellSize, 0),
                new Vector3(.55f, .25f, .44f) * cellSize, Vector3.zero, blueRunestoneMaterial);
            ArtAsset($"GatewayRoof_{i}", spawn, castleTowerRoof,
                spawnCenter + new Vector3(i * 1.35f * cellSize, 1.9f * cellSize, 0),
                new Vector3(.62f, .36f, .62f) * cellSize, Vector3.zero, blueCastleMaterial);
        }

        float baseHeight = .55f * cellSize;
        ArtAsset("RedGoalFoundation", goal, castleTowerBase, goalCenter,
            new Vector3(2.6f, baseHeight / cellSize, 1.65f) * cellSize, Vector3.zero, castleStoneMaterial);
        // Stack the arch on its foundation instead of putting two models in the same volume.
        ArtAsset("RedGoalCore", goal, castleTowerArch, goalCenter + Vector3.up * baseHeight,
            new Vector3(2.05f, 1.15f, 1.2f) * cellSize, Vector3.zero, goalCoreMaterial);
        ArtAsset("CrimsonRunestone", goal, arenaDamagedColumn,
            goalCenter + Vector3.up * (baseHeight + 1.15f * cellSize),
            new Vector3(.7f, .9f, .7f) * cellSize, Vector3.zero, redRunestoneMaterial);
        ArtAsset("CrimsonCrown", goal, castleTowerRoof,
            goalCenter + Vector3.up * (baseHeight + 2.05f * cellSize),
            new Vector3(1.15f, .5f, 1.15f) * cellSize, Vector3.zero, goalCoreMaterial);
        for (int side = -1; side <= 1; side += 2)
        {
            ArtAsset($"GoalFocus_{side}", goal, arenaColumn,
                goalCenter + new Vector3(side * 1.7f * cellSize, 0, 0),
                new Vector3(.44f, 1.25f, .55f) * cellSize, Vector3.zero, redRunestoneMaterial);
            ArtAsset($"GoalFocusRoof_{side}", goal, castleTowerRoof,
                goalCenter + new Vector3(side * 1.7f * cellSize, 1.25f * cellSize, 0),
                new Vector3(.55f, .32f, .55f) * cellSize, Vector3.zero, goalCoreMaterial);
        }
        if (!useBanners) return;
        for (int side = 0; side < 2; side++)
        {
            float x = side == 0 ? -1.05f * cellSize : WidthWorld + 1.05f * cellSize;
            ArtAsset($"EntryStandardMount_{side}", spawn, arenaColumn, new Vector3(x, 0, spawnCenter.z),
                new Vector3(.36f, 1.85f, .36f) * cellSize, Vector3.zero, arenaStoneMaterial);
            ArtAsset($"GoalStandardMount_{side}", goal, arenaColumn, new Vector3(x, 0, goalCenter.z),
                new Vector3(.36f, 1.7f, .36f) * cellSize, Vector3.zero, arenaStoneMaterial);
            ArtAsset($"BlueEntryStandard_{side}", spawn, castleFlagBannerLong, new Vector3(x, .4f * cellSize, spawnCenter.z - .25f * cellSize),
                new Vector3(.8f, 1.35f, .35f) * cellSize, new Vector3(16, 90, 0), blueCastleMaterial);
            ArtAsset($"CrimsonGoalStandard_{side}", goal, castleFlagBannerShort, new Vector3(x, .4f * cellSize, goalCenter.z - .25f * cellSize),
                new Vector3(.8f, 1.2f, .35f) * cellSize, new Vector3(16, 90, 0), redCastleMaterial);
        }
    }

    void BuildFortressDressing(Transform parent)
    {
        Transform edgeStrip = DecorationLayer(parent, "EdgeStrip");
        Transform runes = DecorationLayer(parent, "ArcaneRunes");
        Transform landmarks = DecorationLayer(parent, "MajorLandmarks");
        BuildInterLaneRuneMarkers(runes);
        if (decorationDensity > 0f)
        {
            BuildLaneEdgeStrips(edgeStrip);
            if (useArcaneProps) BuildMajorLandmarks(landmarks);
        }
        DisableDecorationColliders(parent);
    }

    void BuildInterLaneRuneMarkers(Transform parent)
    {
        // The visible circle is drawn analytically by the existing ground
        // shader. This empty marker makes its position deterministic and
        // auditable without adding floor meshes, renderers or colliders.
        float z = LengthWorld * .43f;
        for (int gap = 0; gap < battleLaneCount - 1; gap++)
        {
            float x = RoadStartX(gap) + RoadWidth + laneGap * cellSize * .5f;
            GameObject marker = new GameObject("CentralArcaneSeal");
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(x, -.08f * cellSize, z);
            marker.transform.localRotation = Quaternion.Euler(0f, gap == 0 ? -8f : 12f, 0f);
        }
    }

    static Transform DecorationLayer(Transform parent, string name)
    {
        Transform layer = new GameObject(name).transform;
        layer.SetParent(parent, false);
        return layer;
    }

    void BuildLaneEdgeStrips(Transform parent)
    {
        float groundY = -.085f * cellSize;
        float edgeOffset = .62f * cellSize;
        // The trunk gets one restrained pair of details. The branch sites are
        // intentionally spaced around, rather than on, the hero connectors.
        float trunkZ = Mathf.Max(3f * cellSize, BranchStartWorld * .48f);
        float trunkLeft = RoadCenterX(battleLaneCount / 2) - RoadWidth * .5f;
        for (int side = 0; side < 2; side++)
        {
            float x = side == 0 ? trunkLeft - edgeOffset : trunkLeft + RoadWidth + edgeOffset;
            ArtAsset($"EdgeStrip_TrunkRubble_{side}", parent, castleSmallRocks,
                new Vector3(x, groundY, trunkZ + side * 1.35f * cellSize),
                new Vector3(.72f, .25f, .62f) * cellSize,
                new Vector3(0f, side == 0 ? 24f : -31f, 0f), castleStoneMaterial);
        }

        float[] branchFractions = { .13f, .34f, .61f, .86f };
        for (int lane = 0; lane < battleLaneCount; lane++)
        {
            // Keep both inter-road spaces completely free of prop renderers.
            // Only the two outside verges retain restrained edge dressing.
            if (lane != 0 && lane != battleLaneCount - 1) continue;
            float roadLeft = RoadStartX(lane);
            for (int site = 0; site < branchFractions.Length; site++)
            {
                float z = BranchEndWorld + (LengthWorld - BranchEndWorld) * branchFractions[site];
                // Alternate sides per lane/site to keep both verges legible
                // without making every station an obvious mirrored pair.
                int side = lane == 0 ? 0 : 1;
                float x = side == 0 ? roadLeft - edgeOffset : roadLeft + RoadWidth + edgeOffset;
                float yaw = side == 0 ? 18f + site * 11f : 180f - site * 13f;
                GameObject stone = site == 1 ? arcaneRubble : site == 2 ? arcaneShortColumn : castleSmallRocks;
                Vector3 stoneSize = site == 2 ? new Vector3(.42f, .72f, .42f) :
                    new Vector3(.68f, .25f, .58f);
                ArtAsset($"EdgeStrip_Stone_{lane}_{site}", parent, stone,
                    new Vector3(x, groundY, z), stoneSize * cellSize,
                    new Vector3(0f, yaw, 0f), site == 2 ? arenaStoneMaterial : castleStoneMaterial);

                if (useArcaneProps && (lane + site) % 3 == 0)
                    ArtAsset($"EdgeStrip_Crystal_{lane}_{site}", parent, arcaneCrystalSmall,
                        new Vector3(x + (side == 0 ? -.34f : .34f) * cellSize, groundY,
                            z + .42f * cellSize),
                        new Vector3(.42f, .7f, .42f) * cellSize,
                        new Vector3(0f, yaw + 22f, 0f), crystalCyanMaterial);
                else if (useArcaneProps && (lane + site) % 3 == 1)
                    ArtAsset($"EdgeStrip_Rune_{lane}_{site}", parent, arenaDamagedColumn,
                        new Vector3(x + (side == 0 ? -.22f : .22f) * cellSize, groundY,
                            z - .36f * cellSize),
                        new Vector3(.36f, .62f, .36f) * cellSize,
                        new Vector3(0f, yaw, 0f), arcaneStoneMaterial);
            }
        }
    }

    void BuildBattlefieldBaseGround(Transform parent, float margin, float endPadding)
    {
        float left = -margin, right = WidthWorld + margin;
        float near = -endPadding, far = LengthWorld + endPadding;
        float y = -.085f * cellSize;
        GameObject ground = new GameObject("ContinuousArcaneDistrictGround", typeof(MeshFilter), typeof(MeshRenderer));
        ground.transform.SetParent(parent, false);
        Mesh mesh = new Mesh { name = "Continuous Arcane District Ground" };
        mesh.vertices = new[] { new Vector3(left, y, near), new Vector3(left, y, far),
            new Vector3(right, y, far), new Vector3(right, y, near) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, (far - near) / (8f * cellSize)),
            new Vector2((right - left) / (8f * cellSize), (far - near) / (8f * cellSize)),
            new Vector2((right - left) / (8f * cellSize), 0) };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        ground.GetComponent<MeshFilter>().sharedMesh = mesh;
        ground.GetComponent<MeshRenderer>().sharedMaterial = gardenMaterial ? gardenMaterial : grassMaterial;
    }

    void BuildArcaneDistrictClusters(Transform parent)
    {
        // Two twelve-unit city corridors hold six authored districts. Themes
        // rotate between corridors, so each appears twice without mirrored
        // copy/paste composition. Sites avoid the hero connector rows.
        float[] siteFractions = { .16f, .47f, .79f };
        for (int district = 0; district < battleLaneCount - 1; district++)
        {
            float x = RoadStartX(district) + RoadWidth + laneGap * cellSize * .5f;
            float facing = district == 0 ? 0f : 180f;
            for (int site = 0; site < siteFractions.Length; site++)
            {
                float z = BranchEndWorld + (LengthWorld - BranchEndWorld) * siteFractions[site];
                float ground = -.085f * cellSize;
                int theme = (site + district) % 3;
                if (theme == 0) BuildKnowledgeQuarter(parent, district, site, x, z, ground, facing);
                else if (theme == 1) BuildArcanePlaza(parent, district, site, x, z, ground, facing);
                else BuildCrystalWorkshop(parent, district, site, x, z, ground, facing);
            }
        }
    }

    void BuildKnowledgeQuarter(Transform parent, int district, int site, float x, float z,
        float ground, float facing)
    {
        string id = $"{district}_{site}";
        float variation = district == 0 ? -1f : 1f;
        ArtAsset($"KnowledgeFoundation_{id}", parent, castleTowerHexBase,
            new Vector3(x, ground, z), new Vector3(9f, .16f, 6.4f) * cellSize,
            Vector3.zero, castleStoneMaterial);
        // The high silhouette sits on the camera-far half of the corridor;
        // broken side walls imply library aisles while leaving an open entry.
        ArtAsset($"KnowledgeArchiveArch_{id}", parent, arcaneArch,
            new Vector3(x + 2.45f * cellSize, ground + .16f * cellSize, z - .45f * cellSize),
            new Vector3(3.35f, 3.45f, .82f) * cellSize,
            new Vector3(0f, facing + variation * 5f, 0f), castleStoneMaterial);
        ArtAsset($"KnowledgeCollapsedWingA_{id}", parent, arcaneWallBroken,
            new Vector3(x + .65f * cellSize, ground + .16f * cellSize, z - 2.35f * cellSize),
            new Vector3(4.1f, 2.05f, .65f) * cellSize,
            new Vector3(0f, facing + variation * 8f, 0f), castleStoneMaterial);
        ArtAsset($"KnowledgeCollapsedWingB_{id}", parent, arcaneBrokenWallDouble,
            new Vector3(x + 1.15f * cellSize, ground + .16f * cellSize, z + 2.2f * cellSize),
            new Vector3(3.5f, 1.75f, .65f) * cellSize,
            new Vector3(0f, facing - variation * 13f, 0f), arenaStoneMaterial);
        for (int column = 0; column < 3; column++)
            ArtAsset($"KnowledgePillar{column}_{id}", parent,
                column == 1 ? arcaneRuinedPillar : arcaneColumn,
                new Vector3((x - 2.4f * cellSize) + column * 1.65f * cellSize,
                    ground + .16f * cellSize, z + (column == 1 ? .75f : .15f) * cellSize),
                new Vector3(.68f, column == 1 ? 1.85f : 2.45f, .68f) * cellSize,
                new Vector3(0f, facing + column * 19f + variation * 7f, 0f), castleStoneMaterial);
        ArtAsset($"KnowledgeRuneStone_{id}", parent, arcaneMegalith,
            new Vector3(x - 2.65f * cellSize, ground + .16f * cellSize, z + 2.05f * cellSize),
            new Vector3(1.05f, 1.75f, .8f) * cellSize,
            new Vector3(0f, facing + variation * 18f, 0f), arcaneStoneMaterial);
        ArtAsset($"KnowledgeCrystalAccent_{id}", parent, arcaneCrystalSmall,
            new Vector3(x - 1.25f * cellSize, ground + .16f * cellSize, z - 1.75f * cellSize),
            new Vector3(.55f, .9f, .55f) * cellSize,
            new Vector3(0f, 23f + district * 31f, 0f), crystalCyanMaterial);
        ArtAsset($"KnowledgeRubble_{id}", parent, arcaneBricks,
            new Vector3(x + 2.85f * cellSize, ground + .16f * cellSize, z + 1.65f * cellSize),
            new Vector3(1.35f, .34f, .9f) * cellSize,
            new Vector3(0f, facing + variation * 27f, 0f), arenaStoneMaterial);
    }

    void BuildArcanePlaza(Transform parent, int district, int site, float x, float z,
        float ground, float facing)
    {
        string id = $"{district}_{site}";
        float variation = district == 0 ? -1f : 1f;
        ArtAsset($"PlazaFoundation_{id}", parent, castleTowerHexBase,
            new Vector3(x, ground, z), new Vector3(9.2f, .14f, 6.7f) * cellSize,
            Vector3.zero, castleStoneMaterial);
        // Paired ruins frame an open courtyard. The focal crystal is restrained
        // and low enough that combat remains readable through the plaza.
        for (int side = 0; side < 2; side++)
        {
            float sideSign = side == 0 ? -1f : 1f;
            ArtAsset($"PlazaArch{side}_{id}", parent, side == 0 ? arcaneRoundArch : arcaneArch,
                new Vector3(x + 2.55f * cellSize, ground + .14f * cellSize,
                    z + sideSign * 2.05f * cellSize),
                new Vector3(2.35f, side == 0 ? 2.65f : 2.35f, .7f) * cellSize,
                new Vector3(0f, facing + sideSign * variation * 7f, 0f), castleStoneMaterial);
            ArtAsset($"PlazaColumn{side}_{id}", parent, arcaneShortColumn,
                new Vector3(x - 3.15f * cellSize, ground + .14f * cellSize,
                    z + sideSign * 2.25f * cellSize),
                new Vector3(.68f, 1.55f + side * .2f, .68f) * cellSize,
                new Vector3(0f, facing + side * 29f, 0f), arenaStoneMaterial);
        }
        ArtAsset($"PlazaRearWall_{id}", parent, arcaneBrokenWallDouble,
            new Vector3(x + 3.2f * cellSize, ground + .14f * cellSize, z),
            new Vector3(1.6f, 1.55f, 2.6f) * cellSize,
            new Vector3(0f, 90f + variation * 6f, 0f), castleStoneMaterial);
        ArtAsset($"PlazaCentralPlinth_{id}", parent, arcanePlinth,
            new Vector3(x - .35f * cellSize, ground + .14f * cellSize, z),
            new Vector3(1.75f, .28f, 1.75f) * cellSize, Vector3.zero, castleStoneMaterial);
        ArtAsset($"PlazaCentralRune_{id}", parent, arcaneRuneCrystal,
            new Vector3(x - .35f * cellSize, ground + .41f * cellSize, z),
            new Vector3(.85f, 1.65f, .85f) * cellSize,
            new Vector3(0f, 17f + district * 37f, 0f),
            district == 0 ? crystalCyanMaterial : crystalPurpleMaterial);
        ArtAsset($"PlazaBrokenColumn_{id}", parent, arenaDamagedColumn,
            new Vector3(x - 2.65f * cellSize, ground + .14f * cellSize, z + .35f * cellSize),
            new Vector3(.72f, 1.05f, .72f) * cellSize,
            new Vector3(0f, facing + variation * 21f, 0f), arenaStoneMaterial);
        ArtAsset($"PlazaRubble_{id}", parent, arcaneRubble,
            new Vector3(x + 1.45f * cellSize, ground + .14f * cellSize, z - 1.1f * cellSize),
            new Vector3(1.15f, .3f, .75f) * cellSize,
            new Vector3(0f, facing - variation * 32f, 0f), arenaStoneMaterial);
    }

    void BuildCrystalWorkshop(Transform parent, int district, int site, float x, float z,
        float ground, float facing)
    {
        string id = $"{district}_{site}";
        float variation = district == 0 ? -1f : 1f;
        ArtAsset($"WorkshopFoundation_{id}", parent, castleTowerHexBase,
            new Vector3(x, ground, z), new Vector3(8.7f, .15f, 6.5f) * cellSize,
            Vector3.zero, castleStoneMaterial);
        ArtAsset($"WorkshopShellA_{id}", parent, arcaneWallBroken,
            new Vector3(x + 2.65f * cellSize, ground + .15f * cellSize, z - 1.45f * cellSize),
            new Vector3(2.9f, 2.25f, .65f) * cellSize,
            new Vector3(0f, facing + variation * 11f, 0f), castleStoneMaterial);
        ArtAsset($"WorkshopShellB_{id}", parent, arcaneBrokenWall,
            new Vector3(x + 2.35f * cellSize, ground + .15f * cellSize, z + 1.85f * cellSize),
            new Vector3(3.25f, 1.75f, .65f) * cellSize,
            new Vector3(0f, facing - variation * 17f, 0f), arenaStoneMaterial);
        ArtAsset($"WorkshopEntrySteps_{id}", parent, arcaneStairs,
            new Vector3(x - 3.05f * cellSize, ground + .15f * cellSize, z + .2f * cellSize),
            new Vector3(1.7f, .48f, 2.35f) * cellSize,
            new Vector3(0f, 90f, 0f), arenaStoneMaterial);
        for (int bench = 0; bench < 2; bench++)
        {
            float side = bench == 0 ? -1f : 1f;
            ArtAsset($"WorkshopBench{bench}_{id}", parent, arcanePlinth,
                new Vector3(x - .55f * cellSize, ground + .15f * cellSize,
                    z + side * 1.65f * cellSize),
                new Vector3(2.25f, .52f, 1.05f) * cellSize,
                new Vector3(0f, variation * (bench == 0 ? 7f : -5f), 0f), castleStoneMaterial);
        }
        GameObject[] crystals = { arcaneCrystalBroken, arcaneCrystalMedium, arcaneCrystalSmall };
        Vector3[] offsets = { new Vector3(-.7f, .67f, -1.65f), new Vector3(-.45f, .67f, 1.65f),
            new Vector3(1.05f, .15f, .15f) };
        Vector3[] sizes = { new Vector3(.85f, 1.2f, .85f), new Vector3(.8f, 1.45f, .8f),
            new Vector3(.55f, .8f, .55f) };
        for (int crystal = 0; crystal < crystals.Length; crystal++)
            ArtAsset($"WorkshopCrystalFragment{crystal}_{id}", parent, crystals[crystal],
                new Vector3(x + offsets[crystal].x * cellSize, ground + offsets[crystal].y * cellSize,
                    z + offsets[crystal].z * cellSize), sizes[crystal] * cellSize,
                new Vector3(0f, 19f + crystal * 43f + district * 13f, 0f),
                crystal == 1 && district == 1 ? crystalPurpleMaterial : crystalCyanMaterial);
        ArtAsset($"WorkshopMaterialPile_{id}", parent, arcaneBricks,
            new Vector3(x + 1.05f * cellSize, ground + .15f * cellSize, z - 2.35f * cellSize),
            new Vector3(1.5f, .35f, .85f) * cellSize,
            new Vector3(0f, facing + variation * 35f, 0f), arenaStoneMaterial);
        ArtAsset($"WorkshopRuinedPillar_{id}", parent, arcaneRuinedPillar,
            new Vector3(x + 3.15f * cellSize, ground + .15f * cellSize, z + .25f * cellSize),
            new Vector3(.7f, 1.85f, .7f) * cellSize,
            new Vector3(0f, facing + variation * 24f, 0f), castleStoneMaterial);
    }

    // Outskirts are visual dressing only; ensure imported prefabs cannot
    // accidentally contribute colliders to unit movement or tap targeting.
    static void DisableDecorationColliders(Transform root)
    {
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
    }

    // Four full sanctum/gate compositions mark beginning, middle and late-map
    // regions. Open arches keep sight lines through the foreground landmarks.
    void BuildMajorLandmarks(Transform parent)
    {
        if (!arcaneCrystalLarge || !arcaneMegalith) return;
        float groundY = -.085f * cellSize;
        float[] leftSites = { .38f, .78f };
        float[] rightSites = { .08f, .60f };
        for (int side = 0; side < 2; side++)
        {
            float[] sites = side == 0 ? leftSites : rightSites;
            float x = side == 0 ? -2.9f * cellSize : WidthWorld + 2.9f * cellSize;
            float inward = side == 0 ? 1f : -1f;
            for (int site = 0; site < sites.Length; site++)
            {
                int variant = side * 2 + site;
                float z = Mathf.Lerp(BranchEndWorld, LengthWorld, sites[site]);
                float architectureHeight = side == 0 ? 3.4f : 4.35f;
                ArtAsset($"LandmarkFoundation_{side}_{site}", parent, castleTowerHexBase,
                    new Vector3(x, groundY, z), new Vector3(5.1f, .22f, 5.5f) * cellSize,
                    new Vector3(0f, variant * 9f, 0f), castleStoneMaterial);

                GameObject architecture = variant % 2 == 0 ? arcaneArch : arcaneRoundArch;
                ArtAsset($"LandmarkArchitecture_{side}_{site}", parent, architecture,
                    new Vector3(x, groundY + .22f * cellSize, z + .75f * cellSize),
                    new Vector3(4.65f, architectureHeight, .9f) * cellSize,
                    new Vector3(0f, side == 0 ? 8f + site * 13f : 176f - site * 15f, 0f),
                    castleStoneMaterial);

                for (int wing = 0; wing < 2; wing++)
                    ArtAsset($"LandmarkWing{wing}_{side}_{site}", parent,
                        wing == 0 ? arcaneWallBroken : arcaneBrokenWallDouble,
                        new Vector3(x + (wing == 0 ? -1.15f : 1.15f) * cellSize,
                            groundY + .22f * cellSize, z + (wing == 0 ? -1.65f : 2.1f) * cellSize),
                        new Vector3(3.5f, wing == 0 ? 2.3f : 2.65f, .72f) * cellSize,
                        new Vector3(0f, side == 0 ? 23f + wing * 18f : 157f - wing * 21f, 0f),
                        wing == 0 ? arenaStoneMaterial : castleStoneMaterial);

                for (int column = 0; column < 2; column++)
                    ArtAsset($"LandmarkColumn{column}_{side}_{site}", parent,
                        column == site ? arcaneRuinedPillar : arcaneSquareColumn,
                        new Vector3(x + (column == 0 ? -1.85f : 1.85f) * cellSize,
                            groundY + .22f * cellSize, z - .25f * cellSize),
                        new Vector3(.82f, side == 0 ? 2.7f : 3.25f, .82f) * cellSize,
                        new Vector3(0f, variant * 17f + column * 29f, 0f), castleStoneMaterial);

                ArtAsset($"LandmarkPlinth_{side}_{site}", parent, arcanePlinth,
                    new Vector3(x + inward * 1.15f * cellSize, groundY + .22f * cellSize, z - .2f * cellSize),
                    new Vector3(2.35f, .36f, 2.35f) * cellSize, Vector3.zero, castleStoneMaterial);
                ArtAsset($"LandmarkCrystal_{side}_{site}", parent,
                    site == 0 ? arcaneCrystalLarge : arcaneCrystalTall,
                    new Vector3(x + inward * 1.15f * cellSize, groundY + .56f * cellSize, z - .2f * cellSize),
                    new Vector3(2.05f, site == 0 ? 3.65f : 4.05f, 2.05f) * cellSize,
                    new Vector3(0f, variant * 23f, 0f), variant == 2 ? crystalPurpleMaterial : crystalCyanMaterial);
                ArtAsset($"LandmarkBrokenCrystal_{side}_{site}", parent, arcaneCrystalBroken,
                    new Vector3(x - inward * 1.4f * cellSize, groundY + .22f * cellSize, z - 1.55f * cellSize),
                    new Vector3(1.05f, 1.35f, 1.05f) * cellSize,
                    new Vector3(0f, 35f + variant * 29f, 0f),
                    variant == 1 ? crystalPurpleMaterial : crystalCyanMaterial);
                ArtAsset($"LandmarkMegalith_{side}_{site}", parent, arcaneMegalith,
                    new Vector3(x - inward * 1.25f * cellSize, groundY + .22f * cellSize, z + 1.35f * cellSize),
                    new Vector3(1.55f, side == 0 ? 2.8f : 3.4f, 1.35f) * cellSize,
                    new Vector3(0f, variant * 19f + 11f, 0f), arcaneStoneMaterial);
                ArtAsset($"LandmarkSteps_{side}_{site}", parent, arcaneStairs,
                    new Vector3(x + inward * .2f * cellSize, groundY + .22f * cellSize, z - 2.3f * cellSize),
                    new Vector3(3.2f, .62f, 1.75f) * cellSize,
                    new Vector3(0f, side == 0 ? 0f : 180f, 0f), arenaStoneMaterial);
                ArtAsset($"LandmarkRubble_{side}_{site}", parent,
                    variant % 2 == 0 ? arcaneRubble : arcaneBricks,
                    new Vector3(x - inward * 1.75f * cellSize, groundY + .22f * cellSize, z + 2.15f * cellSize),
                    new Vector3(1.35f, .38f, .9f) * cellSize,
                    new Vector3(0f, variant * 31f, 0f), arenaStoneMaterial);
                if (useGreenery && variant == 3)
                    ArtAsset("LandmarkWitheredTree", parent, castleSmallTree,
                        new Vector3(x - inward * 2.1f * cellSize, groundY + .22f * cellSize, z - .65f * cellSize),
                        new Vector3(1f, 1.7f, 1f) * cellSize,
                        new Vector3(0f, 47f, 0f), natureMaterial);
            }
        }
    }
}
