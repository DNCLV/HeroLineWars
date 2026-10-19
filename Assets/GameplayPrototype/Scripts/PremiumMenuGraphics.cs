using UnityEngine;
using UnityEngine.UI;

/// <summary>Allocation-free vertical UI gradient used by the premium front end.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MenuGradientGraphic : MaskableGraphic
{
    public Color bottomColor = new Color(.01f, .02f, .05f, 1f);
    public Color topColor = new Color(.04f, .1f, .18f, 1f);

    public void Configure(Color bottom, Color top)
    {
        bottomColor = bottom;
        topColor = top;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        Add(vh, new Vector2(r.xMin, r.yMin), bottomColor);
        Add(vh, new Vector2(r.xMin, r.yMax), topColor);
        Add(vh, new Vector2(r.xMax, r.yMax), topColor);
        Add(vh, new Vector2(r.xMax, r.yMin), bottomColor);
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }

    static void Add(VertexHelper vh, Vector2 position, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}

/// <summary>Soft nine-point darkening that keeps the selected hero readable while framing screen edges.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MenuVignetteGraphic : MaskableGraphic
{
    public Color edgeColor = new Color(0f, .008f, .025f, .82f);
    public Color centerColor = new Color(0f, 0f, 0f, 0f);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float[] xs = { r.xMin, Mathf.Lerp(r.xMin, r.xMax, .28f), Mathf.Lerp(r.xMin, r.xMax, .72f), r.xMax };
        float[] ys = { r.yMin, Mathf.Lerp(r.yMin, r.yMax, .25f), Mathf.Lerp(r.yMin, r.yMax, .76f), r.yMax };
        for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++)
        {
            bool edge = x == 0 || x == 3 || y == 0 || y == 3;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = new Vector2(xs[x], ys[y]);
            vertex.color = edge ? edgeColor : centerColor;
            vh.AddVert(vertex);
        }
        for (int y = 0; y < 3; y++)
        for (int x = 0; x < 3; x++)
        {
            int a = y * 4 + x;
            vh.AddTriangle(a, a + 4, a + 5);
            vh.AddTriangle(a, a + 5, a + 1);
        }
    }
}

/// <summary>Beveled eight-sided panel with an integrated metallic border.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ChamferedFrameGraphic : MaskableGraphic
{
    [Min(0f)] public float corner = 14f;
    [Min(1f)] public float borderWidth = 2f;
    public Color fillColor = new Color(.025f, .045f, .075f, .9f);
    public Color borderColor = new Color(.22f, .48f, .68f, .9f);

    public void Configure(Color fill, Color border, float chamfer = 14f, float borderSize = 2f)
    {
        fillColor = fill;
        borderColor = border;
        corner = chamfer;
        borderWidth = borderSize;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float border = Mathf.Clamp(borderWidth, 1f, Mathf.Min(r.width, r.height) * .2f);
        Vector2[] outer = BuildPoints(r, Mathf.Min(corner, Mathf.Min(r.width, r.height) * .35f));
        Rect innerRect = new Rect(r.xMin + border, r.yMin + border,
            Mathf.Max(0f, r.width - border * 2f), Mathf.Max(0f, r.height - border * 2f));
        Vector2[] inner = BuildPoints(innerRect, Mathf.Max(0f, corner - border * .5f));

        for (int i = 0; i < 8; i++) Add(vh, outer[i], borderColor);
        for (int i = 0; i < 8; i++) Add(vh, inner[i], fillColor);
        int center = vh.currentVertCount;
        Add(vh, r.center, fillColor);
        for (int i = 0; i < 8; i++)
        {
            int next = (i + 1) % 8;
            vh.AddTriangle(i, next, 8 + next);
            vh.AddTriangle(i, 8 + next, 8 + i);
            vh.AddTriangle(center, 8 + i, 8 + next);
        }
    }

    static Vector2[] BuildPoints(Rect r, float c) => new[]
    {
        new Vector2(r.xMin + c, r.yMin), new Vector2(r.xMax - c, r.yMin),
        new Vector2(r.xMax, r.yMin + c), new Vector2(r.xMax, r.yMax - c),
        new Vector2(r.xMax - c, r.yMax), new Vector2(r.xMin + c, r.yMax),
        new Vector2(r.xMin, r.yMax - c), new Vector2(r.xMin, r.yMin + c)
    };

    static void Add(VertexHelper vh, Vector2 position, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}

/// <summary>Low-cost ornamental ring used by the gameplay HUD's thumb controls.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ArcaneRingGraphic : MaskableGraphic
{
    [Range(.02f, .25f)] public float thickness = .085f;
    [Range(8, 64)] public int segments = 40;
    [Range(0, 16)] public int accentTicks = 8;
    public Color innerColor = new Color(.12f, .54f, .78f, .95f);
    public Color outerColor = new Color(.76f, .9f, 1f, .92f);
    public Color tickColor = new Color(1f, .72f, .25f, .9f);

    public void Configure(Color inner, Color outer, Color ticks, float ringThickness = .085f)
    {
        innerColor = inner;
        outerColor = outer;
        tickColor = ticks;
        thickness = ringThickness;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float radius = Mathf.Min(r.width, r.height) * .5f;
        float innerRadius = radius * (1f - Mathf.Clamp(thickness, .02f, .25f));
        Vector2 center = r.center;
        int count = Mathf.Clamp(segments, 8, 64);
        for (int i = 0; i < count; i++)
        {
            float a0 = i * Mathf.PI * 2f / count;
            float a1 = (i + 1) * Mathf.PI * 2f / count;
            int start = vh.currentVertCount;
            Add(vh, center + Direction(a0) * innerRadius, innerColor * color);
            Add(vh, center + Direction(a0) * radius, outerColor * color);
            Add(vh, center + Direction(a1) * radius, outerColor * color);
            Add(vh, center + Direction(a1) * innerRadius, innerColor * color);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        int ticks = Mathf.Clamp(accentTicks, 0, 16);
        for (int i = 0; i < ticks; i++)
        {
            float angle = i * Mathf.PI * 2f / ticks;
            float half = Mathf.PI / count * .8f;
            float tickInner = radius * .78f;
            float tickOuter = radius * 1.02f;
            int start = vh.currentVertCount;
            Add(vh, center + Direction(angle - half) * tickInner, tickColor * color);
            Add(vh, center + Direction(angle - half) * tickOuter, tickColor * color);
            Add(vh, center + Direction(angle + half) * tickOuter, tickColor * color);
            Add(vh, center + Direction(angle + half) * tickInner, tickColor * color);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

    static void Add(VertexHelper vh, Vector2 position, Color tint)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = tint;
        vh.AddVert(vertex);
    }
}
