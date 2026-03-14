using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour {
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private bool shouldFaceMoveDirection = false;
    [SerializeField] private bool onlyLookForward = false;
    [SerializeField] private float basePlayerSpeed = 4f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float crouchSpeed = 1.5f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float movingThreshold = 0.01f;

    [SerializeField] private CinemachineCamera thirdPersonPOV;
    [SerializeField] private CinemachineCamera firstPersonPOV;
    private Boolean isFirstPerson = true;

    private float playerSpeed;
    private float verticalVelocity = 0f;
    private Boolean isRunning = false;
    private Boolean isCrouching = false;

    private CharacterController controller;
    private Vector2 moveInput;
    private Vector3 velocity;
    private PlayerInput playerInput;

    private PlayerState playerState;
    private PlayerAnimation playerAnimation;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        playerInput.actions.FindActionMap("POV").Enable();

        playerState = GetComponent<PlayerState>();
        playerAnimation = GetComponent<PlayerAnimation>();

        playerSpeed = basePlayerSpeed;
    }

    public void onMove(InputAction.CallbackContext context) {
        moveInput = context.ReadValue<Vector2>();
        Debug.Log($"Move Input: {moveInput}");
    }

    public void onJump(InputAction.CallbackContext context) {
        if(context.performed && controller.isGrounded) {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            Debug.Log("Jump!");
        }
        Debug.Log($"Jumping {context.performed} - Is on ground: {controller.isGrounded}");
    }

    public void onSprint(InputAction.CallbackContext context) {
        if(context.started) {
            isRunning = true;
            if(isCrouching == false) {
                playerSpeed = sprintSpeed;
                
            }
            Debug.Log("Sprinting!");
        }

        if(context.canceled) {
            if(isCrouching == false) {
                playerSpeed = basePlayerSpeed;
            }

            isRunning = false;
            Debug.Log("Done sprinting!");
        }
    }

    public void onCrouch(InputAction.CallbackContext context) {
        if(context.started){
            playerSpeed = crouchSpeed;
            isCrouching = true;
        }

        if(context.canceled) {
            playerSpeed = basePlayerSpeed;
            isCrouching = false;

            if(isRunning) {
                playerSpeed = sprintSpeed;
            }
        }
    }

    public void OnSwitchPOV(InputAction.CallbackContext context) {
        isFirstPerson = !isFirstPerson;
        if(isFirstPerson) {
            firstPersonPOV.Priority = 10;
            thirdPersonPOV.Priority = 0;
        }else{
            firstPersonPOV.Priority = 0;
            thirdPersonPOV.Priority = 10;
        }

        Debug.Log($"POV Switch is First Person: {isFirstPerson}");
    }

    private void UpdateMovementState() {
        bool isMovementInput = moveInput != Vector2.zero;
        bool isMovingLiterally = IsMovingLiterally();

        PlayerMovementState lateralState = isRunning ? PlayerMovementState.Running : isMovingLiterally || isMovementInput ? PlayerMovementState.Walking : PlayerMovementState.Idling;
        playerState.SetPlayerMovementState(lateralState);
        print($"velocity {controller.velocity.y}");
        if(!controller.isGrounded && controller.velocity.y > 0f) {
            playerState.SetPlayerMovementState(PlayerMovementState.Jumping);
        } else if(!controller.isGrounded && controller.velocity.y <= 0f) {
            playerState.SetPlayerMovementState(PlayerMovementState.Falling);
        }
    }

    private bool IsMovingLiterally() {
        Vector3 lateralVelocity = new Vector3(controller.velocity.x, 0f, controller.velocity.y);

        return lateralVelocity.magnitude > movingThreshold;
    }

    // Update is called once per frame
    void Update() {
        playerAnimation.UpdateAnimationState(moveInput, controller.isGrounded);

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;

        if(shouldFaceMoveDirection && !isFirstPerson && moveDirection.sqrMagnitude > 0.001f) {
            Quaternion rotation = Quaternion.LookRotation(onlyLookForward ? forward : moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 10f * Time.deltaTime);
            //Debug.Log($"Third Person Rotation: {rotation}");
        }

        if(isFirstPerson) {
            Vector3 camForward = firstPersonPOV.transform.forward;
            camForward.y = 0; // Not rotate the body alongside with the camera

            if(camForward.sqrMagnitude > 0.01f) {
                Quaternion rotation = Quaternion.LookRotation(camForward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 10f * Time.deltaTime);
                //Debug.Log($"First Person Rotation: {rotation}");
            }
        }

        if(controller.isGrounded && verticalVelocity < 0) verticalVelocity = 0;
        verticalVelocity += gravity * Time.deltaTime;

        velocity = new Vector3(moveDirection.x * playerSpeed, verticalVelocity, moveDirection.z * playerSpeed);
        controller.Move(velocity * Time.deltaTime);

        UpdateMovementState();
    }

    private void FixedUpdate() {
        // controller.Move(movement * basePlayerSpeed * Time.deltaTime);
        // controller.Move(velocity * Time.deltaTime);
    }
}
