using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Dibuja en el piso el circulo donde el guardia puede detectar al jugador.
// Usa el mismo radio que la deteccion (se encoge al agacharse) y cambia de color segun el estado.

[DisallowMultipleComponent]
[RequireComponent(typeof(CopEnemy))]
public class CopDetectionRangeView : MonoBehaviour
{
    [Header("Colores (alpha = transparencia)")]
    [SerializeField] private Color patrolColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color suspiciousColor = new Color(1f, 0.85f, 0.3f, 0.5f);
    [SerializeField] private Color chaseColor = new Color(1f, 0.3f, 0.25f, 0.5f);

    [Tooltip("Cuando el guardia ignora al jugador (no lleva pasteles). Se muestra el radio normal, mas tenue.")]
    [SerializeField] private Color ignoringColor = new Color(1f, 1f, 1f, 0.15f);

    [Header("Forma")]
    [SerializeField, Range(16, 128)] private int segments = 64;

    [Tooltip("Altura sobre el piso para que el circulo no parpadee contra el suelo.")]
    [SerializeField] private float groundOffset = 0.03f;

    [Tooltip("Que tan rapido cambia el tamaño al agacharse o levantarse.")]
    [SerializeField] private float radiusLerpSpeed = 10f;

    [Tooltip("Opcional. Si esta vacio se usa Sprites/Default (transparente, sin iluminacion).")]
    [SerializeField] private Material materialOverride;

    private CopEnemy guard;
    private NavMeshAgent agent;
    private GameObject view;
    private Material material;
    private Mesh mesh;
    private float displayedRadius = -1f;

    private void Awake()
    {
        guard = GetComponent<CopEnemy>();
        agent = GetComponent<NavMeshAgent>();
        CreateView();
    }

    private void OnEnable()
    {
        if (view != null)
        {
            view.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (view != null)
        {
            view.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (view != null) Destroy(view);
        if (material != null) Destroy(material);
        if (mesh != null) Destroy(mesh);
    }

    private void LateUpdate()
    {
        if (view == null || guard == null)
        {
            return;
        }

        float radius = guard.CurrentDetectionRadius;
        bool ignoringPlayer = radius <= 0f;
        if (ignoringPlayer)
        {
            radius = guard.DetectionRadius;
        }

        displayedRadius = displayedRadius < 0f
            ? radius
            : Mathf.Lerp(displayedRadius, radius, 1f - Mathf.Exp(-radiusLerpSpeed * Time.deltaTime));

        // El objeto no es hijo del guardia: asi su escala o rotacion no deforman el circulo.
        Vector3 position = transform.position;
        position.y -= agent != null ? agent.baseOffset : 0f;
        position.y += groundOffset;
        view.transform.SetPositionAndRotation(position, Quaternion.identity);
        view.transform.localScale = new Vector3(displayedRadius, 1f, displayedRadius);

        material.color = PickColor(ignoringPlayer);
    }

    private Color PickColor(bool ignoringPlayer)
    {
        switch (guard.State)
        {
            case CopEnemy.GuardState.Chase:
                return chaseColor;
            case CopEnemy.GuardState.Suspicious:
                return suspiciousColor;
            default:
                return ignoringPlayer ? ignoringColor : patrolColor;
        }
    }

    private void CreateView()
    {
        view = new GameObject(name + "_DetectionRange");
        view.hideFlags = HideFlags.DontSave;
        SceneManager.MoveGameObjectToScene(view, gameObject.scene);

        mesh = BuildUnitDisc(segments);
        view.AddComponent<MeshFilter>().sharedMesh = mesh;

        Material source = materialOverride != null ? materialOverride : new Material(Shader.Find("Sprites/Default"));
        material = materialOverride != null ? new Material(source) : source;
        material.renderQueue = 3000;

        MeshRenderer meshRenderer = view.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    // Disco de radio 1 en el plano XZ; el tamaño real se da con la escala.
    private static Mesh BuildUnitDisc(int segmentCount)
    {
        Vector3[] vertices = new Vector3[segmentCount + 1];
        Color[] colors = new Color[segmentCount + 1];
        int[] triangles = new int[segmentCount * 3];

        vertices[0] = Vector3.zero;
        colors[0] = Color.white;
        for (int i = 0; i < segmentCount; i++)
        {
            float angle = i * Mathf.PI * 2f / segmentCount;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            colors[i + 1] = Color.white;

            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1 == segmentCount ? 1 : i + 2;
            triangles[i * 3 + 2] = i + 1;
        }

        Mesh disc = new Mesh { name = "GuardDetectionDisc" };
        disc.vertices = vertices;
        disc.colors = colors;
        disc.triangles = triangles;
        disc.RecalculateNormals();
        disc.RecalculateBounds();
        return disc;
    }
}
