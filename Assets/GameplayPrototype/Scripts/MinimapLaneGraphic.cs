using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds a landscape minimap texture from LaneGenerator walkability.</summary>
[RequireComponent(typeof(RawImage))]
public sealed class MinimapLaneGraphic : MonoBehaviour
{
    [SerializeField] LaneGenerator lane;
    [SerializeField, Min(1024)] int textureWidth = 1024;
    [SerializeField, Min(512)] int textureHeight = 512;
    [SerializeField] Color backgroundColor = new Color(.0667f, .102f, .1569f, 1f);
    [SerializeField] Color roadEdgeColor = new Color(.8627f, .9216f, 1f, 1f);
    [SerializeField] Color roadColor = new Color(.6235f, .7059f, .8f, 1f);

    const int PaddingPixels = 24;
    const int SupersampleAxis = 2;
    const int RoadOutlinePixels = 3;
    RawImage rawImage;
    Texture2D generatedTexture;
    float cachedWidth = -1f;
    float cachedLength = -1f;
    float cachedCellSize = -1f;
    float cachedBranchStart = -1f;
    float cachedBranchEnd = -1f;
    int cachedLaneCount = -1;
    int cachedLaneWidth = -1;
    float cachedLaneGap = -1f;
    int generatedWidth = 1024;
    int generatedHeight = 512;

    public LaneGenerator Lane => lane;
    public Texture2D GeneratedTexture => generatedTexture;
    public int RoadPixelCount { get; private set; }

    void Awake() { ConfigureRawImage(); }

    public void SetLane(LaneGenerator value)
    {
        lane = value;
        RebuildTexture();
    }

    public void SetColors(Color background, Color edges, Color roads)
    {
        backgroundColor = background;
        roadEdgeColor = edges;
        roadColor = roads;
        if (lane) RebuildTexture();
    }

    public bool RefreshIfChanged()
    {
        if (!lane) return false;
        if (Mathf.Approximately(cachedWidth, lane.WidthWorld) &&
            Mathf.Approximately(cachedLength, lane.LengthWorld) &&
            Mathf.Approximately(cachedCellSize, lane.cellSize) &&
            Mathf.Approximately(cachedBranchStart, lane.BranchStartWorld) &&
            Mathf.Approximately(cachedBranchEnd, lane.BranchEndWorld) &&
            Mathf.Approximately(cachedLaneGap, lane.laneGap) &&
            cachedLaneCount == lane.battleLaneCount && cachedLaneWidth == lane.laneWidth)
            return false;
        RebuildTexture();
        return true;
    }

    /// <summary>
    /// Fixed overhead transform: red/high-Z is left, blue/low-Z is right;
    /// lane zero/low-X is at the top. It is independent of gameplay camera yaw.
    /// </summary>
    public Vector2 WorldToMap(Vector3 laneLocalPosition, Rect mapBounds)
    {
        float u = TextureU(laneLocalPosition.z);
        float v = TextureV(laneLocalPosition.x);
        return new Vector2(mapBounds.xMin + u * mapBounds.width,
            mapBounds.yMin + v * mapBounds.height);
    }

    void RebuildTexture()
    {
        ConfigureRawImage();
        if (!lane || lane.cellSize <= 0f || lane.WidthWorld <= 0f || lane.LengthWorld <= 0f)
        {
            AssignTexture(null);
            return;
        }

        int width = Mathf.Max(1024, textureWidth);
        int height = Mathf.Max(512, textureHeight);
        generatedWidth = width;
        generatedHeight = height;
        List<Vector2[]> roadPolygons = BuildRoadPolygons();
        float[] roadCoverage = new float[width * height];
        Color32[] pixels = new Color32[width * height];

        for (int py = 0; py < height; py++)
        {
            int rowStart = py * width;
            for (int px = 0; px < width; px++)
            {
                PixelToWorld(px + .5f, py + .5f, width, height, out float worldX, out float worldZ);
                pixels[rowStart + px] = TerrainColor(worldX, worldZ, px, py, width, height);
            }
        }

        int innerWidth = width - PaddingPixels * 2;
        int innerHeight = height - PaddingPixels * 2;
        RoadPixelCount = 0;
        for (int py = PaddingPixels; py < height - PaddingPixels; py++)
        for (int px = PaddingPixels; px < width - PaddingPixels; px++)
        {
            int coveredSamples = 0;
            for (int sampleY = 0; sampleY < SupersampleAxis; sampleY++)
            for (int sampleX = 0; sampleX < SupersampleAxis; sampleX++)
            {
                float samplePx = px + (sampleX + .5f) / SupersampleAxis;
                float samplePy = py + (sampleY + .5f) / SupersampleAxis;
                PixelToWorld(samplePx, samplePy, width, height, out float worldX, out float worldZ);
                if (InsideAnyPolygon(new Vector2(worldX, worldZ), roadPolygons)) coveredSamples++;
            }
            float coverage = coveredSamples / (float)(SupersampleAxis * SupersampleAxis);
            roadCoverage[py * width + px] = coverage;
            if (coverage > 0f) RoadPixelCount++;
        }

        for (int py = PaddingPixels; py < height - PaddingPixels; py++)
        for (int px = PaddingPixels; px < width - PaddingPixels; px++)
        {
            int index = py * width + px;
            float coverage = roadCoverage[index];
            bool nearRoad = coverage > 0f;
            if (!nearRoad)
            {
                for (int offsetY = -RoadOutlinePixels; offsetY <= RoadOutlinePixels && !nearRoad; offsetY++)
                for (int offsetX = -RoadOutlinePixels; offsetX <= RoadOutlinePixels; offsetX++)
                {
                    if (offsetX * offsetX + offsetY * offsetY > RoadOutlinePixels * RoadOutlinePixels) continue;
                    if (roadCoverage[(py + offsetY) * width + px + offsetX] > 0f)
                    { nearRoad = true; break; }
                }
            }

            Color pixel = pixels[index];
            if (nearRoad) pixel = Color.Lerp(pixel, roadEdgeColor, .94f);
            if (coverage > 0f)
            {
                PixelToWorld(px + .5f, py + .5f, width, height, out float worldX, out float worldZ);
                float stoneVariation = .5f + .5f * Mathf.Sin(worldZ * .31f + worldX * .17f);
                Color stone = Color.Lerp(roadColor, Color.white, .025f + stoneVariation * .025f);
                pixel = Color.Lerp(pixel, stone, coverage);
            }
            pixels[index] = pixel;
        }

        ReleaseTexture();
        generatedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
        {
            name = "HLW Landscape Minimap",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        generatedTexture.SetPixels32(pixels);
        generatedTexture.Apply(false, true);
        AssignTexture(generatedTexture);
        CacheLaneSignature();

        bool assigned = rawImage && rawImage.texture == generatedTexture;
        Debug.Log($"[HLW Minimap] Tactical texture={width}x{height}, polygons={roadPolygons.Count}, " +
            $"roadPixels={RoadPixelCount}, antialias={SupersampleAxis}x{SupersampleAxis}, " +
            $"worldX=0..{lane.WidthWorld:0.#}, worldZ=0..{lane.LengthWorld:0.#}, " +
            $"orientation=RED-left/BLUE-right, assigned={assigned}, RawImageColor={rawImage.color}, " +
            $"material={(rawImage.material ? rawImage.material.name : "Default UI")}, " +
            $"active={gameObject.activeInHierarchy}, sibling={transform.GetSiblingIndex()}/{transform.parent.childCount - 1}.", this);
    }

    List<Vector2[]> BuildRoadPolygons()
    {
        List<Vector2[]> polygons = new List<Vector2[]>(lane.battleLaneCount + 2);
        float halfWidth = lane.RoadWidth * .5f;
        float sharedCenter = lane.RoadCenterX(lane.battleLaneCount / 2);
        for (int laneIndex = 0; laneIndex < lane.battleLaneCount; laneIndex++)
        {
            float branchCenter = lane.RoadCenterX(laneIndex);
            polygons.Add(new[]
            {
                new Vector2(sharedCenter - halfWidth, 0f),
                new Vector2(sharedCenter - halfWidth, lane.BranchStartWorld),
                new Vector2(branchCenter - halfWidth, lane.BranchEndWorld),
                new Vector2(branchCenter - halfWidth, lane.LengthWorld),
                new Vector2(branchCenter + halfWidth, lane.LengthWorld),
                new Vector2(branchCenter + halfWidth, lane.BranchEndWorld),
                new Vector2(sharedCenter + halfWidth, lane.BranchStartWorld),
                new Vector2(sharedCenter + halfWidth, 0f)
            });
        }

        for (int row = 0; row < lane.laneLength; row++)
        {
            if (!lane.IsConnectorRow(row) || row * lane.cellSize <= lane.BranchEndWorld) continue;
            float z0 = row * lane.cellSize;
            float z1 = Mathf.Min(lane.LengthWorld, z0 + lane.cellSize);
            float x0 = lane.RoadStartX(0);
            float x1 = lane.RoadStartX(lane.battleLaneCount - 1) + lane.RoadWidth;
            polygons.Add(new[]
            {
                new Vector2(x0, z0), new Vector2(x0, z1),
                new Vector2(x1, z1), new Vector2(x1, z0)
            });
        }
        return polygons;
    }

    Color TerrainColor(float worldX, float worldZ, int px, int py, int width, int height)
    {
        float nx = (px + .5f) / width * 2f - 1f;
        float ny = (py + .5f) / height * 2f - 1f;
        float vignette = Mathf.Clamp01((nx * nx + ny * ny) * .34f);
        float parchmentNoise = Mathf.Sin(px * .071f + py * .037f) *
                               Mathf.Sin(px * .019f - py * .053f);
        Color terrain = Color.Lerp(backgroundColor,
            new Color(.085f, .12f, .175f, 1f), .18f + parchmentNoise * .035f);
        terrain = Color.Lerp(terrain, new Color(.035f, .055f, .09f, 1f), vignette);

        if (worldZ > lane.BranchEndWorld)
        {
            for (int gap = 0; gap < lane.battleLaneCount - 1; gap++)
            {
                float gapMin = lane.RoadStartX(gap) + lane.RoadWidth;
                float gapMax = lane.RoadStartX(gap + 1);
                if (worldX > gapMin && worldX < gapMax)
                    terrain = Color.Lerp(terrain, new Color(.043f, .066f, .105f, 1f), .28f);

                float centerX = (gapMin + gapMax) * .5f;
                float runeZ = lane.LengthWorld * .43f;
                float radius = Mathf.Min((gapMax - gapMin) * .31f, 3.7f * lane.cellSize);
                float distance = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(centerX, runeZ));
                float ring = 1f - Mathf.Clamp01(Mathf.Abs(distance - radius) / (.13f * lane.cellSize));
                float innerRing = 1f - Mathf.Clamp01(Mathf.Abs(distance - radius * .63f) / (.09f * lane.cellSize));
                float crossGlyph = distance < radius * .78f
                    ? 1f - Mathf.Clamp01(Mathf.Min(Mathf.Abs(worldX - centerX), Mathf.Abs(worldZ - runeZ)) /
                                         (.07f * lane.cellSize)) : 0f;
                float glyph = Mathf.Max(ring, Mathf.Max(innerRing * .6f, crossGlyph * .45f));
                if (glyph > 0f)
                    terrain = Color.Lerp(terrain, gap == 0
                        ? new Color(.09f, .43f, .55f, 1f)
                        : new Color(.24f, .19f, .5f, 1f), glyph * .12f);
            }
        }
        return terrain;
    }

    void PixelToWorld(float pixelX, float pixelY, int width, int height, out float worldX, out float worldZ)
    {
        float innerWidth = width - PaddingPixels * 2f;
        float innerHeight = height - PaddingPixels * 2f;
        float u = Mathf.Clamp01((pixelX - PaddingPixels) / innerWidth);
        float v = Mathf.Clamp01((pixelY - PaddingPixels) / innerHeight);
        worldZ = (1f - u) * lane.LengthWorld;
        worldX = (1f - v) * lane.WidthWorld;
    }

    static bool InsideAnyPolygon(Vector2 point, List<Vector2[]> polygons)
    {
        for (int i = 0; i < polygons.Count; i++)
            if (InsidePolygon(point, polygons[i])) return true;
        return false;
    }

    static bool InsidePolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, previous = polygon.Length - 1; i < polygon.Length; previous = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[previous];
            bool crosses = (a.y > point.y) != (b.y > point.y) &&
                           point.x < (b.x - a.x) * (point.y - a.y) /
                           (b.y - a.y) + a.x;
            if (crosses) inside = !inside;
        }
        return inside;
    }

    float TextureU(float worldZ)
    {
        float inner = generatedWidth - PaddingPixels * 2f;
        return (PaddingPixels + (1f - Mathf.Clamp01(worldZ / lane.LengthWorld)) * inner) / generatedWidth;
    }

    float TextureV(float worldX)
    {
        float inner = generatedHeight - PaddingPixels * 2f;
        return (PaddingPixels + (1f - Mathf.Clamp01(worldX / lane.WidthWorld)) * inner) / generatedHeight;
    }

    void ConfigureRawImage()
    {
        if (!rawImage) rawImage = GetComponent<RawImage>();
        if (!rawImage) return;
        rawImage.color = Color.white;
        rawImage.material = null;
        rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        rawImage.raycastTarget = false;
    }

    void AssignTexture(Texture texture)
    {
        ConfigureRawImage();
        if (rawImage) rawImage.texture = texture;
    }

    void CacheLaneSignature()
    {
        cachedWidth = lane.WidthWorld;
        cachedLength = lane.LengthWorld;
        cachedCellSize = lane.cellSize;
        cachedBranchStart = lane.BranchStartWorld;
        cachedBranchEnd = lane.BranchEndWorld;
        cachedLaneCount = lane.battleLaneCount;
        cachedLaneWidth = lane.laneWidth;
        cachedLaneGap = lane.laneGap;
    }

    void ReleaseTexture()
    {
        if (!generatedTexture) return;
        if (Application.isPlaying) Destroy(generatedTexture);
        else DestroyImmediate(generatedTexture);
        generatedTexture = null;
    }

    void OnDestroy() { ReleaseTexture(); }
}
