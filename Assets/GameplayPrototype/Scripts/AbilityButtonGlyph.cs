using UnityEngine;
using UnityEngine.UI;

/// <summary>Small procedural fire-orb glyph used when no ability icon sprite is assigned.</summary>
public sealed class AbilityButtonGlyph : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        Color ember = new Color(1f, .24f, .035f, 1f) * color;
        Color gold = new Color(1f, .78f, .16f, 1f) * color;
        AddTriangle(vh, rect, new Vector2(.08f, .24f), new Vector2(.49f, .5f), new Vector2(.36f, .27f),
            new Color(1f, .2f, .025f, .15f) * color, ember, gold);
        AddTriangle(vh, rect, new Vector2(.19f, .08f), new Vector2(.48f, .43f), new Vector2(.39f, .2f),
            new Color(1f, .3f, .02f, .25f) * color, ember, gold);
        AddCircle(vh, rect, new Vector2(.58f, .55f), .29f, ember, gold, 24);
        AddCircle(vh, rect, new Vector2(.63f, .62f), .13f, gold, Color.white * color, 16);
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
