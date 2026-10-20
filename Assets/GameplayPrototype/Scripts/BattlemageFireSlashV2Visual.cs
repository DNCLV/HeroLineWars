using UnityEngine;
using UnityEngine.Rendering;

/// <summary>The reference crescent grows out from the caster; its inner core remains connected.</summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class BattlemageFireSlashV2Visual : MonoBehaviour
{
    [Min(.5f)] public float bodyDepth = 8f;
    [Min(.05f)] public float heightAboveLane = .24f;
    public Material slashMaterial;
    public float Width { get; private set; }
    public Texture2D SlashTexture { get; private set; }
    public float FrontDistance { get; private set; }

    const int Columns = 33;
    const int Rows = 17;
    MeshRenderer slashRenderer;
    Mesh mesh;
    Vector3[] vertices;
    Vector2[] uvs;
    MaterialPropertyBlock properties;
    Vector3 origin;
    Vector3 forward;
    float distance;
    float currentSize;
    float flameTime;

    public void Initialize(float worldWidth, float travelDistance, Vector3 start, Vector3 direction)
    {
        origin = start;
        forward = direction.normalized;
        distance = Mathf.Max(.1f, travelDistance);
        Width = Mathf.Max(.5f, worldWidth);
        SlashTexture = slashMaterial.GetTexture("_BaseMap") as Texture2D;
        slashRenderer = GetComponent<MeshRenderer>();
        slashRenderer.sharedMaterial = slashMaterial;
        slashRenderer.shadowCastingMode = ShadowCastingMode.Off;
        slashRenderer.receiveShadows = false;
        properties = new MaterialPropertyBlock();

        vertices = new Vector3[Columns * Rows];
        uvs = new Vector2[vertices.Length];
        int[] triangles = new int[(Columns - 1) * (Rows - 1) * 12];
        int index = 0;
        for (int row = 0; row < Rows - 1; row++)
        for (int column = 0; column < Columns - 1; column++)
        {
            int a = row * Columns + column;
            int b = a + 1;
            int c = a + Columns;
            int d = c + 1;
            triangles[index++] = a; triangles[index++] = c; triangles[index++] = b;
            triangles[index++] = b; triangles[index++] = c; triangles[index++] = d;
            triangles[index++] = b; triangles[index++] = c; triangles[index++] = a;
            triangles[index++] = d; triangles[index++] = c; triangles[index++] = b;
        }
        mesh = new Mesh { name = "Animated Fire Slash Reference Crescent" };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        GetComponent<MeshFilter>().sharedMesh = mesh;
        RenderAt(0f, .35f, .25f);
    }

    public void RenderAt(float progress, float opacity, float size = 1f)
    {
        if (!mesh) return;
        progress = Mathf.Clamp01(progress);
        // Rotate the U-shaped reference so its open side faces the caster and
        // its bright convex edge faces down the lane.
        currentSize = Mathf.Max(.05f, Mathf.Min(size, Mathf.Sqrt(progress)));
        FrontDistance = distance * currentSize;
        transform.position = origin + forward * FrontDistance;
        transform.localScale = new Vector3(currentSize, currentSize, 1f);
        flameTime += Time.deltaTime;
        for (int row = 0; row < Rows; row++)
        for (int column = 0; column < Columns; column++)
        {
            float u = column / (float)(Columns - 1); // source image vertical
            float v = row / (float)(Rows - 1); // source image horizontal
            float interior = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(v * Mathf.PI);
            float lick = Mathf.Sin(u * 31f + v * 13f + flameTime * 25f);
            float flow = Mathf.Sin(u * 18f - v * 21f - flameTime * 17f);
            float side = Mathf.Abs(v - .5f) * 2f;
            float advance = u * (1f - .5f * side) + .25f * side;
            float sourceY = .50f + .35f * side * side - .40f * u;
            int vertex = row * Columns + column;
            vertices[vertex] = new Vector3(
                (v - .5f) * Width + interior * lick * .18f,
                (Mathf.Clamp01(advance + interior * flow * .012f) - 1f) * bodyDepth,
                heightAboveLane + interior * lick * .045f);
            uvs[vertex] = new Vector2(
                Mathf.Clamp01(v + interior * flow * .025f + flameTime * .035f),
                Mathf.Clamp01(sourceY + interior * lick * .025f));
        }
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.RecalculateBounds();
        float heat = .82f + .08f * Mathf.Sin(flameTime * 19f);
        properties.SetColor("_BaseColor", new Color(1f, heat, heat, Mathf.Clamp01(opacity) * .78f));
        slashRenderer.SetPropertyBlock(properties);
    }

    public bool TouchesVisibleFire(float forwardDistance, float lateral, float radius)
    {
        if (!SlashTexture || FrontDistance <= .001f) return false;
        float sampleRadius = Mathf.Min(radius * .6f, .45f);
        return Sample(forwardDistance, lateral) ||
            Sample(forwardDistance - sampleRadius, lateral) ||
            Sample(forwardDistance + sampleRadius, lateral) ||
            Sample(forwardDistance, lateral - sampleRadius) ||
            Sample(forwardDistance, lateral + sampleRadius);
    }

    bool Sample(float forwardDistance, float lateral)
    {
        float width = Width * currentSize;
        if (forwardDistance < 0f || forwardDistance > FrontDistance ||
            Mathf.Abs(lateral) > width * .5f) return false;
        float v = lateral / width + .5f;
        float side = Mathf.Abs(v - .5f) * 2f;
        float advance = forwardDistance / FrontDistance;
        float u = (advance - .25f * side) / (1f - .5f * side);
        if (u < 0f || u > 1f) return false;
        float sourceY = .50f + .35f * side * side - .40f * u;
        return SlashTexture.GetPixelBilinear(v, sourceY).a >= .12f;
    }

    void OnDestroy()
    {
        if (mesh)
        {
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }
}
