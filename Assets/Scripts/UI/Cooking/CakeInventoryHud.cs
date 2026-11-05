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

    [Header("Hand")]
    [Tooltip("Square on the right showing what the player holds. Shown when the hand is empty (cell.png).")]
    [SerializeField] private Sprite handEmptySprite;
    [Tooltip("Shown when a cake is in hand; the cake icon is drawn on top (cell2.png).")]
    [SerializeField] private Sprite handFilledSprite;
    [SerializeField] private Vector2 handSize = new Vector2(112f, 112f);
    [SerializeField] private float handGap = 36f;

    [Header("Caught Banner")]
    [SerializeField] private Sprite caughtBannerSprite;
    [SerializeField] private Vector2 caughtBannerSize = new Vector2(1500f, 585f);
    [SerializeField] private float caughtBannerSeconds = 2f;

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
    private Image handBackground;
    private Image handIcon;
    private RectTransform bannerRect;
    private CanvasGroup bannerGroup;
    private Coroutine bannerRoutine;
    private Coroutine messageRoutine;
    private bool menuOpen;
    private float nextBindAttemptTime;

    private void Start()
    {
        Build();
        TryBindInventory();
        Refresh();
    }

    private void OnEnable()
    {
        CopEnemy.PlayerCaughtAny += ShowCaughtBanner;
    }

    private void OnDisable()
    {
        CopEnemy.PlayerCaughtAny -= ShowCaughtBanner;
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.Changed -= Refresh;
        }
    }

    /// <summary>Flashes the "Atrapado" banner in the middle of the screen.</summary>
    public void ShowCaughtBanner()
    {
        if (caughtBannerSprite == null)
        {
            return;
        }

        if (bannerRect == null)
        {
            bannerRect = CreateRect("CaughtBanner", transform);
            bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0.5f, 0.5f);
            bannerRect.pivot = new Vector2(0.5f, 0.5f);
            bannerRect.anchoredPosition = Vector2.zero;
            bannerRect.sizeDelta = caughtBannerSize;

            Image image = bannerRect.gameObject.AddComponent<Image>();
            image.sprite = caughtBannerSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            bannerGroup = bannerRect.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            bannerGroup.interactable = false;
        }

        // Always on top of the other HUD elements.
        bannerRect.SetAsLastSibling();

        if (bannerRoutine != null)
        {
            StopCoroutine(bannerRoutine);
        }

        if (isActiveAndEnabled)
        {
            bannerRoutine = StartCoroutine(BannerRoutine());
        }
    }

    private IEnumerator BannerRoutine()
    {
        const float popSeconds = 0.15f;
        const float fadeSeconds = 0.4f;

        bannerRect.gameObject.SetActive(true);
        float elapsed = 0f;

        while (elapsed < caughtBannerSeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            // Pops in slightly oversized, then settles; fades out at the end.
            float pop = Mathf.Clamp01(elapsed / popSeconds);
            bannerRect.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, pop);
            float remaining = caughtBannerSeconds - elapsed;
            bannerGroup.alpha = Mathf.Clamp01(Mathf.Min(pop, remaining / fadeSeconds));
            yield return null;
        }

        bannerRect.gameObject.SetActive(false);
        bannerRoutine = null;
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

        // Slots on the left, a gap, then the hand square on the right; the whole group is centered in the bar.
        float slotsWidth = slotCount * slotSize.x + (slotCount - 1) * slotSpacing;
        float contentWidth = slotsWidth + handGap + handSize.x;
        float leftEdge = -contentWidth * 0.5f;
        float barWidth = contentWidth + 80f;
        float barHeight = Mathf.Max(slotSize.y, handSize.y) + 58f;

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
        float startX = leftEdge + slotSize.x * 0.5f;
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

        // What the player holds right now (the selected slot).
        RectTransform hand = CreateRect("Hand", root);
        hand.anchorMin = hand.anchorMax = new Vector2(0.5f, 1f);
        hand.pivot = new Vector2(0.5f, 1f);
        hand.anchoredPosition = new Vector2(leftEdge + slotsWidth + handGap + handSize.x * 0.5f, -16f);
        hand.sizeDelta = handSize;
        handBackground = hand.gameObject.AddComponent<Image>();
        handBackground.preserveAspect = true;
        handBackground.raycastTarget = false;

        RectTransform handItem = CreateRect("HeldCake", hand);
        handItem.anchorMin = handItem.anchorMax = new Vector2(0.5f, 0.5f);
        handItem.sizeDelta = handSize * 0.62f;
        handItem.anchoredPosition = new Vector2(0f, 4f);
        handIcon = handItem.gameObject.AddComponent<Image>();
        handIcon.preserveAspect = true;
        handIcon.raycastTarget = false;

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

        RefreshHand();
    }

    private void RefreshHand()
    {
        if (handBackground == null)
        {
            return;
        }

        CakeInventoryItem held = inventory != null ? inventory.SelectedItem : null;
        Sprite background = held != null && handFilledSprite != null ? handFilledSprite : handEmptySprite;
        if (held != null && handFilledSprite == null)
        {
            // Without the filled art, the empty-hand image would show a hand under the cake.
            background = null;
        }

        handBackground.sprite = background;
        handBackground.color = background != null ? Color.white : new Color(1f, 0.93f, 0.8f, 1f);

        handIcon.sprite = held != null ? held.icon : null;
        handIcon.enabled = held != null && held.icon != null;
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
