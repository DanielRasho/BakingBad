using UnityEngine;

[CreateAssetMenu(fileName = "KitchenInteractionVisualProfile", menuName = "Baking Bad/Interaction Visual Profile")]
public class KitchenInteractionVisualProfile : ScriptableObject
{
    [SerializeField] private Color highlightColor = new Color(1f, 0.78f, 0.18f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float tintStrength = 0.45f;
    [SerializeField] private bool useEmission = true;
    [ColorUsage(false, true)]
    [SerializeField] private Color emissionColor = new Color(1f, 0.65f, 0.12f, 1f);
    [SerializeField] private float emissionIntensity = 0.6f;

    public Color HighlightColor
    {
        get { return highlightColor; }
    }

    public float TintStrength
    {
        get { return tintStrength; }
    }

    public bool UseEmission
    {
        get { return useEmission; }
    }

    public Color EmissionColor
    {
        get { return emissionColor * emissionIntensity; }
    }
}
