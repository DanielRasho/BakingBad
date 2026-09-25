using System;
using System.Collections.Generic;
using UnityEngine;

// Draws "#N" over every jail cell on the minimap layer so only the minimap camera sees it,
// plus an icon with that prisoner's status: can order, waiting for a cake, cake ready in the inventory, or cooling down.
// The minimap camera rotates with the player, so labels counter-rotate to stay readable.
[DisallowMultipleComponent]
public class MinimapCellLabels : MonoBehaviour
{
    [Serializable]
    public class ExtraLabel
    {
        public string text;
        public Transform anchor;
        [Tooltip("World X/Z nudge after centering on the floor block, e.g. to clear the player's start spot.")]
        public Vector2 offset;
    }

    private enum CellStatus
    {
        None,
        CanOrder,
        WaitingForCake,
        CakeReady,
        Cooldown
    }

    private sealed class CellView
    {
        public Transform Cell;
        public PrisonerInteractable Prisoner;
        public SpriteRenderer Icon;
        public TextMesh Seconds;
        public TextMesh SecondsShadow;
        public CellStatus Status;
        public Vector3 IconBaseScale;
    }

    [SerializeField] private Transform cellsRoot;
    [SerializeField] private Camera minimapCamera;
    [SerializeField] private Font font;
    [SerializeField] private string minimapLayerName = "Minimap";
    [SerializeField] private float labelHeight = 4.8f;
    [SerializeField] private float characterSize = 0.22f;
    [SerializeField] private int fontSize = 60;
    [SerializeField] private float maxSnapDistance = 4f;
    [SerializeField] private Color textColor = new Color(0.30f, 0.16f, 0.11f, 1f);
    [SerializeField] private Color shadowColor = new Color(1f, 0.97f, 0.9f, 0.9f);
    [Tooltip("Named places that are not jail cells, e.g. the kitchen.")]
    [SerializeField] private ExtraLabel[] extraLabels;

    [Header("Status Icons")]
    [SerializeField] private Sprite canOrderIcon;
    [SerializeField] private Sprite cooldownIcon;
    [Tooltip("Unlit sprite material so icons keep their colors on the minimap.")]
    [SerializeField] private Material iconMaterial;
    [SerializeField] private float iconSize = 1.1f;
    [Tooltip("Icon position relative to the cell number, in minimap units (x right, y up).")]
    [SerializeField] private Vector2 iconOffset = new Vector2(0f, 1.25f);
    [SerializeField] private float secondsCharacterSize = 0.15f;
    [SerializeField] private float refreshInterval = 0.2f;
    [SerializeField] private float readyPulseSpeed = 4f;
    [SerializeField] private float readyPulseAmount = 0.18f;

    private readonly List<Transform> labels = new List<Transform>();
    private readonly List<CellView> cellViews = new List<CellView>();
    private static Sprite waitingIcon;
    private CakeInventory inventory;
    private PrisonOrderManager orderManager;
    private float nextRefreshTime;

    private void Start()
    {
        BuildLabels();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + refreshInterval;
            RefreshStatuses();
        }

        float pulse = 1f + readyPulseAmount * (Mathf.Sin(Time.unscaledTime * readyPulseSpeed) * 0.5f + 0.5f);
        for (int i = 0; i < cellViews.Count; i++)
        {
            CellView view = cellViews[i];
            if (view.Icon != null && view.Status == CellStatus.CakeReady)
            {
                view.Icon.transform.localScale = view.IconBaseScale * pulse;
            }
        }
    }

    private void LateUpdate()
    {
        if (minimapCamera == null)
        {
            return;
        }

        Quaternion upright = Quaternion.Euler(90f, minimapCamera.transform.eulerAngles.y, 0f);
        for (int i = 0; i < labels.Count; i++)
        {
            if (labels[i] != null)
            {
                labels[i].rotation = upright;
            }
        }
    }

    public void BuildLabels()
    {
        ClearLabels();

        if (cellsRoot == null)
        {
            GameObject found = GameObject.Find("JailCells");
            cellsRoot = found != null ? found.transform : null;
        }

        if (cellsRoot == null)
        {
            Debug.LogWarning("MinimapCellLabels could not find the JailCells root.");
            return;
        }

        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        int layer = LayerMask.NameToLayer(minimapLayerName);
        Renderer[] floorBlocks = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < cellsRoot.childCount; i++)
        {
            Transform cell = cellsRoot.GetChild(i);
            Transform labelRoot = AddLabel("MinimapLabel_" + cell.name, "#" + GetCellNumber(cell.name), GetFloorBlockCenter(floorBlocks, cell.position), layer);
            cellViews.Add(CreateCellView(cell, labelRoot, layer));
        }

        if (extraLabels == null)
        {
            return;
        }

        for (int i = 0; i < extraLabels.Length; i++)
        {
            ExtraLabel extra = extraLabels[i];
            if (extra != null && extra.anchor != null && !string.IsNullOrEmpty(extra.text))
            {
                Vector3 center = GetFloorBlockCenter(floorBlocks, extra.anchor.position) + new Vector3(extra.offset.x, 0f, extra.offset.y);
                AddLabel("MinimapLabel_" + extra.text, extra.text, center, layer);
            }
        }
    }

    private Transform AddLabel(string objectName, string text, Vector3 center, int layer)
    {
        Transform labelRoot = CreateTextMesh(objectName, transform, text, textColor, characterSize, layer);
        labelRoot.position = new Vector3(center.x, labelHeight, center.z);
        labelRoot.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Offset copy behind the text works as an outline so it reads on any floor color.
        Transform shadow = CreateTextMesh("Shadow", labelRoot, text, shadowColor, characterSize, layer);
        shadow.localPosition = new Vector3(0.07f, -0.07f, 0.02f);
        shadow.localRotation = Quaternion.identity;

        labels.Add(labelRoot);
        return labelRoot;
    }

    // Icons live under the label so they share its counter-rotation; local x/y are minimap right/up
    // and negative local z is toward the minimap camera.
    private CellView CreateCellView(Transform cell, Transform labelRoot, int layer)
    {
        CellView view = new CellView { Cell = cell, Status = CellStatus.None };

        GameObject iconObject = new GameObject("StatusIcon");
        iconObject.transform.SetParent(labelRoot, false);
        iconObject.transform.localPosition = new Vector3(iconOffset.x, iconOffset.y, -0.03f);
        if (layer >= 0)
        {
            iconObject.layer = layer;
        }

        view.Icon = iconObject.AddComponent<SpriteRenderer>();
        if (iconMaterial != null)
        {
            view.Icon.sharedMaterial = iconMaterial;
        }

        view.Icon.enabled = false;

        Transform seconds = CreateTextMesh("CooldownSeconds", labelRoot, string.Empty, textColor, secondsCharacterSize, layer);
        seconds.localPosition = new Vector3(iconOffset.x + iconSize * 0.05f, iconOffset.y, -0.03f);
        view.Seconds = seconds.GetComponent<TextMesh>();
        view.Seconds.anchor = TextAnchor.MiddleLeft;

        Transform secondsShadow = CreateTextMesh("Shadow", seconds, string.Empty, shadowColor, secondsCharacterSize, layer);
        secondsShadow.localPosition = new Vector3(0.05f, -0.05f, 0.02f);
        view.SecondsShadow = secondsShadow.GetComponent<TextMesh>();
        view.SecondsShadow.anchor = TextAnchor.MiddleLeft;
        return view;
    }

    private void RefreshStatuses()
    {
        if (inventory == null)
        {
            PrisonCookPlayerController player = FindAnyObjectByType<PrisonCookPlayerController>();
            inventory = player != null ? player.Inventory : null;
        }

        if (orderManager == null)
        {
            orderManager = FindAnyObjectByType<PrisonOrderManager>();
        }

        for (int i = 0; i < cellViews.Count; i++)
        {
            CellView view = cellViews[i];
            if (view.Prisoner == null && view.Cell != null)
            {
                // Prisoners are spawned by PrisonOrderManager after the scene starts.
                view.Prisoner = view.Cell.GetComponentInChildren<PrisonerInteractable>(true);
            }

            CakeInventoryItem readyCake;
            CellStatus status = GetStatus(view.Prisoner, out readyCake);
            ApplyStatus(view, status, readyCake);
        }
    }

    private CellStatus GetStatus(PrisonerInteractable prisoner, out CakeInventoryItem readyCake)
    {
        readyCake = null;
        if (prisoner == null)
        {
            return CellStatus.None;
        }

        if (prisoner.HasActiveOrder)
        {
            readyCake = inventory != null ? inventory.FindByOrderId(prisoner.CurrentOrderId) : null;
            return readyCake != null ? CellStatus.CakeReady : CellStatus.WaitingForCake;
        }

        if (prisoner.IsCoolingDown)
        {
            return CellStatus.Cooldown;
        }

        // With the order queue full nobody can take a new order, so don't advertise it.
        return orderManager == null || orderManager.CanQueueMoreOrders ? CellStatus.CanOrder : CellStatus.None;
    }

    private void ApplyStatus(CellView view, CellStatus status, CakeInventoryItem readyCake)
    {
        Sprite sprite = null;
        switch (status)
        {
            case CellStatus.CanOrder:
                sprite = canOrderIcon;
                break;
            case CellStatus.WaitingForCake:
                sprite = GetWaitingIcon();
                break;
            case CellStatus.CakeReady:
                sprite = readyCake != null && readyCake.icon != null ? readyCake.icon : GetWaitingIcon();
                break;
            case CellStatus.Cooldown:
                sprite = cooldownIcon;
                break;
        }

        bool showSeconds = status == CellStatus.Cooldown;
        string secondsText = showSeconds ? Mathf.CeilToInt(view.Prisoner.CooldownRemaining) + "s" : string.Empty;
        view.Seconds.text = secondsText;
        view.SecondsShadow.text = secondsText;

        view.Icon.enabled = sprite != null;
        if (sprite != null && (view.Icon.sprite != sprite || view.Status != status))
        {
            view.Icon.sprite = sprite;
            float largestSide = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            view.IconBaseScale = Vector3.one * (largestSide > 0f ? iconSize / largestSide : 1f);
            view.Icon.transform.localScale = view.IconBaseScale;
            // With the seconds beside it, the clock shifts left so the pair stays centered over the number.
            float iconX = showSeconds ? iconOffset.x - iconSize * 0.5f : iconOffset.x;
            view.Icon.transform.localPosition = new Vector3(iconX, iconOffset.y, -0.03f);
        }

        view.Status = status;
    }

    // Speech bubble with "..." for an order that is still waiting for its cake.
    private static Sprite GetWaitingIcon()
    {
        if (waitingIcon == null)
        {
            waitingIcon = ProceduralSprites.Create("MinimapWaitingIcon", 64, 56, new Vector2(0.5f, 0.5f), SampleWaitingIcon);
        }

        return waitingIcon;
    }

    private static Color SampleWaitingIcon(float px, float py)
    {
        Color fillColor = new Color(1f, 0.98f, 0.90f, 1f);
        Color inkColor = new Color(0.55f, 0.30f, 0.22f, 1f);
        const float outlineWidth = 3.5f;

        Vector2 point = new Vector2(px, py);
        float body = RoundedBoxDistance(point, new Vector2(32f, 34f), new Vector2(24f, 15f), 10f);
        float tail = ProceduralSprites.PolygonDistance(point, new[] { new Vector2(14f, 22f), new Vector2(26f, 22f), new Vector2(11f, 7f) });
        float shape = Mathf.Min(body, tail);

        float dots = float.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            dots = Mathf.Min(dots, (point - new Vector2(20f + i * 12f, 34f)).magnitude - 4f);
        }

        if (shape <= 0f)
        {
            return dots <= 0f ? inkColor : fillColor;
        }

        return shape <= outlineWidth ? inkColor : Color.clear;
    }

    private static float RoundedBoxDistance(Vector2 point, Vector2 center, Vector2 halfSize, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(point.x - center.x), Mathf.Abs(point.y - center.y)) - (halfSize - new Vector2(radius, radius));
        Vector2 outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
        return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }

    // Cell pivots sit near a wall, so center the label on the minimap block drawn for that cell instead.
    private Vector3 GetFloorBlockCenter(Renderer[] floorBlocks, Vector3 cellPosition)
    {
        Vector3 best = cellPosition;
        float bestDistance = maxSnapDistance * maxSnapDistance;
        for (int i = 0; i < floorBlocks.Length; i++)
        {
            Renderer block = floorBlocks[i];
            if (block == null || block is SpriteRenderer || block.GetComponent<TextMesh>() != null)
            {
                continue;
            }

            Vector3 center = block.bounds.center;
            float distance = new Vector2(center.x - cellPosition.x, center.z - cellPosition.z).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = center;
            }
        }

        return best;
    }

    private void ClearLabels()
    {
        for (int i = 0; i < labels.Count; i++)
        {
            if (labels[i] != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(labels[i].gameObject);
                }
                else
                {
                    DestroyImmediate(labels[i].gameObject);
                }
            }
        }

        labels.Clear();
        cellViews.Clear();
    }

    private Transform CreateTextMesh(string objectName, Transform parent, string content, Color color, float size, int layer)
    {
        GameObject labelObject = new GameObject(objectName);
        labelObject.transform.SetParent(parent, false);
        if (layer >= 0)
        {
            labelObject.layer = layer;
        }

        TextMesh textMesh = labelObject.AddComponent<TextMesh>();
        textMesh.text = content;
        textMesh.font = font;
        textMesh.fontSize = fontSize;
        textMesh.characterSize = size;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = color;

        MeshRenderer meshRenderer = labelObject.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = font.material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        return labelObject.transform;
    }

    // Must match PrisonOrderManager's cell naming ("Cell4" -> "4") so the minimap agrees with the order cards.
    private static string GetCellNumber(string cellName)
    {
        if (!string.IsNullOrEmpty(cellName) && cellName.StartsWith("Cell", StringComparison.OrdinalIgnoreCase))
        {
            return cellName.Substring(4);
        }

        return cellName;
    }
}
