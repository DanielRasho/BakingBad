using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class KitchenOrderPrepUIController : MonoBehaviour
{
    [Serializable]
    public class OrderDefinition
    {
        public string id;
        public string cellNumber;
        public string payout;
        public string orderTitle;
        public string cakeColor;
        public string topping;
        public string itemLabel;
        public string spriteId;
    }

    [Serializable]
    private class OrderDefinitionCollection
    {
        public OrderDefinition[] orders;
    }

    private enum OrderRuntimeState
    {
        Pending,
        ActivePrep
    }

    private sealed class RuntimeOrder
    {
        public int RuntimeIndex;
        public OrderDefinition Data;
        public OrderRuntimeState State;
        public OrderCardView CardView;
    }

    [Header("Root")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button prepareButton;
    [SerializeField] private PrisonCookPlayerController playerController;

    [Header("Order Board")]
    [SerializeField] private RectTransform cardsArea;
    [SerializeField] private string orderDataResourcePath = "Orders/KitchenCellOrders";
    [SerializeField] private string orderCardPrefabResourcePath = "Prefabs/OrderCard";
    [SerializeField] private float spawnIntervalSeconds = 10f;
    [SerializeField] private bool spawnFirstOrderImmediately = true;
    [SerializeField] private bool logOrderBoard;

    [Header("Tabs")]
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private Image[] tabBackgrounds;
    [SerializeField] private GameObject[] tabPanels;
    [SerializeField] private Text centerStageTitle;

    [Header("Detail Panel")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private RectTransform detailDropTarget;
    [SerializeField] private Text detailTitleText;
    [SerializeField] private Text detailCakeColorText;
    [SerializeField] private Text detailToppingText;
    [SerializeField] private Text detailItemLabelText;
    [SerializeField] private Text detailPayoutText;
    [SerializeField] private Text detailCellText;

    [Header("Board Summary")]
    [SerializeField] private Text totalMoneyText;

    [Header("Visuals")]
    [SerializeField] private Color tabIdleColor = new Color(0.97f, 0.96f, 0.93f, 1f);
    [SerializeField] private Color tabLockedColor = new Color(0.85f, 0.84f, 0.80f, 1f);
    [SerializeField] private Color tabActiveColor = new Color(1f, 0.92f, 0.76f, 1f);

    private readonly string[] tabTitles =
    {
        "Seleccionar tamano",
        "Colocar objeto",
        "Hornear",
        "Topping"
    };

    private readonly List<RuntimeOrder> allOrders = new List<RuntimeOrder>();
    private readonly List<RuntimeOrder> spawnedOrders = new List<RuntimeOrder>();

    private RectTransform cardsContentRoot;
    private Canvas rootCanvas;
    private OrderCardView orderCardPrefab;
    private RuntimeOrder selectedOrder;
    private RuntimeOrder activePrepOrder;
    private Coroutine spawnRoutine;
    private int activeTabIndex;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool isInitialized;
    private bool playerLockHeld;

    private const float OrderCardWidth = 86f;
    private const float OrderCardHeight = 114f;
    private const float OrderCardSpacing = 14f;
    private const float OrderCardStartX = 18f;
    private const float OrderCardStartY = -8f;

    public bool IsOpen
    {
        get { return menuRoot != null && menuRoot.activeSelf; }
    }

    private void Awake()
    {
        InitializeUi();
    }

    private void Start()
    {
        InitializeUi();
    }

    private void OnDisable()
    {
        SetPlayerInputLocked(false);
    }

    public void Open()
    {
        InitializeUi();
        EnsurePlayerController();
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SetPlayerInputLocked(true);

        SetMenuVisible(true);
        SetOrderBoardInteractable(true);

        if (activePrepOrder != null)
        {
            SetSelectedOrder(activePrepOrder, true);
            SetTabsUnlocked(true);
        }
        else
        {
            if (selectedOrder == null || !spawnedOrders.Contains(selectedOrder))
            {
                SetSelectedOrder(spawnedOrders.Count > 0 ? spawnedOrders[0] : null, false);
            }
            else
            {
                RefreshDetailPanel();
            }

            SetTabsUnlocked(false);
        }

        ActivateTab(0, true);
    }

    public void Close()
    {
        InitializeUi();
        SetMenuVisible(false);
        SetOrderBoardInteractable(false);
        SetPlayerInputLocked(false);
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
    }

    public void RefreshBindings()
    {
        isInitialized = false;
        InitializeUi();
    }

    public void HandleOrderCardClicked(OrderCardView cardView)
    {
        RuntimeOrder order = FindOrderByView(cardView);
        if (order == null || !IsOpen)
        {
            return;
        }

        SetSelectedOrder(order, activePrepOrder != null && order == activePrepOrder);
    }

    public void HandleOrderCardDropped(OrderCardView cardView)
    {
        RuntimeOrder order = FindOrderByView(cardView);
        if (order == null || !IsOpen)
        {
            return;
        }

        SetSelectedOrder(order, activePrepOrder != null && order == activePrepOrder);
    }

    public bool IsPointerOverDetailDropTarget(Vector2 screenPosition)
    {
        if (!IsOpen || detailDropTarget == null)
        {
            return false;
        }

        Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(detailDropTarget, screenPosition, eventCamera);
    }

    private void InitializeUi()
    {
        if (isInitialized)
        {
            return;
        }

        EnsureReferences();
        LoadOrderCardPrefab();
        BuildCardsViewport();
        WireButtons();
        SetMenuVisible(false);
        SetOrderBoardInteractable(false);
        LoadOrdersFromJson();
        SyncBoardSummary();
        SetSelectedOrder(null, false);
        SetTabsUnlocked(false);
        ActivateTab(0, true);
        StartSpawnRoutine();
        isInitialized = true;
    }

    private void EnsureReferences()
    {
        if (rootCanvas == null)
        {
            rootCanvas = GetComponent<Canvas>();
            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>();
            }
        }

        if (cardsArea == null)
        {
            Transform found = transform.Find("OrdersBoard/OrdersHeader/CardsArea");
            if (found == null)
            {
                found = FindDeepChild(transform, "CardsArea");
            }

            cardsArea = found as RectTransform;
        }

        if (detailPanel == null)
        {
            Transform found = FindDeepChild(transform, "DetailPanel");
            if (found != null)
            {
                detailPanel = found.gameObject;
            }
        }

        if (detailDropTarget == null && detailPanel != null)
        {
            detailDropTarget = detailPanel.GetComponent<RectTransform>();
        }

        if (totalMoneyText == null)
        {
            Transform found = FindDeepChild(transform, "MoneyRowText");
            if (found != null)
            {
                totalMoneyText = found.GetComponent<Text>();
            }
        }
    }

    private void LoadOrderCardPrefab()
    {
        if (orderCardPrefab != null)
        {
            return;
        }

        orderCardPrefab = Resources.Load<OrderCardView>(orderCardPrefabResourcePath);
        if (orderCardPrefab == null)
        {
            Debug.LogError("KitchenOrderPrepUIController could not load OrderCard prefab from Resources/" + orderCardPrefabResourcePath);
        }
    }

    private void BuildCardsViewport()
    {
        if (cardsArea == null)
        {
            Debug.LogError("KitchenOrderPrepUIController requires a CardsArea reference.");
            return;
        }

        for (int i = cardsArea.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
            {
                Destroy(cardsArea.GetChild(i).gameObject);
            }
            else
            {
                DestroyImmediate(cardsArea.GetChild(i).gameObject);
            }
        }

        Image cardsAreaImage = cardsArea.GetComponent<Image>();
        if (cardsAreaImage == null)
        {
            cardsAreaImage = cardsArea.gameObject.AddComponent<Image>();
        }

        cardsAreaImage.color = new Color(1f, 1f, 1f, 0.001f);
        cardsAreaImage.raycastTarget = true;

        Mask mask = cardsArea.GetComponent<Mask>();
        if (mask != null)
        {
            Destroy(mask);
        }

        ScrollRect existingScrollRect = cardsArea.GetComponent<ScrollRect>();
        if (existingScrollRect != null)
        {
            Destroy(existingScrollRect);
        }

        LayoutElement existingLayoutElement = cardsArea.GetComponent<LayoutElement>();
        if (existingLayoutElement != null)
        {
            Destroy(existingLayoutElement);
        }

        GameObject contentObject = new GameObject("OrderCardsContent", typeof(RectTransform));
        cardsContentRoot = contentObject.GetComponent<RectTransform>();
        cardsContentRoot.SetParent(cardsArea, false);
        cardsContentRoot.anchorMin = new Vector2(0f, 1f);
        cardsContentRoot.anchorMax = new Vector2(1f, 1f);
        cardsContentRoot.pivot = new Vector2(0f, 1f);
        cardsContentRoot.anchoredPosition = Vector2.zero;
        cardsContentRoot.sizeDelta = Vector2.zero;

        if (logOrderBoard)
        {
            Debug.Log("Order board viewport built. CardsArea sizeDelta=" + cardsArea.sizeDelta + ", childCount=" + cardsArea.childCount);
        }
    }

    private void WireButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        if (prepareButton != null)
        {
            prepareButton.onClick.RemoveAllListeners();
            prepareButton.onClick.AddListener(PrepareSelectedOrder);
        }

        for (int i = 0; i < tabButtons.Length; i++)
        {
            Button button = tabButtons[i];
            if (button == null)
            {
                continue;
            }

            int capturedIndex = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ActivateTab(capturedIndex, false));
        }
    }

    private void LoadOrdersFromJson()
    {
        allOrders.Clear();
        spawnedOrders.Clear();
        selectedOrder = null;
        activePrepOrder = null;

        TextAsset ordersJson = Resources.Load<TextAsset>(orderDataResourcePath);
        if (ordersJson == null)
        {
            Debug.LogError("KitchenOrderPrepUIController could not load order data from Resources/" + orderDataResourcePath);
            return;
        }

        OrderDefinitionCollection collection = JsonUtility.FromJson<OrderDefinitionCollection>(ordersJson.text);
        if (collection == null || collection.orders == null)
        {
            Debug.LogError("KitchenOrderPrepUIController failed to parse order data.");
            return;
        }

        for (int i = 0; i < collection.orders.Length; i++)
        {
            if (collection.orders[i] == null)
            {
                continue;
            }

            RuntimeOrder runtimeOrder = new RuntimeOrder
            {
                RuntimeIndex = i,
                Data = collection.orders[i],
                State = OrderRuntimeState.Pending
            };

            allOrders.Add(runtimeOrder);
        }

        if (logOrderBoard)
        {
            Debug.Log("Loaded " + allOrders.Count + " orders from JSON.");
        }
    }

    private void StartSpawnRoutine()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
        }

        spawnRoutine = StartCoroutine(SpawnOrdersCoroutine());
    }

    private IEnumerator SpawnOrdersCoroutine()
    {
        if (spawnFirstOrderImmediately)
        {
            SpawnNextOrder();
        }

        while (spawnedOrders.Count < allOrders.Count)
        {
            float waitTime = Mathf.Max(0.1f, spawnIntervalSeconds);
            yield return new WaitForSeconds(waitTime);
            SpawnNextOrder();
        }

        spawnRoutine = null;
    }

    private void SpawnNextOrder()
    {
        if (orderCardPrefab == null || cardsContentRoot == null || spawnedOrders.Count >= allOrders.Count)
        {
            return;
        }

        RuntimeOrder runtimeOrder = allOrders[spawnedOrders.Count];
        OrderCardView cardView = Instantiate(orderCardPrefab, cardsContentRoot);
        string cardNameSuffix = string.IsNullOrEmpty(runtimeOrder.Data.cellNumber)
            ? runtimeOrder.RuntimeIndex.ToString()
            : runtimeOrder.Data.cellNumber;
        cardView.name = "OrderCard_" + cardNameSuffix;
        ConfigureCardTransform(cardView.GetComponent<RectTransform>(), spawnedOrders.Count);
        cardView.Initialize(this, runtimeOrder.Data);
        cardView.SetInteractable(IsOpen);
        runtimeOrder.CardView = cardView;
        spawnedOrders.Add(runtimeOrder);

        if (logOrderBoard)
        {
            Debug.Log("Spawned order " + runtimeOrder.RuntimeIndex + " for cell " + runtimeOrder.Data.cellNumber + ". Visible cards=" + spawnedOrders.Count);
        }

        if (selectedOrder == null && IsOpen && activePrepOrder == null)
        {
            SetSelectedOrder(runtimeOrder, false);
        }
        else
        {
            RefreshCardStates();
        }
    }

    private void ConfigureCardTransform(RectTransform cardRect, int orderIndex)
    {
        if (cardRect == null)
        {
            return;
        }

        cardRect.anchorMin = new Vector2(0f, 1f);
        cardRect.anchorMax = new Vector2(0f, 1f);
        cardRect.pivot = new Vector2(0f, 1f);
        cardRect.sizeDelta = new Vector2(OrderCardWidth, OrderCardHeight);
        cardRect.anchoredPosition = new Vector2(
            OrderCardStartX + (OrderCardWidth + OrderCardSpacing) * orderIndex,
            OrderCardStartY);
        cardRect.localScale = Vector3.one;
    }

    private RuntimeOrder FindOrderByView(OrderCardView cardView)
    {
        if (cardView == null)
        {
            return null;
        }

        for (int i = 0; i < spawnedOrders.Count; i++)
        {
            if (spawnedOrders[i].CardView == cardView)
            {
                return spawnedOrders[i];
            }
        }

        return null;
    }

    private void PrepareSelectedOrder()
    {
        if (selectedOrder == null)
        {
            return;
        }

        if (activePrepOrder != null && activePrepOrder != selectedOrder)
        {
            return;
        }

        activePrepOrder = selectedOrder;
        activePrepOrder.State = OrderRuntimeState.ActivePrep;
        SetTabsUnlocked(true);
        ActivateTab(activeTabIndex, true);
        RefreshCardStates();
        RefreshDetailPanel();
    }

    private void SetSelectedOrder(RuntimeOrder order, bool forceUnlockedTabs)
    {
        selectedOrder = order;
        RefreshCardStates();
        RefreshDetailPanel();

        if (activePrepOrder == null && !forceUnlockedTabs)
        {
            SetTabsUnlocked(false);
        }
        else
        {
            SetTabsUnlocked(true);
        }
    }

    private void RefreshCardStates()
    {
        for (int i = 0; i < spawnedOrders.Count; i++)
        {
            RuntimeOrder runtimeOrder = spawnedOrders[i];
            if (runtimeOrder.CardView == null)
            {
                continue;
            }

            bool isSelected = runtimeOrder == selectedOrder;
            bool isActive = runtimeOrder == activePrepOrder || runtimeOrder.State == OrderRuntimeState.ActivePrep;
            runtimeOrder.CardView.SetSelected(isSelected);
            runtimeOrder.CardView.SetActiveState(isActive);
            runtimeOrder.CardView.SetInteractable(IsOpen);
        }

        if (prepareButton != null)
        {
            bool canPrepare = selectedOrder != null && (activePrepOrder == null || activePrepOrder == selectedOrder);
            prepareButton.interactable = canPrepare;
        }
    }

    private void RefreshDetailPanel()
    {
        if (detailPanel != null)
        {
            detailPanel.SetActive(IsOpen);
        }

        if (selectedOrder == null)
        {
            if (detailTitleText != null) detailTitleText.text = "Arrastra una orden aqui";
            if (detailCakeColorText != null) detailCakeColorText.text = "Color de pastel: --";
            if (detailToppingText != null) detailToppingText.text = "Topping: --";
            if (detailItemLabelText != null) detailItemLabelText.text = "Objeto a colocar: --";
            if (detailPayoutText != null) detailPayoutText.text = "Paga: Q0";
            if (detailCellText != null) detailCellText.text = "Celda: --";
            return;
        }

        OrderDefinition order = selectedOrder.Data;
        if (detailTitleText != null) detailTitleText.text = order.orderTitle;
        if (detailCakeColorText != null) detailCakeColorText.text = order.cakeColor;
        if (detailToppingText != null) detailToppingText.text = order.topping;
        if (detailItemLabelText != null) detailItemLabelText.text = order.itemLabel;
        if (detailPayoutText != null) detailPayoutText.text = "Paga: " + order.payout;
        if (detailCellText != null) detailCellText.text = "Celda: " + order.cellNumber;
    }

    private void SetTabsUnlocked(bool unlocked)
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] != null)
            {
                tabButtons[i].interactable = unlocked;
            }

            if (tabBackgrounds[i] != null && i != activeTabIndex)
            {
                tabBackgrounds[i].color = unlocked ? tabIdleColor : tabLockedColor;
            }
        }
    }

    private void ActivateTab(int tabIndex, bool force)
    {
        if (tabPanels == null || tabPanels.Length == 0)
        {
            return;
        }

        activeTabIndex = Mathf.Clamp(tabIndex, 0, tabPanels.Length - 1);

        for (int i = 0; i < tabPanels.Length; i++)
        {
            if (tabPanels[i] != null)
            {
                tabPanels[i].SetActive(i == activeTabIndex);
            }

            if (tabBackgrounds != null && i < tabBackgrounds.Length && tabBackgrounds[i] != null)
            {
                bool tabsUnlocked = activePrepOrder != null;
                if (i == activeTabIndex)
                {
                    tabBackgrounds[i].color = tabsUnlocked ? tabActiveColor : tabLockedColor;
                }
                else
                {
                    tabBackgrounds[i].color = tabsUnlocked ? tabIdleColor : tabLockedColor;
                }
            }
        }

        if (centerStageTitle != null && activeTabIndex >= 0 && activeTabIndex < tabTitles.Length)
        {
            centerStageTitle.text = tabTitles[activeTabIndex];
        }
    }

    private void SetMenuVisible(bool visible)
    {
        if (menuRoot != null)
        {
            menuRoot.SetActive(visible);
        }

        RefreshDetailPanel();
    }

    private void SetOrderBoardInteractable(bool interactable)
    {
        for (int i = 0; i < spawnedOrders.Count; i++)
        {
            if (spawnedOrders[i].CardView != null)
            {
                spawnedOrders[i].CardView.SetInteractable(interactable);
            }
        }
    }

    private void SyncBoardSummary()
    {
        if (totalMoneyText == null)
        {
            return;
        }

        int total = 0;
        for (int i = 0; i < allOrders.Count; i++)
        {
            string payout = allOrders[i].Data != null ? allOrders[i].Data.payout : null;
            if (string.IsNullOrEmpty(payout))
            {
                continue;
            }

            string numeric = payout.Replace("Q", string.Empty).Trim();
            if (int.TryParse(numeric, out int value))
            {
                total += value;
            }
        }

        totalMoneyText.text = "Q" + total;
    }

    private void EnsurePlayerController()
    {
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PrisonCookPlayerController>();
        }
    }

    private void SetPlayerInputLocked(bool locked)
    {
        EnsurePlayerController();

        if (playerController != null)
        {
            if (locked && !playerLockHeld)
            {
                playerController.AddInputLock();
                playerLockHeld = true;
            }
            else if (!locked && playerLockHeld)
            {
                playerController.RemoveInputLock();
                playerLockHeld = false;
            }
        }
    }

    private static Transform FindDeepChild(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName)
            {
                return child;
            }

            Transform nested = FindDeepChild(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
