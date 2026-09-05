using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAnimations : MonoBehaviour
{

    [SerializeField] private Animator animator;
    [Header("Enemy Collision")]
    [Tooltip("Layer assigned to enemies.")]
    [SerializeField] private LayerMask enemyLayer;

    private Vector2 moveInput;
    private Vector2 lastMoveDirection = Vector2.right;
    
    private bool canDash = true;
    private bool isDashing = false;

    void Start()
    {
        Input_Manager.Instance.Actions.Player.Move.performed += OnMove;
        Input_Manager.Instance.Actions.Player.Move.canceled += OnMove;

    }

    private void OnDisable()
    {
        
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
}