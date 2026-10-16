using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Safe-area minimap showing generated walkability and pooled live unit markers.</summary>
public sealed class SchematicMinimapHud : MonoBehaviour
{
    [Header("Display")]
    [Range(.08f, .5f)] public float refreshInterval = .16f;
    [Range(4f, 14f)] public float unitMarkerSize = 8f;
    [Range(9f, 20f)] public float heroMarkerSize = 14f;

    RectTransform safeRoot;
    RectTransform mapRect;
    MinimapLaneGraphic laneGraphic;
    Sprite circleSprite;
    LaneGenerator lane;
    Transform hero;
    LaneUnitCombat heroCombat;
    Image heroOuter;
    Image heroInner;
    readonly Dictionary<LaneUnitCombat, Image> unitMarkers = new Dictionary<LaneUnitCombat, Image>();
    readonly HashSet<LaneUnitCombat> seenMarkers = new HashSet<LaneUnitCombat>();
    readonly List<LaneUnitCombat> activeUnits = new List<LaneUnitCombat>(64);
    readonly List<LaneUnitCombat> staleUnits = new List<LaneUnitCombat>(16);
    readonly Stack<Image> markerPool = new Stack<Image>(32);
    readonly Dictionary<int, int> overlapCounts = new Dictionary<int, int>(64);
    readonly List<Image> baseMarkers = new List<Image>(4);
    float nextRefresh;
    float nextReferenceSearch;
    const float Border = 7f;
    const float OverlapCell = 9f;

    public void Build(RectTransform root, Sprite frameSprite, Sprite markerSprite,
        Vector2 anchoredPosition, Vector2 size)
    {
        safeRoot = root;
        circleSprite = markerSprite;
        Transform previous = safeRoot.Find("SchematicMinimap");
        if (previous) Destroy(previous.gameObject);

        GameObject panel = new GameObject("SchematicMinimap", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Shadow));
        panel.transform.SetParent(safeRoot, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = size;
        Image frame = panel.GetComponent<Image>();
        frame.sprite = frameSprite;
        frame.type = frameSprite ? Image.Type.Sliced : Image.Type.Simple;
        frame.color = frameSprite ? new Color(.58f, .69f, .84f, .94f) : new Color(.035f, .05f, .08f, .95f);
        frame.raycastTarget = false;
        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = new Color(.12f, .65f, .95f, .56f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        Shadow shadow = panel.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, .01f, .035f, .72f);
        shadow.effectDistance = new Vector2(0f, -3f);
        shadow.useGraphicAlpha = true;

        GameObject insetObject = new GameObject("MapInset", typeof(RectTransform), typeof(Image));
        insetObject.transform.SetParent(panel.transform, false);
        Image inset = insetObject.GetComponent<Image>();
        inset.sprite = null;
        inset.type = Image.Type.Simple;
        inset.color = new Color(.012f, .02f, .03f, 1f);
        inset.raycastTarget = false;
        mapRect = insetObject.GetComponent<RectTransform>();
        mapRect.anchorMin = Vector2.zero;
        mapRect.anchorMax = Vector2.one;
        mapRect.offsetMin = new Vector2(Border, Border);
        mapRect.offsetMax = new Vector2(-Border, -Border);

        GameObject drawing = new GameObject("GeneratedWalkableMap", typeof(RectTransform), typeof(MinimapLaneGraphic));
        drawing.transform.SetParent(mapRect, false);
        RectTransform drawingRect = drawing.GetComponent<RectTransform>();
        drawingRect.anchorMin = Vector2.zero;
        drawingRect.anchorMax = Vector2.one;
        drawingRect.offsetMin = drawingRect.offsetMax = Vector2.zero;
        laneGraphic = drawing.GetComponent<MinimapLaneGraphic>();
        laneGraphic.raycastTarget = false;
        laneGraphic.color = Color.white;
        laneGraphic.SetColors(new Color(.13f, .22f, .31f, 1f),
            new Color(.55f, .68f, .78f, 1f));
        drawing.transform.SetAsFirstSibling();

        heroOuter = CreateMarker("BattlemageMarkerOutline", new Color(.06f, .07f, .09f, 1f), heroMarkerSize + 5f);
        heroInner = CreateMarker("BattlemageMarker", new Color(1f, .86f, .32f, 1f), heroMarkerSize);
        heroOuter.transform.SetAsLastSibling();
        heroInner.transform.SetAsLastSibling();
        hero = null;
        heroCombat = null;
        lane = FindAnyObjectByType<LaneGenerator>();
        if (laneGraphic) laneGraphic.SetLane(lane);
        BuildBaseMarkers();
        unitMarkers.Clear();
        seenMarkers.Clear();
        activeUnits.Clear();
        staleUnits.Clear();
        markerPool.Clear();
        overlapCounts.Clear();
        nextReferenceSearch = 0f;
        nextRefresh = 0f;
        UpdateMarkers(true);
    }

    void Update()
    {
        if (!mapRect) return;
        if ((!lane || !hero) && Time.unscaledTime >= nextReferenceSearch)
        {
            if (!lane) lane = FindAnyObjectByType<LaneGenerator>();
            if (!hero)
            {
                HeroLaneController heroController = FindAnyObjectByType<HeroLaneController>();
                if (heroController)
                {
                    hero = heroController.transform;
                    heroCombat = heroController.GetComponent<LaneUnitCombat>();
                }
            }
            nextReferenceSearch = Time.unscaledTime + 1f;
            if (laneGraphic && laneGraphic.Lane != lane)
            {
                laneGraphic.SetLane(lane);
                BuildBaseMarkers();
            }
        }
        if (laneGraphic && laneGraphic.RefreshIfChanged()) BuildBaseMarkers();
        if (Time.unscaledTime >= nextRefresh) UpdateMarkers(false);
    }

    void UpdateMarkers(bool force)
    {
        nextRefresh = Time.unscaledTime + refreshInterval;
        if (!lane || !mapRect) return;
        UnitCombatRegistry.CopyActiveUnits(activeUnits);
        seenMarkers.Clear();
        overlapCounts.Clear();

        for (int i = 0; i < activeUnits.Count; i++)
        {
            LaneUnitCombat unit = activeUnits[i];
            if (!unit || unit == heroCombat) continue;
            Vector3 local = lane.transform.InverseTransformPoint(unit.transform.position);
            if (!TryWorldToMap(local.x, local.z, out Vector2 point)) continue;

            seenMarkers.Add(unit);
            if (!unitMarkers.TryGetValue(unit, out Image marker) || !marker)
            {
                marker = GetPooledMarker(unit.Faction.team == UnitTeam.Friendly ? "AllyMarker" : "EnemyMarker");
                unitMarkers[unit] = marker;
            }
            marker.color = unit.Faction.team == UnitTeam.Friendly
                ? new Color(.16f, .84f, 1f, .98f) : new Color(1f, .2f, .23f, .98f);
            SetMarker(marker, SeparateOverlaps(point));
            marker.gameObject.SetActive(true);
        }

        staleUnits.Clear();
        foreach (KeyValuePair<LaneUnitCombat, Image> entry in unitMarkers)
        {
            if (!entry.Key || !seenMarkers.Contains(entry.Key))
            {
                RecycleMarker(entry.Value);
                staleUnits.Add(entry.Key);
            }
        }
        for (int i = 0; i < staleUnits.Count; i++) unitMarkers.Remove(staleUnits[i]);

        Vector2 heroPoint = default;
        bool heroVisible = false;
        if (hero)
        {
            Vector3 heroLocal = lane.transform.InverseTransformPoint(hero.position);
            heroVisible = TryWorldToMap(heroLocal.x, heroLocal.z, out heroPoint);
        }
        if (heroOuter) heroOuter.gameObject.SetActive(heroVisible);
        if (heroInner) heroInner.gameObject.SetActive(heroVisible);
        if (heroVisible)
        {
            SetMarker(heroOuter, heroPoint);
            SetMarker(heroInner, heroPoint);
            heroOuter.transform.SetAsLastSibling();
            heroInner.transform.SetAsLastSibling();
        }
        if (force && laneGraphic) laneGraphic.SetLane(lane);
    }

    bool TryWorldToMap(float x, float z, out Vector2 point)
    {
        point = default;
        if (x < 0f || x > lane.WidthWorld || z < 0f || z > lane.LengthWorld) return false;
        Rect rect = mapRect.rect;
        point = laneGraphic ? laneGraphic.WorldToMap(new Vector3(x, 0f, z), rect) :
            new Vector2((Mathf.Clamp01(x / lane.WidthWorld) - .5f) * rect.width,
                (Mathf.Clamp01(z / lane.LengthWorld) - .5f) * rect.height);
        return true;
    }

    void BuildBaseMarkers()
    {
        for (int i = 0; i < baseMarkers.Count; i++)
            if (baseMarkers[i]) Destroy(baseMarkers[i].gameObject);
        baseMarkers.Clear();
        if (!lane || !mapRect) return;
        AddBaseMarker("BlueBaseMarker", lane.RoadCenterX(lane.battleLaneCount / 2), lane.cellSize * 2f,
            new Color(.08f, .68f, 1f, 1f));
        for (int i = 0; i < lane.battleLaneCount; i++)
            AddBaseMarker("RedBaseMarker_" + i, lane.RoadCenterX(i), lane.LengthWorld - lane.cellSize * 2f,
                new Color(1f, .2f, .23f, 1f));
    }

    void AddBaseMarker(string name, float x, float z, Color color)
    {
        if (!TryWorldToMap(x, z, out Vector2 point)) return;
        Image outline = CreateMarker(name + "Outline", new Color(.005f, .012f, .02f, 1f), 18f);
        Image crest = CreateMarker(name, color, 12f);
        SetMarker(outline, point);
        SetMarker(crest, point);
        baseMarkers.Add(outline);
        baseMarkers.Add(crest);
    }

    Vector2 SeparateOverlaps(Vector2 point)
    {
        int col = Mathf.RoundToInt(point.x / OverlapCell);
        int row = Mathf.RoundToInt(point.y / OverlapCell);
        int key = (col * 73856093) ^ (row * 19349663);
        overlapCounts.TryGetValue(key, out int count);
        overlapCounts[key] = count + 1;
        if (count == 0) return point;
        float angle = count * 2.39996323f;
        float radius = Mathf.Min(5.5f, 1.8f + Mathf.Sqrt(count) * 1.4f);
        return point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    Image CreateMarker(string name, Color color, float size)
    {
        GameObject markerObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        markerObject.transform.SetParent(mapRect, false);
        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.anchorMin = markerRect.anchorMax = markerRect.pivot = new Vector2(.5f, .5f);
        markerRect.sizeDelta = new Vector2(size, size);
        Image marker = markerObject.GetComponent<Image>();
        marker.sprite = circleSprite;
        marker.color = color;
        marker.raycastTarget = false;
        marker.preserveAspect = true;
        return marker;
    }

    Image GetPooledMarker(string name)
    {
        Image marker = markerPool.Count > 0 ? markerPool.Pop() : CreateMarker(name, Color.white, unitMarkerSize);
        marker.gameObject.name = name;
        RectTransform rect = (RectTransform)marker.transform;
        rect.sizeDelta = new Vector2(unitMarkerSize, unitMarkerSize);
        return marker;
    }

    void RecycleMarker(Image marker)
    {
        if (!marker) return;
        marker.gameObject.SetActive(false);
        markerPool.Push(marker);
    }

    static void SetMarker(Image marker, Vector2 point)
    {
        if (marker) ((RectTransform)marker.transform).anchoredPosition = point;
    }
}
