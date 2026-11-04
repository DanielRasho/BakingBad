using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Makes a UI element draggable in the topping minigame: sauce bottles, toppings and toppings already on the cake.
// It only reports the drag; whoever listens decides what moves and what happens on release.
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class ToppingDragHandle : MonoBehaviour
{
    private const float DragThresholdPixels = 8f;

    // Every handle polls the mouse itself, so only one owns a press: the one drawn on top among those under the cursor.
    private static ToppingDragHandle pressOwner;
    private static int pressOwnerFrame = -1;

    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private bool isInteractable = true;
    private bool isDragging;
    private Vector2 pressStartScreenPosition;
    private Vector2 lastScreenPosition;

    public event Action<Vector2> DragStarted;
    public event Action<Vector2> Dragged;
    public event Action<Vector2> DragEnded;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>();
    }

    private void OnDisable()
    {
        ReleasePress(lastScreenPosition);
    }

    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;
        if (!interactable)
        {
            ReleasePress(lastScreenPosition);
        }
    }

    private void Update()
    {
        Vector2 screenPosition;
        bool pressedThisFrame;
        bool held;
        if (!isInteractable || !ReadMouse(out screenPosition, out pressedThisFrame, out held))
        {
            return;
        }

        lastScreenPosition = screenPosition;
        if (pressedThisFrame && CanTakePressFrom(pressOwner) && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, GetEventCamera()))
        {
            pressOwner = this;
            pressOwnerFrame = Time.frameCount;
            pressStartScreenPosition = screenPosition;
        }

        if (pressOwner != this)
        {
            return;
        }

        if (!held)
        {
            ReleasePress(screenPosition);
            return;
        }

        if (!isDragging && Vector2.Distance(pressStartScreenPosition, screenPosition) >= DragThresholdPixels)
        {
            isDragging = true;
            if (DragStarted != null)
            {
                DragStarted.Invoke(screenPosition);
            }
        }

        if (isDragging && Dragged != null)
        {
            Dragged.Invoke(screenPosition);
        }
    }

    // A press from an earlier frame is stale; in the same frame the topmost handle wins.
    private bool CanTakePressFrom(ToppingDragHandle currentOwner)
    {
        return currentOwner == null
            || pressOwnerFrame != Time.frameCount
            || currentOwner.transform.parent != transform.parent
            || currentOwner.transform.GetSiblingIndex() < transform.GetSiblingIndex();
    }

    private void ReleasePress(Vector2 screenPosition)
    {
        if (pressOwner == this)
        {
            pressOwner = null;
        }

        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        if (DragEnded != null)
        {
            DragEnded.Invoke(screenPosition);
        }
    }

    private bool ReadMouse(out Vector2 screenPosition, out bool pressedThisFrame, out bool held)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null)
        {
            screenPosition = Vector2.zero;
            pressedThisFrame = held = false;
            return false;
        }

        screenPosition = Mouse.current.position.ReadValue();
        pressedThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
        held = Mouse.current.leftButton.isPressed;
        return true;
#else
        screenPosition = Input.mousePosition;
        pressedThisFrame = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
        return true;
#endif
    }

    private Camera GetEventCamera()
    {
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        return rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
    }
}
