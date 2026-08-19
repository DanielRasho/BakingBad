using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class ShapePlacementPieceView : MonoBehaviour
{
    [SerializeField] private RectTransform movementBounds;

    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private bool isInteractable = true;
    private bool isDragging;
    private bool pressHeld;
    private bool startedPressOnPiece;
    private Vector2 pressStartScreenPosition;

    private const float DragThresholdPixels = 8f;

    public event Action Changed;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>();
    }

    public void Initialize(RectTransform nextMovementBounds)
    {
        movementBounds = nextMovementBounds;
        rootCanvas = GetComponentInParent<Canvas>();
    }

    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;
        if (!interactable)
        {
            isDragging = false;
            pressHeld = false;
            startedPressOnPiece = false;
        }
    }

    public void RotateBy(float deltaDegrees)
    {
        if (!isInteractable || rectTransform == null)
        {
            return;
        }

        rectTransform.Rotate(0f, 0f, deltaDegrees);
        NotifyChanged();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (!isInteractable || rectTransform == null || Mouse.current == null)
        {
            return;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        bool geometryHovered = RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, GetEventCamera());

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            pressHeld = true;
            startedPressOnPiece = geometryHovered;
            pressStartScreenPosition = screenPosition;
        }

        if (pressHeld && startedPressOnPiece && !isDragging)
        {
            float distance = Vector2.Distance(pressStartScreenPosition, screenPosition);
            if (distance >= DragThresholdPixels)
            {
                isDragging = true;
            }
        }

        if (isDragging)
        {
            MoveToScreenPosition(screenPosition);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            pressHeld = false;
            startedPressOnPiece = false;
            isDragging = false;
        }
#endif
    }

    private void MoveToScreenPosition(Vector2 screenPosition)
    {
        if (movementBounds == null || rectTransform == null)
        {
            return;
        }

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(movementBounds, screenPosition, GetEventCamera(), out localPoint))
        {
            return;
        }

        Vector2 halfSize = rectTransform.rect.size * 0.5f;
        Rect boundsRect = movementBounds.rect;
        float clampedX = Mathf.Clamp(localPoint.x, boundsRect.xMin + halfSize.x, boundsRect.xMax - halfSize.x);
        float clampedY = Mathf.Clamp(localPoint.y, boundsRect.yMin + halfSize.y, boundsRect.yMax - halfSize.y);
        rectTransform.anchoredPosition = new Vector2(clampedX, clampedY);
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (Changed != null)
        {
            Changed.Invoke();
        }
    }

    private Camera GetEventCamera()
    {
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        if (rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return rootCanvas.worldCamera;
    }
}
