using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Button))]
public class OrderCardView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Structure")]
    [SerializeField] private Image cardBackground;
    [SerializeField] private Button cardButton;
    [SerializeField] private Text cellText;
    [SerializeField] private Text payoutText;
    [SerializeField] private Text statusText;
    [SerializeField] private Image spriteFrame;

    [Header("Visuals")]
    [SerializeField] private Color idleColor = new Color(0.98f, 0.97f, 0.94f, 1f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.95f, 0.82f, 1f);
    [SerializeField] private Color activeColor = new Color(1f, 0.87f, 0.55f, 1f);
    [SerializeField] private Color completedColor = new Color(0.83f, 0.94f, 0.82f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.84f, 0.84f, 0.84f, 1f);
    [SerializeField] private Color textIdleColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private Color textActiveColor = new Color(0.22f, 0.14f, 0.02f, 1f);

    private KitchenOrderPrepUIController controller;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas rootCanvas;
    private Transform originalParent;
    private int originalSiblingIndex;
    private Vector2 originalAnchoredPosition;
    private bool isSelected;
    private bool isActive;
    private bool isCompleted;
    private bool isInteractable;
    private bool isDragging;
    private bool isGeometryHovered;
    private bool isPressHeld;
    private bool startedPressOnCard;
    private string orderId;
    private Vector2 pressStartScreenPosition;
    private const float DragThresholdPixels = 8f;

    public string OrderId
    {
        get { return orderId; }
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (cardButton == null)
        {
            cardButton = GetComponent<Button>();
        }

        if (cardBackground == null)
        {
            cardBackground = GetComponent<Image>();
        }

        if (cellText == null)
        {
            Transform child = transform.Find("CellText");
            if (child != null) cellText = child.GetComponent<Text>();
        }

        if (payoutText == null)
        {
            Transform child = transform.Find("PayoutText");
            if (child != null) payoutText = child.GetComponent<Text>();
        }

        if (statusText == null)
        {
            Transform child = transform.Find("StatusText");
            if (child != null) statusText = child.GetComponent<Text>();
        }

        if (spriteFrame == null)
        {
            Transform child = transform.Find("SpriteFrame");
            if (child != null) spriteFrame = child.GetComponent<Image>();
        }

        rootCanvas = GetComponentInParent<Canvas>();
        ApplyVisuals();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (!isInteractable || Mouse.current == null || rectTransform == null)
        {
            return;
        }

        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Camera eventCamera = GetEventCamera();
        isGeometryHovered = RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera);

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            isPressHeld = true;
            startedPressOnCard = isGeometryHovered;
            pressStartScreenPosition = screenPosition;
        }

        if (isPressHeld && isDragging)
        {
            DragToScreenPosition(screenPosition);
        }
        else if (isPressHeld && startedPressOnCard)
        {
            float dragDistance = Vector2.Distance(pressStartScreenPosition, screenPosition);
            if (dragDistance >= DragThresholdPixels && !isDragging)
            {
                BeginDragInternal();
                DragToScreenPosition(screenPosition);
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            bool shouldClick = startedPressOnCard && !isDragging && isGeometryHovered;
            bool shouldDrop = isDragging;

            isPressHeld = false;
            startedPressOnCard = false;

            if (shouldDrop)
            {
                EndDragInternal(screenPosition);
            }
            else if (shouldClick && controller != null)
            {
                controller.HandleOrderCardClicked(this);
            }
        }
#endif
    }

    public void Initialize(KitchenOrderPrepUIController owner, KitchenOrderPrepUIController.OrderDefinition order)
    {
        controller = owner;
        orderId = order != null ? order.id : string.Empty;

        if (cellText != null)
        {
            cellText.text = order != null ? order.cellNumber : "--";
        }

        if (payoutText != null)
        {
            payoutText.text = order != null ? order.payout : "Q0";
        }

        if (statusText != null)
        {
            statusText.gameObject.SetActive(false);
            statusText.text = "Activa";
        }

        if (spriteFrame != null)
        {
            spriteFrame.color = Color.white;
        }

        ApplyVisuals();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        ApplyVisuals();
    }

    public void SetActiveState(bool active)
    {
        isActive = active;
        ApplyVisuals();
    }

    public void SetCompletedState(bool completed)
    {
        isCompleted = completed;
        ApplyVisuals();
    }

    public void SetStatus(string nextStatus, bool visible)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = nextStatus;
        statusText.gameObject.SetActive(visible);
    }

    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;

        if (cardButton != null)
        {
            cardButton.interactable = interactable;
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        ApplyVisuals();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isInteractable || isDragging || controller == null)
        {
            return;
        }

        controller.HandleOrderCardClicked(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isInteractable || controller == null)
        {
            return;
        }
        
        BeginDragInternal();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || rectTransform == null || rootCanvas == null)
        {
            return;
        }

        DragToScreenPosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        EndDragInternal(eventData.position);
    }

    private void ApplyVisuals()
    {
        if (cardBackground != null)
        {
            Color targetColor = idleColor;
            if (!isInteractable)
            {
                targetColor = disabledColor;
            }
            else if (isCompleted)
            {
                targetColor = completedColor;
            }
            else if (isActive)
            {
                targetColor = activeColor;
            }
            else if (isSelected)
            {
                targetColor = selectedColor;
            }

            cardBackground.color = targetColor;
        }

        Color textColor = (isActive || isCompleted) ? textActiveColor : textIdleColor;
        if (cellText != null) cellText.color = textColor;
        if (payoutText != null) payoutText.color = textColor;

        if (statusText != null)
        {
            if (!statusText.gameObject.activeSelf && (isActive || isCompleted))
            {
                statusText.gameObject.SetActive(true);
            }

            statusText.color = textActiveColor;
        }

        if (spriteFrame != null)
        {
            spriteFrame.color = isInteractable ? Color.white : new Color(0.93f, 0.93f, 0.93f, 1f);
        }
    }

    private void BeginDragInternal()
    {
        if (isDragging || rectTransform == null || controller == null)
        {
            return;
        }

        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        isDragging = true;
        originalParent = rectTransform.parent;
        originalSiblingIndex = rectTransform.GetSiblingIndex();
        originalAnchoredPosition = rectTransform.anchoredPosition;
        rectTransform.SetParent(rootCanvas.transform, true);
        rectTransform.SetAsLastSibling();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }

        ApplyVisuals();
    }

    private void DragToScreenPosition(Vector2 screenPosition)
    {
        if (rectTransform == null || rootCanvas == null)
        {
            return;
        }

        Vector2 localPoint;
        Camera eventCamera = GetEventCamera();
        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out localPoint))
        {
            rectTransform.localPosition = localPoint;
        }
    }

    private void EndDragInternal(Vector2 screenPosition)
    {
        bool droppedOnTarget = controller != null && controller.IsPointerOverDetailDropTarget(screenPosition);

        rectTransform.SetParent(originalParent, false);
        rectTransform.SetSiblingIndex(originalSiblingIndex);
        rectTransform.anchoredPosition = originalAnchoredPosition;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        isDragging = false;
        ApplyVisuals();

        if (droppedOnTarget && controller != null)
        {
            controller.HandleOrderCardDropped(this);
        }
    }

    private Camera GetEventCamera()
    {
        if (rootCanvas == null)
        {
            return null;
        }

        return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
    }
}
