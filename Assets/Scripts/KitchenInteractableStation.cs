using UnityEngine;

[DisallowMultipleComponent]
public class KitchenInteractableStation : MonoBehaviour
{
    public enum StationType
    {
        Generic,
        Sink,
        Cooking,
        Stove,
        KitchenTable
    }

    [SerializeField] private StationType stationType = StationType.Generic;
    [SerializeField] private string displayName;
    [SerializeField] private KitchenInteractionVisualProfile visualProfile;
    [SerializeField] private KitchenOrderPrepUIController prepMenuController;
    [SerializeField] private bool includeChildRenderers = true;
    [SerializeField] private Renderer[] highlightRenderers;
    [SerializeField] private bool logInteractions = true;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock propertyBlock;
    private bool isHighlighted;
    private int interactionCount;

    public StationType Type
    {
        get { return stationType; }
    }

    public string DisplayName
    {
        get { return string.IsNullOrEmpty(displayName) ? name : displayName; }
    }

    public bool CanInteract
    {
        get { return enabled && gameObject.activeInHierarchy; }
    }

    public int InteractionCount
    {
        get { return interactionCount; }
    }

    private void Awake()
    {
        EnsureRenderers();
    }

    private void OnEnable()
    {
        EnsureRenderers();
        RefreshHighlight();
    }

    private void OnDisable()
    {
        SetHighlighted(false);
    }

    private void OnValidate()
    {
        if (highlightRenderers == null || highlightRenderers.Length == 0)
        {
            highlightRenderers = includeChildRenderers
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();
        }
    }

    public void Interact(PrisonCookPlayerController player)
    {
        interactionCount++;

        if (stationType == StationType.KitchenTable || stationType == StationType.Cooking)
        {
            EnsurePrepMenuController();
            if (prepMenuController != null)
            {
                prepMenuController.Open();
            }
        }

        if (!logInteractions)
        {
            return;
        }

        Debug.Log("Station Interact #" + interactionCount + ": " + DisplayName + " (" + stationType + ")");
    }

    public void SetHighlighted(bool highlighted)
    {
        if (isHighlighted == highlighted)
        {
            return;
        }

        isHighlighted = highlighted;
        RefreshHighlight();
    }

    private void EnsureRenderers()
    {
        if (highlightRenderers != null && highlightRenderers.Length > 0)
        {
            return;
        }

        highlightRenderers = includeChildRenderers
            ? GetComponentsInChildren<Renderer>(true)
            : GetComponents<Renderer>();
    }

    private void EnsurePrepMenuController()
    {
        if (prepMenuController != null)
        {
            return;
        }

        prepMenuController = FindAnyObjectByType<KitchenOrderPrepUIController>(FindObjectsInactive.Include);
    }

    private void RefreshHighlight()
    {
        EnsureRenderers();

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        for (int rendererIndex = 0; rendererIndex < highlightRenderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = highlightRenderers[rendererIndex];
            if (targetRenderer == null)
            {
                continue;
            }

            Material[] materials = targetRenderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);
                ApplyMaterialHighlight(material, propertyBlock);
                targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
            }
        }
    }

    private void ApplyMaterialHighlight(Material material, MaterialPropertyBlock block)
    {
        Color tintColor = visualProfile != null ? visualProfile.HighlightColor : new Color(1f, 0.78f, 0.18f, 1f);
        float tintStrength = visualProfile != null ? visualProfile.TintStrength : 1.85f;
        bool useEmission = visualProfile != null && visualProfile.UseEmission;
        Color emissionColor = visualProfile != null ? visualProfile.EmissionColor : Color.black;

        if (material.HasProperty(BaseColorId))
        {
            Color baseColor = material.GetColor(BaseColorId);
            block.SetColor(BaseColorId, isHighlighted ? Color.Lerp(baseColor, tintColor, tintStrength) : baseColor);
        }
        else if (material.HasProperty(ColorId))
        {
            Color baseColor = material.GetColor(ColorId);
            block.SetColor(ColorId, isHighlighted ? Color.Lerp(baseColor, tintColor, tintStrength) : baseColor);
        }

        if (material.HasProperty(EmissionColorId))
        {
            Color color = isHighlighted && useEmission ? emissionColor : Color.black;
            block.SetColor(EmissionColorId, color);
        }
    }
}
