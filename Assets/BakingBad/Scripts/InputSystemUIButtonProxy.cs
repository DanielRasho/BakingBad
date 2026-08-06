using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(RectTransform))]
public class InputSystemUIButtonProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Visual State")]
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color geometryHoverColor = new Color(0.2f, 0.9f, 1f, 1f);
    [SerializeField] private Color eventHoverColor = new Color(0.2f, 1f, 0.45f, 1f);
    [SerializeField] private Color pressedColor = new Color(1f, 0.45f, 0.15f, 1f);

    private Button button;
    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private bool isGeometryHovered;
    private bool isEventHovered;
    private bool isPressed;

    private void Awake()
    {
        button = GetComponent<Button>();
        rectTransform = GetComponent<RectTransform>();

        if (targetGraphic == null)
        {
            targetGraphic = button.targetGraphic != null ? button.targetGraphic : GetComponent<Graphic>();
        }

        rootCanvas = GetComponentInParent<Canvas>();
        ApplyVisualState();
    }

    private void OnEnable()
    {
        ApplyVisualState();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null || button == null)
        {
            return;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Camera eventCamera = GetEventCamera();
        bool geometryHovered = RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera);

        if (geometryHovered != isGeometryHovered)
        {
            isGeometryHovered = geometryHovered;
            ApplyVisualState();
        }

        bool canFallbackClick = geometryHovered && !isEventHovered && button.interactable;
        if (canFallbackClick && Mouse.current.leftButton.wasPressedThisFrame)
        {
            isPressed = true;
            ApplyVisualState();
        }

        if (canFallbackClick && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isPressed = false;
            ApplyVisualState();
            button.onClick.Invoke();
        }

        if (!geometryHovered && isPressed && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isPressed = false;
            ApplyVisualState();
        }
#endif
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isEventHovered = true;
        ApplyVisualState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isEventHovered = false;
        isPressed = false;
        ApplyVisualState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        ApplyVisualState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        if (targetGraphic == null)
        {
            return;
        }

        if (!button || !button.interactable)
        {
            targetGraphic.color = normalColor * new Color(0.6f, 0.6f, 0.6f, 1f);
            return;
        }

        if (isPressed)
        {
            targetGraphic.color = pressedColor;
            return;
        }

        if (isEventHovered)
        {
            targetGraphic.color = eventHoverColor;
            return;
        }

        if (isGeometryHovered)
        {
            targetGraphic.color = geometryHoverColor;
            return;
        }

        targetGraphic.color = normalColor;
    }

    private Camera GetEventCamera()
    {
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        if (rootCanvas == null)
        {
            return null;
        }

        if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return rootCanvas.worldCamera;
    }
}
