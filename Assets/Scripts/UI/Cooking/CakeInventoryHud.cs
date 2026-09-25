using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Bottom hotbar that mirrors the player's CakeInventory: cake icon per slot, its cell underneath,
// and a short feedback line above the bar (deliveries, penalties, full inventory).
[DisallowMultipleComponent]
public class CakeInventoryHud : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite hotbarSprite;
    [SerializeField] private Sprite slotSprite;
    [SerializeField] private Sprite selectedSlotSprite;
    [SerializeField] private Font font;

    [Header("Layout")]
    [SerializeField] private Vector2 slotSize = new Vector2(96f, 104f);
    [SerializeField] private float slotSpacing = 14f;
    [SerializeField] private float bottomMargin = 16f;
    [SerializeField] private float messageSeconds = 3f;

    private CakeInventory inventory;
    private RectTransform root;
    private Image[] slotBackgrounds;
    private Image[] slotIcons;
    private Text[] slotCellTexts;
    private Text messageText;
    private Coroutine messageRoutine;
    private bool menuOpen;
    private float nextBindAttemptTime;

    private void Start()
    {
        Build();
        TryBindInventory();
        Refresh();
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.Changed -= Refresh;
        }
    }

    private void Update()
    {
        // The player lives in another additive scene, so keep looking until it has loaded.
        if (inventory == null && Time.unscaledTime >= nextBindAttemptTime)
        {
            nextBindAttemptTime = Time.unscaledTime + 0.5f;
            TryBindInventory();
        }
    }

    public void SetMenuOpen(bool open)
    {
        menuOpen = open;
        if (root != null)
        {
            root.gameObject.SetActive(!menuOpen);
        }
    }

    public void ShowMessage(string message)
    {
        if (messageText == null)
        {
            Build();
        }

        messageText.text = message;
        messageText.gameObject.SetActive(true);
        if (messageRoutine != null)
        {
            StopCoroutine(messageRoutine);
        }

        if (isActiveAndEnabled)
        {
            messageRoutine = StartCoroutine(HideMessageAfterDelay());
        }
    }

    private IEnumerator HideMessageAfterDelay()
    {
        yield return new WaitForSecondsRealtime(messageSeconds);
        messageText.gameObject.SetActive(false);
        messageRoutine = null;
    }

    private void TryBindInventory()
    {
        if (inventory != null)
        {
            return;
        }

        PrisonCookPlayerController player = FindAnyObjectByType<PrisonCookPlayerController>();
        if (player == null)
        {
            return;
        }

        inventory = player.Inventory;
        inventory.Changed += Refresh;
        Build();
        Refresh();
    }

    private void Build()
    {
        int slotCount = inventory != null ? inventory.SlotCount : 5;
        if (root != null && slotBackgrounds != null && slotBackgrounds.Length == slotCount)
        {
            return;
        }

        if (root != null)
        {
            Destroy(root.gameObject);
        }

        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        float barWidth = slotCount * slotSize.x + (slotCount - 1) * slotSpacing + 80f;
        float barHeight = slotSize.y + 58f;

        root = CreateRect("CakeInventoryHud", transform);
        // First sibling so pause and results overlays still draw on top of the hotbar.
        root.SetAsFirstSibling();
        root.anchorMin = new Vector2(0.5f, 0f);
        root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = new Vector2(0f, bottomMargin);
        root.sizeDelta = new Vector2(barWidth, barHeight);

        Image bar = root.gameObject.AddComponent<Image>();
        bar.sprite = hotbarSprite;
        bar.type = hotbarSprite != null && hotbarSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        // The hotbar art has very wide borders; shrinking them keeps the wood frame proportional at this size.
        bar.pixelsPerUnitMultiplier = 3f;
        bar.color = hotbarSprite != null ? Color.white : new Color(0.8f, 0.5f, 0.4f, 1f);
        bar.raycastTarget = false;

        slotBackgrounds = new Image[slotCount];
        slotIcons = new Image[slotCount];
        slotCellTexts = new Text[slotCount];
        float startX = -(slotCount - 1) * (slotSize.x + slotSpacing) * 0.5f;
        for (int i = 0; i < slotCount; i++)
        {
            RectTransform slot = CreateRect("Slot_" + (i + 1), root);
            slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 1f);
            slot.pivot = new Vector2(0.5f, 1f);
            slot.anchoredPosition = new Vector2(startX + i * (slotSize.x + slotSpacing), -16f);
            slot.sizeDelta = slotSize;
            slotBackgrounds[i] = slot.gameObject.AddComponent<Image>();
            slotBackgrounds[i].raycastTarget = false;

            RectTransform icon = CreateRect("CakeIcon", slot);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = slotSize * 0.72f;
            icon.anchoredPosition = new Vector2(0f, 2f);
            slotIcons[i] = icon.gameObject.AddComponent<Image>();
            slotIcons[i].preserveAspect = true;
            slotIcons[i].raycastTarget = false;

            Text keyText = CreateText("Key", slot, (i + 1).ToString(), 18, TextAnchor.UpperLeft, new Color(0.45f, 0.25f, 0.18f, 1f));
            keyText.rectTransform.anchorMin = Vector2.zero;
            keyText.rectTransform.anchorMax = Vector2.one;
            keyText.rectTransform.offsetMin = new Vector2(12f, 8f);
            keyText.rectTransform.offsetMax = new Vector2(-8f, -8f);

            slotCellTexts[i] = CreateText("Cell", root, string.Empty, 24, TextAnchor.MiddleCenter, new Color(1f, 0.97f, 0.9f, 1f));
            RectTransform cellRect = slotCellTexts[i].rectTransform;
            cellRect.anchorMin = cellRect.anchorMax = new Vector2(0.5f, 1f);
            cellRect.pivot = new Vector2(0.5f, 1f);
            cellRect.anchoredPosition = new Vector2(slot.anchoredPosition.x, -16f - slotSize.y - 2f);
            cellRect.sizeDelta = new Vector2(slotSize.x + slotSpacing, 30f);
            Outline cellOutline = slotCellTexts[i].gameObject.AddComponent<Outline>();
            cellOutline.effectColor = new Color(0.35f, 0.18f, 0.12f, 1f);
        }

        messageText = CreateText("Message", root, string.Empty, 28, TextAnchor.MiddleCenter, new Color(1f, 0.97f, 0.9f, 1f));
        RectTransform messageRect = messageText.rectTransform;
        messageRect.anchorMin = messageRect.anchorMax = new Vector2(0.5f, 1f);
        messageRect.pivot = new Vector2(0.5f, 0f);
        messageRect.anchoredPosition = new Vector2(0f, 8f);
        messageRect.sizeDelta = new Vector2(900f, 40f);
        Outline messageOutline = messageText.gameObject.AddComponent<Outline>();
        messageOutline.effectColor = new Color(0.35f, 0.18f, 0.12f, 1f);
        messageOutline.effectDistance = new Vector2(2f, -2f);
        messageText.gameObject.SetActive(false);

        root.gameObject.SetActive(!menuOpen);
    }

    private void Refresh()
    {
        if (slotBackgrounds == null)
        {
            return;
        }

        for (int i = 0; i < slotBackgrounds.Length; i++)
        {
            CakeInventoryItem item = inventory != null ? inventory.GetItem(i) : null;
            bool selected = inventory != null && inventory.SelectedIndex == i;

            slotBackgrounds[i].sprite = selected && selectedSlotSprite != null ? selectedSlotSprite : slotSprite;
            slotBackgrounds[i].color = slotBackgrounds[i].sprite != null
                ? Color.white
                : (selected ? new Color(1f, 0.98f, 0.9f, 1f) : new Color(0.63f, 0.31f, 0.24f, 1f));

            slotIcons[i].sprite = item != null ? item.icon : null;
            slotIcons[i].enabled = item != null && item.icon != null;
            slotCellTexts[i].text = item != null ? "#" + item.cellNumber : string.Empty;
        }
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        return rectTransform;
    }

    private Text CreateText(string objectName, Transform parent, string content, int fontSize, TextAnchor alignment, Color color)
    {
        RectTransform rectTransform = CreateRect(objectName, parent);
        rectTransform.gameObject.AddComponent<CanvasRenderer>();
        Text text = rectTransform.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.text = content;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }
}
