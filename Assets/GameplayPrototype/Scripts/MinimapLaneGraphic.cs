using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws LaneGenerator's real walkable grid as an opaque UI mesh.</summary>
public sealed class MinimapLaneGraphic : MaskableGraphic
{
    [SerializeField] LaneGenerator lane;
    [SerializeField] Color roadEdgeColor = new Color(.12f, .2f, .28f, 1f);
    [SerializeField] Color roadColor = new Color(.55f, .68f, .78f, 1f);

    const float Padding = 10f;
    const float EdgePixels = 1.5f;
    float cachedWidth = -1f;
    float cachedLength = -1f;
    float cachedCellSize = -1f;
    float cachedBranchStart = -1f;
    float cachedBranchEnd = -1f;
    int cachedLaneCount = -1;
    int cachedLaneWidth = -1;
    float cachedLaneGap = -1f;
    bool diagnosticsPending;

    public LaneGenerator Lane => lane;
    public int RoadVertexCount { get; private set; }
    public int RoadTriangleCount { get; private set; }
    public int WalkableRunCount { get; private set; }

    public void SetLane(LaneGenerator value)
    {
        lane = value;
        CacheLaneSignature();
        diagnosticsPending = true;
        SetVerticesDirty();
    }

    public void SetColors(Color edges, Color roads)
    {
        roadEdgeColor = edges;
        roadColor = roads;
        diagnosticsPending = true;
        SetVerticesDirty();
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

        CacheLaneSignature();
        diagnosticsPending = true;
        SetVerticesDirty();
        return true;
    }

    public Vector2 WorldToMap(Vector3 laneLocalPosition, Rect mapBounds)
    {
        if (!lane) return Vector2.zero;
        float usableWidth = Mathf.Max(1f, mapBounds.width - Padding * 2f);
        float usableHeight = Mathf.Max(1f, mapBounds.height - Padding * 2f);
        return new Vector2(
            mapBounds.xMin + Padding + Mathf.Clamp01(laneLocalPosition.x / lane.WidthWorld) * usableWidth,
            mapBounds.yMin + Padding + Mathf.Clamp01(laneLocalPosition.z / lane.LengthWorld) * usableHeight);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        RoadVertexCount = 0;
        RoadTriangleCount = 0;
        WalkableRunCount = 0;
        Rect bounds = rectTransform.rect;
        if (!lane || lane.cellSize <= 0f || bounds.width <= Padding * 2f || bounds.height <= Padding * 2f)
        {
            LogDiagnostics(bounds);
            return;
        }

        int columns = Mathf.CeilToInt(lane.WidthWorld / lane.cellSize);
        int rows = Mathf.CeilToInt(lane.LengthWorld / lane.cellSize);
        for (int pass = 0; pass < 2; pass++)
        {
            Color32 passColor = pass == 0 ? roadEdgeColor : roadColor;
            float expand = pass == 0 ? EdgePixels : 0f;
            for (int row = 0; row < rows; row++)
            {
                int column = 0;
                while (column < columns)
                {
                    while (column < columns && !IsWalkableCellCenter(column, row)) column++;
                    if (column >= columns) break;
                    int first = column;
                    while (column + 1 < columns && IsWalkableCellCenter(column + 1, row)) column++;
                    int lastExclusive = column + 1;
                    if (pass == 0) WalkableRunCount++;
                    AddRoadQuad(vh, bounds, first, lastExclusive, row, expand, passColor);
                    column = lastExclusive;
                }
            }
        }

        RoadVertexCount = vh.currentVertCount;
        RoadTriangleCount = RoadVertexCount / 2;
        LogDiagnostics(bounds);
    }

    bool IsWalkableCellCenter(int column, int row)
    {
        return lane.IsWalkable((column + .5f) * lane.cellSize, (row + .5f) * lane.cellSize);
    }

    void AddRoadQuad(VertexHelper vh, Rect bounds, int firstColumn, int lastColumnExclusive,
        int row, float expand, Color32 quadColor)
    {
        float usableWidth = Mathf.Max(1f, bounds.width - Padding * 2f);
        float usableHeight = Mathf.Max(1f, bounds.height - Padding * 2f);
        float x0 = bounds.xMin + Padding + firstColumn * lane.cellSize / lane.WidthWorld * usableWidth - expand;
        float x1 = bounds.xMin + Padding + lastColumnExclusive * lane.cellSize / lane.WidthWorld * usableWidth + expand;
        float y0 = bounds.yMin + Padding + row * lane.cellSize / lane.LengthWorld * usableHeight - expand;
        float y1 = bounds.yMin + Padding + (row + 1) * lane.cellSize / lane.LengthWorld * usableHeight + expand;
        AddQuad(vh, new Vector2(x0, y0), new Vector2(x0, y1),
            new Vector2(x1, y1), new Vector2(x1, y0), quadColor);
    }

    static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 quadColor)
    {
        int start = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = quadColor;
        vertex.position = a; vh.AddVert(vertex);
        vertex.position = b; vh.AddVert(vertex);
        vertex.position = c; vh.AddVert(vertex);
        vertex.position = d; vh.AddVert(vertex);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    void CacheLaneSignature()
    {
        if (!lane) return;
        cachedWidth = lane.WidthWorld;
        cachedLength = lane.LengthWorld;
        cachedCellSize = lane.cellSize;
        cachedBranchStart = lane.BranchStartWorld;
        cachedBranchEnd = lane.BranchEndWorld;
        cachedLaneCount = lane.battleLaneCount;
        cachedLaneWidth = lane.laneWidth;
        cachedLaneGap = lane.laneGap;
    }

    void LogDiagnostics(Rect bounds)
    {
        if (!diagnosticsPending || !Application.isPlaying) return;
        diagnosticsPending = false;
        Debug.Log($"[HLW Minimap] Road mesh: vertices={RoadVertexCount}, triangles={RoadTriangleCount}, " +
            $"runs={WalkableRunCount}, bounds={bounds.width:0.#}x{bounds.height:0.#}, " +
            $"active={gameObject.activeInHierarchy}, enabled={enabled}, color={roadColor}, " +
            $"alpha={roadColor.a:0.##}, sibling={transform.GetSiblingIndex()}/{transform.parent.childCount - 1}.", this);
    }
}
