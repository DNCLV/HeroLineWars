using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class HlwCameraController : MonoBehaviour
{
    public LaneGenerator lane;

    [Header("Tactical View")]
    [Tooltip("Downward angle measured up from the horizon.")]
    [Range(25f, 80f)] public float pitch = 40f;
    [Range(-180f, 180f)] public float yaw = 75f;
    [Min(10f)] public float cameraDistance = 36f;
    [Tooltip("Offset from the lane center in lane-local coordinates (X, height, Z).")]
    public Vector3 framingOffset = new Vector3(0f, 0f, -2.5f);

    [Header("Zoom")]
    [Min(4f)] public float orthographicSize = 12f;
    [Min(0.01f)] public float zoomSensitivity = 1f;
    [Min(0.01f)] public float zoomSmoothTime = 0.08f;
    [Min(0.1f)] public float minimumOrthographicSize = 4f;
    [Min(0.1f)] public float maximumOrthographicSize = 30f;

    [Header("Pan")]
    [Min(1f)] public float movementSpeed = 24f;
    public float boundaryPadding = 3f;
    [Tooltip("Recenter automatically when the generator dimensions change.")]
    public bool recenterWhenLaneChanges = true;
    [Min(.05f)] public float heroFocusDuration = .25f;

    Camera cam;
    float targetOrthographicSize;
    float zoomVelocity;
    int lastLength = -1;
    int lastWidth = -1;
    Vector3 focusPoint;
    bool hasFocus;
    Bounds mapBounds;
    bool hasMapBounds;
    Transform cachedFloor;
    Transform focusTransitionTarget;
    Vector3 focusTransitionStart;
    float focusTransitionElapsed;
    readonly List<LaneUnitCombat> focusCandidates = new List<LaneUnitCombat>(64);

    float MinimumZoom => Mathf.Min(minimumOrthographicSize, maximumOrthographicSize);
    float MaximumZoom => Mathf.Max(minimumOrthographicSize, maximumOrthographicSize);

    void Awake()
    {
        cam = GetComponent<Camera>();
        targetOrthographicSize = orthographicSize;
        CenterOnLane();
        Apply();
    }

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        targetOrthographicSize = orthographicSize;
        CenterOnLane();
        Apply();
    }

    void Start()
    {
        Apply();
        Vector3 forward = transform.forward.normalized;
        float effectivePitch = Mathf.Atan2(-forward.y,
            new Vector2(forward.x, forward.z).magnitude) * Mathf.Rad2Deg;
        float effectiveYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        Debug.Log($"[HLW Camera] forward={forward:F4}, effective pitch={effectivePitch:F1} degrees down, " +
                  $"yaw={effectiveYaw:F1}, projection={(cam.orthographic ? "Orthographic" : "Perspective")}, " +
                  $"size={cam.orthographicSize:F1}, distance={cameraDistance:F1}, focus={focusPoint:F2}.", this);
    }

    void OnValidate()
    {
        if (!cam) cam = GetComponent<Camera>();
        // Keep Inspector zoom edits from being pulled back toward an old wheel target.
        targetOrthographicSize = orthographicSize;
        zoomVelocity = 0f;
        Apply();
    }

    void Update()
    {
        if (!lane) return;
        if (recenterWhenLaneChanges &&
            (lastLength != lane.laneLength || lastWidth != lane.laneWidth ||
             cachedFloor != lane.transform.Find("LaneFloor")))
            CenterOnLane();

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame)
                FocusOnPlayerHero();

            Vector2 pan = new Vector2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) -
                (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f));
            if (pan.sqrMagnitude > 0f)
            {
                CancelFocusTransition();
                ScreenGroundAxes(out Vector3 right, out Vector3 up);
                focusPoint += (right * pan.x + up * pan.y).normalized * movementSpeed * Time.unscaledDeltaTime;
                ClampFocusPoint();
            }
        }

        if (Mouse.current != null)
        {
            float rawScroll = Mouse.current.scroll.ReadValue().y;
            // Windows commonly reports 120 per wheel notch; other platforms often report 1.
            float scrollSteps = Mathf.Abs(rawScroll) > 10f ? rawScroll / 120f : rawScroll;
            if (Mathf.Abs(scrollSteps) > 0.001f)
            {
                CancelFocusTransition();
                targetOrthographicSize = Mathf.Clamp(targetOrthographicSize - scrollSteps * zoomSensitivity,
                    MinimumZoom, MaximumZoom);
            }

            if (Mouse.current.middleButton.isPressed)
            {
                CancelFocusTransition();
                Vector2 drag = Mouse.current.delta.ReadValue();
                float worldPerPixel = 2f * orthographicSize / Mathf.Max(1, Screen.height);
                ScreenGroundAxes(out Vector3 screenRight, out Vector3 screenUp);
                focusPoint += (-screenRight * drag.x - screenUp * drag.y) * worldPerPixel;
                ClampFocusPoint();
            }
        }

        UpdateFocusTransition();

        orthographicSize = Mathf.SmoothDamp(orthographicSize, targetOrthographicSize, ref zoomVelocity,
            zoomSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        Apply();
    }

    public bool FocusOnPlayerHero()
    {
        HeroProgression player = HeroProgression.PlayerHero;
        if (player && player.Health && !player.Health.IsDead)
            return BeginFocusTransition(player.transform);
        HeroLaneController fallback = FindAnyObjectByType<HeroLaneController>();
        return fallback && BeginFocusTransition(fallback.transform);
    }

    public bool FocusOnEnemyHero()
    {
        UnitCombatRegistry.CopyActiveUnits(focusCandidates);
        for (int i = 0; i < focusCandidates.Count; i++)
        {
            LaneUnitCombat unit = focusCandidates[i];
            if (unit && !unit.IsDead && unit.Faction && unit.Faction.team == UnitTeam.Enemy &&
                unit.Faction.archetype == UnitArchetype.Battlemage)
                return BeginFocusTransition(unit.transform);
        }
        return false;
    }

    public void CancelFocusTransition()
    {
        focusTransitionTarget = null;
        focusTransitionElapsed = 0f;
    }

    bool BeginFocusTransition(Transform target)
    {
        if (!target || !lane) return false;
        focusTransitionTarget = target;
        focusTransitionStart = focusPoint;
        focusTransitionElapsed = 0f;
        hasFocus = true;
        return true;
    }

    void UpdateFocusTransition()
    {
        if (!focusTransitionTarget) return;
        GoblinHealth health = focusTransitionTarget.GetComponent<GoblinHealth>();
        if (health && health.IsDead)
        {
            CancelFocusTransition();
            return;
        }
        focusTransitionElapsed += Time.unscaledDeltaTime;
        float duration = Mathf.Max(.05f, heroFocusDuration);
        float progress = Mathf.Clamp01(focusTransitionElapsed / duration);
        float smoothProgress = progress * progress * (3f - 2f * progress);
        Vector3 destination = focusTransitionTarget.position;
        destination.y = lane.transform.position.y + framingOffset.y;
        focusPoint = Vector3.Lerp(focusTransitionStart, destination, smoothProgress);
        ClampFocusPoint();
        if (progress >= 1f) CancelFocusTransition();
    }

    void Apply()
    {
        if (!cam) cam = GetComponent<Camera>();
        if (!cam) return;

        targetOrthographicSize = Mathf.Clamp(targetOrthographicSize, MinimumZoom, MaximumZoom);
        orthographicSize = Mathf.Clamp(orthographicSize, MinimumZoom, MaximumZoom);
        cam.orthographic = true;
        cam.orthographicSize = orthographicSize;

        Quaternion rotation = ViewRotation();
        transform.rotation = rotation;
        if (lane && hasFocus)
        {
            ClampFocusPoint();
            // Keep the lane ground point at the center of the orthographic view. Distance
            // controls near/far placement while the orthographic size controls the visible area.
            transform.position = focusPoint - rotation * Vector3.forward * Mathf.Max(10f, cameraDistance);
        }
    }

    // Unity camera local +Z points forward. X rotation directly matches a downward
    // pitch from the horizontal plane: 42 means the forward vector is 42 degrees down.
    Quaternion ViewRotation() => Quaternion.Euler(pitch, yaw, 0f);

    void ScreenGroundAxes(out Vector3 right, out Vector3 up)
    {
        Quaternion rotation = ViewRotation();
        Vector3 groundNormal = lane ? lane.transform.up : Vector3.up;
        right = Vector3.ProjectOnPlane(rotation * Vector3.right, groundNormal).normalized;
        up = Vector3.ProjectOnPlane(rotation * Vector3.up, groundNormal).normalized;
    }

    void RefreshMapBounds()
    {
        hasMapBounds = false;
        cachedFloor = lane ? lane.transform.Find("LaneFloor") : null;
        if (!cachedFloor) return;
        foreach (Renderer renderer in cachedFloor.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled) continue;
            if (!hasMapBounds) { mapBounds = renderer.bounds; hasMapBounds = true; }
            else mapBounds.Encapsulate(renderer.bounds);
        }
    }

    Vector3 LaneCenterWorld() => hasMapBounds
        ? new Vector3(mapBounds.center.x, lane.transform.position.y, mapBounds.center.z)
        : lane.transform.TransformPoint(new Vector3(lane.WidthWorld * .5f, 0f, lane.LengthWorld * .5f));

    void CenterOnLane()
    {
        if (!lane) return;
        RefreshMapBounds();
        focusPoint = LaneCenterWorld() + lane.transform.TransformVector(framingOffset);
        hasFocus = true;
        lastLength = lane.laneLength;
        lastWidth = lane.laneWidth;
        ClampFocusPoint();
        transform.rotation = ViewRotation();
        transform.position = focusPoint - transform.forward * Mathf.Max(10f, cameraDistance);
    }

    void ClampFocusPoint()
    {
        if (!lane || !hasFocus) return;
        if (hasMapBounds)
        {
            focusPoint.x = Mathf.Clamp(focusPoint.x, mapBounds.min.x - boundaryPadding, mapBounds.max.x + boundaryPadding);
            focusPoint.z = Mathf.Clamp(focusPoint.z, mapBounds.min.z - boundaryPadding, mapBounds.max.z + boundaryPadding);
            focusPoint.y = lane.transform.position.y + framingOffset.y;
            return;
        }
        Vector3 local = lane.transform.InverseTransformPoint(focusPoint);
        local.x = Mathf.Clamp(local.x, -boundaryPadding, lane.WidthWorld + boundaryPadding);
        local.z = Mathf.Clamp(local.z, -boundaryPadding, lane.LengthWorld + boundaryPadding);
        local.y = framingOffset.y;
        focusPoint = lane.transform.TransformPoint(local);
    }
}
