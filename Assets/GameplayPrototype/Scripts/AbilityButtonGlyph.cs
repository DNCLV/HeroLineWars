using UnityEngine;
using UnityEngine.UI;

public enum AbilityGlyphStyle { Fireball, Faultbreaker, EarthSpikes, FireSlash }

/// <summary>Lightweight hero-specific fallback glyphs used when no imported ability icon exists.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class AbilityButtonGlyph : MaskableGraphic
{
    public AbilityGlyphStyle style;

    public void SetStyle(AbilityGlyphStyle value)
    {
        if (style == value) return;
        style = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (style == AbilityGlyphStyle.Faultbreaker)
        {
            DrawFaultbreaker(vh, rect);
            return;
        }
        if (style == AbilityGlyphStyle.EarthSpikes)
        {
            DrawEarthSpikes(vh, rect);
            return;
        }
        if (style == AbilityGlyphStyle.FireSlash)
        {
            DrawFireSlash(vh, rect);
            return;
        }
        Color ember = new Color(1f, .24f, .035f, 1f) * color;
        Color gold = new Color(1f, .78f, .16f, 1f) * color;
        AddTriangle(vh, rect, new Vector2(.08f, .24f), new Vector2(.49f, .5f), new Vector2(.36f, .27f),
            new Color(1f, .2f, .025f, .15f) * color, ember, gold);
        AddTriangle(vh, rect, new Vector2(.19f, .08f), new Vector2(.48f, .43f), new Vector2(.39f, .2f),
            new Color(1f, .3f, .02f, .25f) * color, ember, gold);
        AddCircle(vh, rect, new Vector2(.58f, .55f), .29f, ember, gold, 24);
        AddCircle(vh, rect, new Vector2(.63f, .62f), .13f, gold, Color.white * color, 16);
    }

    void DrawFaultbreaker(VertexHelper vh, Rect rect)
    {
        Color stone = new Color(.34f, .58f, .72f, 1f) * color;
        Color arcane = new Color(.24f, .88f, 1f, 1f) * color;
        Color bright = new Color(.82f, .97f, 1f, 1f) * color;
        AddTriangle(vh, rect, new Vector2(.19f, .58f), new Vector2(.52f, .83f), new Vector2(.43f, .47f), stone, bright, arcane);
        AddTriangle(vh, rect, new Vector2(.81f, .58f), new Vector2(.48f, .83f), new Vector2(.57f, .47f), stone, bright, arcane);
        AddTriangle(vh, rect, new Vector2(.29f, .37f), new Vector2(.71f, .37f), new Vector2(.5f, .14f), arcane, arcane, bright);
        AddCircle(vh, rect, new Vector2(.5f, .48f), .12f, arcane, bright, 12);
    }

    void DrawEarthSpikes(VertexHelper vh, Rect rect)
    {
        Color deep = new Color(.18f, .42f, .58f, 1f) * color;
        Color arcane = new Color(.27f, .86f, 1f, 1f) * color;
        Color bright = new Color(.78f, .96f, 1f, 1f) * color;
        AddTriangle(vh, rect, new Vector2(.12f, .18f), new Vector2(.35f, .18f), new Vector2(.29f, .68f), deep, arcane, bright);
        AddTriangle(vh, rect, new Vector2(.31f, .16f), new Vector2(.62f, .16f), new Vector2(.51f, .88f), deep, arcane, bright);
        AddTriangle(vh, rect, new Vector2(.57f, .18f), new Vector2(.86f, .18f), new Vector2(.72f, .62f), deep, arcane, bright);
    }

    void DrawFireSlash(VertexHelper vh, Rect rect)
    {
        Color ember = new Color(1f, .32f, .06f, 1f) * color;
        Color gold = new Color(1f, .78f, .2f, 1f) * color;
        AddTriangle(vh, rect, new Vector2(.12f, .45f), new Vector2(.86f, .72f),
            new Vector2(.65f, .51f), ember, gold, ember);
        AddTriangle(vh, rect, new Vector2(.12f, .45f), new Vector2(.65f, .51f),
            new Vector2(.32f, .29f), ember, gold, new Color(.6f, .09f, .03f) * color);
    }

    static void AddCircle(VertexHelper vh, Rect rect, Vector2 center, float radius,
        Color edgeColor, Color centerColor, int segments)
    {
        int centerIndex = vh.currentVertCount;
        AddVertex(vh, rect, center, centerColor);
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            AddVertex(vh, rect, p, edgeColor);
        }
        for (int i = 0; i < segments; i++)
            vh.AddTriangle(centerIndex, centerIndex + 1 + i, centerIndex + 1 + (i + 1) % segments);
    }

    static void AddTriangle(VertexHelper vh, Rect rect, Vector2 a, Vector2 b, Vector2 c,
        Color colorA, Color colorB, Color colorC)
    {
        int start = vh.currentVertCount;
        AddVertex(vh, rect, a, colorA);
        AddVertex(vh, rect, b, colorB);
        AddVertex(vh, rect, c, colorC);
        vh.AddTriangle(start, start + 1, start + 2);
    }

    static void AddVertex(VertexHelper vh, Rect rect, Vector2 normalized, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = new Vector3(rect.xMin + normalized.x * rect.width,
            rect.yMin + normalized.y * rect.height, 0f);
        vertex.color = color;
        vh.AddVert(vertex);
    }
}
