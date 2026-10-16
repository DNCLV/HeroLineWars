using UnityEngine;
using System.Collections.Generic;

/// <summary>Tap-to-move controller for the Battlemage on the generator's lane plane.</summary>
public sealed class HeroLaneController : MonoBehaviour
{
    public LaneGenerator lane;
    [Min(.1f)] public float movementSpeed = 3.5f;
    [Min(.1f)] public float acceleration = 18f;
    [Min(.01f)] public float stoppingDistance = .22f;
    [Min(1f)] public float turnSpeed = 720f;
    [Min(0f)] public float boundaryInset = .5f;
    [Min(.1f)] public float modelScale = 1.12f;
    [Min(.1f)] public float selectionRingSize = 1.15f;
    public float modelYawOffset;
    [Range(0, 1)] public int allowedLaneIndex;

    Vector3 destination;
    Transform modelRoot;
    Transform destinationMarker;
    Transform selectionRing;
    FireballAbility fireball;
    bool hasDestination;
    bool scaledModel;
    readonly List<Vector3> route = new List<Vector3>();
    int routeIndex;
    Vector2 planarVelocity;

    public bool IsMoving { get; private set; }
    public Vector3 Destination => destination;

    public void CancelMovement()
    {
        destination = transform.position;
        hasDestination = false;
        IsMoving = false;
        planarVelocity = Vector2.zero;
        route.Clear();
        routeIndex = 0;
        if (destinationMarker) destinationMarker.gameObject.SetActive(false);
    }

    void Awake()
    {
        GameplayInputRouter.EnsureInstance(transform.parent);
        if (!lane) lane = FindAnyObjectByType<LaneGenerator>();
        fireball = GetComponent<FireballAbility>();
        if (transform.childCount > 0) modelRoot = transform.GetChild(0);
        SetupGroundEffects();
    }

    void OnEnable()
    {
        GameplayInputRouter.Tapped += OnLaneTap;
    }

    void OnDisable()
    {
        GameplayInputRouter.Tapped -= OnLaneTap;
    }

    void Start()
    {
        if (modelRoot && !scaledModel)
        {
            modelRoot.localScale *= modelScale;
            scaledModel = true;
        }
        if (selectionRing && modelRoot)
        {
            float footprint = GameplayVisuals.GetGroundFootprintRadius(modelRoot, UnitArchetype.Battlemage);
            selectionRing.localScale = new Vector3(footprint * selectionRingSize, 1f,
                footprint * selectionRingSize);
        }
    }

    void Update()
    {
        if (!lane || !hasDestination) { IsMoving = false; return; }
        Vector3 local = lane.transform.InverseTransformPoint(transform.position);
        Vector3 target = lane.transform.InverseTransformPoint(routeIndex < route.Count ? route[routeIndex] : destination);
        Vector2 delta = new Vector2(target.x - local.x, target.z - local.z);
        float distance = delta.magnitude;
        if (routeIndex >= route.Count && distance <= stoppingDistance + .04f && planarVelocity.magnitude <= .08f)
        {
            planarVelocity = Vector2.zero;
            hasDestination = false;
            IsMoving = false;
            if (destinationMarker) destinationMarker.gameObject.SetActive(false);
            return;
        }
        float arriveRadius = routeIndex < route.Count ? Mathf.Max(.08f, lane.cellSize * .08f) : stoppingDistance;
        if (distance <= arriveRadius)
        {
            if (routeIndex < route.Count - 1) routeIndex++;
            else if (routeIndex < route.Count) routeIndex++;
            else if (planarVelocity.sqrMagnitude < .0025f)
            {
                hasDestination = false;
                IsMoving = false;
                if (destinationMarker) destinationMarker.gameObject.SetActive(false);
                return;
            }
            target = lane.transform.InverseTransformPoint(routeIndex < route.Count ? route[routeIndex] : destination);
            delta = new Vector2(target.x - local.x, target.z - local.z);
            distance = delta.magnitude;
        }

        bool finalTarget = routeIndex >= route.Count;
        float desiredSpeed = finalTarget ? Mathf.Min(movementSpeed, Mathf.Sqrt(2f * acceleration * Mathf.Max(0f, distance - stoppingDistance))) : movementSpeed;
        Vector2 desiredVelocity = distance > .001f ? delta / distance * desiredSpeed : Vector2.zero;
        planarVelocity = Vector2.MoveTowards(planarVelocity, desiredVelocity, acceleration * Time.deltaTime);
        if (finalTarget && distance <= stoppingDistance && planarVelocity.magnitude > desiredSpeed)
            planarVelocity = Vector2.ClampMagnitude(planarVelocity, desiredSpeed);
        IsMoving = planarVelocity.sqrMagnitude > .0025f || distance > stoppingDistance;
        local.x += planarVelocity.x * Time.deltaTime;
        local.z += planarVelocity.y * Time.deltaTime;
        local.y = 0f;
        transform.position = lane.transform.TransformPoint(local);
        if (planarVelocity.sqrMagnitude > .01f)
        {
            Vector3 worldDirection = lane.transform.TransformDirection(new Vector3(planarVelocity.x, 0f, planarVelocity.y));
            Quaternion facing = Quaternion.LookRotation(worldDirection, lane.transform.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * Time.deltaTime);
        }
    }

    void OnLaneTap(Vector2 screenPosition)
    {
        if (!lane || (fireball && fireball.IsTargeting)) return;
        if (!GameplayInputRouter.TryGetLanePoint(screenPosition, lane, out Vector3 point)) return;
        Vector3 local = lane.transform.InverseTransformPoint(point);
        if (!lane.IsPointInRoad(allowedLaneIndex, local.x, local.z, boundaryInset)) return;
        if (!BuildRoute(lane.transform.InverseTransformPoint(transform.position), local)) return;
        local.y = .015f;
        destination = lane.transform.TransformPoint(local);
        hasDestination = true;
        IsMoving = true;
        if (destinationMarker)
        {
            destinationMarker.position = destination;
            destinationMarker.gameObject.SetActive(true);
        }
    }

    bool BuildRoute(Vector3 start, Vector3 end)
    {
        int columns = Mathf.RoundToInt(lane.WidthWorld / lane.cellSize);
        int rows = lane.laneLength;
        int sx = Mathf.Clamp(Mathf.FloorToInt(start.x / lane.cellSize), 0, columns - 1);
        int sz = Mathf.Clamp(Mathf.FloorToInt(start.z / lane.cellSize), 0, rows - 1);
        int ex = Mathf.Clamp(Mathf.FloorToInt(end.x / lane.cellSize), 0, columns - 1);
        int ez = Mathf.Clamp(Mathf.FloorToInt(end.z / lane.cellSize), 0, rows - 1);
        if (!lane.IsWalkable((sx + .5f) * lane.cellSize, (sz + .5f) * lane.cellSize)) return false;
        if (SegmentIsWalkable(start, end))
        {
            route.Clear();
            routeIndex = 0;
            return true;
        }
        int origin = sz * columns + sx, target = ez * columns + ex;
        int[] previous = new int[columns * rows];
        for (int i = 0; i < previous.Length; i++) previous[i] = -1;
        Queue<int> queue = new Queue<int>();
        previous[origin] = origin;
        queue.Enqueue(origin);
        while (queue.Count > 0 && previous[target] < 0)
        {
            int cell = queue.Dequeue(), x = cell % columns, z = cell / columns;
            for (int direction = 0; direction < 4; direction++)
            {
                int nx = x + (direction == 0 ? -1 : direction == 1 ? 1 : 0);
                int nz = z + (direction == 2 ? -1 : direction == 3 ? 1 : 0);
                if (nx < 0 || nx >= columns || nz < 0 || nz >= rows) continue;
                int neighbor = nz * columns + nx;
                if (previous[neighbor] >= 0 || !lane.IsWalkable((nx + .5f) * lane.cellSize, (nz + .5f) * lane.cellSize)) continue;
                previous[neighbor] = cell;
                queue.Enqueue(neighbor);
            }
        }
        if (previous[target] < 0) return false;
        List<Vector3> cellPath = new List<Vector3>();
        for (int cell = target; cell != origin; cell = previous[cell])
            cellPath.Add(new Vector3((cell % columns + .5f) * lane.cellSize, .015f,
                (cell / columns + .5f) * lane.cellSize));
        cellPath.Reverse();

        // String-pull the grid route: keep only the furthest reachable waypoint
        // in a clear walkable segment, eliminating tile-by-tile steering.
        route.Clear();
        Vector3 cursor = start;
        int pathIndex = 0;
        while (pathIndex < cellPath.Count)
        {
            int furthest = pathIndex;
            for (int candidate = cellPath.Count - 1; candidate >= pathIndex; candidate--)
                if (SegmentIsWalkable(cursor, cellPath[candidate])) { furthest = candidate; break; }
            route.Add(lane.transform.TransformPoint(cellPath[furthest]));
            cursor = cellPath[furthest];
            pathIndex = furthest + 1;
        }
        if (!SegmentIsWalkable(cursor, end)) route.Add(lane.transform.TransformPoint(new Vector3(end.x, .015f, end.z)));
        routeIndex = 0;
        return true;
    }

    bool SegmentIsWalkable(Vector3 from, Vector3 to)
    {
        Vector2 a = new Vector2(from.x, from.z);
        Vector2 b = new Vector2(to.x, to.z);
        float distance = Vector2.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(.1f, lane.cellSize * .2f)));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = Vector2.Lerp(a, b, i / (float)steps);
            if (!lane.IsWalkable(point.x, point.y)) return false;
        }
        return true;
    }

    void SetupGroundEffects()
    {
        if (!selectionRing)
        {
            selectionRing = GameplayVisuals.CreateGroundRing("SelectionRing", .045f);
            selectionRing.SetParent(transform, false);
            selectionRing.localPosition = new Vector3(0f, .025f, 0f);
            selectionRing.localScale = new Vector3(selectionRingSize, 1f, selectionRingSize);
        }
        if (!destinationMarker)
        {
            destinationMarker = GameplayVisuals.CreateGroundRing("DestinationMarker", .12f);
            if (transform.parent) destinationMarker.SetParent(transform.parent, true);
            destinationMarker.localScale = new Vector3(.42f, 1f, .42f);
            destinationMarker.gameObject.SetActive(false);
        }
    }

}
