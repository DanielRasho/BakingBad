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
        public int basePayout;
        public string orderTitle;
        public string cakeColor;
        public string topping;
        public string contrabandItem;
        public string cakeSize;
        public string shapeType;
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

    private enum PlacementShapeType
    {
        Square,
        Triangle,
        Rectangle
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
        public PlacementShapeType ShapeType;
        public int BasePayoutValue;
        public Vector2 TargetAnchoredPosition;
        public float TargetRotationDegrees;
        public CakeSizeOption SelectedCakeSize = CakeSizeOption.None;
        public bool SizeMatched;
        public bool PlacementCompleted;
        public float PlacementScore;
        public Vector2 PieceAnchoredPosition;
        public float PieceRotationDegrees;
        public bool PiecePoseInitialized;
        public bool BakeInProgress;
        public float BakeProgressNormalized;
        public BakeResult BakeOutcome = BakeResult.None;
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
    private int activeTabIndex;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool isInitialized;
    private bool playerLockHeld;

    private Text detailStatusText;

    private Button[] sizeOptionButtons;
    private Image[] sizeOptionBackgrounds;
    private Text sizeFeedbackText;

    private RectTransform placementPlayArea;
    private RectTransform placementTargetRect;
    private Text placementTargetLabelText;
    private RectTransform placementPieceRect;
    private Text placementPieceLabelText;
    private ShapePlacementPieceView placementPieceView;
    private Text placementStatusText;
    private Text placementScoreText;
    private Button placementRotateLeftButton;
    private Button placementRotateRightButton;
    private Button placementConfirmButton;

    private Image bakeProgressFillImage;
    private RectTransform bakePerfectZoneRect;
    private Text bakeStatusText;
    private Text bakeHintText;
    private Button bakeActionButton;
    private Text bakeButtonLabelText;

    private Button[] toppingOptionButtons;
    private Image[] toppingOptionBackgrounds;
    private Text[] toppingOptionLabelTexts;
    private Text toppingFeedbackText;

    private const float OrderCardWidth = 86f;
    private const float OrderCardHeight = 114f;
    private const float OrderCardSpacing = 14f;
    private const float OrderCardStartX = 18f;
    private const float OrderCardStartY = -8f;
    private const float PlacementRotationStep = 15f;
    private const float PlacementDistanceThreshold = 28f;
    private const float PlacementAngleThreshold = 18f;
    private const float BakeDurationSeconds = 4.5f;
    private const float PerfectBakeWindowWidth = 0.12f;
    private const float PerfectBakeWindowMinCenter = 0.26f;
    private const float PerfectBakeWindowMaxCenter = 0.74f;

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

    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        UpdateBakeState();
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
            ActivateTab(GetFirstAvailableTabIndex(activePrepOrder), true);
        }
        else
        {
            RuntimeOrder nextOrder = selectedOrder != null && selectedOrder.State == OrderRuntimeState.Pending
                ? selectedOrder
                : GetFirstPendingSpawnedOrder();
            SetSelectedOrder(nextOrder, false);
            ActivateTab(0, true);
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

    public void HandleOrderCardClicked(OrderCardView cardView)
    {
        RuntimeOrder order = FindOrderByView(cardView);
        if (!CanSelectOrderFromBoard(order))
        {
            return;
        }

        SetSelectedOrder(order, activePrepOrder != null && order == activePrepOrder);
    }

    public void HandleOrderCardDropped(OrderCardView cardView)
    {
        RuntimeOrder order = FindOrderByView(cardView);
        if (!CanSelectOrderFromBoard(order))
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
        defaultFont = detailTitleText != null && detailTitleText.font != null
            ? detailTitleText.font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        LoadOrderCardPrefab();
        BuildCardsViewport();
        WireButtons();
        BuildRuntimeTabContent();
        BuildRuntimeDetailStatus();
        SetMenuVisible(false);
        SetOrderBoardInteractable(false);
        LoadOrdersFromJson();
        SyncBoardSummary();
        SetSelectedOrder(null, false);
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
        EnsureButtonInputProxy(closeButton);
        EnsureButtonInputProxy(prepareButton);

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
            EnsureButtonInputProxy(button);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ActivateTab(capturedIndex, false));
        }
    }

    private void BuildRuntimeDetailStatus()
    {
        if (detailPanel == null || detailStatusText != null)
        {
            return;
        }

        RectTransform panelRect = detailPanel.GetComponent<RectTransform>();
        Text status = CreateText("DetailStatusText", panelRect, "Estado: --", 18, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(status.rectTransform, new Vector2(0.08f, 0f), new Vector2(0.92f, 0f), new Vector2(0f, 96f), new Vector2(0f, 24f), new Vector2(0.5f, 0.5f));
        detailStatusText = status;
    }

    private void BuildRuntimeTabContent()
    {
        if (tabPanels == null || tabPanels.Length < 4)
        {
            return;
        }

        BuildSizeTab(tabPanels[0].GetComponent<RectTransform>());
        BuildPlacementTab(tabPanels[1].GetComponent<RectTransform>());
        BuildBakeTab(tabPanels[2].GetComponent<RectTransform>());
        BuildToppingTab(tabPanels[3].GetComponent<RectTransform>());
    }

    private void BuildSizeTab(RectTransform panel)
    {
        if (panel == null)
        {
            return;
        }

        ClearChildren(panel);
        Text title = CreateText("Instruction", panel, "Selecciona el tamano exacto del pastel.", 26, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(520f, 44f), new Vector2(0.5f, 0.5f));

        sizeOptionButtons = new Button[3];
        sizeOptionBackgrounds = new Image[3];
        float[] xPositions = { -220f, 0f, 220f };
        for (int i = 0; i < 3; i++)
        {
            Button button = CreateButton("SizeOption_" + CakeSizeLabels[i], panel, new Color(0.97f, 0.96f, 0.93f, 1f));
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            SetRect(buttonRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(xPositions[i], -10f), new Vector2(180f, 220f), new Vector2(0.5f, 0.5f));

            Image placeholder = CreateImage("SpritePlaceholder", buttonRect, Color.white);
            SetRect(placeholder.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(92f, 92f), new Vector2(0.5f, 0.5f));
            Outline outline = placeholder.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1f, -1f);

            Text label = CreateText("SizeLabel", buttonRect, CakeSizeLabels[i], 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(label.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), Vector2.zero, new Vector2(150f, 50f), new Vector2(0.5f, 0.5f));

            int capturedIndex = i;
            button.onClick.AddListener(() => HandleCakeSizeSelected(CakeSizeValues[capturedIndex]));

            sizeOptionButtons[i] = button;
            sizeOptionBackgrounds[i] = button.GetComponent<Image>();
        }

        sizeFeedbackText = CreateText("SizeFeedbackText", panel, "Selecciona un tamano para continuar.", 20, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetRect(sizeFeedbackText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(560f, 28f), new Vector2(0.5f, 0.5f));
    }

    private void BuildPlacementTab(RectTransform panel)
    {
        if (panel == null)
        {
            return;
        }

        ClearChildren(panel);
        Text title = CreateText("Instruction", panel, "Mueve y rota la pieza hasta encajarla en la silueta.", 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(620f, 40f), new Vector2(0.5f, 0.5f));

        placementPlayArea = CreateRect("PlacementPlayArea", panel);
        SetRect(placementPlayArea, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0f, -8f), new Vector2(600f, 330f), new Vector2(0.5f, 0.5f));
        Image playAreaImage = placementPlayArea.gameObject.AddComponent<Image>();
        playAreaImage.color = new Color(0.94f, 0.93f, 0.90f, 1f);

        placementTargetRect = CreateRect("PlacementTarget", placementPlayArea);
        Image targetImage = placementTargetRect.gameObject.AddComponent<Image>();
        targetImage.color = new Color(0.2f, 0.2f, 0.2f, 0.18f);
        placementTargetLabelText = CreateText("TargetLabel", placementTargetRect, "OBJETIVO", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(placementTargetLabelText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));

        placementPieceRect = CreateRect("PlacementPiece", placementPlayArea);
        Image pieceImage = placementPieceRect.gameObject.AddComponent<Image>();
        pieceImage.color = new Color(1f, 0.96f, 0.78f, 1f);
        placementPieceLabelText = CreateText("PieceLabel", placementPieceRect, "PIEZA", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(placementPieceLabelText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        placementPieceView = placementPieceRect.gameObject.AddComponent<ShapePlacementPieceView>();
        placementPieceView.Initialize(placementPlayArea);
        placementPieceView.Changed += HandlePlacementPieceChanged;

        placementRotateLeftButton = CreateButton("RotateLeftButton", panel, new Color(0.97f, 0.96f, 0.93f, 1f));
        SetRect(placementRotateLeftButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-170f, 92f), new Vector2(120f, 42f), new Vector2(0.5f, 0.5f));
        Text rotateLeftLabel = CreateText("Label", placementRotateLeftButton.GetComponent<RectTransform>(), "Girar -", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(rotateLeftLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        placementRotateLeftButton.onClick.AddListener(() => RotatePlacementPiece(-PlacementRotationStep));

        placementRotateRightButton = CreateButton("RotateRightButton", panel, new Color(0.97f, 0.96f, 0.93f, 1f));
        SetRect(placementRotateRightButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-20f, 92f), new Vector2(120f, 42f), new Vector2(0.5f, 0.5f));
        Text rotateRightLabel = CreateText("Label", placementRotateRightButton.GetComponent<RectTransform>(), "Girar +", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(rotateRightLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        placementRotateRightButton.onClick.AddListener(() => RotatePlacementPiece(PlacementRotationStep));

        placementConfirmButton = CreateButton("ConfirmPlacementButton", panel, new Color(0.97f, 0.96f, 0.93f, 1f));
        SetRect(placementConfirmButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150f, 92f), new Vector2(180f, 42f), new Vector2(0.5f, 0.5f));
        Text confirmLabel = CreateText("Label", placementConfirmButton.GetComponent<RectTransform>(), "Verificar encaje", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(confirmLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        placementConfirmButton.onClick.AddListener(ConfirmPlacement);

        placementStatusText = CreateText("PlacementStatusText", panel, "Aun no has colocado el objeto.", 19, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetRect(placementStatusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(560f, 24f), new Vector2(0.5f, 0.5f));

        placementScoreText = CreateText("PlacementScoreText", panel, "Precision: 0%", 18, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(placementScoreText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(400f, 24f), new Vector2(0.5f, 0.5f));
    }

    private void BuildBakeTab(RectTransform panel)
    {
        if (panel == null)
        {
            return;
        }

        ClearChildren(panel);
        Text title = CreateText("Instruction", panel, "Deten el horneado cuando el pastel este cocinado.", 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(620f, 40f), new Vector2(0.5f, 0.5f));

        RectTransform gaugeRoot = CreateRect("BakeGaugeRoot", panel);
        SetRect(gaugeRoot, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), new Vector2(0f, -10f), new Vector2(520f, 46f), new Vector2(0.5f, 0.5f));
        Image gaugeBackground = gaugeRoot.gameObject.AddComponent<Image>();
        gaugeBackground.color = new Color(0.26f, 0.22f, 0.18f, 1f);

        RectTransform fillRect = CreateRect("BakeFill", gaugeRoot);
        SetRect(fillRect, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
        bakeProgressFillImage = fillRect.gameObject.AddComponent<Image>();
        bakeProgressFillImage.color = new Color(0.82f, 0.56f, 0.2f, 1f);

        bakePerfectZoneRect = CreateRect("PerfectBakeZone", gaugeRoot);
        bakePerfectZoneRect.anchorMin = new Vector2(0f, 0f);
        bakePerfectZoneRect.anchorMax = new Vector2(0f, 1f);
        bakePerfectZoneRect.pivot = new Vector2(0f, 0.5f);
        Image perfectZoneImage = bakePerfectZoneRect.gameObject.AddComponent<Image>();
        perfectZoneImage.color = new Color(0.34f, 0.72f, 0.38f, 0.95f);

        Text rawLabel = CreateText("RawLabel", gaugeRoot, "Crudo", 16, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(rawLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, 18f), new Vector2(120f, 20f), new Vector2(0f, 0.5f));

        Text burntLabel = CreateText("BurntLabel", gaugeRoot, "Quemado", 16, FontStyle.Bold, TextAnchor.MiddleRight);
        SetRect(burntLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, 18f), new Vector2(120f, 20f), new Vector2(1f, 0.5f));

        bakeStatusText = CreateText("BakeStatusText", panel, "Estado: sin empezar", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(bakeStatusText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -94f), new Vector2(540f, 28f), new Vector2(0.5f, 0.5f));

        bakeHintText = CreateText("BakeHintText", panel, "Deten la barra dentro del area verde.", 18, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetRect(bakeHintText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -122f), new Vector2(560f, 24f), new Vector2(0.5f, 0.5f));

        bakeActionButton = CreateButton("BakeActionButton", panel, new Color(0.97f, 0.96f, 0.93f, 1f));
        SetRect(bakeActionButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -162f), new Vector2(220f, 52f), new Vector2(0.5f, 0.5f));
        bakeButtonLabelText = CreateText("Label", bakeActionButton.GetComponent<RectTransform>(), "Iniciar horneado", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(bakeButtonLabelText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        bakeActionButton.onClick.AddListener(HandleBakeAction);
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
        float[] xPositions = { -220f, 0f, 220f };

        for (int i = 0; i < 3; i++)
        {
            Button button = CreateButton("ToppingOption_" + i, panel, new Color(0.97f, 0.96f, 0.93f, 1f));
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            SetRect(buttonRect, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), new Vector2(xPositions[i], -20f), new Vector2(180f, 180f), new Vector2(0.5f, 0.5f));

            Image placeholder = CreateImage("SpritePlaceholder", buttonRect, Color.white);
            SetRect(placeholder.rectTransform, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), Vector2.zero, new Vector2(82f, 82f), new Vector2(0.5f, 0.5f));
            Outline outline = placeholder.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1f, -1f);

            Text label = CreateText("ToppingLabel", buttonRect, "--", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(label.rectTransform, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), Vector2.zero, new Vector2(150f, 52f), new Vector2(0.5f, 0.5f));

            int capturedIndex = i;
            button.onClick.AddListener(() => HandleToppingSelected(capturedIndex));

            toppingOptionButtons[i] = button;
            toppingOptionBackgrounds[i] = button.GetComponent<Image>();
            toppingOptionLabelTexts[i] = label;
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
            OrderDefinition definition = collection.orders[i];
            if (definition == null)
            {
                continue;
            }

            PlacementShapeType parsedShapeType = ParseShapeType(definition.shapeType);
            RuntimeOrder runtimeOrder = new RuntimeOrder
            {
                RuntimeIndex = i,
                Data = definition,
                State = OrderRuntimeState.Pending,
                RequiredCakeSize = ParseCakeSize(definition.cakeSize),
                ShapeType = parsedShapeType,
                BasePayoutValue = definition.basePayout > 0 ? definition.basePayout : ParsePayoutValue(definition.payout),
                TargetAnchoredPosition = GetPlacementTargetPosition(i),
                TargetRotationDegrees = GetPlacementTargetRotation(i, parsedShapeType)
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
        runtimeOrder.CardView = cardView;
        spawnedOrders.Add(runtimeOrder);

        if (logOrderBoard)
        {
            Debug.Log("Spawned order " + runtimeOrder.RuntimeIndex + " for cell " + runtimeOrder.Data.cellNumber + ". Visible cards=" + spawnedOrders.Count);
        }

        RefreshCardStates();
        if (selectedOrder == null && IsOpen && activePrepOrder == null)
        {
            SetSelectedOrder(runtimeOrder, false);
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

    private void PrepareSelectedOrder()
    {
        if (selectedOrder == null || selectedOrder.State != OrderRuntimeState.Pending)
        {
            return;
        }

        if (activePrepOrder != null && activePrepOrder != selectedOrder)
        {
            return;
        }

        activePrepOrder = selectedOrder;
        activePrepOrder.State = OrderRuntimeState.ActivePrep;
        RefreshAllUi();
        ActivateTab(0, true);
    }

    private void HandleCakeSizeSelected(CakeSizeOption selectedSize)
    {
        if (activePrepOrder == null || activePrepOrder.State != OrderRuntimeState.ActivePrep)
        {
            return;
        }

        activePrepOrder.SelectedCakeSize = selectedSize;
        activePrepOrder.SizeMatched = selectedSize == activePrepOrder.RequiredCakeSize;

        if (activePrepOrder.SizeMatched)
        {
            sizeFeedbackText.text = "Tamano correcto: " + GetCakeSizeLabel(selectedSize) + ".";
        }
        else
        {
            sizeFeedbackText.text = "Este no es el tamano adecuado del pastel.";
        }

        RefreshAllUi();

        if (activePrepOrder.SizeMatched)
        {
            ActivateTab(1, true);
        }
    }

    private void RotatePlacementPiece(float deltaDegrees)
    {
        if (placementPieceView == null || activePrepOrder == null || !activePrepOrder.SizeMatched || activePrepOrder.PlacementCompleted)
        {
            return;
        }

        placementPieceView.RotateBy(deltaDegrees);
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
        if (activePrepOrder == null || !activePrepOrder.SizeMatched || activePrepOrder.PlacementCompleted)
        {
            return;
        }

        float score = GetPlacementScore(activePrepOrder);
        float distance = Vector2.Distance(activePrepOrder.PieceAnchoredPosition, activePrepOrder.TargetAnchoredPosition);
        float angleDelta = GetRotationDelta(activePrepOrder.PieceRotationDegrees, activePrepOrder.TargetRotationDegrees, GetShapeRotationSymmetry(activePrepOrder.ShapeType));

        if (distance <= PlacementDistanceThreshold && angleDelta <= PlacementAngleThreshold)
        {
            activePrepOrder.PlacementCompleted = true;
            activePrepOrder.PlacementScore = score;
            placementStatusText.text = "Encaje correcto. El objeto ya esta oculto dentro del pastel.";
            placementScoreText.text = "Precision: " + Mathf.RoundToInt(score * 100f) + "%";
            RefreshAllUi();
            ActivateTab(2, true);
            return;
        }

        placementStatusText.text = "Aun no encaja. Ajusta mejor la posicion y la rotacion.";
        placementScoreText.text = "Precision actual: " + Mathf.RoundToInt(score * 100f) + "%";
    }

    private void HandleBakeAction()
    {
        if (activePrepOrder == null || !activePrepOrder.PlacementCompleted)
        {
            return;
        }

        if (!activePrepOrder.BakeInProgress && activePrepOrder.BakeOutcome == BakeResult.Perfect)
        {
            return;
        }

        if (!activePrepOrder.BakeInProgress)
        {
            BeginBakeAttempt(activePrepOrder);
            RefreshBakeUi();
            return;
        }

        ResolveBake(activePrepOrder, false);
    }

    private void BeginBakeAttempt(RuntimeOrder order)
    {
        if (order == null)
        {
            return;
        }

        order.BakeOutcome = BakeResult.None;
        order.BakeInProgress = true;
        order.BakeProgressNormalized = 0f;
        EnsurePerfectBakeWindow(order, true);
    }

    private void EnsurePerfectBakeWindow(RuntimeOrder order, bool forceNewWindow = false)
    {
        if (order == null)
        {
            return;
        }

        if (!forceNewWindow && order.PerfectBakeWindowEnd > order.PerfectBakeWindowStart)
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
        if (activePrepOrder == null || activePrepOrder.BakeOutcome != BakeResult.Perfect || activePrepOrder.ToppingOptions == null)
        {
            return;
        }

        if (optionIndex < 0 || optionIndex >= activePrepOrder.ToppingOptions.Length)
        {
            return;
        }

        string selectedTopping = activePrepOrder.ToppingOptions[optionIndex];
        activePrepOrder.SelectedTopping = selectedTopping;
        activePrepOrder.ToppingMatched = string.Equals(selectedTopping, activePrepOrder.Data.topping, StringComparison.OrdinalIgnoreCase);

        if (!activePrepOrder.ToppingMatched)
        {
            toppingFeedbackText.text = "Este no es el topping adecuado del pastel.";
            RefreshToppingUi();
            return;
        }

        if (!CreateAndHoldCake(activePrepOrder))
        {
            toppingFeedbackText.text = "Las manos del jugador ya estan ocupadas. Suelta el pastel actual primero.";
            RefreshToppingUi();
            return;
        }

        activePrepOrder.CakeGenerated = true;
        activePrepOrder.State = OrderRuntimeState.Completed;
        activePrepOrder.FinalPayout = CalculateFinalPayout(activePrepOrder);
        toppingFeedbackText.text = "Pastel terminado. El jugador ahora lo lleva en las manos.";

        RuntimeOrder completedOrder = activePrepOrder;
        activePrepOrder = null;
        selectedOrder = completedOrder;
        RefreshAllUi();
        ActivateTab(0, true);
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
        RefreshTabAvailability();
        RefreshSizeTab();
        RefreshPlacementUi();
        RefreshBakeUi();
        RefreshToppingUi();
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
            bool isCompleted = runtimeOrder.State == OrderRuntimeState.Completed;
            bool isInteractable = IsOpen && runtimeOrder.State == OrderRuntimeState.Pending && (activePrepOrder == null || activePrepOrder == runtimeOrder);

            runtimeOrder.CardView.SetSelected(isSelected);
            runtimeOrder.CardView.SetActiveState(isActive);
            runtimeOrder.CardView.SetCompletedState(isCompleted);
            runtimeOrder.CardView.SetInteractable(isInteractable);

            if (isCompleted)
            {
                runtimeOrder.CardView.SetStatus("Lista", true);
            }
            else if (isActive)
            {
                runtimeOrder.CardView.SetStatus("Activa", true);
            }
            else
            {
                runtimeOrder.CardView.SetStatus(string.Empty, false);
            }
        }

        if (prepareButton != null)
        {
            bool canPrepare = selectedOrder != null
                && selectedOrder.State == OrderRuntimeState.Pending
                && (activePrepOrder == null || activePrepOrder == selectedOrder);
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
            if (detailCakeColorText != null) detailCakeColorText.text = "Color de pastel: --\nTamano: --";
            if (detailToppingText != null) detailToppingText.text = "Topping: --";
            if (detailItemLabelText != null) detailItemLabelText.text = "Objeto a colocar: --";
            if (detailPayoutText != null) detailPayoutText.text = "Paga: Q0";
            if (detailCellText != null) detailCellText.text = "Celda: --";
            if (detailStatusText != null) detailStatusText.text = "Estado: --";
            return;
        }

        OrderDefinition order = selectedOrder.Data;
        if (detailTitleText != null) detailTitleText.text = order.orderTitle;
        if (detailCakeColorText != null) detailCakeColorText.text = "Color de pastel: " + order.cakeColor + "\nTamano: " + GetCakeSizeLabel(selectedOrder.RequiredCakeSize);
        if (detailToppingText != null) detailToppingText.text = "Topping: " + order.topping;
        if (detailItemLabelText != null) detailItemLabelText.text = "Objeto a colocar: " + order.contrabandItem;
        if (detailPayoutText != null) detailPayoutText.text = "Paga: Q" + GetDisplayPayout(selectedOrder);
        if (detailCellText != null) detailCellText.text = "Celda: " + order.cellNumber;
        if (detailStatusText != null) detailStatusText.text = "Estado: " + GetOrderStatusLabel(selectedOrder);
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
            sizeOptionButtons[i].interactable = sizeTabAvailable;
            if (sizeOptionBackgrounds != null && i < sizeOptionBackgrounds.Length && sizeOptionBackgrounds[i] != null)
            {
                sizeOptionBackgrounds[i].color = isSelected ? tabActiveColor : tabIdleColor;
            }
        }

        if (activePrepOrder == null)
        {
            sizeFeedbackText.text = "Bloqueado hasta elegir una orden y pulsar Preparar.";
            return;
        }

        if (activePrepOrder.SizeMatched)
        {
            sizeFeedbackText.text = "Tamano confirmado: " + GetCakeSizeLabel(activePrepOrder.RequiredCakeSize) + ".";
            return;
        }

        if (activePrepOrder.SelectedCakeSize != CakeSizeOption.None)
        {
            sizeFeedbackText.text = "Seleccion actual: " + GetCakeSizeLabel(activePrepOrder.SelectedCakeSize) + ".";
            return;
        }

        sizeFeedbackText.text = "La orden requiere un tamano especifico.";
    }

    private void RefreshPlacementUi()
    {
        if (placementPlayArea == null || placementTargetRect == null || placementPieceRect == null || placementPieceView == null)
        {
            return;
        }

        bool hasActiveOrder = activePrepOrder != null;
        bool placementAvailable = hasActiveOrder && activePrepOrder.SizeMatched;
        placementPieceView.SetInteractable(placementAvailable && !activePrepOrder.PlacementCompleted);
        placementConfirmButton.interactable = placementAvailable && !activePrepOrder.PlacementCompleted;
        placementRotateLeftButton.interactable = placementAvailable && !activePrepOrder.PlacementCompleted;
        placementRotateRightButton.interactable = placementAvailable && !activePrepOrder.PlacementCompleted;

        if (!hasActiveOrder)
        {
            placementTargetRect.gameObject.SetActive(false);
            placementPieceRect.gameObject.SetActive(false);
            placementStatusText.text = "Bloqueado hasta confirmar el tamano.";
            placementScoreText.text = "Precision: 0%";
            return;
        }

        placementTargetRect.gameObject.SetActive(true);
        placementPieceRect.gameObject.SetActive(true);
        ConfigurePlacementRuntimeOrder(activePrepOrder);

        if (activePrepOrder.PlacementCompleted)
        {
            placementStatusText.text = "Objeto oculto correctamente dentro del pastel.";
            placementScoreText.text = "Precision: " + Mathf.RoundToInt(activePrepOrder.PlacementScore * 100f) + "%";
        }
        else if (!activePrepOrder.SizeMatched)
        {
            placementStatusText.text = "Primero debes elegir el tamano correcto.";
            placementScoreText.text = "Precision: 0%";
        }
        else
        {
            placementStatusText.text = "Arrastra la pieza y usa Girar +/- para alinearla.";
            placementScoreText.text = "Precision: " + Mathf.RoundToInt(GetPlacementScore(activePrepOrder) * 100f) + "%";
        }
    }

    private void RefreshBakeUi()
    {
        if (bakeStatusText == null || bakeActionButton == null || bakeButtonLabelText == null || bakeProgressFillImage == null)
        {
            return;
        }

        if (activePrepOrder == null)
        {
            bakeStatusText.text = "Estado: bloqueado";
            if (bakeHintText != null) bakeHintText.text = "Deten la barra dentro del area verde.";
            bakeActionButton.interactable = false;
            bakeButtonLabelText.text = "Iniciar horneado";
            SetBakeFillAmount(0f);
            RefreshPerfectBakeZoneUi(null);
            return;
        }

        if (!activePrepOrder.PlacementCompleted)
        {
            bakeStatusText.text = "Estado: bloqueado hasta colocar el objeto";
            if (bakeHintText != null) bakeHintText.text = "Primero oculta el objeto dentro del pastel.";
            bakeActionButton.interactable = false;
            bakeButtonLabelText.text = "Iniciar horneado";
            SetBakeFillAmount(0f);
            RefreshPerfectBakeZoneUi(activePrepOrder);
            return;
        }

        RefreshPerfectBakeZoneUi(activePrepOrder);
        SetBakeFillAmount(activePrepOrder.BakeProgressNormalized);

        if (activePrepOrder.BakeInProgress)
        {
            bakeStatusText.text = "Estado: horneando";
            if (bakeHintText != null) bakeHintText.text = "Saca el pastel dentro del area verde para que quede perfecto.";
            bakeActionButton.interactable = true;
            bakeButtonLabelText.text = "Sacar del horno";
            return;
        }

        if (activePrepOrder.BakeOutcome == BakeResult.None)
        {
            bakeStatusText.text = "Estado: listo para hornear";
            if (bakeHintText != null) bakeHintText.text = "El area verde cambia de lugar en cada intento.";
            bakeActionButton.interactable = true;
            bakeButtonLabelText.text = "Iniciar horneado";
            return;
        }

        bakeStatusText.text = "Estado: " + GetBakeResultLabel(activePrepOrder.BakeOutcome);
        bakeActionButton.interactable = activePrepOrder.BakeOutcome != BakeResult.Perfect;
        bakeButtonLabelText.text = activePrepOrder.BakeOutcome == BakeResult.Perfect ? "Horneado resuelto" : "Reintentar horneado";
        if (bakeHintText != null)
        {
            bakeHintText.text = activePrepOrder.BakeOutcome == BakeResult.Perfect
                ? "Perfecto. Ya puedes pasar al topping."
                : "Fallaste el punto. Debes volver a hornear hasta que quede perfecto.";
        }
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
        bool toppingAvailable = activePrepOrder.BakeOutcome == BakeResult.Perfect;
        SetToppingButtonsInteractable(toppingAvailable);

        for (int i = 0; i < toppingOptionButtons.Length; i++)
        {
            if (toppingOptionLabelTexts != null && i < toppingOptionLabelTexts.Length && toppingOptionLabelTexts[i] != null)
            {
                toppingOptionLabelTexts[i].text = activePrepOrder.ToppingOptions != null && i < activePrepOrder.ToppingOptions.Length
                    ? activePrepOrder.ToppingOptions[i]
                    : "--";
            }

            if (toppingOptionBackgrounds != null && i < toppingOptionBackgrounds.Length && toppingOptionBackgrounds[i] != null)
            {
                bool isSelected = activePrepOrder.ToppingOptions != null
                    && i < activePrepOrder.ToppingOptions.Length
                    && string.Equals(activePrepOrder.SelectedTopping, activePrepOrder.ToppingOptions[i], StringComparison.OrdinalIgnoreCase);
                toppingOptionBackgrounds[i].color = isSelected ? tabActiveColor : tabIdleColor;
            }
        }

        if (!toppingAvailable)
        {
            toppingFeedbackText.text = "Consigue un horneado perfecto antes de agregar el topping.";
        }
        else if (activePrepOrder.CakeGenerated)
        {
            toppingFeedbackText.text = "Pastel terminado y entregado al jugador.";
        }
        else if (activePrepOrder.SelectedTopping != null)
        {
            toppingFeedbackText.text = activePrepOrder.ToppingMatched
                ? "Topping correcto. Genera el pastel."
                : "Ese topping no coincide con la orden.";
        }
        else
        {
            toppingFeedbackText.text = "Elige el topping pedido por el preso.";
        }
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
            case 0:
                return true;
            case 1:
                return activePrepOrder.SizeMatched;
            case 2:
                return activePrepOrder.PlacementCompleted;
            case 3:
                return activePrepOrder.BakeOutcome == BakeResult.Perfect;
            default:
                return false;
        }
    }

    private int GetFirstAvailableTabIndex(RuntimeOrder order)
    {
        if (order == null || order.State != OrderRuntimeState.ActivePrep)
        {
            return 0;
        }

        if (!order.SizeMatched)
        {
            return 0;
        }

        if (!order.PlacementCompleted)
        {
            return 1;
        }

        if (order.BakeOutcome != BakeResult.Perfect)
        {
            return 2;
        }

        return 3;
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

        int total = 0;
        for (int i = 0; i < allOrders.Count; i++)
        {
            total += allOrders[i].BasePayoutValue;
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

    private void UpdateBakeState()
    {
        if (!IsOpen || activePrepOrder == null || !activePrepOrder.BakeInProgress)
        {
            return;
        }

        activePrepOrder.BakeProgressNormalized += Time.deltaTime / BakeDurationSeconds;
        if (activePrepOrder.BakeProgressNormalized >= 1f)
        {
            activePrepOrder.BakeProgressNormalized = 1f;
            ResolveBake(activePrepOrder, true);
            return;
        }

        RefreshBakeUi();
    }

    private void ResolveBake(RuntimeOrder order, bool forcedBurnt)
    {
        if (order == null)
        {
            return;
        }

        order.BakeInProgress = false;

        if (forcedBurnt)
        {
            order.BakeOutcome = BakeResult.Burnt;
        }
        else if (order.BakeProgressNormalized < order.PerfectBakeWindowStart)
        {
            order.BakeOutcome = BakeResult.Undercooked;
        }
        else if (order.BakeProgressNormalized <= order.PerfectBakeWindowEnd)
        {
            order.BakeOutcome = BakeResult.Perfect;
        }
        else
        {
            order.BakeOutcome = BakeResult.Burnt;
        }

        RefreshAllUi();
        ActivateTab(order.BakeOutcome == BakeResult.Perfect ? 3 : 2, true);
    }

    private void ConfigurePlacementRuntimeOrder(RuntimeOrder order)
    {
        if (order == null)
        {
            return;
        }

        Vector2 targetSize = GetShapeSize(order.ShapeType);
        placementTargetRect.sizeDelta = targetSize;
        placementTargetRect.anchoredPosition = order.TargetAnchoredPosition;
        placementTargetRect.localRotation = Quaternion.Euler(0f, 0f, order.TargetRotationDegrees);
        placementTargetLabelText.text = GetShapeLabel(order.ShapeType);

        placementPieceRect.sizeDelta = targetSize;
        placementPieceLabelText.text = order.Data.contrabandItem;

        if (!order.PiecePoseInitialized)
        {
            order.PieceAnchoredPosition = GetDefaultPieceStartPosition(order.ShapeType);
            order.PieceRotationDegrees = NormalizeAngle(order.TargetRotationDegrees + 45f);
            order.PiecePoseInitialized = true;
        }

        if (order.PlacementCompleted)
        {
            order.PieceAnchoredPosition = order.TargetAnchoredPosition;
            order.PieceRotationDegrees = order.TargetRotationDegrees;
        }

        placementPieceRect.anchoredPosition = order.PieceAnchoredPosition;
        placementPieceRect.localRotation = Quaternion.Euler(0f, 0f, order.PieceRotationDegrees);
    }

    private float GetPlacementScore(RuntimeOrder order)
    {
        if (order == null)
        {
            return 0f;
        }

        float distance = Vector2.Distance(order.PieceAnchoredPosition, order.TargetAnchoredPosition);
        float distanceScore = 1f - Mathf.Clamp01(distance / 180f);
        float rotationDelta = GetRotationDelta(order.PieceRotationDegrees, order.TargetRotationDegrees, GetShapeRotationSymmetry(order.ShapeType));
        float rotationScore = 1f - Mathf.Clamp01(rotationDelta / 90f);
        return Mathf.Clamp01(distanceScore * 0.65f + rotationScore * 0.35f);
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

        float placementMultiplier = Mathf.Lerp(0.7f, 1.25f, Mathf.Clamp01(order.PlacementScore));
        float bakeMultiplier = 1f;
        switch (order.BakeOutcome)
        {
            case BakeResult.Undercooked:
                bakeMultiplier = 0.75f;
                break;
            case BakeResult.Perfect:
                bakeMultiplier = 1.15f;
                break;
            case BakeResult.Burnt:
                bakeMultiplier = 0.6f;
                break;
        }

        return Mathf.RoundToInt(order.BasePayoutValue * placementMultiplier * bakeMultiplier);
    }

    private bool CreateAndHoldCake(RuntimeOrder order)
    {
        EnsurePlayerController();
        if (playerController == null || playerController.HasHeldCake)
        {
            return false;
        }

        GameObject cakeObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cakeObject.name = "CakePickup_" + order.Data.id;
        cakeObject.transform.localScale = new Vector3(0.38f, 0.12f, 0.38f);

        Renderer renderer = cakeObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = GetCakeColorFromTopping(order.SelectedTopping);
        }

        CakePickup cakePickup = cakeObject.AddComponent<CakePickup>();
        cakePickup.Initialize(order.Data.id, order.Data.orderTitle, CalculateFinalPayout(order));
        return playerController.TryHoldCake(cakePickup);
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

    private void SetBakeFillAmount(float normalized)
    {
        if (bakeProgressFillImage == null)
        {
            return;
        }

        RectTransform fillRect = bakeProgressFillImage.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    private RuntimeOrder GetFirstPendingSpawnedOrder()
    {
        for (int i = 0; i < spawnedOrders.Count; i++)
        {
            if (spawnedOrders[i].State == OrderRuntimeState.Pending)
            {
                return spawnedOrders[i];
            }
        }

        return null;
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

    private static PlacementShapeType ParseShapeType(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return PlacementShapeType.Square;
        }

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Contains("tri"))
        {
            return PlacementShapeType.Triangle;
        }

        if (normalized.Contains("rect"))
        {
            return PlacementShapeType.Rectangle;
        }

        return PlacementShapeType.Square;
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

    private static string GetShapeLabel(PlacementShapeType shapeType)
    {
        switch (shapeType)
        {
            case PlacementShapeType.Rectangle:
                return "RECT";
            case PlacementShapeType.Triangle:
                return "TRI";
            case PlacementShapeType.Square:
            default:
                return "CUAD";
        }
    }

    private static Vector2 GetShapeSize(PlacementShapeType shapeType)
    {
        switch (shapeType)
        {
            case PlacementShapeType.Rectangle:
                return new Vector2(138f, 82f);
            case PlacementShapeType.Triangle:
                return new Vector2(106f, 106f);
            case PlacementShapeType.Square:
            default:
                return new Vector2(96f, 96f);
        }
    }

    private static Vector2 GetDefaultPieceStartPosition(PlacementShapeType shapeType)
    {
        switch (shapeType)
        {
            case PlacementShapeType.Rectangle:
                return new Vector2(-170f, -88f);
            case PlacementShapeType.Triangle:
                return new Vector2(-160f, -74f);
            case PlacementShapeType.Square:
            default:
                return new Vector2(-165f, -80f);
        }
    }

    private static Vector2 GetPlacementTargetPosition(int runtimeIndex)
    {
        Vector2[] presets =
        {
            new Vector2(90f, 36f),
            new Vector2(-40f, 54f),
            new Vector2(44f, -18f),
            new Vector2(-88f, 12f),
            new Vector2(0f, 72f),
            new Vector2(108f, -44f),
            new Vector2(-100f, -30f),
            new Vector2(26f, 8f)
        };

        return presets[runtimeIndex % presets.Length];
    }

    private static float GetPlacementTargetRotation(int runtimeIndex, PlacementShapeType shapeType)
    {
        switch (shapeType)
        {
            case PlacementShapeType.Triangle:
                return (runtimeIndex % 3) * 30f;
            case PlacementShapeType.Rectangle:
                return (runtimeIndex % 2) * 90f;
            case PlacementShapeType.Square:
            default:
                return (runtimeIndex % 4) * 22.5f;
        }
    }

    private static float GetShapeRotationSymmetry(PlacementShapeType shapeType)
    {
        switch (shapeType)
        {
            case PlacementShapeType.Rectangle:
                return 180f;
            case PlacementShapeType.Triangle:
                return 120f;
            case PlacementShapeType.Square:
            default:
                return 90f;
        }
    }

    private static float GetRotationDelta(float currentDegrees, float targetDegrees, float symmetry)
    {
        float delta = Mathf.Abs(Mathf.DeltaAngle(currentDegrees, targetDegrees));
        if (symmetry <= 0f)
        {
            return delta;
        }

        float wrapped = delta % symmetry;
        return Mathf.Min(wrapped, symmetry - wrapped);
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

            if (order.SizeMatched)
            {
                return "Esperando objeto";
            }

            return "Preparando";
        }

        return "Pendiente";
    }

    private static string GetBakeResultLabel(BakeResult result)
    {
        switch (result)
        {
            case BakeResult.Undercooked:
                return "crudo";
            case BakeResult.Perfect:
                return "perfecto";
            case BakeResult.Burnt:
                return "quemado";
            case BakeResult.None:
            default:
                return "sin empezar";
        }
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

    private static Color GetCakeColor(string colorName)
    {
        if (string.IsNullOrEmpty(colorName))
        {
            return new Color(0.95f, 0.87f, 0.72f, 1f);
        }

        string normalized = colorName.Trim().ToLowerInvariant();
        if (normalized.Contains("rosa"))
        {
            return new Color(0.96f, 0.62f, 0.74f, 1f);
        }

        if (normalized.Contains("mora") || normalized.Contains("morado"))
        {
            return new Color(0.60f, 0.45f, 0.78f, 1f);
        }

        if (normalized.Contains("blanco") || normalized.Contains("crema") || normalized.Contains("beige"))
        {
            return new Color(0.95f, 0.91f, 0.82f, 1f);
        }

        if (normalized.Contains("cafe") || normalized.Contains("choco"))
        {
            return new Color(0.46f, 0.28f, 0.16f, 1f);
        }

        return new Color(0.95f, 0.87f, 0.72f, 1f);
    }

    private static Color GetCakeColorFromTopping(string toppingName)
    {
        if (string.IsNullOrEmpty(toppingName))
        {
            return new Color(0.95f, 0.87f, 0.72f, 1f);
        }

        string normalized = toppingName.Trim().ToLowerInvariant();
        if (normalized.Contains("crema"))
        {
            return new Color(0.96f, 0.92f, 0.82f, 1f);
        }

        if (normalized.Contains("choco"))
        {
            return new Color(0.46f, 0.28f, 0.16f, 1f);
        }

        if (normalized.Contains("mora"))
        {
            return new Color(0.58f, 0.42f, 0.76f, 1f);
        }

        if (normalized.Contains("fresa"))
        {
            return new Color(0.92f, 0.56f, 0.66f, 1f);
        }

        if (normalized.Contains("glaseado"))
        {
            return new Color(0.90f, 0.90f, 0.94f, 1f);
        }

        if (normalized.Contains("azucar"))
        {
            return new Color(0.97f, 0.97f, 0.97f, 1f);
        }

        if (normalized.Contains("caramelo"))
        {
            return new Color(0.76f, 0.55f, 0.28f, 1f);
        }

        return new Color(0.95f, 0.87f, 0.72f, 1f);
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
