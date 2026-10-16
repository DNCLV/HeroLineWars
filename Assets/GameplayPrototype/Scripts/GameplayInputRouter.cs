using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>Unifies touch and editor mouse taps while keeping UI taps out of lane actions.</summary>
public sealed class GameplayInputRouter : MonoBehaviour
{
    public static event Action<Vector2> Tapped;
    public static event Action<Vector2> TargetingTap;
    public static Vector2 PointerPosition { get; private set; }
    public static bool HasPointerPosition { get; private set; }
    static bool currentTapConsumed;
    static readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    public static GameplayInputRouter EnsureInstance(Transform gameplayRoot = null)
    {
        GameplayInputRouter router = FindAnyObjectByType<GameplayInputRouter>();
        if (router) return router;
        GameObject routerObject = new GameObject("GameplayInputRouter");
        if (gameplayRoot) routerObject.transform.SetParent(gameplayRoot, false);
        return routerObject.AddComponent<GameplayInputRouter>();
    }

    void Update()
    {
        HasPointerPosition = TryReadPointer(out Vector2 pointer);
        if (HasPointerPosition) PointerPosition = pointer;

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            TouchControl touch = touchscreen.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                int id = touch.touchId.ReadValue();
                Vector2 position = touch.position.ReadValue();
                if (!IsOverUI(id, position)) DispatchTap(position);
                return;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Vector2 position = mouse.position.ReadValue();
            if (!IsOverUI(-1, position)) DispatchTap(position);
        }
    }

    static void DispatchTap(Vector2 position)
    {
        currentTapConsumed = false;
        TargetingTap?.Invoke(position);
        if (!currentTapConsumed) Tapped?.Invoke(position);
    }

    public static void ConsumeCurrentTap() { currentTapConsumed = true; }

    public static bool TryGetLanePoint(Vector2 screenPosition, LaneGenerator lane, out Vector3 worldPoint)
    {
        worldPoint = default;
        Camera camera = Camera.main;
        if (!camera) camera = FindAnyObjectByType<Camera>();
        if (!camera || !lane) return false;
        Ray ray = camera.ScreenPointToRay(screenPosition);
        Plane plane = new Plane(lane.transform.up, lane.transform.position);
        if (!plane.Raycast(ray, out float distance)) return false;
        Vector3 hit = ray.GetPoint(distance);
        Vector3 local = lane.transform.InverseTransformPoint(hit);
        if (local.x < 0f || local.x > lane.WidthWorld || local.z < 0f || local.z > lane.LengthWorld)
            return false;
        worldPoint = hit;
        return true;
    }

    public static bool TryGetPointerLanePoint(LaneGenerator lane, out Vector3 worldPoint)
    {
        worldPoint = default;
        return HasPointerPosition && TryGetLanePoint(PointerPosition, lane, out worldPoint);
    }

    static bool TryReadPointer(out Vector2 position)
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
        {
            position = touchscreen.primaryTouch.position.ReadValue();
            return true;
        }
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            position = mouse.position.ReadValue();
            return true;
        }
        position = default;
        return false;
    }

    static bool IsOverUI(int pointerId, Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (!eventSystem) return false;
        PointerEventData pointer = new PointerEventData(eventSystem)
        {
            pointerId = pointerId,
            position = screenPosition
        };
        uiRaycastResults.Clear();
        eventSystem.RaycastAll(pointer, uiRaycastResults);
        foreach (RaycastResult result in uiRaycastResults)
            if (result.module is GraphicRaycaster) return true;
        return false;
    }
}
