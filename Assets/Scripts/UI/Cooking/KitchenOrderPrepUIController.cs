using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class KitchenOrderPrepUIController : MonoBehaviour
{
    [Serializable]
    public class OrderDefinition
    {
        public string id;
        public string cellNumber;
        public string payout;
        public int basePayout;
        public string orderTitle;
        public string cakeFlavor;
        public string topping;
        public string contrabandItem;
        public string cakeSize;
        public string shapeType;
        public string spriteId;
    }

    [Serializable]
    public class NamedIconEntry
    {
        public string itemName;
        public Sprite icon;
    }

    [Serializable]
    public class CakeFlavorEntry
    {
        public string flavorName;
        [Tooltip("Cake sprites for Small, Medium and Large, in that order.")]
        public Sprite[] sizeSprites;
    }

    [Serializable]
    private class OrderDefinitionCollection
    {
        public OrderDefinition[] orders;
    }

    private enum OrderRuntimeState
    {
        Pending,
        ActivePrep,
        Completed
    }

    private enum CakeSizeOption
    {
        None,
        Small,
        Medium,
        Large
    }

    private enum SizeGameType
    {
        CorrectMold,
        SortCakes,
        FillScale
    }

    private enum PlacementHoleShape
    {
        Circle,
        Star,
        Hexagon,
        Flower,
        Blob,
        Diamond
    }

    private enum PlacementIcon
    {
        RotateLeft,
        RotateRight,
        ZoomIn,
        ZoomOut,
        ArrowLeft,
        ArrowRight
    }

    private enum BakeResult
    {
        None,
        Undercooked,
        Perfect,
        Burnt
    }

    private sealed class RuntimeOrder
    {
        public int RuntimeIndex;
        public OrderDefinition Data;
        public OrderRuntimeState State;
        public OrderCardView CardView;
        public CakeSizeOption RequiredCakeSize;
        public PlacementHoleShape HoleShape;
        public float HoleRotationDegrees;
        public int BasePayoutValue;
        public Vector2 TargetAnchoredPosition;
        public float TargetRotationDegrees;
        public float TargetScale;
        public float PieceScale = 1f;
        public string SelectedFlavor;
        public bool FlavorMatched;
        public int FlavorCarouselIndex;
        public CakeSizeOption SelectedCakeSize = CakeSizeOption.None;
        public SizeGameType SizeGame;
        public bool SizeGameAssigned;
        public bool SizeGameCompleted;
        public bool SizeMatched;
        public bool PlacementCorrect;
        public bool PlacementCompleted;
        public float PlacementScore;
        public Vector2 PieceAnchoredPosition;
        public float PieceRotationDegrees;
        public bool PiecePoseInitialized;
        public bool BakeInProgress;
        public float BakeProgressNormalized;
        public float BakeDirection = 1f;
        public BakeResult BakeOutcome = BakeResult.None;
        public BakeResult LastBakeMiss = BakeResult.None;
        public float PerfectBakeWindowStart;
        public float PerfectBakeWindowEnd;
        public string SelectedTopping;
        public bool ToppingMatched;
        public bool CakeGenerated;
        public int FinalPayout;
        public string[] ToppingOptions;
    }

    [Header("Root")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private PrisonCookPlayerController playerController;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Order Board")]
    [SerializeField] private RectTransform cardsArea;
    [SerializeField] private TextAsset ordersJson;
    [SerializeField] private GameObject orderCardPrefabResourcePath;
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
    [SerializeField] private Button prepareButton;
    [SerializeField] private Text detailTitleText;
    [SerializeField] private Text detailCakeColorText;
    [SerializeField] private Text detailToppingText;
    [SerializeField] private Text detailItemLabelText;
    [SerializeField] private Text detailPayoutText;
    [SerializeField] private Text detailCellText;
    [SerializeField] private GameObject dragHintOverlay;
    [SerializeField] private RectTransform dragHintPointer;
    [Tooltip("Icon shown in the recipe card for each contrabandItem name in the orders JSON.")]
    [SerializeField] private NamedIconEntry[] contrabandIcons;
    [Tooltip("Cake slice shown in the topping options and the inventory for each topping name.")]
    [SerializeField] private NamedIconEntry[] toppingCakeIcons;
    [SerializeField] private Sprite toppingSlotSprite;
    [SerializeField] private Sprite toppingSelectedSlotSprite;
    [Tooltip("Container shown as the drop target in the mold minigame.")]
    [SerializeField] private Sprite preparationContainerSprite;
    [Tooltip("Cake sprites for Small, Medium and Large, in that order. Used until a flavor is chosen.")]
    [SerializeField] private Sprite[] cakeSizeSprites;
    [Tooltip("Flavors shown in the flavor carousel; flavorName must match cakeFlavor in the orders JSON.")]
    [SerializeField] private CakeFlavorEntry[] cakeFlavors;
    [SerializeField] private CakeInventoryHud inventoryHud;

    [Header("Payout Penalties (fraction of base payout)")]
    [SerializeField, Range(0f, 1f)] private float wrongFlavorPenalty = 0.2f;
    [SerializeField, Range(0f, 1f)] private float wrongSizePenalty = 0.2f;
    [SerializeField, Range(0f, 1f)] private float badPlacementPenalty = 0.2f;
    [SerializeField, Range(0f, 1f)] private float wrongToppingPenalty = 0.2f;

    [Header("Placement Buttons")]
    [SerializeField] private Sprite placementIconButtonSprite;
    [SerializeField] private Sprite placementConfirmButtonSprite;

    [Header("Oven")]
    [SerializeField] private Sprite ovenOffSprite;
    [SerializeField] private Sprite ovenOnSprite;
    [SerializeField] private Sprite ovenOnButtonSprite;
    [SerializeField] private Sprite ovenOnButtonPressedSprite;
    [SerializeField] private Sprite ovenOffButtonSprite;
    [SerializeField] private Sprite ovenOffButtonPressedSprite;
    [SerializeField] private Sprite burntCakeSprite;

    [Header("Dialogs")]
    [Tooltip("Same art as the pause menu so every popup looks alike.")]
    [SerializeField] private Sprite dialogPanelSprite;
    [SerializeField] private Sprite dialogPrimaryButtonSprite;
    [SerializeField] private Sprite dialogSecondaryButtonSprite;
    [SerializeField] private Sprite dialogCloseButtonSprite;
    [SerializeField] private string gameplaySceneName = "MainMap";

    [Header("Board Summary")]
    [SerializeField] private Text totalMoneyText;
    [SerializeField] private Text timerText;

    [Header("Visuals")]
    [SerializeField] private Color tabIdleColor = new Color(0.97f, 0.96f, 0.93f, 1f);
    [SerializeField] private Color tabLockedColor = new Color(0.85f, 0.84f, 0.80f, 1f);
    [SerializeField] private Color tabActiveColor = new Color(1f, 0.92f, 0.76f, 1f);

    private const int FlavorTabIndex = 0;
    private const int SizeTabIndex = 1;
    private const int PlacementTabIndex = 2;
    private const int BakeTabIndex = 3;
    private const int ToppingTabIndex = 4;

    private readonly string[] tabTitles =
    {
        "Elegir sabor",
        "Seleccionar tamano",
        "Colocar objeto",
        "Hornear",
        "Topping"
    };

    private static readonly string[] CakeSizeLabels = { "Pequeno", "Mediano", "Grande" };
    private static readonly CakeSizeOption[] CakeSizeValues = { CakeSizeOption.Small, CakeSizeOption.Medium, CakeSizeOption.Large };
    private static readonly string[] FallbackToppings = { "Crema", "Chocolate", "Mora", "Azucar glass", "Fresa", "Glaseado", "Caramelo" };

    private readonly List<RuntimeOrder> allOrders = new List<RuntimeOrder>();
    private readonly List<RuntimeOrder> spawnedOrders = new List<RuntimeOrder>();

    private RectTransform cardsContentRoot;
    private Canvas rootCanvas;
    private Font defaultFont;
    private OrderCardView orderCardPrefab;
    private RuntimeOrder selectedOrder;
    private RuntimeOrder activePrepOrder;
    private Coroutine spawnRoutine;
    private Coroutine dragHintPulseRoutine;
    private int activeTabIndex;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool isInitialized;
    private bool playerLockHeld;
    private int displayedMoney;
    private int lastDisplayedTimerSeconds = -1;
    private GameObject resultsMenuRoot;
    private Text resultsTitleText;
    private Text resultsSubtitleText;
    private Text resultsStatsText;
    private Text resultsPrimaryLabel;
    private bool resultsGoalReached;
    private Text controlsPopupTitleText;
    private Text controlsPopupGoalText;
    private Text dayGoalHudText;
    private int currentDay = 1;
    private int dayGoal;
    private int perfectCakeCount;
    private int prepMistakeCount;
    private GameObject mapOverlayRoot;
    private RectTransform mapContentRoot;
    private GameObject controlsPopupRoot;
    private bool controlsPopupShown;
    private bool controlsPopupLockHeld;

    private Text detailStatusText;
    private RectTransform detailItemFrame;
    private Image detailItemIcon;
    private Text detailItemPlaceholderText;

    private GameObject stageTabPanelsRoot;
    private GameObject tabBarRoot;
    private Text waitingOrderText;
    private Text dragHintText;
    private string dragHintDefaultText;
    private const string WaitingOrderMessage = "Esperando orden para iniciar...";
    private const string InventoryFullMessage = "Inventario lleno\nEntrega un pastel antes de preparar otra orden.";

    private Image flavorCakeImage;
    private Text flavorNameText;
    private Image[] flavorPageDots;
    private Button flavorPreviousButton;
    private Button flavorNextButton;
    private Button flavorSelectButton;
    private Text flavorFeedbackText;

    private Button[] sizeOptionButtons;
    private Image[] sizeOptionBackgrounds;
    private Text sizeFeedbackText;
    private RectTransform sizeGameContent;
    private RectTransform sizeGameRoot;
    private RectTransform sizeSortArea;
    private RectTransform sizeMoldTarget;
    private RectTransform sizeScaleIndicator;
    private Button sizeScaleSelectButton;
    private Text sizeScaleSelectLabel;
    private float sizeScalePosition;
    private float sizeScaleDirection = 1f;
    private bool sizeScaleRunning;
    private readonly List<SizeGamePieceView> sizeGamePieces = new List<SizeGamePieceView>();
    private bool sizeSortValidated;
    private RuntimeOrder sizeGameBuiltFor;
    private static Sprite cakeSizeCylinderSprite;
    private static Sprite flavorDotSprite;

    private RectTransform placementPlayArea;
    private RectTransform placementHoleRect;
    private Image placementHoleImage;
    private RectTransform placementTargetRect;
    private Image placementShadowImage;
    private RectTransform placementPieceRect;
    private Image placementPieceImage;
    private ShapePlacementPieceView placementPieceView;
    private Text placementStatusText;
    private Text placementScoreText;
    private Button placementRotateLeftButton;
    private Button placementRotateRightButton;
    private Button placementScaleDownButton;
    private Button placementScaleUpButton;
    private Button placementConfirmButton;
    private static readonly Dictionary<PlacementHoleShape, Sprite> placementHoleSprites = new Dictionary<PlacementHoleShape, Sprite>();
    private static readonly Dictionary<PlacementIcon, Sprite> placementIconSprites = new Dictionary<PlacementIcon, Sprite>();

    private Image bakeOvenImage;
    private RectTransform bakeMarkerRect;
    private RectTransform bakePerfectZoneRect;
    private Image bakeRawCakeImage;
    private Text bakeStatusText;
    private Button bakeOffButton;
    private Button bakeOnButton;

    private Button[] toppingOptionButtons;
    private Image[] toppingOptionBackgrounds;
    private Text[] toppingOptionLabelTexts;
    private Image[] toppingOptionCakeImages;
    private Text toppingFeedbackText;

    private const float OrderCardWidth = 136f;
    private const float OrderCardHeight = 176f;
    private const float OrderCardSpacing = 14f;
    private const float OrderCardStartX = 18f;
    private const float OrderCardStartY = 0f;
    private const float PlacementRotationStep = 15f;
    private const float PlacementDistanceThreshold = 28f;
    private const float PlacementAngleThreshold = 18f;
    private const float PlacementScaleStep = 0.1f;
    private const float PlacementScaleMin = 0.5f;
    private const float PlacementScaleMax = 1.6f;
    private const float PlacementScaleThreshold = 0.06f;
    private const float PlacementPieceBoxSize = 140f;
    private const float PlacementHoleSizeFactor = 1.3f;
    private static readonly Vector2 PlacementPlayAreaSize = new Vector2(820f, 340f);
    private static readonly Vector2 PlacementPieceStartPosition = new Vector2(-270f, 0f);
    private static readonly float[] PlacementTargetScales = { 0.7f, 0.8f, 0.9f, 1.1f, 1.2f, 1.3f };
    // Fraction of the bar the marker crosses per second, back and forth.
    private const float BakeMarkerSpeed = 0.6f;
    // The oven art is laid out in its own pixels (ovenOff/ovenOn are 500x282) and scaled to the display size.
    private static readonly Vector2 OvenSourceSize = new Vector2(500f, 282f);
    private static readonly Vector2 OvenDisplaySize = new Vector2(760f, 429f);
    private const float PerfectBakeWindowWidth = 0.12f;
    private const float PerfectBakeWindowMinCenter = 0.26f;
    private const float PerfectBakeWindowMaxCenter = 0.74f;

    public bool IsOpen
    {
        get { return menuRoot != null && menuRoot.activeSelf; }
    }

    public int PerfectCakeCount
    {
        get { return perfectCakeCount; }
    }

    public int PrepMistakeCount
    {
        get { return prepMistakeCount; }
    }

    // Orders on the board that are not cakes yet; finished cakes leave the board for the inventory.
    public int QueuedOrderCount
    {
        get { return spawnedOrders.Count; }
    }

    private void Awake()
    {
        InitializeUi();
    }

    private void Start()
    {
        InitializeUi();
    }

    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        UpdateBakeState();
        UpdateSizeScaleGame();
    }

    private void OnDisable()
    {
        SetPlayerInputLocked(false);
        CloseControlsPopup();
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
            ActivateTab(GetFirstAvailableTabIndex(activePrepOrder), true);
        }
        else
        {
            RuntimeOrder nextOrder = selectedOrder != null && selectedOrder.State == OrderRuntimeState.Pending
                ? selectedOrder
                : null;
            SetSelectedOrder(nextOrder, false);
            ActivateTab(FlavorTabIndex, true);
        }

        RefreshAllUi();
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

    public bool HasOrder(string orderId)
    {
        if (string.IsNullOrEmpty(orderId))
        {
            return false;
        }

        for (int i = 0; i < allOrders.Count; i++)
        {
            if (allOrders[i] != null && allOrders[i].Data != null && allOrders[i].Data.id == orderId)
            {
                return true;
            }
        }

        return false;
    }

    public void AddOrder(OrderDefinition definition)
    {
        InitializeUi();

        if (definition == null || string.IsNullOrEmpty(definition.id) || HasOrder(definition.id))
        {
            return;
        }

        RuntimeOrder runtimeOrder = CreateRuntimeOrder(definition, allOrders.Count);
        allOrders.Add(runtimeOrder);

        if (orderCardPrefab != null && cardsContentRoot != null)
        {
            OrderCardView cardView = Instantiate(orderCardPrefab, cardsContentRoot);
            cardView.name = "OrderCard_" + definition.cellNumber.Replace(" ", string.Empty);
            ConfigureCardTransform(cardView.GetComponent<RectTransform>(), spawnedOrders.Count);
            cardView.Initialize(this, runtimeOrder.Data);
            runtimeOrder.CardView = cardView;
            SetCardItemIcon(runtimeOrder);
            spawnedOrders.Add(runtimeOrder);
        }

        RefreshAllUi();
    }

    public void RemoveOrder(string orderId)
    {
        InitializeUi();

        RuntimeOrder removedOrder = null;
        for (int i = allOrders.Count - 1; i >= 0; i--)
        {
            RuntimeOrder order = allOrders[i];
            if (order != null && order.Data != null && order.Data.id == orderId)
            {
                removedOrder = order;
                allOrders.RemoveAt(i);
            }
        }

        for (int i = spawnedOrders.Count - 1; i >= 0; i--)
        {
            RuntimeOrder order = spawnedOrders[i];
            if (order != null && order.Data != null && order.Data.id == orderId)
            {
                if (order.CardView != null)
                {
                    DestroyOrderCard(order.CardView);
                }

                spawnedOrders.RemoveAt(i);
            }
        }

        OrderCardView[] sceneCards = FindObjectsByType<OrderCardView>(FindObjectsInactive.Include);
        for (int i = 0; i < sceneCards.Length; i++)
        {
            if (sceneCards[i] != null && sceneCards[i].OrderId == orderId)
            {
                DestroyOrderCard(sceneCards[i]);
            }
        }

        if (selectedOrder == removedOrder)
        {
            selectedOrder = null;
        }

        if (activePrepOrder == removedOrder)
        {
            activePrepOrder = null;
        }

        RepositionOrderCards();
        RefreshAllUi();
    }

    private void DestroyOrderCard(OrderCardView cardView)
    {
        if (cardView == null)
        {
            return;
        }

        cardView.gameObject.SetActive(false);
        Destroy(cardView.gameObject);
    }

    public void SetMoney(int amount)
    {
        displayedMoney = Mathf.Max(0, amount);
        SyncBoardSummary();
        RefreshDayGoalHud();
    }

    public void SetDayGoal(int day, int goal)
    {
        InitializeUi();
        currentDay = Mathf.Max(1, day);
        dayGoal = Mathf.Max(0, goal);

        if (controlsPopupTitleText != null)
        {
            controlsPopupTitleText.text = "Día " + currentDay;
        }

        if (controlsPopupGoalText != null)
        {
            controlsPopupGoalText.text = "Meta del día: Q" + dayGoal;
        }

        RefreshDayGoalHud();
    }

    private void BuildDayGoalHud()
    {
        if (dayGoalHudText != null || totalMoneyText == null || totalMoneyText.transform.parent == null)
        {
            return;
        }

        // Sits right under the money card so the target is always next to what you have.
        RectTransform moneyCard = totalMoneyText.rectTransform;
        dayGoalHudText = CreateText("DayGoalText", moneyCard.parent, string.Empty, 24, FontStyle.Normal, TextAnchor.UpperCenter);
        dayGoalHudText.horizontalOverflow = HorizontalWrapMode.Overflow;
        dayGoalHudText.resizeTextForBestFit = false;
        SetRect(dayGoalHudText.rectTransform, moneyCard.anchorMin, moneyCard.anchorMax, moneyCard.anchoredPosition + new Vector2(0f, -30f), new Vector2(220f, 30f), new Vector2(0.5f, 1f));
        Outline outline = dayGoalHudText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.18f, 0.12f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);
        RefreshDayGoalHud();
    }

    private void RefreshDayGoalHud()
    {
        if (dayGoalHudText == null)
        {
            return;
        }

        dayGoalHudText.gameObject.SetActive(dayGoal > 0);
        dayGoalHudText.text = "Meta: Q" + dayGoal;
        dayGoalHudText.color = displayedMoney >= dayGoal ? new Color(0.62f, 1f, 0.55f, 1f) : new Color(1f, 0.97f, 0.9f, 1f);
    }

    public void SetTimerSeconds(float seconds)
    {
        if (timerText == null)
        {
            EnsureReferences();
        }

        if (timerText == null)
        {
            return;
        }

        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        if (totalSeconds == lastDisplayedTimerSeconds)
        {
            return;
        }

        lastDisplayedTimerSeconds = totalSeconds;
        int minutes = totalSeconds / 60;
        int remainder = totalSeconds % 60;
        timerText.text = minutes.ToString("00") + ":" + remainder.ToString("00");
    }

    public void ShowDayResults(DaySummary summary)
    {
        InitializeUi();
        Close();
        if (resultsMenuRoot == null)
        {
            BuildResultsMenu();
        }

        resultsGoalReached = summary.GoalReached;
        int mistakes = summary.PrepMistakes + summary.WrongDeliveries;

        if (resultsTitleText != null)
        {
            resultsTitleText.text = resultsGoalReached ? "¡Día " + summary.Day + " completado!" : "Game Over";
            resultsTitleText.color = resultsGoalReached ? Color.black : new Color(0.62f, 0.12f, 0.10f, 1f);
        }

        if (resultsSubtitleText != null)
        {
            resultsSubtitleText.text = resultsGoalReached
                ? "Alcanzaste la meta de Q" + summary.Goal
                : "No alcanzaste la meta de Q" + summary.Goal;
        }

        if (resultsStatsText != null)
        {
            resultsStatsText.text =
                "Dinero obtenido: Q" + Mathf.Max(0, summary.Money) + " / Q" + summary.Goal + "\n" +
                "Órdenes entregadas: " + summary.DeliveredOrders + "\n" +
                "Pasteles perfectos: " + summary.PerfectCakes + "\n" +
                "Errores cometidos: " + mistakes + " (" + summary.PrepMistakes + " al preparar, " + summary.WrongDeliveries + " al entregar)\n" +
                "Órdenes sin entregar: " + summary.UndeliveredOrders;
        }

        if (resultsPrimaryLabel != null)
        {
            resultsPrimaryLabel.text = resultsGoalReached ? "Ir al siguiente día" : "Reintentar día";
        }

        if (resultsMenuRoot != null)
        {
            resultsMenuRoot.SetActive(true);
        }
    }

    private void HandleResultsPrimaryButton()
    {
        if (resultsGoalReached)
        {
            DayProgress.AdvanceDay();
        }

        // Reloading the gameplay scene resets orders, money and the timer; DayProgress keeps the day number.
        Time.timeScale = 1f;
        SetPlayerInputLocked(false);
        CloseControlsPopup();
        SceneManager.LoadScene(gameplaySceneName);
    }
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SetPlayerInputLocked(false);
        CloseControlsPopup();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void SetMapVisible(bool visible, Vector3 playerPosition, IList<PrisonerInteractable> prisonerViews)
    {
        InitializeUi();
        if (mapOverlayRoot == null)
        {
            BuildMapOverlay();
        }

        if (mapOverlayRoot == null)
        {
            return;
        }

        mapOverlayRoot.SetActive(visible);
        if (!visible || mapContentRoot == null)
        {
            return;
        }

        PopulateMapOverlay(playerPosition, prisonerViews);
    }

    public void HandleOrderCardClicked(OrderCardView cardView)
    {
        RuntimeOrder order = FindOrderByView(cardView);
        if (!CanSelectOrderFromBoard(order))
        {
            return;
        }

        SetSelectedOrder(order, false);
    }

    public void HandleOrderCardDropped(OrderCardView cardView)
    {
        RuntimeOrder order = FindOrderByView(cardView);
        if (!CanSelectOrderFromBoard(order))
        {
            return;
        }

        BeginPrepFromDroppedOrder(order);
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
        defaultFont = detailTitleText != null && detailTitleText.font != null
            ? detailTitleText.font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildCardsViewport();
        WireButtons();
        HidePrepareButton();
        HideRuntimeDetailStatus();
        BuildDetailItemIcon();
        BuildRuntimeTabContent();
        BuildWaitingOrderState();
        BuildResultsMenu();
        BuildMapOverlay();
        BuildControlsPopup();
        BuildDayGoalHud();
        SetMenuVisible(false);
        SetOrderBoardInteractable(false);
        allOrders.Clear();
        spawnedOrders.Clear();
        selectedOrder = null;
        activePrepOrder = null;
        SetMoney(0);
        SetTimerSeconds(900f);
        SyncBoardSummary();
        SetSelectedOrder(null, false);
        ActivateTab(FlavorTabIndex, true);
        isInitialized = true;
        ShowControlsPopup();
    }

    private void ShowControlsPopup()
    {
        if (controlsPopupShown || controlsPopupRoot == null)
        {
            return;
        }

        controlsPopupShown = true;
        controlsPopupRoot.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        EnsurePlayerController();
        if (playerController != null && !controlsPopupLockHeld)
        {
            playerController.AddInputLock();
            controlsPopupLockHeld = true;
        }
    }

    private void CloseControlsPopup()
    {
        if (controlsPopupRoot != null)
        {
            controlsPopupRoot.SetActive(false);
        }

        if (playerController != null && controlsPopupLockHeld)
        {
            playerController.RemoveInputLock();
        }

        controlsPopupLockHeld = false;
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

        if (orderCardPrefab == null && orderCardPrefabResourcePath != null)
        {
            orderCardPrefab = orderCardPrefabResourcePath.GetComponent<OrderCardView>();
            if (orderCardPrefab == null)
            {
                Debug.LogError("Assigned order card prefab does not have an OrderCardView component.");
            }
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

        if (timerText == null)
        {
            Transform found = FindDeepChild(transform, "TimeRowText");
            if (found != null)
            {
                timerText = found.GetComponent<Text>();
            }
        }

        if (prepareButton == null && detailPanel != null)
        {
            Transform found = FindDeepChild(detailPanel.transform, "PrepareButton");
            if (found == null)
            {
                found = FindDeepChild(detailPanel.transform, "Preparar");
            }

            if (found != null)
            {
                prepareButton = found.GetComponent<Button>();
            }
        }

        if (dragHintOverlay == null)
        {
            Transform found = FindDeepChild(transform, "DragHintOverlay");
            if (found != null)
            {
                dragHintOverlay = found.gameObject;
            }
        }

        if (dragHintPointer == null && dragHintOverlay != null)
        {
            dragHintPointer = FindDeepChild(dragHintOverlay.transform, "DragHintPointer") as RectTransform;
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
        EnsureButtonInputProxy(closeButton);
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
            ButtonSfx.Attach(closeButton, SfxId.KitchenUiClick);
        }

        for (int i = 0; i < tabButtons.Length; i++)
        {
            Button button = tabButtons[i];
            if (button == null)
            {
                continue;
            }

            int capturedIndex = i;
            EnsureButtonInputProxy(button);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ActivateTab(capturedIndex, false));
            ButtonSfx.Attach(button, SfxId.KitchenUiClick);
        }
    }

    private void HidePrepareButton()
    {
        if (prepareButton == null)
        {
            return;
        }

        prepareButton.onClick.RemoveAllListeners();
        prepareButton.gameObject.SetActive(false);
    }

    private void BeginPrepFromDroppedOrder(RuntimeOrder order)
    {
        if (order == null || order.State != OrderRuntimeState.Pending || activePrepOrder != null || IsInventoryFull())
        {
            RefreshAllUi();
            return;
        }

        selectedOrder = order;
        activePrepOrder = order;
        activePrepOrder.State = OrderRuntimeState.ActivePrep;
        AudioManager.Play(SfxId.OrderActivate);
        ActivateTab(FlavorTabIndex, true);
        RefreshAllUi();
    }

    private void HideRuntimeDetailStatus()
    {
        if (detailStatusText == null)
        {
            return;
        }

        detailStatusText.text = string.Empty;
        detailStatusText.gameObject.SetActive(false);
    }

    private void BuildDetailItemIcon()
    {
        if (detailPanel == null)
        {
            return;
        }

        detailItemFrame = FindDeepChild(detailPanel.transform, "ItemFrame") as RectTransform;
        if (detailItemFrame == null)
        {
            return;
        }

        Transform placeholder = FindDeepChild(detailItemFrame, "ItemPlaceholder");
        detailItemPlaceholderText = placeholder != null ? placeholder.GetComponent<Text>() : null;

        Transform existing = detailItemFrame.Find("ItemIcon");
        if (existing != null)
        {
            detailItemIcon = existing.GetComponent<Image>();
            return;
        }

        detailItemIcon = CreateImage("ItemIcon", detailItemFrame, Color.white);
        detailItemIcon.preserveAspect = true;
        detailItemIcon.raycastTarget = false;
        detailItemIcon.gameObject.SetActive(false);
    }

    private void RefreshDetailItemIcon(string contrabandItem)
    {
        if (detailItemIcon == null || detailItemFrame == null)
        {
            return;
        }

        Sprite icon = GetContrabandIcon(contrabandItem);
        detailItemIcon.gameObject.SetActive(icon != null);
        if (detailItemPlaceholderText != null)
        {
            detailItemPlaceholderText.gameObject.SetActive(icon == null);
        }

        if (icon == null)
        {
            return;
        }

        detailItemIcon.sprite = icon;
        const float padding = 16f;
        SetRect(detailItemIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        FitIconInBox(detailItemIcon.rectTransform, icon, detailItemFrame.rect.size - new Vector2(padding, padding));
    }

    public static void FitIconInBox(RectTransform iconRect, Sprite icon, Vector2 box)
    {
        // Tall tools (screwdriver, file...) lie down so they use a wide box instead of shrinking to a sliver.
        bool lieDown = icon.rect.height > icon.rect.width * 1.3f && box.x > box.y;
        iconRect.sizeDelta = lieDown ? new Vector2(box.y, box.x) : box;
        iconRect.localRotation = Quaternion.Euler(0f, 0f, lieDown ? -90f : 0f);
    }

    private void SetCardItemIcon(RuntimeOrder runtimeOrder)
    {
        if (runtimeOrder.CardView != null && runtimeOrder.Data != null)
        {
            runtimeOrder.CardView.SetItemIcon(GetContrabandIcon(runtimeOrder.Data.contrabandItem));
        }
    }

    private Sprite GetContrabandIcon(string contrabandItem)
    {
        return FindNamedIcon(contrabandIcons, contrabandItem);
    }

    private Sprite GetToppingCakeIcon(string topping)
    {
        return FindNamedIcon(toppingCakeIcons, topping);
    }

    private static Sprite FindNamedIcon(NamedIconEntry[] entries, string itemName)
    {
        if (entries == null || string.IsNullOrEmpty(itemName))
        {
            return null;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            NamedIconEntry entry = entries[i];
            if (entry != null && entry.icon != null && string.Equals(entry.itemName, itemName, StringComparison.OrdinalIgnoreCase))
            {
                return entry.icon;
            }
        }

        return null;
    }

    private void NormalizeDetailPanelLayout()
    {
        if (detailPanel == null)
        {
            return;
        }

        RectTransform panelRect = detailPanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            Vector2 size = panelRect.sizeDelta;
            size.y = Mathf.Max(size.y, 560f);
            size.x = Mathf.Max(size.x, 260f);
            panelRect.sizeDelta = size;
        }

        ConfigureDetailText(detailTitleText, 26, FontStyle.Bold, TextAnchor.MiddleCenter);
        ConfigureDetailText(detailCakeColorText, 18, FontStyle.Normal, TextAnchor.UpperLeft);
        ConfigureDetailText(detailToppingText, 18, FontStyle.Normal, TextAnchor.UpperLeft);
        ConfigureDetailText(detailItemLabelText, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
        ConfigureDetailText(detailPayoutText, 19, FontStyle.Bold, TextAnchor.MiddleLeft);
        ConfigureDetailText(detailCellText, 19, FontStyle.Bold, TextAnchor.MiddleRight);

        if (detailTitleText != null)
        {
            SetRect(detailTitleText.rectTransform, new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), new Vector2(0f, -66f), new Vector2(0f, 78f), new Vector2(0.5f, 0.5f));
        }

        if (detailCakeColorText != null)
        {
            SetRect(detailCakeColorText.rectTransform, new Vector2(0.10f, 1f), new Vector2(0.90f, 1f), new Vector2(0f, -166f), new Vector2(0f, 84f), new Vector2(0.5f, 0.5f));
        }

        if (detailToppingText != null)
        {
            SetRect(detailToppingText.rectTransform, new Vector2(0.10f, 1f), new Vector2(0.90f, 1f), new Vector2(0f, -236f), new Vector2(0f, 34f), new Vector2(0.5f, 0.5f));
        }

        if (detailItemLabelText != null)
        {
            SetRect(detailItemLabelText.rectTransform, new Vector2(0.10f, 0.5f), new Vector2(0.90f, 0.5f), new Vector2(0f, 34f), new Vector2(0f, 72f), new Vector2(0.5f, 0.5f));
        }

        Transform itemFrame = FindDeepChild(detailPanel.transform, "ItemFrame");
        if (itemFrame == null)
        {
            itemFrame = FindDeepChild(detailPanel.transform, "ItemPlaceholder");
        }

        RectTransform itemFrameRect = itemFrame as RectTransform;
        if (itemFrameRect != null)
        {
            SetRect(itemFrameRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(112f, 72f), new Vector2(0.5f, 0.5f));
        }

        if (detailPayoutText != null)
        {
            SetRect(detailPayoutText.rectTransform, new Vector2(0.10f, 0f), new Vector2(0.50f, 0f), new Vector2(0f, 62f), new Vector2(0f, 34f), new Vector2(0.5f, 0.5f));
        }

        if (detailCellText != null)
        {
            SetRect(detailCellText.rectTransform, new Vector2(0.50f, 0f), new Vector2(0.90f, 0f), new Vector2(0f, 62f), new Vector2(0f, 34f), new Vector2(0.5f, 0.5f));
        }
    }

    private static void ConfigureDetailText(Text text, int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        if (text == null)
        {
            return;
        }

        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(12, fontSize - 8);
        text.resizeTextMaxSize = fontSize;
    }

    private void BuildRuntimeTabContent()
    {
        if (tabPanels == null || tabPanels.Length <= ToppingTabIndex)
        {
            Debug.LogError("KitchenOrderPrepUIController needs " + (ToppingTabIndex + 1) + " tab panels (sabor, tamano, objeto, horno, topping).");
            return;
        }

        BuildFlavorTab(tabPanels[FlavorTabIndex].GetComponent<RectTransform>());
        BuildSizeTab(tabPanels[SizeTabIndex].GetComponent<RectTransform>());
        BuildPlacementTab(tabPanels[PlacementTabIndex].GetComponent<RectTransform>());
        BuildBakeTab(tabPanels[BakeTabIndex].GetComponent<RectTransform>());
        BuildToppingTab(tabPanels[ToppingTabIndex].GetComponent<RectTransform>());
    }

    private void BuildWaitingOrderState()
    {
        if (dragHintOverlay != null && dragHintText == null)
        {
            dragHintText = dragHintOverlay.GetComponentInChildren<Text>(true);
            dragHintDefaultText = dragHintText != null ? dragHintText.text : string.Empty;
        }

        if (tabPanels != null && tabPanels.Length > 0 && tabPanels[0] != null && tabPanels[0].transform.parent != null)
        {
            stageTabPanelsRoot = tabPanels[0].transform.parent.gameObject;
        }

        if (tabButtons != null && tabButtons.Length > 0 && tabButtons[0] != null && tabButtons[0].transform.parent != null)
        {
            tabBarRoot = tabButtons[0].transform.parent.gameObject;
        }

        Transform stagePanel = stageTabPanelsRoot != null ? stageTabPanelsRoot.transform.parent : null;
        if (stagePanel == null)
        {
            return;
        }

        Transform existing = stagePanel.Find("WaitingOrderText");
        if (existing != null)
        {
            waitingOrderText = existing.GetComponent<Text>();
            return;
        }

        waitingOrderText = CreateText("WaitingOrderText", stagePanel, WaitingOrderMessage, 44, FontStyle.Normal, TextAnchor.MiddleCenter);
        waitingOrderText.color = new Color(0.38f, 0.37f, 0.36f, 1f);
        SetRect(waitingOrderText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-120f, -120f), new Vector2(0.5f, 0.5f));
    }

    private void RefreshStageVisibility()
    {
        bool hasActiveOrder = activePrepOrder != null && activePrepOrder.State == OrderRuntimeState.ActivePrep;

        if (stageTabPanelsRoot != null) stageTabPanelsRoot.SetActive(hasActiveOrder);
        if (tabBarRoot != null) tabBarRoot.SetActive(hasActiveOrder);
        if (centerStageTitle != null) centerStageTitle.gameObject.SetActive(hasActiveOrder);
        if (waitingOrderText != null) waitingOrderText.gameObject.SetActive(!hasActiveOrder);

        bool inventoryFull = !hasActiveOrder && IsInventoryFull();
        if (waitingOrderText != null)
        {
            waitingOrderText.text = inventoryFull ? InventoryFullMessage : WaitingOrderMessage;
            waitingOrderText.color = inventoryFull ? new Color(0.62f, 0.14f, 0.10f, 1f) : new Color(0.38f, 0.37f, 0.36f, 1f);
        }

        if (dragHintText != null)
        {
            dragHintText.text = inventoryFull ? "Inventario lleno" : dragHintDefaultText;
        }

        // The pulsing arrow invites a drag, which is exactly what can't happen with a full inventory.
        if (dragHintPointer != null)
        {
            dragHintPointer.gameObject.SetActive(!inventoryFull);
        }
    }

    private bool IsInventoryFull()
    {
        EnsurePlayerController();
        return playerController != null && playerController.Inventory.IsFull;
    }

    private void BuildFlavorTab(RectTransform panel)
    {
        if (panel == null)
        {
            return;
        }

        ClearChildren(panel);
        Text title = CreateText("Instruction", panel, "Navega entre los pasteles y elige el del sabor pedido.", 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(820f, 40f), new Vector2(0.5f, 0.5f));

        flavorCakeImage = CreateImage("FlavorCake", panel, Color.white);
        flavorCakeImage.preserveAspect = true;
        flavorCakeImage.raycastTarget = false;
        SetRect(flavorCakeImage.rectTransform, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), new Vector2(0f, 10f), new Vector2(340f, 261f), new Vector2(0.5f, 0.5f));

        // The name under each cake so the colors can't be mistaken for another flavor.
        flavorNameText = CreateText("FlavorName", panel, string.Empty, 30, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(flavorNameText.rectTransform, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), new Vector2(0f, -146f), new Vector2(360f, 38f), new Vector2(0.5f, 0.5f));

        flavorPreviousButton = CreatePlacementIconButton("PreviousFlavorButton", panel, PlacementIcon.ArrowLeft, Vector2.zero);
        SetRect(flavorPreviousButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), new Vector2(-300f, 0f), new Vector2(80f, 84f), new Vector2(0.5f, 0.5f));
        flavorPreviousButton.onClick.AddListener(() => MoveFlavorCarousel(-1));
        ButtonSfx.Attach(flavorPreviousButton, SfxId.KitchenUiClick);

        flavorNextButton = CreatePlacementIconButton("NextFlavorButton", panel, PlacementIcon.ArrowRight, Vector2.zero);
        SetRect(flavorNextButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), new Vector2(300f, 0f), new Vector2(80f, 84f), new Vector2(0.5f, 0.5f));
        flavorNextButton.onClick.AddListener(() => MoveFlavorCarousel(1));
        ButtonSfx.Attach(flavorNextButton, SfxId.KitchenUiClick);

        int flavorCount = cakeFlavors != null ? cakeFlavors.Length : 0;
        flavorPageDots = new Image[flavorCount];
        const float dotSpacing = 26f;
        for (int i = 0; i < flavorCount; i++)
        {
            Image dot = CreateImage("FlavorDot_" + i, panel, Color.white);
            dot.sprite = GetFlavorDotSprite();
            dot.raycastTarget = false;
            SetRect(dot.rectTransform, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), new Vector2((i - (flavorCount - 1) * 0.5f) * dotSpacing, -182f), new Vector2(14f, 14f), new Vector2(0.5f, 0.5f));
            flavorPageDots[i] = dot;
        }

        flavorSelectButton = CreateButton("SelectFlavorButton", panel, Color.white);
        StylePlacementButton(flavorSelectButton, placementConfirmButtonSprite, true);
        SetRect(flavorSelectButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(320f, 84f), new Vector2(0.5f, 0.5f));
        Text selectLabel = CreateText("Label", flavorSelectButton.GetComponent<RectTransform>(), "Elegir este pastel", 32, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetRect(selectLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 3f), new Vector2(-40f, -20f), new Vector2(0.5f, 0.5f));
        flavorSelectButton.onClick.AddListener(ConfirmFlavorSelection);
        ButtonSfx.Attach(flavorSelectButton, SfxId.KitchenUiClick);

        flavorFeedbackText = CreateText("FlavorFeedbackText", panel, "Selecciona el sabor del pastel.", 20, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetRect(flavorFeedbackText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(760f, 28f), new Vector2(0.5f, 0.5f));
    }

    private void MoveFlavorCarousel(int direction)
    {
        if (activePrepOrder == null || IsFlavorChosen(activePrepOrder) || cakeFlavors == null || cakeFlavors.Length == 0)
        {
            return;
        }

        int count = cakeFlavors.Length;
        activePrepOrder.FlavorCarouselIndex = ((activePrepOrder.FlavorCarouselIndex + direction) % count + count) % count;
        RefreshFlavorUi();
    }

    private void ConfirmFlavorSelection()
    {
        if (activePrepOrder == null || activePrepOrder.State != OrderRuntimeState.ActivePrep || IsFlavorChosen(activePrepOrder))
        {
            return;
        }

        CakeFlavorEntry flavor = GetCarouselFlavor(activePrepOrder);
        if (flavor == null)
        {
            return;
        }

        // A wrong flavor is allowed; it only lowers the payout, like the topping.
        activePrepOrder.SelectedFlavor = flavor.flavorName;
        activePrepOrder.FlavorMatched = string.Equals(flavor.flavorName, activePrepOrder.Data.cakeFlavor, StringComparison.OrdinalIgnoreCase);
        RefreshAllUi();
        ActivateTab(SizeTabIndex, true);
    }

    // A random first cake that is never the ordered flavor, so the player always has to look for it.
    private int GetCarouselStartIndex(string orderedFlavor)
    {
        if (cakeFlavors == null || cakeFlavors.Length == 0)
        {
            return 0;
        }

        List<int> candidates = new List<int>();
        for (int i = 0; i < cakeFlavors.Length; i++)
        {
            if (cakeFlavors[i] == null || !string.Equals(cakeFlavors[i].flavorName, orderedFlavor, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(i);
            }
        }

        return candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : 0;
    }

    private CakeFlavorEntry GetCarouselFlavor(RuntimeOrder order)
    {
        if (order == null || cakeFlavors == null || cakeFlavors.Length == 0)
        {
            return null;
        }

        return cakeFlavors[Mathf.Clamp(order.FlavorCarouselIndex, 0, cakeFlavors.Length - 1)];
    }

    private CakeFlavorEntry FindFlavor(string flavorName)
    {
        if (cakeFlavors == null || string.IsNullOrEmpty(flavorName))
        {
            return null;
        }

        for (int i = 0; i < cakeFlavors.Length; i++)
        {
            if (cakeFlavors[i] != null && string.Equals(cakeFlavors[i].flavorName, flavorName, StringComparison.OrdinalIgnoreCase))
            {
                return cakeFlavors[i];
            }
        }

        return null;
    }

    private static Sprite GetFlavorSizeSprite(CakeFlavorEntry flavor, int sizeIndex)
    {
        if (flavor == null || flavor.sizeSprites == null || sizeIndex < 0 || sizeIndex >= flavor.sizeSprites.Length)
        {
            return null;
        }

        return flavor.sizeSprites[sizeIndex];
    }

    private static bool IsFlavorChosen(RuntimeOrder order)
    {
        return order != null && !string.IsNullOrEmpty(order.SelectedFlavor);
    }

    private void RefreshFlavorUi()
    {
        if (flavorCakeImage == null || flavorFeedbackText == null)
        {
            return;
        }

        bool canChoose = activePrepOrder != null && activePrepOrder.State == OrderRuntimeState.ActivePrep && !IsFlavorChosen(activePrepOrder);
        flavorPreviousButton.interactable = canChoose;
        flavorNextButton.interactable = canChoose;
        flavorSelectButton.interactable = canChoose;

        // Once chosen, the carousel stays on the picked cake.
        CakeFlavorEntry shownFlavor = activePrepOrder != null && IsFlavorChosen(activePrepOrder)
            ? FindFlavor(activePrepOrder.SelectedFlavor)
            : GetCarouselFlavor(activePrepOrder);
        Sprite cakeSprite = GetFlavorSizeSprite(shownFlavor, 2);
        flavorCakeImage.sprite = cakeSprite;
        flavorCakeImage.enabled = cakeSprite != null;
        if (flavorNameText != null)
        {
            flavorNameText.text = shownFlavor != null ? shownFlavor.flavorName : string.Empty;
        }

        int shownIndex = shownFlavor != null ? Array.IndexOf(cakeFlavors, shownFlavor) : -1;
        for (int i = 0; flavorPageDots != null && i < flavorPageDots.Length; i++)
        {
            flavorPageDots[i].color = i == shownIndex ? new Color(0.55f, 0.30f, 0.22f, 1f) : new Color(0.55f, 0.30f, 0.22f, 0.3f);
        }

        if (activePrepOrder == null)
        {
            flavorFeedbackText.text = "Bloqueado hasta seleccionar una orden.";
        }
        else if (IsFlavorChosen(activePrepOrder))
        {
            flavorFeedbackText.text = activePrepOrder.FlavorMatched
                ? "Sabor correcto."
                : "Ese no era el sabor pedido. Te pagaran menos por este pastel.";
        }
        else
        {
            flavorFeedbackText.text = "Usa las flechas para ver los pasteles y elige el de " + (activePrepOrder.Data.cakeFlavor ?? string.Empty).ToLowerInvariant() + ".";
        }
    }

    private void BuildSizeTab(RectTransform panel)
    {
        if (panel == null)
        {
            return;
        }

        ClearChildren(panel);
        sizeOptionButtons = new Button[0];
        sizeOptionBackgrounds = new Image[0];
        sizeGameContent = panel;
        sizeFeedbackText = CreateText("SizeFeedbackText", panel, "Selecciona el tamano exacto del pastel.", 22, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetRect(sizeFeedbackText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(700f, 34f), new Vector2(0.5f, 0.5f));
    }

    private void EnsureSizeGameAssigned(RuntimeOrder order)
    {
        if (order == null || order.SizeGameAssigned)
        {
            return;
        }

        order.SizeGame = (SizeGameType)UnityEngine.Random.Range(0, 3);
        order.SizeGameAssigned = true;
        order.SizeGameCompleted = false;
    }

    private void BuildSizeGame(RuntimeOrder order)
    {
        if (sizeGameContent == null || order == null)
        {
            return;
        }

        if (sizeGameBuiltFor == order && sizeGameRoot != null && sizeGameRoot.childCount > 0)
        {
            return;
        }

        EnsureSizeGameAssigned(order);
        if (sizeGameRoot == null)
        {
            sizeGameRoot = CreateRect("SizeGameRoot", sizeGameContent);
            SetRect(sizeGameRoot, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-40f, -110f), new Vector2(0.5f, 0.5f));
        }

        ClearChildren(sizeGameRoot);
        sizeGamePieces.Clear();
        sizeSortArea = null;
        sizeMoldTarget = null;
        sizeScaleIndicator = null;
        sizeScaleSelectButton = null;
        sizeScaleRunning = false;
        sizeSortValidated = false;
        sizeGameBuiltFor = order;

        switch (order.SizeGame)
        {
            case SizeGameType.CorrectMold:
                BuildCorrectMoldGame(order);
                break;
            case SizeGameType.SortCakes:
                BuildSortCakesGame(order);
                break;
            case SizeGameType.FillScale:
                BuildFillScaleGame(order);
                break;
        }
    }

    private void BuildCorrectMoldGame(RuntimeOrder order)
    {
        Text title = CreateText("MoldInstruction", sizeGameRoot, "Arrastra el molde del tamano solicitado.", 26, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(720f, 44f), new Vector2(0.5f, 0.5f));
        CreateSizeDragPieces(sizeGameRoot, false, 80f);
        RectTransform targetRoot = CreateRect("MoldTarget", sizeGameRoot);
        SetRect(targetRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(440f, 210f), new Vector2(0.5f, 0.5f));
        sizeMoldTarget = targetRoot;
        RectTransform targetVisual = CreateRect("ContainerVisual", targetRoot);
        SetRect(targetVisual, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(340f, 150f), new Vector2(0.5f, 0.5f));
        Image targetImage = targetVisual.gameObject.AddComponent<Image>();
        targetImage.sprite = preparationContainerSprite;
        targetImage.color = Color.white;
        targetImage.preserveAspect = true;
        targetImage.raycastTarget = false;
        Text targetLabel = CreateText("Label", targetRoot, "Zona de preparacion", 18, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetRect(targetLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(440f, 32f), new Vector2(0.5f, 0.5f));
    }

    private void BuildSortCakesGame(RuntimeOrder order)
    {
        Text title = CreateText("SortInstruction", sizeGameRoot, "Ordena los pasteles de menor a mayor y selecciona el solicitado.", 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(820f, 44f), new Vector2(0.5f, 0.5f));
        sizeSortArea = CreateRect("SortArea", sizeGameRoot);
        SetRect(sizeSortArea, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(760f, 190f), new Vector2(0.5f, 0.5f));
        Image areaImage = sizeSortArea.gameObject.AddComponent<Image>();
        areaImage.color = new Color(0.96f, 0.92f, 0.84f, 1f);
        areaImage.raycastTarget = false;
        int[] shuffled = { 2, 0, 1 };
        for (int i = 0; i < shuffled.Length; i++)
        {
            CreateSizePiece(sizeSortArea, shuffled[i], new Vector2(-250f + i * 250f, 0f));
        }
    }

    private void BuildFillScaleGame(RuntimeOrder order)
    {
        Text title = CreateText("ScaleInstruction", sizeGameRoot, "Deten la medida en el tamano solicitado.", 26, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(720f, 44f), new Vector2(0.5f, 0.5f));
        RectTransform bar = CreateRect("ScaleBar", sizeGameRoot);
        SetRect(bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(720f, 70f), new Vector2(0.5f, 0.5f));
        Image barImage = bar.gameObject.AddComponent<Image>();
        barImage.color = new Color(0.82f, 0.80f, 0.75f, 1f);
        barImage.raycastTarget = false;
        Sprite cylinderSprite = GetSizeSprite(1);
        float[] cylinderWidths = { 100f, 145f, 190f };
        float cylinderAspect = cylinderSprite.rect.height / cylinderSprite.rect.width;
        for (int i = 0; i < 3; i++)
        {
            RectTransform mold = CreateRect("ScaleMold_" + i, bar);
            SetRect(mold, new Vector2((i + 0.5f) / 3f, 0.5f), new Vector2((i + 0.5f) / 3f, 0.5f), new Vector2(0f, 0f), new Vector2(cylinderWidths[i], cylinderWidths[i] * cylinderAspect), new Vector2(0.5f, 0.5f));
            Image moldImage = mold.gameObject.AddComponent<Image>();
            moldImage.sprite = GetSizeSprite(i);
            moldImage.color = Color.white;
            moldImage.preserveAspect = true;
            moldImage.raycastTarget = false;
            Text label = CreateText("ScaleLabel_" + i, bar, CakeSizeLabels[i], 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(label.rectTransform, new Vector2((i + 0.5f) / 3f, 0f), new Vector2((i + 0.5f) / 3f, 0f), new Vector2(0f, -46f), new Vector2(190f, 28f), new Vector2(0.5f, 0.5f));
        }
        sizeScaleIndicator = CreateRect("ScaleIndicator", bar);
        SetRect(sizeScaleIndicator, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 92f), new Vector2(0.5f, 0.5f));
        Image indicatorImage = sizeScaleIndicator.gameObject.AddComponent<Image>();
        indicatorImage.color = new Color(0.55f, 0.18f, 0.12f, 1f);
        indicatorImage.raycastTarget = false;
        sizeScaleSelectButton = CreateButton("ScaleSelectButton", sizeGameRoot, new Color(0.97f, 0.96f, 0.93f, 1f));
        SetRect(sizeScaleSelectButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(230f, 58f), new Vector2(0.5f, 0.5f));
        sizeScaleSelectLabel = CreateText("Label", sizeScaleSelectButton.transform, "Seleccionar", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(sizeScaleSelectLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        sizeScaleSelectButton.onClick.AddListener(SelectScalePosition);
        ButtonSfx.Attach(sizeScaleSelectButton, SfxId.KitchenUiClick);
        sizeScaleRunning = true;
    }

    private void CreateSizeDragPieces(RectTransform parent, bool shuffled, float verticalOffset = 0f)
    {
        int[] indexes = shuffled ? new[] { 2, 0, 1 } : new[] { 0, 1, 2 };
        for (int i = 0; i < indexes.Length; i++)
        {
            CreateSizePiece(parent, indexes[i], new Vector2(-250f + i * 250f, verticalOffset));
        }
    }

    private SizeGamePieceView CreateSizePiece(RectTransform parent, int sizeIndex, Vector2 position)
    {
        GameObject pieceObject = new GameObject("SizePiece_" + CakeSizeLabels[sizeIndex], typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(SizeGamePieceView));
        RectTransform pieceRect = pieceObject.GetComponent<RectTransform>();
        pieceRect.SetParent(parent, false);
        float[] cylinderWidths = { 100f, 145f, 190f };
        float width = cylinderWidths[Mathf.Clamp(sizeIndex, 0, cylinderWidths.Length - 1)];
        Sprite cylinderSprite = GetSizeSprite(sizeIndex);
        float cylinderAspect = cylinderSprite.rect.height / cylinderSprite.rect.width;
        SetRect(pieceRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(width, width * cylinderAspect), new Vector2(0.5f, 0.5f));
        Image image = pieceObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 1f);
        image.sprite = cylinderSprite;
        image.preserveAspect = true;
        image.raycastTarget = true;
        Outline outline = pieceObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
        Text label = CreateText("Label", pieceObject.transform, CakeSizeLabels[sizeIndex], 20, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -22f), new Vector2(Mathf.Max(150f, width + 30f), 30f), new Vector2(0.5f, 0.5f));
        SizeGamePieceView piece = pieceObject.GetComponent<SizeGamePieceView>();
        piece.Initialize(sizeIndex, parent, rootCanvas);
        piece.Clicked += HandleSizePieceClicked;
        piece.DragEnded += HandleSizePieceDragEnded;
        sizeGamePieces.Add(piece);
        return piece;
    }

    private void HandleSizePieceClicked(SizeGamePieceView piece)
    {
        if (activePrepOrder == null || piece == null)
        {
            return;
        }

        HandleCakeSizeSelected(CakeSizeValues[piece.SizeIndex]);
    }

    private void HandleSizePieceDragEnded(SizeGamePieceView piece)
    {
        if (activePrepOrder == null || piece == null)
        {
            return;
        }

        if (activePrepOrder.SizeGame == SizeGameType.CorrectMold)
        {
            RectTransform pieceRect = piece.GetComponent<RectTransform>();
            if (sizeMoldTarget != null && Vector2.Distance(pieceRect.anchoredPosition, sizeMoldTarget.anchoredPosition) < 150f)
            {
                HandleCakeSizeSelected(CakeSizeValues[piece.SizeIndex]);
            }
        }
        else if (activePrepOrder.SizeGame == SizeGameType.SortCakes)
        {
            ValidateSortGame();
        }
    }

    private void ValidateSortGame()
    {
        if (sizeGamePieces.Count < 3)
        {
            return;
        }

        sizeSortValidated = true;
        for (int i = 0; i < sizeGamePieces.Count; i++)
        {
            for (int j = i + 1; j < sizeGamePieces.Count; j++)
            {
                if (sizeGamePieces[i].SizeIndex > sizeGamePieces[j].SizeIndex && sizeGamePieces[i].GetComponent<RectTransform>().anchoredPosition.x < sizeGamePieces[j].GetComponent<RectTransform>().anchoredPosition.x)
                {
                    sizeSortValidated = false;
                }
                if (sizeGamePieces[i].SizeIndex < sizeGamePieces[j].SizeIndex && sizeGamePieces[i].GetComponent<RectTransform>().anchoredPosition.x > sizeGamePieces[j].GetComponent<RectTransform>().anchoredPosition.x)
                {
                    sizeSortValidated = false;
                }
            }
        }

        if (sizeFeedbackText != null)
        {
            sizeFeedbackText.text = sizeSortValidated ? "Orden correcto. Ahora selecciona el tamano solicitado." : "Ordena los pasteles de menor a mayor.";
        }
    }

    private void UpdateSizeScaleGame()
    {
        if (!sizeScaleRunning || sizeScaleIndicator == null || activePrepOrder == null || activePrepOrder.SizeGame != SizeGameType.FillScale)
        {
            return;
        }

        sizeScalePosition += sizeScaleDirection * Time.unscaledDeltaTime * 0.65f;
        if (sizeScalePosition >= 1f)
        {
            sizeScalePosition = 1f;
            sizeScaleDirection = -1f;
        }
        else if (sizeScalePosition <= 0f)
        {
            sizeScalePosition = 0f;
            sizeScaleDirection = 1f;
        }

        sizeScaleIndicator.anchorMin = new Vector2(sizeScalePosition, 0.5f);
        sizeScaleIndicator.anchorMax = new Vector2(sizeScalePosition, 0.5f);
        sizeScaleIndicator.anchoredPosition = Vector2.zero;
    }

    private void SelectScalePosition()
    {
        if (activePrepOrder == null || activePrepOrder.SizeGame != SizeGameType.FillScale)
        {
            return;
        }

        // After a miss the same button restarts the indicator.
        if (!sizeScaleRunning)
        {
            SetSizeScaleRunning(true);
            RefreshSizeTab();
            return;
        }

        int selectedIndex = Mathf.Clamp(Mathf.FloorToInt(sizeScalePosition * 3f), 0, 2);
        if (CakeSizeValues[selectedIndex] != activePrepOrder.RequiredCakeSize)
        {
            // The indicator stays where it was stopped so the player sees the miss, like the oven.
            SetSizeScaleRunning(false);
            RefreshSizeTab();
            return;
        }

        HandleCakeSizeSelected(CakeSizeValues[selectedIndex]);
    }

    private void SetSizeScaleRunning(bool running)
    {
        sizeScaleRunning = running;
        if (sizeScaleSelectLabel != null)
        {
            sizeScaleSelectLabel.text = running ? "Seleccionar" : "Reintentar";
        }
    }

    private void BuildPlacementTab(RectTransform panel)
    {
        if (panel == null)
        {
            return;
        }

        ClearChildren(panel);
        Text title = CreateText("Instruction", panel, "Cubre la sombra con el objeto: muevelo, giralo y ajusta su tamano.", 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(820f, 40f), new Vector2(0.5f, 0.5f));

        placementPlayArea = CreateRect("PlacementPlayArea", panel);
        SetRect(placementPlayArea, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), new Vector2(0f, 0f), PlacementPlayAreaSize, new Vector2(0.5f, 0.5f));
        Image playAreaImage = placementPlayArea.gameObject.AddComponent<Image>();
        playAreaImage.color = new Color(0.97f, 0.91f, 0.82f, 1f);
        Outline playAreaOutline = placementPlayArea.gameObject.AddComponent<Outline>();
        playAreaOutline.effectColor = new Color(0.55f, 0.30f, 0.22f, 1f);
        playAreaOutline.effectDistance = new Vector2(2f, -2f);

        // Draw order: hole in the cake, the item's shadow inside it, then the draggable item on top.
        placementHoleImage = CreateImage("PlacementHole", placementPlayArea, Color.white);
        placementHoleImage.raycastTarget = false;
        placementHoleRect = placementHoleImage.rectTransform;

        placementShadowImage = CreateImage("PlacementTargetShadow", placementPlayArea, new Color(0.10f, 0.05f, 0.03f, 0.85f));
        placementShadowImage.preserveAspect = true;
        placementShadowImage.raycastTarget = false;
        placementTargetRect = placementShadowImage.rectTransform;

        placementPieceImage = CreateImage("PlacementPiece", placementPlayArea, Color.white);
        placementPieceImage.preserveAspect = true;
        placementPieceRect = placementPieceImage.rectTransform;
        placementPieceView = placementPieceRect.gameObject.AddComponent<ShapePlacementPieceView>();
        placementPieceView.Initialize(placementPlayArea);
        placementPieceView.Changed += HandlePlacementPieceChanged;

        const float controlsY = 106f;
        placementRotateLeftButton = CreatePlacementIconButton("RotateLeftButton", panel, PlacementIcon.RotateLeft, new Vector2(-360f, controlsY));
        placementRotateLeftButton.onClick.AddListener(() => RotatePlacementPiece(PlacementRotationStep));

        placementRotateRightButton = CreatePlacementIconButton("RotateRightButton", panel, PlacementIcon.RotateRight, new Vector2(-266f, controlsY));
        placementRotateRightButton.onClick.AddListener(() => RotatePlacementPiece(-PlacementRotationStep));

        placementScaleDownButton = CreatePlacementIconButton("ScaleDownButton", panel, PlacementIcon.ZoomOut, new Vector2(-146f, controlsY));
        placementScaleDownButton.onClick.AddListener(() => ScalePlacementPiece(-PlacementScaleStep));
        ButtonSfx.Attach(placementScaleDownButton, SfxId.KitchenUiClick);

        placementScaleUpButton = CreatePlacementIconButton("ScaleUpButton", panel, PlacementIcon.ZoomIn, new Vector2(-52f, controlsY));
        placementScaleUpButton.onClick.AddListener(() => ScalePlacementPiece(PlacementScaleStep));
        ButtonSfx.Attach(placementScaleUpButton, SfxId.KitchenUiClick);

        placementConfirmButton = CreateButton("ConfirmPlacementButton", panel, Color.white);
        StylePlacementButton(placementConfirmButton, placementConfirmButtonSprite, true);
        SetRect(placementConfirmButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(230f, controlsY), new Vector2(320f, 84f), new Vector2(0.5f, 0.5f));
        Text confirmLabel = CreateText("Label", placementConfirmButton.GetComponent<RectTransform>(), "Verificar encaje", 32, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetRect(confirmLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 3f), new Vector2(-40f, -20f), new Vector2(0.5f, 0.5f));
        placementConfirmButton.onClick.AddListener(ConfirmPlacement);
        ButtonSfx.Attach(placementConfirmButton, SfxId.KitchenUiClick);

        placementStatusText = CreateText("PlacementStatusText", panel, "Aun no has colocado el objeto.", 19, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetRect(placementStatusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(820f, 24f), new Vector2(0.5f, 0.5f));

        placementScoreText = CreateText("PlacementScoreText", panel, "Precision: 0%", 18, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(placementScoreText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(400f, 22f), new Vector2(0.5f, 0.5f));
    }

    private Button CreatePlacementIconButton(string objectName, RectTransform panel, PlacementIcon icon, Vector2 position)
    {
        Button button = CreateButton(objectName, panel, Color.white);
        StylePlacementButton(button, placementIconButtonSprite, false);
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        SetRect(buttonRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), position, new Vector2(80f, 84f), new Vector2(0.5f, 0.5f));

        Image iconImage = CreateImage("Icon", buttonRect, Color.white);
        iconImage.sprite = GetPlacementIconSprite(icon);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        SetRect(iconImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(54f, 54f), new Vector2(0.5f, 0.5f));
        return button;
    }

    private static void StylePlacementButton(Button button, Sprite sprite, bool sliced)
    {
        if (sprite == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.preserveAspect = !sliced;

        // The sprite already has its own border, so the generic black outline would double it.
        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
        {
            if (Application.isPlaying)
            {
                Destroy(outline);
            }
            else
            {
                DestroyImmediate(outline);
            }
        }

        if (button.GetComponent<CursorChange>() == null)
        {
            button.gameObject.AddComponent<CursorChange>();
        }
    }

    private void BuildBakeTab(RectTransform panel)
    {
        if (panel == null)
        {
            return;
        }

        ClearChildren(panel);
        Text title = CreateText("Instruction", panel, "Apaga el horno con OFF cuando la linea pase por el area verde.", 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(820f, 40f), new Vector2(0.5f, 0.5f));

        bakeOvenImage = CreateImage("Oven", panel, Color.white);
        bakeOvenImage.sprite = ovenOffSprite;
        bakeOvenImage.preserveAspect = true;
        bakeOvenImage.raycastTarget = false;
        SetRect(bakeOvenImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), OvenDisplaySize, new Vector2(0.5f, 0.5f));
        RectTransform ovenRect = bakeOvenImage.rectTransform;

        // The bar sits on the oven front, right under the glass.
        RectTransform gaugeRoot = CreateRect("BakeGaugeRoot", ovenRect);
        PlaceOnOven(gaugeRoot, new Vector2(201f, 244f), new Vector2(340f, 22f));
        Image gaugeBackground = gaugeRoot.gameObject.AddComponent<Image>();
        gaugeBackground.color = new Color(0.26f, 0.22f, 0.18f, 1f);
        gaugeBackground.raycastTarget = false;
        Outline gaugeOutline = gaugeRoot.gameObject.AddComponent<Outline>();
        gaugeOutline.effectColor = new Color(0.30f, 0.10f, 0.12f, 1f);
        gaugeOutline.effectDistance = new Vector2(2f, -2f);

        bakePerfectZoneRect = CreateRect("PerfectBakeZone", gaugeRoot);
        bakePerfectZoneRect.pivot = new Vector2(0f, 0.5f);
        Image perfectZoneImage = bakePerfectZoneRect.gameObject.AddComponent<Image>();
        perfectZoneImage.color = new Color(0.34f, 0.72f, 0.38f, 0.95f);
        perfectZoneImage.raycastTarget = false;

        bakeMarkerRect = CreateRect("BakeMarker", gaugeRoot);
        Image markerImage = bakeMarkerRect.gameObject.AddComponent<Image>();
        markerImage.color = new Color(1f, 0.96f, 0.86f, 1f);
        markerImage.raycastTarget = false;
        Outline markerOutline = bakeMarkerRect.gameObject.AddComponent<Outline>();
        markerOutline.effectColor = new Color(0.30f, 0.10f, 0.12f, 1f);
        markerOutline.effectDistance = new Vector2(1.5f, -1.5f);
        SetBakeMarkerPosition(0f);

        // ON and OFF side by side on the control panel, above the knob; ON only works to retry after a miss.
        bakeOnButton = CreateOvenButton("OnButton", ovenRect, ovenOnButtonSprite, ovenOnButtonPressedSprite, new Vector2(416f, 78f));
        bakeOnButton.onClick.AddListener(HandleOvenOn);
        bakeOffButton = CreateOvenButton("OffButton", ovenRect, ovenOffButtonSprite, ovenOffButtonPressedSprite, new Vector2(464f, 78f));
        bakeOffButton.onClick.AddListener(HandleOvenOff);

        // The two ends of the bar: raw on the left, burnt on the right.
        bakeRawCakeImage = CreateBakeSideCake("RawCake", panel, null, -485f, "Crudo");
        CreateBakeSideCake("BurntCake", panel, burntCakeSprite, 485f, "Quemado");

        bakeStatusText = CreateText("BakeStatusText", panel, string.Empty, 20, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetRect(bakeStatusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(820f, 28f), new Vector2(0.5f, 0.5f));
    }

    private Button CreateOvenButton(string objectName, RectTransform ovenRect, Sprite sprite, Sprite pressedSprite, Vector2 sourceCenter)
    {
        Button button = CreateButton(objectName, ovenRect, Color.white);
        StylePlacementButton(button, sprite, false);
        if (pressedSprite != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState spriteState = button.spriteState;
            spriteState.pressedSprite = pressedSprite;
            button.spriteState = spriteState;
        }

        PlaceOnOven(button.GetComponent<RectTransform>(), sourceCenter, new Vector2(42f, 47f));
        ButtonSfx.Attach(button, SfxId.KitchenUiClick);
        return button;
    }

    private Image CreateBakeSideCake(string objectName, RectTransform panel, Sprite sprite, float x, string label)
    {
        Image cakeImage = CreateImage(objectName, panel, Color.white);
        cakeImage.sprite = sprite;
        cakeImage.preserveAspect = true;
        cakeImage.raycastTarget = false;
        SetRect(cakeImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -120f), new Vector2(160f, 122f), new Vector2(0.5f, 0.5f));

        Text labelText = CreateText(objectName + "Label", panel, label, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -200f), new Vector2(160f, 28f), new Vector2(0.5f, 0.5f));
        return cakeImage;
    }

    // Positions a child of the oven image using coordinates measured on the oven art (origin top-left).
    private static void PlaceOnOven(RectTransform rect, Vector2 sourceCenter, Vector2 sourceSize)
    {
        float scale = OvenDisplaySize.x / OvenSourceSize.x;
        SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(sourceCenter.x, -sourceCenter.y) * scale, sourceSize * scale, new Vector2(0.5f, 0.5f));
    }

    private void BuildToppingTab(RectTransform panel)
    {
        if (panel == null)
        {
            return;
        }

        ClearChildren(panel);
        Text title = CreateText("Instruction", panel, "Elige el topping correcto para terminar el pedido.", 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(640f, 40f), new Vector2(0.5f, 0.5f));

        toppingOptionButtons = new Button[3];
        toppingOptionBackgrounds = new Image[3];
        toppingOptionLabelTexts = new Text[3];
        toppingOptionCakeImages = new Image[3];
        float[] xPositions = { -240f, 0f, 240f };

        for (int i = 0; i < 3; i++)
        {
            Button button = CreateButton("ToppingOption_" + i, panel, new Color(0.97f, 0.96f, 0.93f, 1f));
            StylePlacementButton(button, toppingSlotSprite, true);
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            SetRect(buttonRect, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), new Vector2(xPositions[i], -10f), new Vector2(200f, 216f), new Vector2(0.5f, 0.5f));

            Image cakeImage = CreateImage("CakeSlice", buttonRect, Color.white);
            cakeImage.preserveAspect = true;
            cakeImage.raycastTarget = false;
            SetRect(cakeImage.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(130f, 120f), new Vector2(0.5f, 0.5f));

            Text label = CreateText("ToppingLabel", buttonRect, "--", 26, FontStyle.Normal, TextAnchor.MiddleCenter);
            label.color = toppingSlotSprite != null ? Color.white : Color.black;
            SetRect(label.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), Vector2.zero, new Vector2(170f, 44f), new Vector2(0.5f, 0.5f));

            int capturedIndex = i;
            button.onClick.AddListener(() => HandleToppingSelected(capturedIndex));
            ButtonSfx.Attach(button, SfxId.KitchenUiClick);

            toppingOptionButtons[i] = button;
            toppingOptionBackgrounds[i] = button.GetComponent<Image>();
            toppingOptionLabelTexts[i] = label;
            toppingOptionCakeImages[i] = cakeImage;
        }

        toppingFeedbackText = CreateText("ToppingFeedbackText", panel, "Selecciona el topping correcto.", 20, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetRect(toppingFeedbackText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(560f, 28f), new Vector2(0.5f, 0.5f));
    }

    private void LoadOrdersFromJson()
    {
        allOrders.Clear();
        spawnedOrders.Clear();
        selectedOrder = null;
        activePrepOrder = null;

        if (ordersJson == null)
        {
            Debug.LogError("No json orders provided");
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
            OrderDefinition definition = collection.orders[i];
            if (definition == null)
            {
                continue;
            }

            allOrders.Add(CreateRuntimeOrder(definition, i));
        }

        if (logOrderBoard)
        {
            Debug.Log("Loaded " + allOrders.Count + " orders from JSON.");
        }
    }

    private RuntimeOrder CreateRuntimeOrder(OrderDefinition definition, int runtimeIndex)
    {
        float targetScale = PlacementTargetScales[UnityEngine.Random.Range(0, PlacementTargetScales.Length)];
        float holeHalfSize = PlacementPieceBoxSize * targetScale * PlacementHoleSizeFactor * 0.5f;
        return new RuntimeOrder
        {
            RuntimeIndex = runtimeIndex,
            Data = definition,
            State = OrderRuntimeState.Pending,
            RequiredCakeSize = ParseCakeSize(definition != null ? definition.cakeSize : null),
            HoleShape = (PlacementHoleShape)UnityEngine.Random.Range(0, Enum.GetValues(typeof(PlacementHoleShape)).Length),
            HoleRotationDegrees = UnityEngine.Random.Range(0f, 360f),
            BasePayoutValue = definition != null && definition.basePayout > 0 ? definition.basePayout : ParsePayoutValue(definition != null ? definition.payout : null),
            // The hole always sits on the right half so the piece can start on the left without overlapping it.
            TargetAnchoredPosition = new Vector2(
                UnityEngine.Random.Range(40f, PlacementPlayAreaSize.x * 0.5f - holeHalfSize - 10f),
                UnityEngine.Random.Range(-(PlacementPlayAreaSize.y * 0.5f - holeHalfSize - 10f), PlacementPlayAreaSize.y * 0.5f - holeHalfSize - 10f)),
            TargetRotationDegrees = UnityEngine.Random.Range(0, 24) * PlacementRotationStep,
            TargetScale = targetScale,
            FlavorCarouselIndex = GetCarouselStartIndex(definition != null ? definition.cakeFlavor : null)
        };
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
        runtimeOrder.CardView = cardView;
        SetCardItemIcon(runtimeOrder);
        spawnedOrders.Add(runtimeOrder);

        if (logOrderBoard)
        {
            Debug.Log("Spawned order " + runtimeOrder.RuntimeIndex + " for cell " + runtimeOrder.Data.cellNumber + ". Visible cards=" + spawnedOrders.Count);
        }

        RefreshCardStates();
    }

    private void RepositionOrderCards()
    {
        for (int i = 0; i < spawnedOrders.Count; i++)
        {
            RuntimeOrder order = spawnedOrders[i];
            if (order != null && order.CardView != null)
            {
                ConfigureCardTransform(order.CardView.GetComponent<RectTransform>(), i);
            }
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

    private bool CanSelectOrderFromBoard(RuntimeOrder order)
    {
        if (order == null || !IsOpen)
        {
            return false;
        }

        if (order.State == OrderRuntimeState.Completed)
        {
            return false;
        }

        if (activePrepOrder != null && order != activePrepOrder)
        {
            return false;
        }

        return true;
    }

    private void SelectAndBeginPrep(RuntimeOrder order)
    {
        bool wasAlreadyActive = order != null && order == activePrepOrder;
        selectedOrder = order;

        if (order != null && order.State == OrderRuntimeState.Pending && activePrepOrder == null && !IsInventoryFull())
        {
            activePrepOrder = order;
            activePrepOrder.State = OrderRuntimeState.ActivePrep;
        AudioManager.Play(SfxId.OrderActivate);
            RefreshAllUi();
            ActivateTab(FlavorTabIndex, true);
            return;
        }

        RefreshAllUi();

        if (wasAlreadyActive)
        {
            ActivateTab(GetFirstAvailableTabIndex(activePrepOrder), true);
        }
    }

    private void HandleCakeSizeSelected(CakeSizeOption selectedSize)
    {
        if (activePrepOrder == null || activePrepOrder.State != OrderRuntimeState.ActivePrep)
        {
            return;
        }

        if (activePrepOrder.SizeGameCompleted || IsSizeChosen(activePrepOrder))
        {
            return;
        }

        bool correct = selectedSize == activePrepOrder.RequiredCakeSize;
        if (activePrepOrder.SizeGame == SizeGameType.SortCakes && !sizeSortValidated)
        {
            ShowSizeGameFeedback("Ordena los pasteles de menor a mayor.");
            return;
        }

        if (!correct)
        {
            ShowSizeGameFeedback("Este no es el tamano adecuado.");
            return;
        }

        activePrepOrder.SelectedCakeSize = selectedSize;
        activePrepOrder.SizeMatched = true;
        activePrepOrder.SizeGameCompleted = true;
        sizeScaleRunning = false;
        ShowSizeGameFeedback("Tamano correcto.");
        RefreshAllUi();
        ActivateTab(PlacementTabIndex, true);
    }

    private void ShowSizeGameFeedback(string message)
    {
        if (sizeFeedbackText != null)
        {
            sizeFeedbackText.text = message;
        }
    }

    private static bool IsSizeChosen(RuntimeOrder order)
    {
        return order != null && order.SelectedCakeSize != CakeSizeOption.None;
    }

    private void RotatePlacementPiece(float deltaDegrees)
    {
        if (placementPieceView == null || activePrepOrder == null || !IsSizeChosen(activePrepOrder) || activePrepOrder.PlacementCompleted)
        {
            return;
        }

        // Los botones de rotar no llevan click de UI: el engranaje suena una vez por cada rotacion real.
        if (placementPieceView.RotateBy(deltaDegrees))
        {
            // Delta negativo = boton de rotar a la derecha.
            AudioManager.PlayPieceRotate(deltaDegrees < 0f);
        }
    }

    private void ScalePlacementPiece(float deltaScale)
    {
        if (placementPieceRect == null || activePrepOrder == null || !IsSizeChosen(activePrepOrder) || activePrepOrder.PlacementCompleted)
        {
            return;
        }

        // Rounded to the step so the target scales stay exactly reachable.
        float nextScale = Mathf.Round((activePrepOrder.PieceScale + deltaScale) / PlacementScaleStep) * PlacementScaleStep;
        activePrepOrder.PieceScale = Mathf.Clamp(nextScale, PlacementScaleMin, PlacementScaleMax);
        placementPieceRect.localScale = new Vector3(activePrepOrder.PieceScale, activePrepOrder.PieceScale, 1f);
        HandlePlacementPieceChanged();
    }

    private void HandlePlacementPieceChanged()
    {
        if (activePrepOrder == null || placementPieceRect == null)
        {
            return;
        }

        activePrepOrder.PieceAnchoredPosition = placementPieceRect.anchoredPosition;
        activePrepOrder.PieceRotationDegrees = NormalizeAngle(placementPieceRect.localEulerAngles.z);
        activePrepOrder.PiecePoseInitialized = true;
        placementScoreText.text = "Precision: " + Mathf.RoundToInt(GetPlacementScore(activePrepOrder) * 100f) + "%";
    }

    private void ConfirmPlacement()
    {
        if (activePrepOrder == null || !IsSizeChosen(activePrepOrder) || activePrepOrder.PlacementCompleted)
        {
            return;
        }

        float score = GetPlacementScore(activePrepOrder);
        float distance = Vector2.Distance(activePrepOrder.PieceAnchoredPosition, activePrepOrder.TargetAnchoredPosition);
        float angleDelta = Mathf.Abs(Mathf.DeltaAngle(activePrepOrder.PieceRotationDegrees, activePrepOrder.TargetRotationDegrees));
        float scaleDelta = Mathf.Abs(activePrepOrder.PieceScale - activePrepOrder.TargetScale);

        // A loose fit is allowed; it only lowers the payout when the cake is finished.
        activePrepOrder.PlacementCorrect = distance <= PlacementDistanceThreshold && angleDelta <= PlacementAngleThreshold && scaleDelta <= PlacementScaleThreshold;
        activePrepOrder.PlacementCompleted = true;
        activePrepOrder.PlacementScore = score;
        // The oven turns on by itself; ON is only needed to retry after a miss.
        BeginBakeAttempt(activePrepOrder);
        RefreshAllUi();
        ActivateTab(BakeTabIndex, true);
    }

    private void HandleOvenOff()
    {
        if (activePrepOrder == null || !activePrepOrder.BakeInProgress)
        {
            return;
        }

        ResolveBake(activePrepOrder);
    }

    private void HandleOvenOn()
    {
        if (activePrepOrder == null || !activePrepOrder.PlacementCompleted || activePrepOrder.BakeInProgress || activePrepOrder.BakeOutcome != BakeResult.None)
        {
            return;
        }

        BeginBakeAttempt(activePrepOrder);
        RefreshBakeUi();
    }

    private void BeginBakeAttempt(RuntimeOrder order)
    {
        if (order == null)
        {
            return;
        }

        order.BakeOutcome = BakeResult.None;
        order.LastBakeMiss = BakeResult.None;
        order.BakeInProgress = true;
        order.BakeProgressNormalized = 0f;
        order.BakeDirection = 1f;
        // Keep the zone the player already sees so it never jumps when the bar starts.
        EnsurePerfectBakeWindow(order);
    }

    private void EnsurePerfectBakeWindow(RuntimeOrder order)
    {
        if (order == null)
        {
            return;
        }

        if (order.PerfectBakeWindowEnd > order.PerfectBakeWindowStart)
        {
            return;
        }

        float center = UnityEngine.Random.Range(PerfectBakeWindowMinCenter, PerfectBakeWindowMaxCenter);
        float halfWidth = PerfectBakeWindowWidth * 0.5f;
        order.PerfectBakeWindowStart = Mathf.Clamp01(center - halfWidth);
        order.PerfectBakeWindowEnd = Mathf.Clamp01(center + halfWidth);

        if (order.PerfectBakeWindowEnd - order.PerfectBakeWindowStart < PerfectBakeWindowWidth)
        {
            order.PerfectBakeWindowEnd = Mathf.Clamp01(order.PerfectBakeWindowStart + PerfectBakeWindowWidth);
            order.PerfectBakeWindowStart = Mathf.Clamp01(order.PerfectBakeWindowEnd - PerfectBakeWindowWidth);
        }
    }

    private void RefreshPerfectBakeZoneUi(RuntimeOrder order)
    {
        if (bakePerfectZoneRect == null)
        {
            return;
        }

        if (order == null)
        {
            bakePerfectZoneRect.gameObject.SetActive(false);
            return;
        }

        EnsurePerfectBakeWindow(order);
        bakePerfectZoneRect.gameObject.SetActive(true);
        bakePerfectZoneRect.anchorMin = new Vector2(order.PerfectBakeWindowStart, 0f);
        bakePerfectZoneRect.anchorMax = new Vector2(order.PerfectBakeWindowEnd, 1f);
        bakePerfectZoneRect.offsetMin = Vector2.zero;
        bakePerfectZoneRect.offsetMax = Vector2.zero;
    }

    private void HandleToppingSelected(int optionIndex)
    {
        if (activePrepOrder == null || activePrepOrder.BakeOutcome == BakeResult.None || activePrepOrder.ToppingOptions == null)
        {
            return;
        }

        if (optionIndex < 0 || optionIndex >= activePrepOrder.ToppingOptions.Length)
        {
            return;
        }

        // A wrong topping is allowed; it only lowers the payout.
        string selectedTopping = activePrepOrder.ToppingOptions[optionIndex];
        activePrepOrder.SelectedTopping = selectedTopping;
        activePrepOrder.ToppingMatched = string.Equals(selectedTopping, activePrepOrder.Data.topping, StringComparison.OrdinalIgnoreCase);
        activePrepOrder.FinalPayout = CalculateFinalPayout(activePrepOrder);

        if (!AddCakeToInventory(activePrepOrder))
        {
            activePrepOrder.SelectedTopping = null;
            RefreshToppingUi();
            toppingFeedbackText.text = "Tu inventario esta lleno. Entrega un pastel antes de terminar este.";
            return;
        }

        RuntimeOrder finishedOrder = activePrepOrder;
        finishedOrder.CakeGenerated = true;
        finishedOrder.State = OrderRuntimeState.Completed;

        activePrepOrder = null;
        selectedOrder = null;
        Close();
        // The cake now lives in the inventory with its cell, so the order card would only be clutter.
        RemoveOrder(finishedOrder.Data.id);
        int cakeMistakes = GetCakeMistakes(finishedOrder).Count;
        prepMistakeCount += cakeMistakes;
        if (cakeMistakes == 0)
        {
            perfectCakeCount++;
        }

        ShowInventoryMessage(GetFinishedCakeMessage(finishedOrder));
    }

    private void SetSelectedOrder(RuntimeOrder order, bool forceUnlockedTabs)
    {
        selectedOrder = order;
        RefreshAllUi();

        if (forceUnlockedTabs && activePrepOrder != null)
        {
            ActivateTab(GetFirstAvailableTabIndex(activePrepOrder), true);
        }
    }

    private void RefreshAllUi()
    {
        RefreshCardStates();
        RefreshDetailPanel();
        RefreshStageVisibility();
        RefreshTabAvailability();
        RefreshFlavorUi();
        RefreshSizeTab();
        RefreshPlacementUi();
        RefreshBakeUi();
        RefreshToppingUi();
    }

    private void RefreshCardStates()
    {
        // A finished cake needs a free slot, so no new order may start while the inventory is full.
        bool blockedByInventory = activePrepOrder == null && IsInventoryFull();
        for (int i = 0; i < spawnedOrders.Count; i++)
        {
            RuntimeOrder runtimeOrder = spawnedOrders[i];
            if (runtimeOrder.CardView == null)
            {
                continue;
            }

            bool isSelected = runtimeOrder == selectedOrder;
            bool isActive = runtimeOrder == activePrepOrder || runtimeOrder.State == OrderRuntimeState.ActivePrep;
            bool isCompleted = runtimeOrder.State == OrderRuntimeState.Completed;
            bool isInteractable = IsOpen && !blockedByInventory && runtimeOrder.State == OrderRuntimeState.Pending && (activePrepOrder == null || activePrepOrder == runtimeOrder);

            runtimeOrder.CardView.SetSelected(isSelected);
            runtimeOrder.CardView.SetActiveState(isActive);
            runtimeOrder.CardView.SetCompletedState(isCompleted);
            runtimeOrder.CardView.SetInteractable(isInteractable);

            runtimeOrder.CardView.SetStatus(string.Empty, false);
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
            if (detailTitleText != null) detailTitleText.text = string.Empty;
            if (detailCakeColorText != null) detailCakeColorText.text = string.Empty;
            if (detailToppingText != null) detailToppingText.text = string.Empty;
            if (detailItemLabelText != null) detailItemLabelText.text = string.Empty;
            if (detailPayoutText != null) detailPayoutText.text = string.Empty;
            if (detailCellText != null) detailCellText.text = string.Empty;
            if (detailStatusText != null) detailStatusText.text = string.Empty;
            RefreshDetailItemIcon(null);
            if (dragHintOverlay != null) dragHintOverlay.SetActive(true);
            StartDragHintPulse();
            return;
        }

        StopDragHintPulse();
        if (dragHintOverlay != null) dragHintOverlay.SetActive(false);

        OrderDefinition order = selectedOrder.Data;
        if (detailTitleText != null) detailTitleText.text = order.orderTitle;
        // The whole recipe lives in one text block so a wrapped long value pushes the next line down instead of overlapping it.
        if (detailCakeColorText != null) detailCakeColorText.text = "Sabor: " + order.cakeFlavor + "\nTamano: " + GetCakeSizeLabel(selectedOrder.RequiredCakeSize) + "\nTopping: " + order.topping;
        if (detailToppingText != null) detailToppingText.text = string.Empty;
        if (detailItemLabelText != null) detailItemLabelText.text = "Objeto a colocar:";
        RefreshDetailItemIcon(order.contrabandItem);
        if (detailPayoutText != null) detailPayoutText.text = "Paga: Q" + GetDisplayPayout(selectedOrder);
        if (detailCellText != null) detailCellText.text = "Celda: " + order.cellNumber;
        if (detailStatusText != null)
        {
            detailStatusText.text = string.Empty;
            detailStatusText.gameObject.SetActive(false);
        }
    }

    private void StartDragHintPulse()
    {
        if (dragHintPulseRoutine != null || dragHintPointer == null || !gameObject.activeInHierarchy)
        {
            return;
        }

        dragHintPulseRoutine = StartCoroutine(DragHintPulseRoutine());
    }

    private void StopDragHintPulse()
    {
        if (dragHintPulseRoutine != null)
        {
            StopCoroutine(dragHintPulseRoutine);
            dragHintPulseRoutine = null;
        }

        if (dragHintPointer != null)
        {
            dragHintPointer.localScale = Vector3.one;
        }
    }

    private IEnumerator DragHintPulseRoutine()
    {
        RectTransform pointerRect = dragHintPointer;
        const float pulseSpeed = 2.2f;
        const float minScale = 0.85f;
        const float maxScale = 1.15f;

        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            float scale = Mathf.Lerp(minScale, maxScale, t);
            pointerRect.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
    }

    private void RefreshTabAvailability()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null)
            {
                continue;
            }

            bool available = IsTabAvailable(i);
            tabButtons[i].interactable = available;

            if (tabBackgrounds != null && i < tabBackgrounds.Length && tabBackgrounds[i] != null)
            {
                if (i == activeTabIndex)
                {
                    tabBackgrounds[i].color = available ? tabActiveColor : tabLockedColor;
                }
                else
                {
                    tabBackgrounds[i].color = available ? tabIdleColor : tabLockedColor;
                }
            }
        }
    }

    private void RefreshSizeTab()
    {
        if (sizeOptionButtons == null || sizeFeedbackText == null)
        {
            return;
        }

        bool sizeTabAvailable = activePrepOrder != null && activePrepOrder.State == OrderRuntimeState.ActivePrep;
        for (int i = 0; i < sizeOptionButtons.Length; i++)
        {
            bool isSelected = activePrepOrder != null && activePrepOrder.SelectedCakeSize == CakeSizeValues[i];
            sizeOptionButtons[i].interactable = sizeTabAvailable && !IsSizeChosen(activePrepOrder);
            if (sizeOptionBackgrounds != null && i < sizeOptionBackgrounds.Length && sizeOptionBackgrounds[i] != null)
            {
                sizeOptionBackgrounds[i].color = isSelected ? tabActiveColor : tabIdleColor;
            }
        }

        sizeFeedbackText.color = Color.black;
        if (activePrepOrder == null)
        {
            sizeFeedbackText.text = "Bloqueado hasta seleccionar una orden.";
            return;
        }

        if (IsSizeChosen(activePrepOrder))
        {
            sizeFeedbackText.text = "Tamano elegido: " + GetCakeSizeLabel(activePrepOrder.SelectedCakeSize) + ".";
            return;
        }

        // The cakes take the chosen flavor's color, so the minigame waits for the flavor.
        if (!IsFlavorChosen(activePrepOrder))
        {
            if (sizeGameRoot != null && sizeGameBuiltFor != activePrepOrder)
            {
                ClearChildren(sizeGameRoot);
                sizeGamePieces.Clear();
                sizeScaleRunning = false;
                sizeGameBuiltFor = null;
            }

            sizeFeedbackText.text = "Primero elige el sabor del pastel.";
            return;
        }

        EnsureSizeGameAssigned(activePrepOrder);
        BuildSizeGame(activePrepOrder);
        if (activePrepOrder.SizeGame == SizeGameType.CorrectMold)
        {
            sizeFeedbackText.text = "Arrastra el molde correcto hacia la zona de preparacion.";
        }
        else if (activePrepOrder.SizeGame == SizeGameType.SortCakes)
        {
            sizeFeedbackText.text = sizeSortValidated ? "Orden correcto. Selecciona el tamano solicitado." : "Ordena los pasteles de menor a mayor.";
        }
        else if (sizeScaleRunning)
        {
            sizeFeedbackText.text = "Deten el indicador en la zona del tamano solicitado.";
        }
        else
        {
            sizeFeedbackText.text = "Ese no es el tamano solicitado. Presiona Reintentar.";
            sizeFeedbackText.color = new Color(0.62f, 0.14f, 0.10f, 1f);
        }
    }

    private void RefreshPlacementUi()
    {
        if (placementPlayArea == null || placementTargetRect == null || placementPieceRect == null || placementPieceView == null)
        {
            return;
        }

        bool hasActiveOrder = activePrepOrder != null;
        bool placementAvailable = hasActiveOrder && IsSizeChosen(activePrepOrder);
        bool canEdit = placementAvailable && !activePrepOrder.PlacementCompleted;
        placementPieceView.SetInteractable(canEdit);
        placementConfirmButton.interactable = canEdit;
        placementRotateLeftButton.interactable = canEdit;
        placementRotateRightButton.interactable = canEdit;
        placementScaleDownButton.interactable = canEdit;
        placementScaleUpButton.interactable = canEdit;

        if (!hasActiveOrder)
        {
            placementHoleRect.gameObject.SetActive(false);
            placementTargetRect.gameObject.SetActive(false);
            placementPieceRect.gameObject.SetActive(false);
            placementStatusText.text = "Bloqueado hasta confirmar el tamano.";
            placementScoreText.text = "Precision: 0%";
            return;
        }

        placementHoleRect.gameObject.SetActive(true);
        placementTargetRect.gameObject.SetActive(true);
        placementPieceRect.gameObject.SetActive(true);
        ConfigurePlacementRuntimeOrder(activePrepOrder);

        if (activePrepOrder.PlacementCompleted)
        {
            placementStatusText.text = activePrepOrder.PlacementCorrect
                ? "Objeto oculto correctamente dentro del pastel."
                : "El objeto quedo mal colocado. Te pagaran menos por este pastel.";
            placementScoreText.text = "Precision: " + Mathf.RoundToInt(activePrepOrder.PlacementScore * 100f) + "%";
        }
        else if (!IsSizeChosen(activePrepOrder))
        {
            placementStatusText.text = "Primero elige el tamano del pastel.";
            placementScoreText.text = "Precision: 0%";
        }
        else
        {
            placementStatusText.text = "Arrastra el objeto y usa los botones para girarlo y cambiar su tamano.";
            placementScoreText.text = "Precision: " + Mathf.RoundToInt(GetPlacementScore(activePrepOrder) * 100f) + "%";
        }
    }

    private void RefreshBakeUi()
    {
        if (bakeStatusText == null || bakeOvenImage == null || bakeOffButton == null || bakeOnButton == null)
        {
            return;
        }

        bool baking = activePrepOrder != null && activePrepOrder.BakeInProgress;
        bakeOvenImage.sprite = baking && ovenOnSprite != null ? ovenOnSprite : ovenOffSprite;
        if (bakeRawCakeImage != null)
        {
            bakeRawCakeImage.sprite = GetSizeSprite(2);
        }

        bool canRetry = activePrepOrder != null && activePrepOrder.PlacementCompleted && !baking && activePrepOrder.BakeOutcome == BakeResult.None;
        SetOvenButtonInteractable(bakeOffButton, baking);
        // Both stay visible as a pair; each one is dimmed while it can't be used.
        SetOvenButtonInteractable(bakeOnButton, canRetry);
        bakeStatusText.color = Color.black;

        if (activePrepOrder == null)
        {
            bakeStatusText.text = "Bloqueado hasta seleccionar una orden.";
            SetBakeMarkerPosition(0f);
            RefreshPerfectBakeZoneUi(null);
            return;
        }

        RefreshPerfectBakeZoneUi(activePrepOrder);
        SetBakeMarkerPosition(activePrepOrder.BakeProgressNormalized);

        if (!activePrepOrder.PlacementCompleted)
        {
            bakeStatusText.text = "Primero oculta el objeto dentro del pastel.";
        }
        else if (baking)
        {
            bakeStatusText.text = "Presiona OFF cuando la linea este dentro del area verde.";
        }
        else if (activePrepOrder.BakeOutcome == BakeResult.Perfect)
        {
            bakeStatusText.text = "Perfecto. Ya puedes pasar al topping.";
        }
        else
        {
            bakeStatusText.text = (activePrepOrder.LastBakeMiss == BakeResult.Undercooked ? "Quedo crudo." : "Se quemo.") + " Presiona ON para intentarlo otra vez.";
            bakeStatusText.color = new Color(0.62f, 0.14f, 0.10f, 1f);
        }
    }

    private static void SetOvenButtonInteractable(Button button, bool interactable)
    {
        button.interactable = interactable;
        // SpriteSwap has no disabled tint, so a locked button is dimmed by hand.
        button.image.color = interactable ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
    }

    private void RefreshToppingUi()
    {
        if (toppingOptionButtons == null || toppingFeedbackText == null)
        {
            return;
        }

        if (activePrepOrder == null)
        {
            toppingFeedbackText.text = "Bloqueado hasta terminar de hornear.";
            SetToppingButtonsInteractable(false);
            return;
        }

        EnsureToppingOptions(activePrepOrder);
        bool toppingAvailable = activePrepOrder.BakeOutcome != BakeResult.None && !activePrepOrder.BakeInProgress;
        SetToppingButtonsInteractable(toppingAvailable);

        for (int i = 0; i < toppingOptionButtons.Length; i++)
        {
            string option = activePrepOrder.ToppingOptions != null && i < activePrepOrder.ToppingOptions.Length
                ? activePrepOrder.ToppingOptions[i]
                : null;

            if (toppingOptionLabelTexts != null && i < toppingOptionLabelTexts.Length && toppingOptionLabelTexts[i] != null)
            {
                toppingOptionLabelTexts[i].text = option ?? "--";
            }

            if (toppingOptionCakeImages != null && i < toppingOptionCakeImages.Length && toppingOptionCakeImages[i] != null)
            {
                Sprite cakeIcon = GetToppingCakeIcon(option);
                toppingOptionCakeImages[i].sprite = cakeIcon;
                toppingOptionCakeImages[i].enabled = cakeIcon != null;
            }

            if (toppingOptionBackgrounds != null && i < toppingOptionBackgrounds.Length && toppingOptionBackgrounds[i] != null)
            {
                bool isSelected = option != null && string.Equals(activePrepOrder.SelectedTopping, option, StringComparison.OrdinalIgnoreCase);
                Sprite slotSprite = isSelected && toppingSelectedSlotSprite != null ? toppingSelectedSlotSprite : toppingSlotSprite;
                if (slotSprite != null)
                {
                    toppingOptionBackgrounds[i].sprite = slotSprite;
                    toppingOptionBackgrounds[i].color = Color.white;
                }
                else
                {
                    toppingOptionBackgrounds[i].color = isSelected ? tabActiveColor : tabIdleColor;
                }
            }
        }

        toppingFeedbackText.text = toppingAvailable
            ? "Elige el topping pedido por el preso. El pastel ira a tu inventario."
            : "Termina de hornear antes de agregar el topping.";
    }

    private void ActivateTab(int tabIndex, bool force)
    {
        if (tabPanels == null || tabPanels.Length == 0)
        {
            return;
        }

        int clampedIndex = Mathf.Clamp(tabIndex, 0, tabPanels.Length - 1);
        if (!force && !IsTabAvailable(clampedIndex))
        {
            return;
        }

        activeTabIndex = clampedIndex;

        for (int i = 0; i < tabPanels.Length; i++)
        {
            if (tabPanels[i] != null)
            {
                tabPanels[i].SetActive(i == activeTabIndex);
            }
        }

        if (centerStageTitle != null && activeTabIndex >= 0 && activeTabIndex < tabTitles.Length)
        {
            centerStageTitle.text = tabTitles[activeTabIndex];
        }

        RefreshTabAvailability();
    }

    private bool IsTabAvailable(int tabIndex)
    {
        if (activePrepOrder == null || activePrepOrder.State != OrderRuntimeState.ActivePrep)
        {
            return false;
        }

        switch (tabIndex)
        {
            case FlavorTabIndex:
                return true;
            case SizeTabIndex:
                return IsFlavorChosen(activePrepOrder);
            case PlacementTabIndex:
                return IsSizeChosen(activePrepOrder);
            case BakeTabIndex:
                return activePrepOrder.PlacementCompleted;
            case ToppingTabIndex:
                return activePrepOrder.BakeOutcome != BakeResult.None;
            default:
                return false;
        }
    }

    private int GetFirstAvailableTabIndex(RuntimeOrder order)
    {
        if (order == null || order.State != OrderRuntimeState.ActivePrep || !IsFlavorChosen(order))
        {
            return FlavorTabIndex;
        }

        if (!IsSizeChosen(order))
        {
            return SizeTabIndex;
        }

        if (!order.PlacementCompleted)
        {
            return PlacementTabIndex;
        }

        if (order.BakeOutcome == BakeResult.None)
        {
            return BakeTabIndex;
        }

        return ToppingTabIndex;
    }

    private void SetMenuVisible(bool visible)
    {
        if (menuRoot != null)
        {
            menuRoot.SetActive(visible);
        }

        if (inventoryHud != null)
        {
            inventoryHud.SetMenuOpen(visible);
        }

        RefreshDetailPanel();
    }

    private void SetOrderBoardInteractable(bool interactable)
    {
        for (int i = 0; i < spawnedOrders.Count; i++)
        {
            if (spawnedOrders[i].CardView != null)
            {
                bool canUse = interactable
                    && spawnedOrders[i].State == OrderRuntimeState.Pending
                    && (activePrepOrder == null || activePrepOrder == spawnedOrders[i]);
                spawnedOrders[i].CardView.SetInteractable(canUse);
            }
        }
    }

    private void SyncBoardSummary()
    {
        if (totalMoneyText == null)
        {
            return;
        }

        totalMoneyText.text = "Q" + displayedMoney;
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

    private void UpdateBakeState()
    {
        if (!IsOpen || activePrepOrder == null || !activePrepOrder.BakeInProgress)
        {
            return;
        }

        // The marker bounces between both ends of the bar until the player presses OFF.
        float progress = activePrepOrder.BakeProgressNormalized + activePrepOrder.BakeDirection * Time.deltaTime * BakeMarkerSpeed;
        if (progress >= 1f)
        {
            progress = 2f - progress;
            activePrepOrder.BakeDirection = -1f;
        }
        else if (progress <= 0f)
        {
            progress = -progress;
            activePrepOrder.BakeDirection = 1f;
        }

        activePrepOrder.BakeProgressNormalized = Mathf.Clamp01(progress);
        SetBakeMarkerPosition(activePrepOrder.BakeProgressNormalized);
    }

    private void ResolveBake(RuntimeOrder order)
    {
        if (order == null)
        {
            return;
        }

        order.BakeInProgress = false;

        if (order.BakeProgressNormalized >= order.PerfectBakeWindowStart && order.BakeProgressNormalized <= order.PerfectBakeWindowEnd)
        {
            order.BakeOutcome = BakeResult.Perfect;
            RefreshAllUi();
            ActivateTab(ToppingTabIndex, true);
            return;
        }

        // A miss doesn't finish the cake: the player turns the oven back on with ON and tries again.
        order.LastBakeMiss = order.BakeProgressNormalized < order.PerfectBakeWindowStart ? BakeResult.Undercooked : BakeResult.Burnt;
        RefreshBakeUi();
    }

    private void ConfigurePlacementRuntimeOrder(RuntimeOrder order)
    {
        if (order == null)
        {
            return;
        }

        Sprite itemIcon = GetContrabandIcon(order.Data.contrabandItem);
        Vector2 boxSize = new Vector2(PlacementPieceBoxSize, PlacementPieceBoxSize);

        float holeSize = PlacementPieceBoxSize * order.TargetScale * PlacementHoleSizeFactor;
        placementHoleImage.sprite = GetPlacementHoleSprite(order.HoleShape);
        placementHoleRect.sizeDelta = new Vector2(holeSize, holeSize);
        placementHoleRect.anchoredPosition = order.TargetAnchoredPosition;
        placementHoleRect.localRotation = Quaternion.Euler(0f, 0f, order.HoleRotationDegrees);

        placementShadowImage.sprite = itemIcon;
        placementShadowImage.enabled = itemIcon != null;
        placementTargetRect.sizeDelta = boxSize;
        placementTargetRect.anchoredPosition = order.TargetAnchoredPosition;
        placementTargetRect.localRotation = Quaternion.Euler(0f, 0f, order.TargetRotationDegrees);
        placementTargetRect.localScale = new Vector3(order.TargetScale, order.TargetScale, 1f);

        placementPieceImage.sprite = itemIcon;
        placementPieceImage.color = itemIcon != null ? Color.white : new Color(1f, 0.96f, 0.78f, 1f);
        placementPieceRect.sizeDelta = boxSize;

        if (!order.PiecePoseInitialized)
        {
            // Start clearly off in every axis so the player has to use each control at least once.
            order.PieceAnchoredPosition = PlacementPieceStartPosition;
            order.PieceRotationDegrees = NormalizeAngle(order.TargetRotationDegrees + UnityEngine.Random.Range(3, 10) * PlacementRotationStep * (UnityEngine.Random.value < 0.5f ? -1f : 1f));
            order.PieceScale = order.TargetScale < 1f ? order.TargetScale + 0.3f : order.TargetScale - 0.3f;
            order.PiecePoseInitialized = true;
        }

        if (order.PlacementCompleted)
        {
            order.PieceAnchoredPosition = order.TargetAnchoredPosition;
            order.PieceRotationDegrees = order.TargetRotationDegrees;
            order.PieceScale = order.TargetScale;
        }

        placementPieceRect.anchoredPosition = order.PieceAnchoredPosition;
        placementPieceRect.localRotation = Quaternion.Euler(0f, 0f, order.PieceRotationDegrees);
        placementPieceRect.localScale = new Vector3(order.PieceScale, order.PieceScale, 1f);
    }

    private float GetPlacementScore(RuntimeOrder order)
    {
        if (order == null)
        {
            return 0f;
        }

        float distance = Vector2.Distance(order.PieceAnchoredPosition, order.TargetAnchoredPosition);
        float distanceScore = 1f - Mathf.Clamp01(distance / 180f);
        float rotationDelta = Mathf.Abs(Mathf.DeltaAngle(order.PieceRotationDegrees, order.TargetRotationDegrees));
        float rotationScore = 1f - Mathf.Clamp01(rotationDelta / 90f);
        float scaleScore = 1f - Mathf.Clamp01(Mathf.Abs(order.PieceScale - order.TargetScale) / 0.5f);
        return Mathf.Clamp01(distanceScore * 0.5f + rotationScore * 0.25f + scaleScore * 0.25f);
    }

    private void EnsureToppingOptions(RuntimeOrder order)
    {
        if (order == null || order.ToppingOptions != null)
        {
            return;
        }

        List<string> options = new List<string>();
        options.Add(order.Data.topping);

        for (int i = 0; i < FallbackToppings.Length && options.Count < 3; i++)
        {
            if (string.Equals(FallbackToppings[i], order.Data.topping, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            options.Add(FallbackToppings[i]);
        }

        order.ToppingOptions = options.ToArray();
    }

    private int CalculateFinalPayout(RuntimeOrder order)
    {
        if (order == null)
        {
            return 0;
        }

        float penalty = 0f;
        if (!order.FlavorMatched) penalty += wrongFlavorPenalty;
        if (!order.SizeMatched) penalty += wrongSizePenalty;
        if (!order.PlacementCorrect) penalty += badPlacementPenalty;
        if (!order.ToppingMatched) penalty += wrongToppingPenalty;

        return Mathf.Max(0, Mathf.RoundToInt(order.BasePayoutValue * (1f - Mathf.Clamp01(penalty))));
    }

    private static List<string> GetCakeMistakes(RuntimeOrder order)
    {
        List<string> mistakes = new List<string>();
        if (!order.FlavorMatched) mistakes.Add("sabor");
        if (!order.SizeMatched) mistakes.Add("tamano");
        if (!order.PlacementCorrect) mistakes.Add("objeto");
        if (!order.ToppingMatched) mistakes.Add("topping");
        return mistakes;
    }

    private static string GetFinishedCakeMessage(RuntimeOrder order)
    {
        List<string> mistakes = GetCakeMistakes(order);
        string message = "Pastel para la celda #" + order.Data.cellNumber + " listo: Q" + order.FinalPayout;
        return mistakes.Count == 0 ? message + ". Perfecto!" : message + " (errores: " + string.Join(", ", mistakes.ToArray()) + ")";
    }
    private bool AddCakeToInventory(RuntimeOrder order)
    {
        EnsurePlayerController();
        if (playerController == null)
        {
            return false;
        }

        return playerController.Inventory.TryAdd(new CakeInventoryItem
        {
            orderId = order.Data.id,
            orderTitle = order.Data.orderTitle,
            cellNumber = order.Data.cellNumber,
            payoutValue = order.FinalPayout,
            icon = GetToppingCakeIcon(order.SelectedTopping)
        });
    }

    public void ShowInventoryMessage(string message)
    {
        if (inventoryHud != null)
        {
            inventoryHud.ShowMessage(message);
        }
    }

    private void SetToppingButtonsInteractable(bool interactable)
    {
        if (toppingOptionButtons == null)
        {
            return;
        }

        for (int i = 0; i < toppingOptionButtons.Length; i++)
        {
            if (toppingOptionButtons[i] != null)
            {
                toppingOptionButtons[i].interactable = interactable;
            }
        }
    }

    private void SetBakeMarkerPosition(float normalized)
    {
        if (bakeMarkerRect == null)
        {
            return;
        }

        // Taller than the bar so the line stays visible over the green zone.
        float x = Mathf.Clamp01(normalized);
        SetRect(bakeMarkerRect, new Vector2(x, 0f), new Vector2(x, 1f), Vector2.zero, new Vector2(6f, 16f), new Vector2(0.5f, 0.5f));
    }

    private static CakeSizeOption ParseCakeSize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return CakeSizeOption.Medium;
        }

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.StartsWith("small") || normalized.StartsWith("pequ"))
        {
            return CakeSizeOption.Small;
        }

        if (normalized.StartsWith("large") || normalized.StartsWith("gran"))
        {
            return CakeSizeOption.Large;
        }

        return CakeSizeOption.Medium;
    }

    private static string GetCakeSizeLabel(CakeSizeOption size)
    {
        switch (size)
        {
            case CakeSizeOption.Small:
                return "Pequeno";
            case CakeSizeOption.Large:
                return "Grande";
            case CakeSizeOption.Medium:
            default:
                return "Mediano";
        }
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle < 0f)
        {
            angle += 360f;
        }

        while (angle >= 360f)
        {
            angle -= 360f;
        }

        return angle;
    }

    private static int ParsePayoutValue(string payout)
    {
        if (string.IsNullOrEmpty(payout))
        {
            return 0;
        }

        string numeric = payout.Replace("Q", string.Empty).Trim();
        int value;
        return int.TryParse(numeric, out value) ? value : 0;
    }

    private static string GetOrderStatusLabel(RuntimeOrder order)
    {
        if (order == null)
        {
            return "--";
        }

        if (order.State == OrderRuntimeState.Completed)
        {
            return "Lista";
        }

        if (order.State == OrderRuntimeState.ActivePrep)
        {
            if (order.CakeGenerated)
            {
                return "Pastel creado";
            }

            if (order.BakeOutcome != BakeResult.None)
            {
                return "Esperando topping";
            }

            if (order.PlacementCompleted)
            {
                return "Lista para hornear";
            }

            if (IsSizeChosen(order))
            {
                return "Esperando objeto";
            }

            if (IsFlavorChosen(order))
            {
                return "Esperando tamano";
            }

            return "Preparando";
        }

        return "Pendiente";
    }

    private static int GetDisplayPayout(RuntimeOrder order)
    {
        if (order == null)
        {
            return 0;
        }

        if (order.FinalPayout > 0)
        {
            return order.FinalPayout;
        }

        return order.BasePayoutValue;
    }

    private void BuildResultsMenu()
    {
        if (resultsMenuRoot != null || rootCanvas == null)
        {
            return;
        }

        Image blocker = CreateImage("DayResultsRoot", rootCanvas.transform, new Color(0f, 0f, 0f, 0.55f));
        resultsMenuRoot = blocker.gameObject;
        SetRect(blocker.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));

        Image panel = CreateDialogPanel("ResultsPanel", blocker.transform, new Vector2(820f, 640f));

        resultsTitleText = CreateText("ResultsTitle", panel.transform, "Fin del día", 50, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(resultsTitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(720f, 64f), new Vector2(0.5f, 0.5f));

        resultsSubtitleText = CreateText("ResultsSubtitle", panel.transform, string.Empty, 28, FontStyle.Normal, TextAnchor.MiddleCenter);
        resultsSubtitleText.color = new Color(0.35f, 0.18f, 0.12f, 1f);
        SetRect(resultsSubtitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(720f, 40f), new Vector2(0.5f, 0.5f));

        resultsStatsText = CreateText("ResultsStats", panel.transform, string.Empty, 28, FontStyle.Normal, TextAnchor.MiddleLeft);
        resultsStatsText.lineSpacing = 1.25f;
        resultsStatsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        resultsStatsText.verticalOverflow = VerticalWrapMode.Truncate;
        SetRect(resultsStatsText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(720f, 240f), new Vector2(0.5f, 0.5f));

        Button primaryButton = CreateDialogButton("ResultsPrimaryButton", panel.transform, dialogPrimaryButtonSprite, "Ir al siguiente día", Color.black, new Vector2(-180f, 90f), out resultsPrimaryLabel);
        primaryButton.onClick.AddListener(HandleResultsPrimaryButton);
        ButtonSfx.Attach(primaryButton, SfxId.UiClick);

        Text mainMenuLabel;
        Button mainMenuButton = CreateDialogButton("ResultsMainMenuButton", panel.transform, dialogSecondaryButtonSprite, "Main Menu", new Color(0.992f, 0.961f, 0.902f, 1f), new Vector2(180f, 90f), out mainMenuLabel);
        mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        ButtonSfx.Attach(mainMenuButton, SfxId.UiClick);

        resultsMenuRoot.SetActive(false);
    }

    // Panel and buttons mirror the pause menu: Container3 panel, white/red square buttons, 50 pt title.
    private Image CreateDialogPanel(string objectName, Transform parent, Vector2 size)
    {
        Image panel = CreateImage(objectName, parent, new Color(0.96f, 0.94f, 0.90f, 1f));
        SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, new Vector2(0.5f, 0.5f));
        if (dialogPanelSprite != null)
        {
            panel.sprite = dialogPanelSprite;
            panel.type = Image.Type.Sliced;
        }
        else
        {
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        return panel;
    }

    private Button CreateDialogButton(string objectName, Transform parent, Sprite sprite, string label, Color labelColor, Vector2 bottomOffset, out Text labelText)
    {
        Button button = CreateButton(objectName, parent, sprite != null ? Color.white : new Color(1f, 0.88f, 0.60f, 1f));
        StylePlacementButton(button, sprite, true);
        SetRect(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), bottomOffset, new Vector2(320f, 72f), new Vector2(0.5f, 0.5f));
        labelText = CreateText("Label", button.transform, label, 28, FontStyle.Normal, TextAnchor.MiddleCenter);
        labelText.color = labelColor;
        SetRect(labelText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 3f), new Vector2(-24f, -12f), new Vector2(0.5f, 0.5f));
        return button;
    }
    private void BuildControlsPopup()
    {
        if (controlsPopupRoot != null || rootCanvas == null)
        {
            return;
        }

        Image blocker = CreateImage("ControlsPopupRoot", rootCanvas.transform, new Color(0f, 0f, 0f, 0.50f));
        controlsPopupRoot = blocker.gameObject;
        SetRect(blocker.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));

        Image panel = CreateDialogPanel("ControlsPopupPanel", blocker.transform, new Vector2(860f, 600f));

        controlsPopupTitleText = CreateText("ControlsPopupTitle", panel.transform, "Día " + currentDay, 50, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(controlsPopupTitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(720f, 64f), new Vector2(0.5f, 0.5f));

        controlsPopupGoalText = CreateText("ControlsPopupGoal", panel.transform, dayGoal > 0 ? "Meta del día: Q" + dayGoal : string.Empty, 34, FontStyle.Normal, TextAnchor.MiddleCenter);
        controlsPopupGoalText.color = new Color(0.62f, 0.20f, 0.12f, 1f);
        SetRect(controlsPopupGoalText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(720f, 48f), new Vector2(0.5f, 0.5f));

        Text goalHint = CreateText("ControlsPopupGoalHint", panel.transform, "Junta ese dinero antes de que se acabe el tiempo o será Game Over.", 22, FontStyle.Normal, TextAnchor.MiddleCenter);
        goalHint.color = new Color(0.35f, 0.18f, 0.12f, 1f);
        SetRect(goalHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -188f), new Vector2(700f, 56f), new Vector2(0.5f, 0.5f));

        Text body = CreateText(
            "ControlsPopupBody",
            panel.transform,
            "- Utiliza Space o E para interactuar con tu estación de cocina y con los prisioneros.\n\n- Los pasteles terminados van a tu inventario. Elige uno con las teclas 1-5 o la rueda del mouse y habla con el preso de esa celda para entregarlo.",
            24,
            FontStyle.Normal,
            TextAnchor.MiddleLeft);
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Overflow;
        SetRect(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(700f, 170f), new Vector2(0.5f, 0.5f));

        Button closeXButton = CreateButton("ControlsPopupCloseX", panel.transform, new Color(0.97f, 0.96f, 0.93f, 1f));
        StylePlacementButton(closeXButton, dialogCloseButtonSprite, false);
        SetRect(closeXButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -52f), new Vector2(52f, 48f), new Vector2(0.5f, 0.5f));
        Text closeXLabel = CreateText("Label", closeXButton.transform, "X", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(closeXLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        closeXButton.onClick.AddListener(CloseControlsPopup);
        ButtonSfx.Attach(closeXButton, SfxId.UiClick);

        Text continueLabel;
        Button continueButton = CreateDialogButton("ControlsPopupContinue", panel.transform, dialogPrimaryButtonSprite, "¡A cocinar!", Color.black, new Vector2(0f, 90f), out continueLabel);
        continueButton.onClick.AddListener(CloseControlsPopup);
        ButtonSfx.Attach(continueButton, SfxId.UiClick);

        controlsPopupRoot.SetActive(false);
    }
    private void BuildMapOverlay()
    {
        if (mapOverlayRoot != null || rootCanvas == null)
        {
            return;
        }

        Image blocker = CreateImage("MapOverlayRoot", rootCanvas.transform, new Color(0f, 0f, 0f, 0.45f));
        mapOverlayRoot = blocker.gameObject;
        SetRect(blocker.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));

        Image panel = CreateImage("MapPanel", blocker.transform, new Color(0.94f, 0.91f, 0.83f, 0.98f));
        SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 500f), new Vector2(0.5f, 0.5f));
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        Text title = CreateText("MapTitle", panel.transform, "Mapa", 30, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(300f, 36f), new Vector2(0.5f, 0.5f));

        mapContentRoot = CreateRect("MapContent", panel.transform);
        SetRect(mapContentRoot, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.86f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));

        mapOverlayRoot.SetActive(false);
    }

    private void PopulateMapOverlay(Vector3 playerPosition, IList<PrisonerInteractable> prisonerViews)
    {
        ClearChildren(mapContentRoot);

        if (prisonerViews == null || prisonerViews.Count == 0)
        {
            Text empty = CreateText("MapEmpty", mapContentRoot, "No hay celdas registradas.", 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(empty.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            return;
        }

        Vector2 min = new Vector2(playerPosition.x, playerPosition.z);
        Vector2 max = min;
        for (int i = 0; i < prisonerViews.Count; i++)
        {
            if (prisonerViews[i] == null)
            {
                continue;
            }

            Vector2 point = new Vector2(prisonerViews[i].transform.position.x, prisonerViews[i].transform.position.z);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        Vector2 size = max - min;
        size.x = Mathf.Max(size.x, 1f);
        size.y = Mathf.Max(size.y, 1f);

        for (int i = 0; i < prisonerViews.Count; i++)
        {
            PrisonerInteractable prisoner = prisonerViews[i];
            if (prisoner == null)
            {
                continue;
            }

            Vector2 normalized = new Vector2(
                (prisoner.transform.position.x - min.x) / size.x,
                (prisoner.transform.position.z - min.y) / size.y);

            Image marker = CreateImage("MapMarker_" + prisoner.CellId, mapContentRoot, GetMapStateColor(prisoner.MapState));
            SetRect(marker.rectTransform, normalized, normalized, Vector2.zero, new Vector2(74f, 34f), new Vector2(0.5f, 0.5f));
            Text label = CreateText("Label", marker.transform, prisoner.CellLabel.Replace("Celda ", string.Empty), 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        }

        Vector2 playerNormalized = new Vector2(
            (playerPosition.x - min.x) / size.x,
            (playerPosition.z - min.y) / size.y);
        Image playerMarker = CreateImage("MapPlayerMarker", mapContentRoot, new Color(0.2f, 0.45f, 1f, 1f));
        SetRect(playerMarker.rectTransform, playerNormalized, playerNormalized, Vector2.zero, new Vector2(42f, 42f), new Vector2(0.5f, 0.5f));
        Text playerLabel = CreateText("Label", playerMarker.transform, "TU", 15, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(playerLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
    }

    private static Color GetMapStateColor(string state)
    {
        if (state == "Pedido")
        {
            return new Color(1f, 0.86f, 0.36f, 1f);
        }

        if (state == "Cooldown")
        {
            return new Color(0.65f, 0.65f, 0.65f, 1f);
        }

        return new Color(0.78f, 0.95f, 0.72f, 1f);
    }

    private RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        return rectTransform;
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Sprite GetFlavorDotSprite()
    {
        if (flavorDotSprite != null)
        {
            return flavorDotSprite;
        }

        const int size = 32;
        flavorDotSprite = ProceduralSprites.Create("FlavorDot", size, size, new Vector2(0.5f, 0.5f), (x, y) => new Vector2(x - size * 0.5f, y - size * 0.5f).magnitude <= size * 0.5f - 1f ? Color.white : Color.clear);
        return flavorDotSprite;
    }

    private static Sprite GetCakeSizeCylinderSprite()
    {
        if (cakeSizeCylinderSprite != null)
        {
            return cakeSizeCylinderSprite;
        }

        const int width = 128;
        const int height = 112;
        cakeSizeCylinderSprite = ProceduralSprites.Create("CakeSizeCylinder", width, height, new Vector2(0.5f, 0f), (x, y) => SampleCylinder(x, y, width, height));
        return cakeSizeCylinderSprite;
    }

    private Sprite GetSizeSprite(int sizeIndex)
    {
        // The size minigame shows cakes in the flavor the player just picked.
        Sprite flavorSprite = activePrepOrder != null ? GetFlavorSizeSprite(FindFlavor(activePrepOrder.SelectedFlavor), sizeIndex) : null;
        if (flavorSprite != null)
        {
            return flavorSprite;
        }

        if (cakeSizeSprites != null && sizeIndex >= 0 && sizeIndex < cakeSizeSprites.Length && cakeSizeSprites[sizeIndex] != null)
        {
            return cakeSizeSprites[sizeIndex];
        }

        return GetCakeSizeCylinderSprite();
    }

    private static Sprite GetPlacementHoleSprite(PlacementHoleShape shape)
    {
        Sprite sprite;
        if (placementHoleSprites.TryGetValue(shape, out sprite) && sprite != null)
        {
            return sprite;
        }

        const int size = 256;
        sprite = ProceduralSprites.Create("PlacementHole_" + shape, size, size, new Vector2(0.5f, 0.5f), (x, y) => SamplePlacementHole(shape, x - size * 0.5f, y - size * 0.5f, size * 0.5f - 8f));
        placementHoleSprites[shape] = sprite;
        return sprite;
    }

    private static Color SamplePlacementHole(PlacementHoleShape shape, float px, float py, float radius)
    {
        float distance = GetHoleShapeDistance(shape, new Vector2(px, py), radius);
        if (distance > 0f)
        {
            return Color.clear;
        }

        const float rimWidth = 5f;
        if (distance > -rimWidth)
        {
            return new Color(0.42f, 0.25f, 0.18f, 1f);
        }

        // Darker toward the rim so the cut reads as a hollow in the cake.
        float depth = Mathf.Clamp01((-distance - rimWidth) / 40f);
        return Color.Lerp(new Color(0.50f, 0.35f, 0.26f, 1f), new Color(0.68f, 0.51f, 0.38f, 1f), depth);
    }

    private static float GetHoleShapeDistance(PlacementHoleShape shape, Vector2 point, float radius)
    {
        float length = point.magnitude;
        float angle = Mathf.Atan2(point.y, point.x);
        switch (shape)
        {
            case PlacementHoleShape.Star:
                return ProceduralSprites.PolygonDistance(point, CreateStarVertices(5, radius, radius * 0.5f));
            case PlacementHoleShape.Hexagon:
                return ProceduralSprites.PolygonDistance(point, CreateStarVertices(3, radius, radius));
            case PlacementHoleShape.Diamond:
                return ProceduralSprites.PolygonDistance(point, new[] { new Vector2(0f, radius), new Vector2(-radius * 0.72f, 0f), new Vector2(0f, -radius), new Vector2(radius * 0.72f, 0f) });
            case PlacementHoleShape.Flower:
                return length - radius * (0.82f + 0.18f * Mathf.Cos(6f * angle));
            case PlacementHoleShape.Blob:
                return length - radius * (0.86f + 0.08f * Mathf.Sin(3f * angle + 0.7f) + 0.06f * Mathf.Cos(5f * angle - 0.3f));
            case PlacementHoleShape.Circle:
            default:
                return length - radius;
        }
    }

    // Alternating outer/inner vertices; equal radii give a regular polygon with 2 * points sides.
    private static Vector2[] CreateStarVertices(int points, float outerRadius, float innerRadius)
    {
        Vector2[] vertices = new Vector2[points * 2];
        for (int i = 0; i < vertices.Length; i++)
        {
            float vertexAngle = Mathf.PI * 0.5f + i * Mathf.PI / points;
            float vertexRadius = i % 2 == 0 ? outerRadius : innerRadius;
            vertices[i] = new Vector2(Mathf.Cos(vertexAngle), Mathf.Sin(vertexAngle)) * vertexRadius;
        }

        return vertices;
    }

    private static Sprite GetPlacementIconSprite(PlacementIcon icon)
    {
        Sprite sprite;
        if (placementIconSprites.TryGetValue(icon, out sprite) && sprite != null)
        {
            return sprite;
        }

        const int size = 64;
        sprite = ProceduralSprites.Create("PlacementIcon_" + icon, size, size, new Vector2(0.5f, 0.5f), (x, y) => SamplePlacementIcon(icon, x, y));
        placementIconSprites[icon] = sprite;
        return sprite;
    }

    private static Color SamplePlacementIcon(PlacementIcon icon, float px, float py)
    {
        Color fillColor = new Color(1f, 0.98f, 0.90f, 1f);
        Color inkColor = new Color(0.55f, 0.30f, 0.22f, 1f);
        const float outlineWidth = 3.5f;

        float shapeDistance;
        float symbolDistance = float.MaxValue;
        switch (icon)
        {
            case PlacementIcon.RotateLeft:
            case PlacementIcon.RotateRight:
                // Rotate right is the mirror image of rotate left.
                Vector2 point = new Vector2(icon == PlacementIcon.RotateRight ? 64f - px : px, py);
                shapeDistance = RotateArrowDistance(point);
                break;
            case PlacementIcon.ArrowLeft:
            case PlacementIcon.ArrowRight:
                // Arrow right is the mirror image of arrow left.
                Vector2 arrowPoint = new Vector2(icon == PlacementIcon.ArrowRight ? 64f - px : px, py);
                shapeDistance = ProceduralSprites.PolygonDistance(arrowPoint, new[]
                {
                    new Vector2(12f, 32f), new Vector2(32f, 51f), new Vector2(32f, 40f), new Vector2(52f, 40f),
                    new Vector2(52f, 24f), new Vector2(32f, 24f), new Vector2(32f, 13f)
                });
                break;
            default:
                Vector2 lensCenter = new Vector2(27f, 37f);
                Vector2 lensPoint = new Vector2(px, py);
                float lens = (lensPoint - lensCenter).magnitude - 17f;
                float handle = ProceduralSprites.CapsuleDistance(lensPoint, new Vector2(38f, 26f), new Vector2(52f, 12f), 5f);
                shapeDistance = Mathf.Min(lens, handle);
                symbolDistance = ProceduralSprites.CapsuleDistance(lensPoint, new Vector2(19f, 37f), new Vector2(35f, 37f), 2.8f);
                if (icon == PlacementIcon.ZoomIn)
                {
                    symbolDistance = Mathf.Min(symbolDistance, ProceduralSprites.CapsuleDistance(lensPoint, new Vector2(27f, 29f), new Vector2(27f, 45f), 2.8f));
                }
                break;
        }

        if (symbolDistance <= 0f)
        {
            return inkColor;
        }

        if (shapeDistance <= 0f)
        {
            return fillColor;
        }

        return shapeDistance <= outlineWidth ? inkColor : Color.clear;
    }

    // Counterclockwise arc with an arrowhead at its end.
    private static float RotateArrowDistance(Vector2 point)
    {
        Vector2 center = new Vector2(32f, 32f);
        const float radius = 17f;
        const float halfWidth = 4f;
        float startAngle = -80f * Mathf.Deg2Rad;
        float endAngle = 160f * Mathf.Deg2Rad;

        Vector2 local = point - center;
        float pointAngle = Mathf.Atan2(local.y, local.x);
        float sweep = Mathf.Repeat(pointAngle - startAngle, Mathf.PI * 2f);
        Vector2 arcStart = center + new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle)) * radius;
        Vector2 arcEnd = center + new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * radius;
        float arc = sweep <= endAngle - startAngle
            ? Mathf.Abs(local.magnitude - radius) - halfWidth
            : Mathf.Min((point - arcStart).magnitude, (point - arcEnd).magnitude) - halfWidth;

        Vector2 tangent = new Vector2(-Mathf.Sin(endAngle), Mathf.Cos(endAngle));
        Vector2 normal = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));
        Vector2 baseCenter = arcEnd - tangent * 2f;
        Vector2[] head = { arcEnd + tangent * 11f, baseCenter - normal * 10f, baseCenter + normal * 10f };
        return Mathf.Min(arc, ProceduralSprites.PolygonDistance(point, head));
    }

    private static Color SampleCylinder(float px, float py, int width, int height)
    {
        const float margin = 3f;
        const float outlineWidth = 3f;
        float radiusX = width * 0.5f - margin;
        float radiusY = radiusX * 0.28f;
        float centerX = width * 0.5f;
        float topY = height - margin - radiusY;
        float bottomY = margin + radiusY;

        float dx = px - centerX;
        if (Mathf.Abs(dx) > radiusX)
        {
            return Color.clear;
        }

        float topDistance = EllipseEdgeDistance(dx, py - topY, radiusX, radiusY);
        float bottomDistance = EllipseEdgeDistance(dx, py - bottomY, radiusX, radiusY);
        bool insideTop = topDistance <= 0f;
        bool insideBody = py >= bottomY && py <= topY;
        bool insideBottom = bottomDistance <= 0f;
        if (!insideTop && !insideBody && !insideBottom)
        {
            return Color.clear;
        }

        Color outlineColor = new Color(0.20f, 0.20f, 0.22f, 1f);
        bool onSide = radiusX - Mathf.Abs(dx) < outlineWidth;
        bool onTopRim = insideTop && topDistance > -outlineWidth;
        bool onBottomRim = py < bottomY && bottomDistance > -outlineWidth;
        if (onSide || onTopRim || onBottomRim)
        {
            return outlineColor;
        }

        if (insideTop)
        {
            float highlight = Mathf.Clamp01(1f - Mathf.Abs(py - topY) / radiusY) * 0.05f;
            float value = 0.84f + highlight;
            return new Color(value, value, value + 0.02f, 1f);
        }

        // Light comes from the upper left, so the body brightens left of center.
        float normalizedX = dx / radiusX;
        float lit = Mathf.Sqrt(Mathf.Clamp01(1f - (normalizedX + 0.35f) * (normalizedX + 0.35f) / 1.8225f));
        float bodyValue = Mathf.Lerp(0.40f, 0.72f, lit);
        return new Color(bodyValue, bodyValue, bodyValue + 0.02f, 1f);
    }

    private static float EllipseEdgeDistance(float dx, float dy, float radiusX, float radiusY)
    {
        float value = (dx * dx) / (radiusX * radiusX) + (dy * dy) / (radiusY * radiusY) - 1f;
        float gradientX = 2f * dx / (radiusX * radiusX);
        float gradientY = 2f * dy / (radiusY * radiusY);
        float gradientLength = Mathf.Sqrt(gradientX * gradientX + gradientY * gradientY);
        return gradientLength > 0.0001f ? value / gradientLength : -Mathf.Min(radiusX, radiusY);
    }

    private Text CreateText(string objectName, Transform parent, string content, int fontSize, FontStyle fontStyle, TextAnchor anchor)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        Text text = gameObject.GetComponent<Text>();
        text.font = defaultFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = anchor;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(10, fontSize - 10);
        text.resizeTextMaxSize = fontSize;
        text.text = content;
        text.color = Color.black;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(string objectName, Transform parent, Color backgroundColor)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.color = backgroundColor;
        Button button = gameObject.GetComponent<Button>();
        button.targetGraphic = image;
        gameObject.AddComponent<InputSystemUIButtonProxy>();
        Outline outline = gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, -1f);
        return button;
    }

    private void EnsureButtonInputProxy(Button button)
    {
        if (button == null)
        {
            return;
        }

        if (button.GetComponent<InputSystemUIButtonProxy>() == null)
        {
            button.gameObject.AddComponent<InputSystemUIButtonProxy>();
        }

        Text[] childTexts = button.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < childTexts.Length; i++)
        {
            if (childTexts[i] != null)
            {
                childTexts[i].raycastTarget = false;
            }
        }
    }

    private static void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 pivot)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.pivot = pivot;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    private static void ClearChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
            {
                Destroy(root.GetChild(i).gameObject);
            }
            else
            {
                DestroyImmediate(root.GetChild(i).gameObject);
            }
        }
    }

[DisallowMultipleComponent]
public sealed class SizeGamePieceView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public Action<SizeGamePieceView> Clicked;
    public Action<SizeGamePieceView> DragEnded;
    public int SizeIndex { get; private set; }

    private RectTransform rectTransform;
    private RectTransform dragRoot;
    private Vector2 pointerOffset;
    private Canvas rootCanvas;
    private bool pressHeld;
    private bool isDragging;
    private Vector2 pressStartScreenPosition;
    private const float DragThresholdPixels = 8f;

    // Every piece polls the mouse itself, so overlapping cakes would all grab the same press and stick together.
    // Only one piece owns a press: the one drawn on top among those under the cursor.
    private static SizeGamePieceView pressOwner;
    private static int pressOwnerFrame = -1;

    public void Initialize(int sizeIndex, RectTransform root, Canvas targetCanvas)
    {
        SizeIndex = sizeIndex;
        rectTransform = GetComponent<RectTransform>();
        dragRoot = root;
        rootCanvas = GetComponentInParent<Canvas>();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (rectTransform == null || dragRoot == null || UnityEngine.InputSystem.Mouse.current == null)
        {
            return;
        }

        Vector2 screenPosition = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        bool hovered = RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, GetEventCamera());

        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            pressHeld = true;
            pressStartScreenPosition = screenPosition;
            if (hovered && CanTakePressFrom(pressOwner))
            {
                pressOwner = this;
                pressOwnerFrame = Time.frameCount;
                // Keep the grab point under the cursor instead of snapping the piece's center to it.
                RectTransformUtility.ScreenPointToLocalPointInRectangle(dragRoot, screenPosition, GetEventCamera(), out Vector2 localPoint);
                pointerOffset = rectTransform.anchoredPosition - localPoint;
            }
        }

        bool ownsPress = pressOwner == this;
        if (pressHeld && ownsPress && !isDragging && Vector2.Distance(pressStartScreenPosition, screenPosition) >= DragThresholdPixels)
        {
            isDragging = true;
            transform.SetAsLastSibling();
        }

        if (isDragging)
        {
            MoveToScreenPosition(screenPosition);
        }

        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasReleasedThisFrame)
        {
            bool clicked = pressHeld && ownsPress && !isDragging;
            pressHeld = false;
            if (ownsPress)
            {
                pressOwner = null;
            }

            if (isDragging)
            {
                DragEnded?.Invoke(this);
            }
            isDragging = false;
            if (clicked)
            {
                Clicked?.Invoke(this);
            }
        }
#endif
    }

    // A press from an earlier frame is stale (its owner was destroyed before release); in the same frame the topmost piece wins.
    private bool CanTakePressFrom(SizeGamePieceView currentOwner)
    {
        return currentOwner == null
            || pressOwnerFrame != Time.frameCount
            || currentOwner.transform.parent != transform.parent
            || currentOwner.transform.GetSiblingIndex() < transform.GetSiblingIndex();
    }

    private void OnDestroy()
    {
        if (pressOwner == this)
        {
            pressOwner = null;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (rectTransform == null || dragRoot == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(dragRoot, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);
        pointerOffset = rectTransform.anchoredPosition - localPoint;
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform == null || dragRoot == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(dragRoot, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            rectTransform.anchoredPosition = localPoint + pointerOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DragEnded?.Invoke(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Mouse input is handled explicitly above so it works with the project's Input System UI setup.
    }

    private void MoveToScreenPosition(Vector2 screenPosition)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(dragRoot, screenPosition, GetEventCamera(), out Vector2 localPoint))
        {
            return;
        }

        Vector2 halfSize = rectTransform.rect.size * 0.5f;
        Rect bounds = dragRoot.rect;
        float x = Mathf.Clamp(localPoint.x + pointerOffset.x, bounds.xMin + halfSize.x, bounds.xMax - halfSize.x);
        float y = Mathf.Clamp(localPoint.y + pointerOffset.y, bounds.yMin + halfSize.y, bounds.yMax - halfSize.y);
        rectTransform.anchoredPosition = new Vector2(x, y);
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
