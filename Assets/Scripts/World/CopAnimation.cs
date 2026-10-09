using UnityEngine;

/// <summary>
/// Drives the cop's Animator. Lives on the Sprite child; the guard it listens to
/// is assigned in the Inspector.
///
/// Animator parameters used:
///   InputX    (float) normalized facing direction, horizontal
///   InputY    (float) normalized facing direction, vertical
///   IsWalking (bool)  true while the guard is moving
/// </summary>
public class CopAnimation : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The guard logic this sprite listens to (normally the parent Cop object).")]
    [SerializeField] private CopEnemy guard;
    [SerializeField] private Animator animator;
    [Tooltip("Optional. If set (e.g. the Main Camera), directions are measured relative to it. " +
             "If empty, world X maps to InputX and world Z maps to InputY.")]
    [SerializeField] private Transform directionReference;

    private static readonly int InputXHash = Animator.StringToHash("InputX");
    private static readonly int InputYHash = Animator.StringToHash("InputY");
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

    // Speed above which the guard counts as walking.
    private const float WalkingSpeedThreshold = 0.1f;

    private bool subscribed;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void OnEnable()
    {
        if (guard == null || animator == null)
        {
            Debug.LogError("CopAnimation needs a GuardEnemy and an Animator assigned.", this);
            enabled = false;
            return;
        }

        guard.StateChanged += HandleStateChanged;
        guard.PlayerCaught += HandlePlayerCaught;
        subscribed = true;

        // Sync with whatever state the guard is already in.
        HandleStateChanged(guard.State);
    }

    private void OnDisable()
    {
        if (!subscribed || guard == null)
        {
            return;
        }

        guard.StateChanged -= HandleStateChanged;
        guard.PlayerCaught -= HandlePlayerCaught;
        subscribed = false;
    }

    private void Update()
    {
        // Movement changes every frame, so it is read directly instead of sent as an event.
        Vector3 velocity = guard.Velocity;
        velocity.y = 0f;

        bool walking = velocity.magnitude > WalkingSpeedThreshold;
        animator.SetBool(IsWalkingHash, walking);

        // Moving: face where the NavMeshAgent is going.
        // Standing: face where the guard looks (e.g. towards the player while suspicious).
        Vector3 worldDir = walking ? velocity : guard.transform.forward;
        Vector2 input = ToInput(worldDir);

        if (input.sqrMagnitude > 0.0001f)
        {
            animator.SetFloat(InputXHash, input.x);
            animator.SetFloat(InputYHash, input.y);
        }
    }

    // Converts a world direction on the ground plane into a normalized InputX / InputY pair.
    private Vector2 ToInput(Vector3 worldDir)
    {
        worldDir.y = 0f;

        Vector2 input;
        if (directionReference != null)
        {
            Vector3 right = directionReference.right;
            right.y = 0f;

            // A camera looking straight down has no flat forward, so fall back to its up vector.
            Vector3 forward = directionReference.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = directionReference.up;
                forward.y = 0f;
            }

            input = new Vector2(
                Vector3.Dot(worldDir, right.normalized),
                Vector3.Dot(worldDir, forward.normalized));
        }
        else
        {
            input = new Vector2(worldDir.x, worldDir.z);
        }

        return input.normalized;
    }

    // Called whenever the guard switches state (Patrol, Suspicious, Chase, Stunned).
    private void HandleStateChanged(CopEnemy.GuardState newState)
    {
        // Nothing to animate yet. Add state-based animation here when needed.
    }

    // Called once when the guard catches the player.
    private void HandlePlayerCaught()
    {
        // Nothing to animate yet. Add a catch/attack animation here when needed.
    }

    // Editor only: pre-fills the fields when the component is added. They stay editable.
    private void Reset()
    {
        guard = GetComponentInParent<CopEnemy>();
        animator = GetComponent<Animator>();
    }
}