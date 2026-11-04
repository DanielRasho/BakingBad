using UnityEngine;

[DisallowMultipleComponent]
public class PrisonerInteractable : MonoBehaviour
{
    [SerializeField] private string cellId;
    [SerializeField] private string cellLabel;
    [SerializeField] private string currentOrderId;
    [SerializeField] private string currentOrderTitle;
    [SerializeField] private float cooldownEndsAt;

    private const float PromptWorldSize = 0.42f;
    private const float PromptHeightAboveSprite = 0.6f;
    private const float PromptPulseSpeed = 5f;
    private const float PromptPulseAmount = 0.1f;

    private static Sprite promptSprite;

    private PrisonOrderManager manager;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer promptRenderer;
    private bool promptRequested;

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

    // The player calls this for the prisoner it would interact with, so the key only shows when pressing it works.
    public void SetPromptVisible(bool visible)
    {
        promptRequested = visible;
        if (!visible && promptRenderer != null)
        {
            promptRenderer.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!promptRequested)
        {
            return;
        }

        if (promptRenderer == null)
        {
            CreatePrompt();
        }

        // A prisoner cooling down ignores the key, so it isn't offered.
        promptRenderer.enabled = !IsCoolingDown;

        Vector3 top = bodyRenderer != null ? bodyRenderer.bounds.center + Vector3.up * bodyRenderer.bounds.extents.y : transform.position + Vector3.up * 1.5f;
        Transform prompt = promptRenderer.transform;
        prompt.position = top + Vector3.up * PromptHeightAboveSprite;

        Camera viewCamera = Camera.main;
        if (viewCamera != null)
        {
            prompt.rotation = viewCamera.transform.rotation;
        }

        float pulse = 1f + PromptPulseAmount * Mathf.Sin(Time.unscaledTime * PromptPulseSpeed);
        Vector3 parentScale = transform.lossyScale;
        float size = PromptWorldSize / promptRenderer.sprite.bounds.size.x * pulse;
        prompt.localScale = new Vector3(size / parentScale.x, size / parentScale.y, size / parentScale.z);
    }

    private void CreatePrompt()
    {
        bodyRenderer = GetComponent<SpriteRenderer>();

        GameObject promptObject = new GameObject("InteractPrompt");
        promptObject.transform.SetParent(transform, false);
        promptObject.layer = gameObject.layer;
        promptRenderer = promptObject.AddComponent<SpriteRenderer>();
        promptRenderer.sprite = GetPromptSprite();
        promptRenderer.sortingOrder = 100;
    }

    // A keyboard key with an "E" on it.
    private static Sprite GetPromptSprite()
    {
        if (promptSprite == null)
        {
            promptSprite = ProceduralSprites.Create("InteractPromptE", 64, 64, new Vector2(0.5f, 0.5f), SamplePromptKey);
        }

        return promptSprite;
    }

    private static Color SamplePromptKey(float px, float py)
    {
        Color faceColor = new Color(1f, 0.98f, 0.90f, 1f);
        Color sideColor = new Color(0.82f, 0.70f, 0.58f, 1f);
        Color inkColor = new Color(0.35f, 0.18f, 0.12f, 1f);
        const float outlineWidth = 3f;

        Vector2 point = new Vector2(px, py);
        float face = RoundedBoxDistance(point, new Vector2(32f, 36f), new Vector2(25f, 23f), 9f);
        float key = RoundedBoxDistance(point, new Vector2(32f, 32f), new Vector2(25f, 27f), 9f);

        float letter = ProceduralSprites.CapsuleDistance(point, new Vector2(23f, 25f), new Vector2(23f, 47f), 3.2f);
        letter = Mathf.Min(letter, ProceduralSprites.CapsuleDistance(point, new Vector2(23f, 47f), new Vector2(41f, 47f), 3.2f));
        letter = Mathf.Min(letter, ProceduralSprites.CapsuleDistance(point, new Vector2(23f, 36f), new Vector2(38f, 36f), 3.2f));
        letter = Mathf.Min(letter, ProceduralSprites.CapsuleDistance(point, new Vector2(23f, 25f), new Vector2(41f, 25f), 3.2f));

        if (letter <= 0f)
        {
            return inkColor;
        }

        if (face <= 0f)
        {
            return faceColor;
        }

        if (key <= 0f)
        {
            return sideColor;
        }

        return key <= outlineWidth ? inkColor : Color.clear;
    }

    private static float RoundedBoxDistance(Vector2 point, Vector2 center, Vector2 halfSize, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(point.x - center.x), Mathf.Abs(point.y - center.y)) - (halfSize - new Vector2(radius, radius));
        Vector2 outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
        return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }
}
