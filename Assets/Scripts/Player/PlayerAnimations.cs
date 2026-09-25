using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAnimations : MonoBehaviour
{
    private static readonly int IsSneakingHash = Animator.StringToHash("IsSneaking");

    private Animator animator;
    private PrisonCookPlayerController playerController;
    private bool hasSneakParameter;
    private Vector2 moveInput;
    private Vector2 lastMoveDirection = Vector2.right;
    
    // private bool isWalking = false;
    // private bool isDashing = false;

    private void Awake()
    {
       animator = GetComponent<Animator>(); 
       playerController = GetComponentInParent<PrisonCookPlayerController>();
       hasSneakParameter = HasParameter(IsSneakingHash, AnimatorControllerParameterType.Bool);
       if (!hasSneakParameter)
       {
           Debug.LogWarning("PlayerAnimations: el Animator no tiene el parámetro Bool 'IsSneaking'.", this);
       }
    }

    private void OnEnable()
    {
        if (playerController != null)
        {
            playerController.SneakStateChanged += OnSneakStateChanged;
            OnSneakStateChanged(playerController.IsSneaking);
        }
    }

    void Start()
    {
        Input_Manager.Instance.Actions.Player.Move.performed += OnMove;
        Input_Manager.Instance.Actions.Player.Move.canceled += OnMove;

    }

    private void OnDisable()
    {
        if (playerController != null)
        {
            playerController.SneakStateChanged -= OnSneakStateChanged;
        }

        Input_Manager.Instance.Actions.Player.Move.performed -= OnMove;
        Input_Manager.Instance.Actions.Player.Move.canceled -= OnMove;

    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();

        if (moveInput != Vector2.zero)
            lastMoveDirection = moveInput.normalized;

        animator.SetBool("IsWalking", moveInput != Vector2.zero);
        animator.SetFloat("InputX", lastMoveDirection.x);
        animator.SetFloat("InputY", lastMoveDirection.y);
    }

    private void OnSneakStateChanged(bool isSneaking)
    {
        if (animator != null && hasSneakParameter)
        {
            animator.SetBool(IsSneakingHash, isSneaking);
        }
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == hash && parameter.type == type)
            {
                return true;
            }
        }

        return false;
    }
}