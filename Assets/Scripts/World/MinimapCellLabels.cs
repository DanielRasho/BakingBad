using System.Collections.Generic;
using UnityEngine;

// Draws "#N" over every jail cell on the minimap layer so only the minimap camera sees it.
// The minimap camera rotates with the player, so labels counter-rotate to stay readable.
[DisallowMultipleComponent]
public class MinimapCellLabels : MonoBehaviour
{
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

    private readonly List<Transform> labels = new List<Transform>();

    private void Start()
    {
        BuildLabels();
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
            string label = "#" + GetCellNumber(cell.name);
            Vector3 center = GetFloorBlockCenter(floorBlocks, cell.position);
            Vector3 position = new Vector3(center.x, labelHeight, center.z);

            Transform labelRoot = CreateTextMesh("MinimapLabel_" + cell.name, transform, label, textColor, layer);
            labelRoot.position = position;
            labelRoot.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Offset copy behind the text works as an outline so it reads on any floor color.
            Transform shadow = CreateTextMesh("Shadow", labelRoot, label, shadowColor, layer);
            shadow.localPosition = new Vector3(0.07f, -0.07f, 0.02f);
            shadow.localRotation = Quaternion.identity;

            labels.Add(labelRoot);
        }
    }

    // Cell pivots sit near a wall, so center the label on the minimap block drawn for that cell instead.
    private Vector3 GetFloorBlockCenter(Renderer[] floorBlocks, Vector3 cellPosition)
    {
        Vector3 best = cellPosition;
        float bestDistance = maxSnapDistance * maxSnapDistance;
        for (int i = 0; i < floorBlocks.Length; i++)
        {
            Renderer block = floorBlocks[i];
            if (block == null || block.GetComponent<TextMesh>() != null)
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
    }

    private Transform CreateTextMesh(string objectName, Transform parent, string content, Color color, int layer)
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
        textMesh.characterSize = characterSize;
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
        if (!string.IsNullOrEmpty(cellName) && cellName.StartsWith("Cell", System.StringComparison.OrdinalIgnoreCase))
        {
            return cellName.Substring(4);
        }

        return cellName;
    }
}
