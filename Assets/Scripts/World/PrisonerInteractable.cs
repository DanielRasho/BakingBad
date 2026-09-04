using UnityEngine;

[DisallowMultipleComponent]
public class PrisonerInteractable : MonoBehaviour
{
    [SerializeField] private string cellId;
    [SerializeField] private string cellLabel;
    [SerializeField] private string currentOrderId;
    [SerializeField] private string currentOrderTitle;
    [SerializeField] private float cooldownEndsAt;

    private PrisonOrderManager manager;

    public string CellId
    {
        get { return cellId; }
    }

    public string CellLabel
    {
        get { return string.IsNullOrEmpty(cellLabel) ? cellId : cellLabel; }
    }

    public string CurrentOrderId
    {
        get { return currentOrderId; }
    }

    public bool HasActiveOrder
    {
        get { return !string.IsNullOrEmpty(currentOrderId); }
    }

    public bool IsCoolingDown
    {
        get { return cooldownEndsAt > Time.time; }
    }

    public float CooldownRemaining
    {
        get { return Mathf.Max(0f, cooldownEndsAt - Time.time); }
    }

    public string MapState
    {
        get
        {
            if (HasActiveOrder)
            {
                return "Pedido";
            }

            if (IsCoolingDown)
            {
                return "Cooldown";
            }

            return "Libre";
        }
    }

    public void Initialize(PrisonOrderManager owner, string nextCellId, string nextCellLabel)
    {
        manager = owner;
        cellId = nextCellId;
        cellLabel = nextCellLabel;
        currentOrderId = string.Empty;
        currentOrderTitle = string.Empty;
        cooldownEndsAt = 0f;
    }

    public void Interact(PrisonCookPlayerController player)
    {
        if (manager == null)
        {
            manager = FindAnyObjectByType<PrisonOrderManager>();
        }

        if (manager != null)
        {
            manager.HandlePrisonerInteraction(this, player);
        }
    }

    public void AssignOrder(KitchenOrderPrepUIController.OrderDefinition order)
    {
        currentOrderId = order != null ? order.id : string.Empty;
        currentOrderTitle = order != null ? order.orderTitle : string.Empty;
    }

    public void MarkDelivered(float cooldownSeconds)
    {
        currentOrderId = string.Empty;
        currentOrderTitle = string.Empty;
        cooldownEndsAt = Time.time + Mathf.Max(0f, cooldownSeconds);
    }
}
