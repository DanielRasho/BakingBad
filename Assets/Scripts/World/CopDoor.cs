using UnityEngine;

/// <summary>
/// Door that swings open (rotating around the world Y axis) when a guard gets close and closes again
/// once nobody is near. Put it on the door object; set a Hinge if the door's pivot is not at its hinge.
/// </summary>
public class CopDoor : MonoBehaviour
{
    [Header("Opening")]
    [Tooltip("Degrees to rotate around Y when open. Use a negative value to swing the other way.")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float rotationSpeed = 180f;
    [Tooltip("Optional point to rotate around. If empty, the door rotates around its own pivot.")]
    [SerializeField] private Transform hinge;

    [Header("Detection")]
    [SerializeField] private float openRadius = 2.5f;
    [Tooltip("Seconds the door stays open after the last guard leaves the radius.")]
    [SerializeField] private float closeDelaySeconds = 1f;
    [SerializeField] private bool openForPlayer = false;
    [SerializeField] private float scanInterval = 0.15f;

    private float currentAngle;
    private float closeTimer;
    private float nextScanTime;
    private bool wantsOpen;

    private void Update()
    {
        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + Mathf.Max(0.02f, scanInterval);
            wantsOpen = SomeoneNearby();
        }

        if (wantsOpen)
        {
            closeTimer = closeDelaySeconds;
        }
        else if (closeTimer > 0f)
        {
            closeTimer -= Time.deltaTime;
        }

        float target = wantsOpen || closeTimer > 0f ? openAngle : 0f;
        float next = Mathf.MoveTowards(currentAngle, target, rotationSpeed * Time.deltaTime);
        float delta = next - currentAngle;

        if (Mathf.Abs(delta) > 0.0001f)
        {
            if (hinge != null)
            {
                transform.RotateAround(hinge.position, Vector3.up, delta);
            }
            else
            {
                transform.Rotate(Vector3.up, delta, Space.World);
            }

            currentAngle = next;
        }
    }

    private bool SomeoneNearby()
    {
        float sqrRadius = openRadius * openRadius;
        Vector3 center = transform.position;

        CopEnemy[] guards = FindObjectsByType<CopEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < guards.Length; i++)
        {
            if (IsWithin(guards[i].transform.position, center, sqrRadius))
            {
                return true;
            }
        }

        if (openForPlayer)
        {
            PrisonCookPlayerController player = FindFirstObjectByType<PrisonCookPlayerController>();
            if (player != null && IsWithin(player.transform.position, center, sqrRadius))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWithin(Vector3 point, Vector3 center, float sqrRadius)
    {
        Vector3 flat = point - center;
        flat.y = 0f;
        return flat.sqrMagnitude <= sqrRadius;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, openRadius);

        if (hinge != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(hinge.position, 0.1f);
        }
    }
}
