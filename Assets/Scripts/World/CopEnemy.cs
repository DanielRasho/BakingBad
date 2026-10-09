using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Patrolling guard. Notices the player inside a radius (smaller and slower while sneaking),
/// chases once fully alerted and steals money on contact.
/// Raises StateChanged and PlayerCaught so visuals/audio can react without the guard knowing about them.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class CopEnemy : MonoBehaviour
{
    public enum GuardState
    {
        Patrol,
        Suspicious,
        Chase,
        Stunned
    }

    [Header("References")] [SerializeField]
    private PrisonCookPlayerController player;

    [SerializeField] private PrisonOrderManager orderManager;

    [Header("Patrol")] [SerializeField] private Transform[] waypoints;
    [SerializeField] private float patrolSpeed = 1.6f;
    [SerializeField] private float waypointWaitSeconds = 1.5f;
    [SerializeField] private float waypointReachDistance = 0.4f;
    [SerializeField] private bool randomWaypoints = false;

    [Header("Detection")] [Tooltip("Radius where the guard notices a player who is walking/running.")] [SerializeField]
    private float detectionRadius = 6f;

    [Tooltip("Radius where the guard notices a sneaking player.")] [SerializeField]
    private float sneakDetectionRadius = 2.5f;

    [Tooltip("Seconds to fully notice a player standing at point blank range while not sneaking.")] [SerializeField]
    private float timeToDetect = 1.2f;

    [Tooltip("Multiplier on the time to detect while the player sneaks (higher = takes longer).")] [SerializeField]
    private float sneakDetectTimeMultiplier = 3f;

    [Tooltip("Seconds for the alert meter to empty when the player is out of range.")] [SerializeField]
    private float alertDecaySeconds = 2f;

    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask obstructionLayers = ~0;
    [SerializeField] private float eyeHeight = 1f;

    [Tooltip(
        "Cake in hand: big radius walking, small radius sneaking. Cakes only in inventory: small radius. No cakes: ignored.")]
    [SerializeField]
    private bool onlyTargetCakeCarrier = true;

    [Header("Chase")] [SerializeField] private float chaseSpeed = 3.0f;
    [SerializeField] private float catchDistance = 1f;

    [Tooltip("The guard gives up when the player stays beyond this radius.")] [SerializeField]
    private float loseRadius = 9f;

    [SerializeField] private float loseSightSeconds = 3f;

    [Header("Steal")] [SerializeField] private int moneyStolen = 40;

    [Tooltip("Pause after catching the player before returning to patrol.")] [SerializeField]
    private float stunSeconds = 3f;

    [Header("Debug")] [SerializeField] private bool drawGizmos = true;

    private NavMeshAgent agent;
    private GuardState state = GuardState.Patrol;
    private int waypointIndex = -1;
    private float waitTimer;
    private float alert;
    private float loseTimer;
    private float stunTimer;
    private Vector3 lastKnownPlayerPos;

    /// <summary>Raised whenever the guard switches state. Passes the new state.</summary>
    public event System.Action<GuardState> StateChanged;

    /// <summary>Raised once at the moment the guard catches the player.</summary>
    public event System.Action PlayerCaught;

    public GuardState State
    {
        get { return state; }
    }

    public float Alert01
    {
        get { return alert; }
    }

    /// <summary>World-space velocity of the guard (zero while standing still).</summary>
    public Vector3 Velocity
    {
        get { return agent != null ? agent.velocity : Vector3.zero; }
    }

    /// <summary>Current movement speed in units per second.</summary>
    public float CurrentSpeed
    {
        get { return Velocity.magnitude; }
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = patrolSpeed;
    }

    private void Start()
    {
        ResolveReferences();
        GoToNextWaypoint();
    }

    private void Update()
    {
        if (player == null || orderManager == null)
        {
            ResolveReferences();
        }

        if (!agent.isOnNavMesh)
        {
            return;
        }

        switch (state)
        {
            case GuardState.Patrol:
            case GuardState.Suspicious:
                UpdatePatrol();
                break;
            case GuardState.Chase:
                UpdateChase();
                break;
            case GuardState.Stunned:
                UpdateStunned();
                break;
        }
    }

    // Single place where the state changes, so listeners are always notified.
    private void SetState(GuardState newState)
    {
        if (state == newState)
        {
            return;
        }

        state = newState;

        if (StateChanged != null)
        {
            StateChanged(newState);
        }
    }

    private void UpdatePatrol()
    {
        UpdateAlertMeter();

        if (alert >= 1f)
        {
            StartChase();
            return;
        }

        SetState(alert > 0f ? GuardState.Suspicious : GuardState.Patrol);

        // Freeze while noticing something so the player gets a visible warning.
        agent.isStopped = state == GuardState.Suspicious;
        if (agent.isStopped)
        {
            FaceTarget(lastKnownPlayerPos);
            return;
        }

        if (waypoints == null || waypoints.Length == 0)
        {
            return;
        }

        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                GoToNextWaypoint();
            }

            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= waypointReachDistance)
        {
            waitTimer = waypointWaitSeconds;
        }
    }

    private void UpdateAlertMeter()
    {
        bool sneaking;
        float distance;
        float radius;

        if (CanSeePlayer(out radius, out sneaking, out distance))
        {
            float time = Mathf.Max(0.05f, timeToDetect) * (sneaking ? sneakDetectTimeMultiplier : 1f);

            // Closer players are noticed faster: 1x at the edge of the radius, up to 2x at point blank.
            float proximity = 1f + (1f - Mathf.Clamp01(distance / radius));
            alert += Time.deltaTime / time * proximity;
            lastKnownPlayerPos = player.transform.position;
        }
        else
        {
            alert -= Time.deltaTime / Mathf.Max(0.05f, alertDecaySeconds);
        }

        alert = Mathf.Clamp01(alert);
    }

    private bool CanSeePlayer(out float radius, out bool sneaking, out float distance)
    {
        radius = 0f;
        sneaking = false;
        distance = float.MaxValue;

        if (player == null || !player.isActiveAndEnabled)
        {
            return false;
        }

        sneaking = player.IsSneaking;
        radius = GetDetectionRadius();
        if (radius <= 0f)
        {
            return false;
        }

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        distance = toPlayer.magnitude;

        if (distance > radius)
        {
            return false;
        }

        if (!requireLineOfSight)
        {
            return true;
        }

        return HasLineOfSight();
    }

    // Cake in hand: big radius walking, small radius sneaking.
    // Cakes only in the inventory: small radius. No cakes: 0 (ignored).
    private float GetDetectionRadius()
    {
        if (!onlyTargetCakeCarrier)
        {
            return player.IsSneaking ? sneakDetectionRadius : detectionRadius;
        }

        CakeInventory inventory = player.Inventory;
        if (inventory == null)
        {
            return 0f;
        }

        if (inventory.SelectedItem != null)
        {
            return player.IsSneaking ? sneakDetectionRadius : detectionRadius;
        }

        return inventory.HasAnyCake ? sneakDetectionRadius : 0f;
    }

    private bool HasLineOfSight()
    {
        Vector3 from = transform.position + Vector3.up * eyeHeight;
        Vector3 to = player.transform.position + Vector3.up * eyeHeight;
        Vector3 dir = to - from;

        RaycastHit hit;
        if (Physics.Raycast(from, dir.normalized, out hit, dir.magnitude, obstructionLayers,
                QueryTriggerInteraction.Ignore))
        {
            Transform t = hit.transform;
            return t == player.transform || t.IsChildOf(player.transform) || t.IsChildOf(transform);
        }

        return true;
    }

    private void StartChase()
    {
        alert = 1f;
        loseTimer = 0f;
        agent.isStopped = false;
        agent.speed = chaseSpeed;
        SetState(GuardState.Chase);
    }

    private void UpdateChase()
    {
        // Once the guard has seen the cake, it keeps chasing until the player escapes,
        // even if the cake is stored or delivered.
        if (player == null)
        {
            ReturnToPatrol();
            return;
        }

        Vector3 playerPos = player.transform.position;
        agent.SetDestination(playerPos);

        Vector3 flat = playerPos - transform.position;
        flat.y = 0f;
        float distance = flat.magnitude;

        if (distance <= catchDistance)
        {
            CatchPlayer();
            return;
        }

        bool tooFar = distance > loseRadius;
        bool hidden = requireLineOfSight && !HasLineOfSight();
        loseTimer = (tooFar || hidden) ? loseTimer + Time.deltaTime : 0f;

        if (loseTimer >= loseSightSeconds)
        {
            ReturnToPatrol();
        }
    }

    private void CatchPlayer()
    {
        if (orderManager != null)
        {
            orderManager.LoseMoney(moneyStolen);
        }

        stunTimer = stunSeconds;
        alert = 0f;
        agent.isStopped = true;
        SetState(GuardState.Stunned);

        if (PlayerCaught != null)
        {
            PlayerCaught();
        }
    }

    private void UpdateStunned()
    {
        stunTimer -= Time.deltaTime;
        if (stunTimer <= 0f)
        {
            ReturnToPatrol();
        }
    }

    private void ReturnToPatrol()
    {
        alert = 0f;
        agent.isStopped = false;
        agent.speed = patrolSpeed;
        SetState(GuardState.Patrol);
        GoToNextWaypoint();
    }

    private void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0 || !agent.isOnNavMesh)
        {
            return;
        }

        if (randomWaypoints && waypoints.Length > 1)
        {
            int next;
            do
            {
                next = Random.Range(0, waypoints.Length);
            } while (next == waypointIndex);

            waypointIndex = next;
        }
        else
        {
            waypointIndex = (waypointIndex + 1) % waypoints.Length;
        }

        Transform target = waypoints[waypointIndex];
        if (target != null)
        {
            agent.SetDestination(target.position);
        }
    }

    private void FaceTarget(Vector3 worldPos)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(dir),
            Time.deltaTime * 8f);
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PrisonCookPlayerController>();
        }

        if (orderManager == null)
        {
            orderManager = FindFirstObjectByType<PrisonOrderManager>();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, sneakDetectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);

        if (waypoints == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
            {
                continue;
            }

            Gizmos.DrawSphere(waypoints[i].position, 0.2f);
            Transform next = waypoints[(i + 1) % waypoints.Length];
            if (next != null)
            {
                Gizmos.DrawLine(waypoints[i].position, next.position);
            }
        }
    }
}
