using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class PrisonCookPlayerController : MonoBehaviour
{
    public enum CardinalDirection
    {
        North,
        South,
        East,
        West
    }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.4f;
    [SerializeField] private Camera movementCamera;

    [Header("Facing")]
    [SerializeField] private CardinalDirection startingDirection = CardinalDirection.South;
    [SerializeField] private bool rotateToCardinalDirection = true;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 8.5f;
    [SerializeField] private float dashDuration = 0.14f;
    [SerializeField] private float dashCooldown = 0.65f;

    [Header("Interact")]
    [SerializeField] private float interactRange = 0.9f;
    [SerializeField] private float interactHeight = 0.7f;
    [SerializeField] private float interactRadius = 0.45f;
    [SerializeField] private LayerMask interactLayers = ~0;
    [SerializeField] private bool logInteractions = true;

    private CharacterController controller;
    private CardinalDirection currentFacing;
    private KitchenInteractableStation currentInteractable;
    private Vector3 dashDirection;
    private float dashTimeRemaining;
    private float dashCooldownRemaining;
    private float verticalVelocity;
    private int inputLockCount;

    private const float CollisionSkin = 0.03f;
    private const float VisualCollisionPadding = 0.07f;

    public bool InputLocked
    {
        get { return inputLockCount > 0; }
    }

    public void AddInputLock()
    {
        inputLockCount++;
    }

    public void RemoveInputLock()
    {
        inputLockCount = Mathf.Max(0, inputLockCount - 1);
    }

    private void Awake()
    {
        EnsureController();

        if (movementCamera == null)
        {
            movementCamera = Camera.main;
        }

        currentFacing = startingDirection;
        ApplyFacingRotation();
    }

    private void OnEnable()
    {
        EnsureController();
        currentFacing = startingDirection;
        ApplyFacingRotation();
    }

    private void OnDisable()
    {
        SetCurrentInteractable(null);
    }

    private void Update()
    {
        EnsureController();

        if (controller == null)
        {
            return;
        }

        if (InputLocked)
        {
            if (rotateToCardinalDirection)
            {
                ApplyFacingRotation();
            }

            SetCurrentInteractable(FindCurrentInteractable());
            return;
        }

        Vector2 input = ReadMovementInput();
        Vector3 moveDirection = GetCameraRelativeDirection(input);

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            SetFacingFromWorldDirection(moveDirection);
        }

        if (WasDashPressedThisFrame() && dashCooldownRemaining <= 0f)
        {
            dashDirection = moveDirection.sqrMagnitude > 0.001f
                ? moveDirection.normalized
                : GetFacingVector();
            dashTimeRemaining = dashDuration;
            dashCooldownRemaining = dashCooldown;
        }

        if (dashCooldownRemaining > 0f)
        {
            dashCooldownRemaining -= Time.deltaTime;
        }

        Vector3 horizontalVelocity = moveDirection * moveSpeed;
        if (dashTimeRemaining > 0f)
        {
            horizontalVelocity = dashDirection * dashSpeed;
            dashTimeRemaining -= Time.deltaTime;
        }

        ApplyGrounding();

        Vector3 horizontalMove = ResolveHorizontalMove(horizontalVelocity * Time.deltaTime);
        Vector3 verticalMove = Vector3.up * verticalVelocity * Time.deltaTime;
        controller.Move(horizontalMove + verticalMove);

        if (rotateToCardinalDirection)
        {
            ApplyFacingRotation();
        }

        SetCurrentInteractable(FindCurrentInteractable());

        if (WasInteractPressedThisFrame())
        {
            TryInteract();
        }
    }

    private Vector2 ReadMovementInput()
    {
        Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed) input.y += 1f;
        }
#endif

        return Vector2.ClampMagnitude(input, 1f);
    }

    private bool WasDashPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.shiftKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

        return false;
    }

    private bool WasInteractPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

        return false;
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= 0.001f)
        {
            return Vector3.zero;
        }

        Transform cameraTransform = movementCamera != null ? movementCamera.transform : null;
        Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return (right * input.x + forward * input.y).normalized;
    }

    private void SetFacingFromWorldDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
        {
            currentFacing = direction.x >= 0f ? CardinalDirection.East : CardinalDirection.West;
            return;
        }

        currentFacing = direction.z >= 0f ? CardinalDirection.North : CardinalDirection.South;
    }

    private Vector3 GetFacingVector()
    {
        return GetCardinalVector(currentFacing);
    }

    private static Vector3 GetCardinalVector(CardinalDirection direction)
    {
        switch (direction)
        {
            case CardinalDirection.North:
                return Vector3.forward;
            case CardinalDirection.East:
                return Vector3.right;
            case CardinalDirection.West:
                return Vector3.left;
            case CardinalDirection.South:
            default:
                return Vector3.back;
        }
    }

    private static Quaternion GetCardinalRotation(CardinalDirection direction)
    {
        switch (direction)
        {
            case CardinalDirection.North:
                return Quaternion.Euler(0f, 180f, 0f);
            case CardinalDirection.East:
                return Quaternion.Euler(0f, -90f, 0f);
            case CardinalDirection.West:
                return Quaternion.Euler(0f, 90f, 0f);
            case CardinalDirection.South:
            default:
                return Quaternion.identity;
        }
    }

    private void ApplyFacingRotation()
    {
        transform.rotation = GetCardinalRotation(currentFacing);
    }

    private void ApplyGrounding()
    {
        EnsureController();

        if (controller == null)
        {
            return;
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -1f;
            return;
        }

        verticalVelocity += Physics.gravity.y * Time.deltaTime;
    }

    private Vector3 ResolveHorizontalMove(Vector3 desiredMove)
    {
        EnsureController();

        if (controller == null)
        {
            return Vector3.zero;
        }

        if (desiredMove.sqrMagnitude <= 0.000001f)
        {
            return Vector3.zero;
        }

        Vector3 direction = desiredMove.normalized;
        float distance = desiredMove.magnitude;
        GetControllerCapsule(out Vector3 bottom, out Vector3 top, out float radius);

        RaycastHit[] hits = Physics.CapsuleCastAll(
            bottom,
            top,
            radius,
            direction,
            distance + CollisionSkin,
            ~0,
            QueryTriggerInteraction.Ignore);

        float allowedDistance = distance;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.distance <= 0f)
            {
                continue;
            }

            allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - CollisionSkin));
        }

        return direction * allowedDistance;
    }

    private void GetControllerCapsule(out Vector3 bottom, out Vector3 top, out float radius)
    {
        EnsureController();

        radius = Mathf.Max(0.01f, controller.radius + VisualCollisionPadding);
        float height = Mathf.Max(controller.height, controller.radius * 2f);
        float halfSegment = Mathf.Max(0f, (height * 0.5f) - controller.radius);
        Vector3 center = transform.TransformPoint(controller.center);

        bottom = center + Vector3.down * halfSegment;
        top = center + Vector3.up * halfSegment;
    }

    private void EnsureController()
    {
        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }
    }

    private void TryInteract()
    {
        KitchenInteractableStation target = currentInteractable != null
            ? currentInteractable
            : FindCurrentInteractable();

        if (target == null)
        {
            if (logInteractions)
            {
                Debug.Log("Interact: no object in range.");
            }

            return;
        }

        target.Interact(this);

        if (logInteractions)
        {
            Debug.Log("Interact: " + target.DisplayName);
        }
    }

    private KitchenInteractableStation FindCurrentInteractable()
    {
        Vector3 center = GetInteractCenter(GetFacingVector());
        Collider[] hits = Physics.OverlapSphere(center, interactRadius, interactLayers, QueryTriggerInteraction.Collide);
        KitchenInteractableStation closestStation = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            KitchenInteractableStation station = hit.GetComponentInParent<KitchenInteractableStation>();
            if (station == null || !station.CanInteract)
            {
                continue;
            }

            Vector3 nearestPoint = hit.bounds.ClosestPoint(center);
            float distance = (nearestPoint - center).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestStation = station;
            }
        }

        return closestStation;
    }

    private void SetCurrentInteractable(KitchenInteractableStation nextInteractable)
    {
        if (currentInteractable == nextInteractable)
        {
            return;
        }

        if (currentInteractable != null)
        {
            currentInteractable.SetHighlighted(false);
        }

        currentInteractable = nextInteractable;

        if (currentInteractable != null)
        {
            currentInteractable.SetHighlighted(true);
        }
    }

    private Vector3 GetInteractCenter(Vector3 facing)
    {
        return transform.position + Vector3.up * interactHeight + facing * interactRange;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 direction = Application.isPlaying ? GetFacingVector() : GetCardinalVector(startingDirection);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetInteractCenter(direction), interactRadius);
    }
}
