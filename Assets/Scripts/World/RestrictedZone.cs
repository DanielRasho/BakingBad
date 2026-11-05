using UnityEngine;

/// <summary>
/// Area the player must not enter (for example the room behind the guards' door). While the player is
/// inside, every guard on the map chases them, even without a cake. Needs a Collider that defines the area.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RestrictedZone : MonoBehaviour
{
    [SerializeField] private PrisonCookPlayerController player;
    [Tooltip("Multiplier on the guards' chase speed while the player is inside. Back to normal when the player leaves.")]
    [SerializeField] private float chaseSpeedMultiplier = 2.5f;
    [SerializeField] private float scanInterval = 0.15f;

    private Collider zone;
    private float nextScanTime;

    public bool PlayerInside { get; private set; }

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Awake()
    {
        zone = GetComponent<Collider>();
    }

    private void Update()
    {
        if (Time.time < nextScanTime)
        {
            return;
        }

        nextScanTime = Time.time + Mathf.Max(0.02f, scanInterval);

        if (player == null)
        {
            player = FindFirstObjectByType<PrisonCookPlayerController>();
        }

        bool wasInside = PlayerInside;
        PlayerInside = player != null && player.isActiveAndEnabled && Contains(player.transform.position);

        if (!PlayerInside)
        {
            if (wasInside)
            {
                SetGuardsBoost(1f);
            }

            return;
        }

        // Applied every scan so guards spawned while the player is inside are included too.
        CopEnemy[] guards = FindObjectsByType<CopEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < guards.Length; i++)
        {
            guards[i].SetSpeedBoost(chaseSpeedMultiplier);
            guards[i].AlertToPlayer();
        }
    }

    private void OnDisable()
    {
        if (PlayerInside)
        {
            PlayerInside = false;
            SetGuardsBoost(1f);
        }
    }

    private static void SetGuardsBoost(float multiplier)
    {
        CopEnemy[] guards = FindObjectsByType<CopEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < guards.Length; i++)
        {
            guards[i].SetSpeedBoost(multiplier);
        }
    }

    // Height is ignored so a tall or short collider does not matter.
    private bool Contains(Vector3 worldPoint)
    {
        worldPoint.y = zone.bounds.center.y;
        Vector3 closest = zone.ClosestPoint(worldPoint);
        return (closest - worldPoint).sqrMagnitude < 0.0001f;
    }

    private void OnDrawGizmos()
    {
        Collider col = zone != null ? zone : GetComponent<Collider>();
        if (col == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.25f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}
