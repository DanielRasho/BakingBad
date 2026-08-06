using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class KitchenOrderPrepUIController : MonoBehaviour
{
    [Serializable]
    private class OrderData
    {
        public string cellNumber;
        public string payout;
        public string orderTitle;
        public string cakeColor;
        public string topping;
        public string itemLabel;
    }

    [Header("Root")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button prepareButton;
    [SerializeField] private PrisonCookPlayerController playerController;

    [Header("Order Cards")]
    [SerializeField] private Button[] orderButtons;
    [SerializeField] private Image[] orderSelectionFrames;
    [SerializeField] private Text[] orderCellTexts;
    [SerializeField] private Text[] orderPayoutTexts;

    [Header("Tabs")]
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private Image[] tabBackgrounds;
    [SerializeField] private GameObject[] tabPanels;
    [SerializeField] private Text centerStageTitle;

    [Header("Detail Panel")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private Text detailTitleText;
    [SerializeField] private Text detailCakeColorText;
    [SerializeField] private Text detailToppingText;
    [SerializeField] private Text detailItemLabelText;
    [SerializeField] private Text detailPayoutText;
    [SerializeField] private Text detailCellText;

    [Header("Visuals")]
    [SerializeField] private Color cardIdleColor = new Color(0.98f, 0.97f, 0.94f, 1f);
    [SerializeField] private Color cardSelectedColor = new Color(1f, 0.95f, 0.82f, 1f);
    [SerializeField] private Color tabIdleColor = new Color(0.97f, 0.96f, 0.93f, 1f);
    [SerializeField] private Color tabLockedColor = new Color(0.85f, 0.84f, 0.80f, 1f);
    [SerializeField] private Color tabActiveColor = new Color(1f, 0.92f, 0.76f, 1f);

    [Header("Sample Data")]
    [SerializeField] private OrderData[] orders =
    {
        new OrderData
        {
            cellNumber = "40",
            payout = "Q50",
            orderTitle = "Pastel de fresas con crema",
            cakeColor = "Color de pastel: Rosa",
            topping = "Topping: Crema y fresa",
            itemLabel = "Objeto a colocar: Fresa",
        },
        new OrderData
        {
            cellNumber = "110",
            payout = "Q60",
            orderTitle = "Pastel de vainilla con mora",
            cakeColor = "Color de pastel: Crema",
            topping = "Topping: Mora",
            itemLabel = "Objeto a colocar: Mora",
        },
        new OrderData
        {
            cellNumber = "60",
            payout = "Q35",
            orderTitle = "Pastel sencillo con glaseado",
            cakeColor = "Color de pastel: Blanco",
            topping = "Topping: Glaseado",
            itemLabel = "Objeto a colocar: Glaseado",
        }
    };

    private readonly string[] tabTitles =
    {
        "Seleccionar tamano",
        "Colocar objeto",
        "Hornear",
        "Topping"
    };

    private int selectedOrderIndex = -1;
    private int activeTabIndex;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool isInitialized;
    private bool playerLockHeld;

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
        SetSelectedOrder(selectedOrderIndex >= 0 ? selectedOrderIndex : 0);
        SetTabsUnlocked(true);
        ActivateTab(0, true);
    }

    public void Close()
    {
        InitializeUi();
        SetSelectedOrder(-1);
        SetTabsUnlocked(true);
        ActivateTab(0, true);
        SetMenuVisible(false);
        SetPlayerInputLocked(false);
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
    }

    public void RefreshBindings()
    {
        isInitialized = false;
        InitializeUi();
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

        for (int i = 0; i < orderButtons.Length; i++)
        {
            Button button = orderButtons[i];
            if (button == null)
            {
                continue;
            }

            int capturedIndex = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SetSelectedOrder(capturedIndex));
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

    private void SyncOrderCards()
    {
        for (int i = 0; i < orderCellTexts.Length; i++)
        {
            if (orderCellTexts[i] != null)
            {
                orderCellTexts[i].text = i < orders.Length ? orders[i].cellNumber : "--";
            }

            if (orderPayoutTexts[i] != null)
            {
                orderPayoutTexts[i].text = i < orders.Length ? orders[i].payout : "Q0";
            }
        }
    }

    private void InitializeUi()
    {
        if (isInitialized)
        {
            return;
        }

        WireButtons();
        SyncOrderCards();
        SetMenuVisible(false);
        SetSelectedOrder(-1);
        SetTabsUnlocked(true);
        ActivateTab(0, true);
        SetPlayerInputLocked(false);
        isInitialized = true;
    }

    private void PrepareSelectedOrder()
    {
        if (selectedOrderIndex < 0 || selectedOrderIndex >= orders.Length)
        {
            return;
        }

        ActivateTab(activeTabIndex, true);
    }

    private void SetSelectedOrder(int orderIndex)
    {
        selectedOrderIndex = orderIndex >= 0 && orderIndex < orders.Length ? orderIndex : -1;

        if (detailPanel != null)
        {
            detailPanel.SetActive(selectedOrderIndex >= 0);
        }

        for (int i = 0; i < orderSelectionFrames.Length; i++)
        {
            if (orderSelectionFrames[i] == null)
            {
                continue;
            }

            orderSelectionFrames[i].color = i == selectedOrderIndex ? cardSelectedColor : cardIdleColor;
        }

        if (prepareButton != null)
        {
            prepareButton.interactable = selectedOrderIndex >= 0;
        }

        if (selectedOrderIndex < 0)
        {
            return;
        }

        OrderData order = orders[selectedOrderIndex];
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
        activeTabIndex = Mathf.Clamp(tabIndex, 0, tabPanels.Length - 1);

        for (int i = 0; i < tabPanels.Length; i++)
        {
            if (tabPanels[i] != null)
            {
                tabPanels[i].SetActive(i == activeTabIndex);
            }

            if (tabBackgrounds[i] != null)
            {
                if (i == activeTabIndex)
                {
                    tabBackgrounds[i].color = tabActiveColor;
                }
                else
                {
                    tabBackgrounds[i].color = tabIdleColor;
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

        SetOrderButtonsInteractable(visible);
    }

    private void SetOrderButtonsInteractable(bool interactable)
    {
        for (int i = 0; i < orderButtons.Length; i++)
        {
            if (orderButtons[i] != null)
            {
                orderButtons[i].interactable = interactable;
            }
        }
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
}
