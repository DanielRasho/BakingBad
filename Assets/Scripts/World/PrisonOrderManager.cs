using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class PrisonOrderManager : MonoBehaviour
{
    [Serializable]
    private class OrderDefinitionCollection
    {
        public KitchenOrderPrepUIController.OrderDefinition[] orders;
    }

    [Header("Data")]
    [SerializeField] private TextAsset ordersJson;
    [SerializeField] private Transform jailCellsRoot;
    [SerializeField] private PrisonCookPlayerController player;
    [SerializeField] private KitchenOrderPrepUIController orderUi;

    [Header("Timing")]
    [SerializeField] private float dayDurationSeconds = 600f;
    [SerializeField] private float prisonerOrderCooldownSeconds = 15f;

    [Header("Prisoners")]
    [SerializeField] private Transform prisonerVisualSource;
    [SerializeField] private Vector3 prisonerVisualOffset = Vector3.zero;
    [SerializeField] private Vector3 prisonerColliderCenter = new Vector3(0f, 0.75f, 0f);
    [SerializeField] private Vector3 prisonerColliderSize = new Vector3(0.9f, 1.5f, 0.9f);

    [Header("Debug")]
    [SerializeField] private bool logOrderFlow = true;

    private readonly List<KitchenOrderPrepUIController.OrderDefinition> availableOrders = new List<KitchenOrderPrepUIController.OrderDefinition>();
    private readonly List<PrisonerInteractable> prisoners = new List<PrisonerInteractable>();
    private float remainingDaySeconds;
    private int earnedMoney;
    private int deliveredOrders;
    private bool dayEnded;
    private bool mapVisible;
    private bool playerLockHeld;
    private bool referencesResolved;

    private void Start()
    {
        remainingDaySeconds = dayDurationSeconds;
        StartCoroutine(InitializeRoutine());
    }

    private void Update()
    {
        if (!referencesResolved)
        {
            ResolveReferences();
        }

        HandleMapInput();
        UpdateDayTimer();
    }

    private void OnDisable()
    {
        if (playerLockHeld && player != null)
        {
            player.RemoveInputLock();
            playerLockHeld = false;
        }
    }

    private IEnumerator InitializeRoutine()
    {
        ResolveReferences();
        LoadOrderPool();
        SpawnPrisoners();

        while (orderUi == null)
        {
            ResolveReferences();
            yield return null;
        }

        SyncHud();
    }

    public void HandlePrisonerInteraction(PrisonerInteractable prisoner, PrisonCookPlayerController interactingPlayer)
    {
        if (dayEnded || prisoner == null)
        {
            return;
        }

        if (interactingPlayer == null)
        {
            interactingPlayer = player;
        }

        if (TryDeliverOrder(prisoner, interactingPlayer))
        {
            return;
        }

        TryRequestOrder(prisoner);
    }

    private bool TryDeliverOrder(PrisonerInteractable prisoner, PrisonCookPlayerController interactingPlayer)
    {
        if (interactingPlayer == null || !prisoner.HasActiveOrder)
        {
            return false;
        }

        CakePickup cake;
        if (!interactingPlayer.TryDeliverHeldCake(prisoner.CurrentOrderId, out cake))
        {
            if (interactingPlayer.HasHeldCake && logOrderFlow)
            {
                Debug.Log("Delivery rejected at " + prisoner.CellLabel + ": wrong cake for order " + prisoner.CurrentOrderId + ".");
            }

            return false;
        }

        int payout = cake != null ? cake.PayoutValue : 0;
        earnedMoney += payout;
        deliveredOrders++;
        if (cake != null)
        {
            Destroy(cake.gameObject);
        }

        if (orderUi != null)
        {
            orderUi.RemoveOrder(prisoner.CurrentOrderId);
            orderUi.SetMoney(earnedMoney);
        }

        prisoner.MarkDelivered(prisonerOrderCooldownSeconds);
        RefreshMapIfVisible();

        if (logOrderFlow)
        {
            Debug.Log("Delivered order to " + prisoner.CellLabel + " for Q" + payout + ".");
        }

        return true;
    }

    private bool TryRequestOrder(PrisonerInteractable prisoner)
    {
        ResolveReferences();
        if (orderUi == null)
        {
            Debug.LogWarning("Cannot request an order yet because KitchenUI has not finished loading.");
            return false;
        }

        if (prisoner.HasActiveOrder)
        {
            if (logOrderFlow)
            {
                Debug.Log(prisoner.CellLabel + " already has an active order.");
            }

            return false;
        }

        if (prisoner.IsCoolingDown)
        {
            if (logOrderFlow)
            {
                Debug.Log(prisoner.CellLabel + " is cooling down for " + Mathf.CeilToInt(prisoner.CooldownRemaining) + "s.");
            }

            return false;
        }

        if (availableOrders.Count == 0)
        {
            if (logOrderFlow)
            {
                Debug.Log("No daily orders left in the pool.");
            }

            return false;
        }

        int orderIndex = UnityEngine.Random.Range(0, availableOrders.Count);
        KitchenOrderPrepUIController.OrderDefinition assignedOrder = CloneOrderForPrisoner(availableOrders[orderIndex], prisoner);
        availableOrders.RemoveAt(orderIndex);
        prisoner.AssignOrder(assignedOrder);

        if (orderUi != null)
        {
            orderUi.AddOrder(assignedOrder);
        }

        RefreshMapIfVisible();

        if (logOrderFlow)
        {
            Debug.Log(prisoner.CellLabel + " requested " + assignedOrder.orderTitle + " (" + assignedOrder.id + ").");
        }

        return true;
    }

    private void UpdateDayTimer()
    {
        if (dayEnded)
        {
            return;
        }

        remainingDaySeconds = Mathf.Max(0f, remainingDaySeconds - Time.deltaTime);
        if (orderUi != null)
        {
            orderUi.SetTimerSeconds(remainingDaySeconds);
        }

        if (remainingDaySeconds <= 0f)
        {
            EndDay();
        }
    }

    private void EndDay()
    {
        dayEnded = true;
        if (player != null && !playerLockHeld)
        {
            player.AddInputLock();
            playerLockHeld = true;
        }

        if (orderUi != null)
        {
            orderUi.SetMapVisible(false, Vector3.zero, null);
            orderUi.ShowDayResults(earnedMoney, deliveredOrders);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void HandleMapInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.mKey.wasPressedThisFrame || dayEnded)
        {
            return;
        }

        mapVisible = !mapVisible;
        RefreshMapIfVisible();
#endif
    }

    private void RefreshMapIfVisible()
    {
        if (orderUi == null)
        {
            return;
        }

        if (!mapVisible)
        {
            orderUi.SetMapVisible(false, Vector3.zero, null);
            return;
        }

        Vector3 playerPosition = player != null ? player.transform.position : Vector3.zero;
        orderUi.SetMapVisible(true, playerPosition, prisoners);
    }

    private void SyncHud()
    {
        if (orderUi == null)
        {
            return;
        }

        orderUi.SetMoney(earnedMoney);
        orderUi.SetTimerSeconds(remainingDaySeconds);
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PrisonCookPlayerController>();
        }

        if (orderUi == null)
        {
            orderUi = FindAnyObjectByType<KitchenOrderPrepUIController>(FindObjectsInactive.Include);
        }

        if (jailCellsRoot == null)
        {
            jailCellsRoot = FindTransformByName("JailCells");
        }

        referencesResolved = player != null && orderUi != null && jailCellsRoot != null;
    }

    private void LoadOrderPool()
    {
        availableOrders.Clear();
        if (ordersJson == null)
        {
            Debug.LogError("PrisonOrderManager requires an orders JSON TextAsset.");
            return;
        }

        OrderDefinitionCollection collection = JsonUtility.FromJson<OrderDefinitionCollection>(ordersJson.text);
        if (collection == null || collection.orders == null)
        {
            Debug.LogError("PrisonOrderManager failed to parse order JSON.");
            return;
        }

        for (int i = 0; i < collection.orders.Length; i++)
        {
            if (collection.orders[i] != null)
            {
                availableOrders.Add(collection.orders[i]);
            }
        }
    }

    private void SpawnPrisoners()
    {
        if (jailCellsRoot == null)
        {
            Debug.LogError("PrisonOrderManager could not find JailCells root.");
            return;
        }

        prisoners.Clear();
        for (int i = 0; i < jailCellsRoot.childCount; i++)
        {
            Transform cellPoint = jailCellsRoot.GetChild(i);
            if (cellPoint == null)
            {
                continue;
            }

            PrisonerInteractable existingPrisoner = cellPoint.GetComponentInChildren<PrisonerInteractable>(true);
            if (existingPrisoner != null)
            {
                existingPrisoner.Initialize(this, cellPoint.name, GetCellLabel(cellPoint.name));
                prisoners.Add(existingPrisoner);
                continue;
            }

            GameObject prisonerObject = new GameObject("Prisoner_" + cellPoint.name);
            prisonerObject.transform.SetParent(cellPoint, false);
            prisonerObject.transform.localPosition = Vector3.zero;
            prisonerObject.transform.localRotation = Quaternion.identity;

            CopyPrisonerVisual(prisonerObject.transform);

            BoxCollider collider = prisonerObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = prisonerColliderCenter;
            collider.size = prisonerColliderSize;

            PrisonerInteractable prisoner = prisonerObject.AddComponent<PrisonerInteractable>();
            prisoner.Initialize(this, cellPoint.name, GetCellLabel(cellPoint.name));
            prisoners.Add(prisoner);
        }
    }

    private void CopyPrisonerVisual(Transform prisonerRoot)
    {
        Transform source = prisonerVisualSource != null
            ? prisonerVisualSource
            : player != null ? player.transform : null;

        if (source == null)
        {
            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            placeholder.name = "PrisonerPlaceholder";
            placeholder.transform.SetParent(prisonerRoot, false);
            placeholder.transform.localPosition = prisonerVisualOffset + new Vector3(0f, 0.75f, 0f);
            placeholder.transform.localScale = new Vector3(0.55f, 0.75f, 0.55f);
            Destroy(placeholder.GetComponent<Collider>());
            return;
        }

        for (int i = 0; i < source.childCount; i++)
        {
            Transform visualChild = source.GetChild(i);
            GameObject clone = Instantiate(visualChild.gameObject, prisonerRoot);
            clone.name = visualChild.name;
            clone.transform.localPosition = visualChild.localPosition + prisonerVisualOffset;
            clone.transform.localRotation = visualChild.localRotation;
            clone.transform.localScale = visualChild.localScale;
        }
    }

    private static KitchenOrderPrepUIController.OrderDefinition CloneOrderForPrisoner(KitchenOrderPrepUIController.OrderDefinition source, PrisonerInteractable prisoner)
    {
        string orderId = source.id + "_" + prisoner.CellId;
        return new KitchenOrderPrepUIController.OrderDefinition
        {
            id = orderId,
            cellNumber = prisoner.CellLabel,
            payout = source.payout,
            basePayout = source.basePayout,
            orderTitle = source.orderTitle,
            cakeColor = source.cakeColor,
            topping = source.topping,
            contrabandItem = source.contrabandItem,
            cakeSize = source.cakeSize,
            shapeType = source.shapeType,
            spriteId = source.spriteId
        };
    }

    private static string GetCellLabel(string cellId)
    {
        if (string.IsNullOrEmpty(cellId))
        {
            return "Celda";
        }

        if (cellId.StartsWith("Cell", StringComparison.OrdinalIgnoreCase))
        {
            return "Celda " + cellId.Substring(4);
        }

        return cellId;
    }

    private static Transform FindTransformByName(string objectName)
    {
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            if (allTransforms[i].name == objectName)
            {
                return allTransforms[i];
            }
        }

        return null;
    }
}
