using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Shared lightweight runtime-only materials and short hit/impact flashes.</summary>
public static class GameplayVisuals
{
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static readonly Dictionary<Color, Material> impactMaterials = new Dictionary<Color, Material>();
    static readonly Dictionary<float, Mesh> ringMeshes = new Dictionary<float, Mesh>();
    static readonly Dictionary<UnitArchetype, float> archetypeFootprints = new Dictionary<UnitArchetype, float>();
    static readonly Stack<CombatLifetime> impactPool = new Stack<CombatLifetime>(24);
    static readonly HashSet<CombatLifetime> allImpactEffects = new HashSet<CombatLifetime>();
    const int MaxImpactEffects = 24;
    static int impactEffectsCreated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void InitializeResourceLifecycle()
    {
        CleanupRuntimeResources(false);
        Application.quitting -= OnApplicationQuitting;
        Application.quitting += OnApplicationQuitting;
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnEditorPlayModeChanged;
        EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
#endif
    }

    static void OnApplicationQuitting() { CleanupRuntimeResources(false); }

#if UNITY_EDITOR
    static void OnEditorPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) CleanupRuntimeResources(true);
    }
#endif

    static void CleanupRuntimeResources(bool immediate)
    {
        GoblinHealth.ResetFlashResources();
        foreach (CombatLifetime effect in allImpactEffects)
            DestroyRuntimeObject(effect ? effect.gameObject : null, immediate);
        allImpactEffects.Clear();
        impactPool.Clear();
        impactEffectsCreated = 0;

        foreach (Material material in materials.Values) DestroyRuntimeObject(material, immediate);
        materials.Clear();
        impactMaterials.Clear();
        foreach (Mesh mesh in ringMeshes.Values) DestroyRuntimeObject(mesh, immediate);
        ringMeshes.Clear();
        archetypeFootprints.Clear();
    }

    static void DestroyRuntimeObject(Object resource, bool immediate)
    {
        if (!resource) return;
        if (immediate) Object.DestroyImmediate(resource);
        else Object.Destroy(resource);
    }

    public static Material GetMaterial(string name, Color color)
    {
        if (materials.TryGetValue(name, out Material cached) && cached) return cached;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (!shader)
        {
            Debug.LogWarning("No compatible unlit shader is available for gameplay visuals.");
            return null;
        }
        Material material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        materials[name] = material;
        return material;
    }

    public static Transform CreateGroundRing(string name, float width)
    {
        const int segments = 24;
        GameObject ring = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        if (!ringMeshes.TryGetValue(width, out Mesh mesh) || !mesh)
        {
            mesh = new Mesh { name = "HLW GroundRingMesh" };
            Vector3[] vertices = new Vector3[segments * 2];
            int[] triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                vertices[i * 2] = new Vector3(x, 0f, z);
                vertices[i * 2 + 1] = new Vector3(x * (1f - width), 0f, z * (1f - width));
                int next = (i + 1) % segments;
                int t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = next * 2 + 1;
                triangles[t + 2] = next * 2;
                triangles[t + 3] = i * 2;
                triangles[t + 4] = i * 2 + 1;
                triangles[t + 5] = next * 2 + 1;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            ringMeshes[width] = mesh;
        }
        ring.GetComponent<MeshFilter>().sharedMesh = mesh;
        Renderer renderer = ring.GetComponent<Renderer>();
        renderer.sharedMaterial = GetMaterial(name.Contains("Fireball") ? "HLW Ability Valid" :
            "HLW Cyan Ground Marker", name.Contains("Fireball") ? new Color(1f, .42f, .08f) :
            new Color(.12f, .78f, 1f, .72f));
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ring.transform;
    }

    public static float GetGroundFootprintRadius(Transform visualRoot, UnitArchetype archetype)
    {
        if (!visualRoot) return .35f;
        if (archetypeFootprints.TryGetValue(archetype, out float cached)) return cached;

        // Sample vertices near the bottom of each mesh. This estimates the ground-contact
        // footprint and deliberately excludes height, wings, weapons, and renderer padding.
        MeshFilter[] filters = visualRoot.GetComponentsInChildren<MeshFilter>(true);
        List<Vector3> vertices = new List<Vector3>(256);
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
        bool foundGeometry = false;
        for (int f = 0; f < filters.Length; f++)
        {
            MeshFilter filter = filters[f];
            Mesh mesh = filter ? filter.sharedMesh : null;
            if (!mesh || !mesh.isReadable) continue;
            vertices.Clear();
            mesh.GetVertices(vertices);
            if (vertices.Count == 0) continue;
            float meshMinY = float.PositiveInfinity, meshMaxY = float.NegativeInfinity;
            for (int i = 0; i < vertices.Count; i++)
            {
                float y = visualRoot.InverseTransformPoint(filter.transform.TransformPoint(vertices[i])).y;
                meshMinY = Mathf.Min(meshMinY, y);
                meshMaxY = Mathf.Max(meshMaxY, y);
            }
            float contactLimit = meshMinY + Mathf.Max(.04f, (meshMaxY - meshMinY) * .07f);
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 point = visualRoot.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
                if (point.y > contactLimit) continue;
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minZ = Mathf.Min(minZ, point.z);
                maxZ = Mathf.Max(maxZ, point.z);
                foundGeometry = true;
            }
        }

        float radius;
        if (foundGeometry)
            radius = Mathf.Max(.2f, Mathf.Max(maxX - minX, maxZ - minZ) * .56f);
        else
        {
            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) radius = .35f;
            else
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                radius = Mathf.Max(.2f, Mathf.Max(bounds.size.x, bounds.size.z) * .36f);
            }
        }
        if (foundGeometry)
        {
            Vector3 visualScale = visualRoot.lossyScale;
            radius *= Mathf.Max(Mathf.Abs(visualScale.x), Mathf.Abs(visualScale.z));
        }
        archetypeFootprints[archetype] = radius;
        return radius;
    }

    public static void Impact(Vector3 position, float size, Color color, float duration = .16f)
    {
        CombatLifetime effect = null;
        while (impactPool.Count > 0 && !effect) effect = impactPool.Pop();
        if (!effect)
        {
            if (impactEffectsCreated >= MaxImpactEffects) return;
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "PooledCombatImpact";
            Collider collider = flash.GetComponent<Collider>();
            if (collider) Object.Destroy(collider);
            Renderer renderer = flash.GetComponent<Renderer>();
            if (renderer)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            effect = flash.AddComponent<CombatLifetime>();
            impactEffectsCreated++;
            allImpactEffects.Add(effect);
        }
        effect.Play(position, size, color, duration);
    }

    internal static Material ImpactMaterial(Color color)
    {
        if (impactMaterials.TryGetValue(color, out Material cached) && cached) return cached;
        string name = "HLW Impact " + ColorUtility.ToHtmlStringRGBA(color);
        Material material = GetMaterial(name, color);
        impactMaterials[color] = material;
        return material;
    }

    internal static void RecycleImpact(CombatLifetime effect)
    {
        if (!effect) return;
        effect.gameObject.SetActive(false);
        if (impactPool.Count < MaxImpactEffects) impactPool.Push(effect);
        else Object.Destroy(effect.gameObject);
    }
}

public sealed class CombatLifetime : MonoBehaviour
{
    float duration;
    float elapsed;
    float maximumSize;
    Renderer cachedRenderer;

    void Awake() { cachedRenderer = GetComponent<Renderer>(); }

    public void Play(Vector3 position, float size, Color color, float effectDuration)
    {
        duration = Mathf.Max(.06f, effectDuration);
        elapsed = 0f;
        maximumSize = Mathf.Max(.05f, size);
        transform.position = position;
        transform.localScale = Vector3.one * maximumSize * .25f;
        if (cachedRenderer) cachedRenderer.sharedMaterial = GameplayVisuals.ImpactMaterial(color);
        gameObject.SetActive(true);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        transform.localScale = Vector3.one * Mathf.Lerp(maximumSize * .25f, maximumSize, t);
        if (elapsed >= duration) GameplayVisuals.RecycleImpact(this);
    }
}
