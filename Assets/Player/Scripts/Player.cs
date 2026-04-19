using System;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : NetworkBehaviour  {
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private bool shouldFaceMoveDirection = false;
    [SerializeField] private bool onlyLookForward = false;
    [SerializeField] private float basePlayerSpeed = 4f;
    [SerializeField] private float sprintTreshold = 0.70f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float crouchSpeed = 1.5f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float movingThreshold = 0.01f;
    [SerializeField] private float crouchHeight = 1.4f;
    [SerializeField] private Vector3 crouchCenter = new Vector3(0, -0.3f, 0);
    [SerializeField] private float crouchTransitionSpeed = 2f;

    [SerializeField] private SaveManager saveManager;
    [SerializeField] private CinemachineCamera thirdPersonPOV;
    [SerializeField] private CinemachineCamera firstPersonPOV;
    [SerializeField] private ThirdPersonCameraLook thirdPersonLook;
    [SerializeField] private FirstPersonCameraLook firstPersonLook;
    private Boolean isFirstPerson = true;

    private float playerSpeed;
    private float verticalVelocity = 0f;
    private Boolean isRunning = false;
    private Boolean isCrouching = false;

    private float standHeight;
    private Vector3 standCenter;

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
        standCenter = controller.center;
        standHeight = controller.height;

        int currentPOV = int.Parse(saveManager.GetOneData("pov") ?? "0");
        isFirstPerson = currentPOV == 0;
        Debug.Log(currentPOV);
        Debug.Log(isFirstPerson);
        SetCharacter(isFirstPerson);
    }

    public void OnMove(InputAction.CallbackContext context) {
        moveInput = context.ReadValue<Vector2>();

        var device = context.control.device;
        /* if(device is Keyboard){
        }else */
        if(device is Gamepad) {
            Debug.Log($"X: {moveInput.x}");
            Debug.Log($"Y: {moveInput.y}");
            if(moveInput.x > sprintTreshold || moveInput.y > sprintTreshold || moveInput.x < -sprintTreshold) {
                if(isCrouching == false && moveInput.y > -0.50f) {
                    isRunning = true;
                    playerSpeed = sprintSpeed;
                    Debug.Log("Sprinting!");
                }else{
                    isRunning = false;
                    playerSpeed = basePlayerSpeed;
                    Debug.Log("Done sprinting!");
                }
            }else{
                if(isCrouching == false) {
                    isRunning = false;
                    playerSpeed = basePlayerSpeed;
                    Debug.Log("Done sprinting!");
                }
            }

            moveInput = moveInput.normalized;
        }

        Debug.Log($"Move Input: {moveInput}");
    }

    public void OnJump(InputAction.CallbackContext context) {
        if(context.performed && controller.isGrounded) {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            Debug.Log("Jump!");
        }
        Debug.Log($"Jumping {context.performed} - Is on ground: {controller.isGrounded}");
    }

    public void OnSprint(InputAction.CallbackContext context) {
        if(moveInput.y == -1) return;

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

    public void OnCrouch(InputAction.CallbackContext context) {
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
            saveManager.SaveOneData("0", "pov");
        }else{
            saveManager.SaveOneData("1", "pov");
        }

        SetCharacter(isFirstPerson);

        Debug.Log($"POV Switch is First Person: {isFirstPerson}");
    }

    private void UpdateMovementState() {
        bool isMovementInput = moveInput != Vector2.zero;
        bool isMovingLiterally = IsMovingLiterally();

        PlayerMovementState lateralState = isRunning ? PlayerMovementState.Running : isMovingLiterally || isMovementInput ? PlayerMovementState.Walking : PlayerMovementState.Idling;
        playerState.SetPlayerMovementState(lateralState);
        //print($"velocity {controller.velocity.y}");
        if(!controller.isGrounded && controller.velocity.y > 0f) {
            playerState.SetPlayerMovementState(PlayerMovementState.Jumping);
        } else if(!controller.isGrounded && controller.velocity.y <= 0f) {
            playerState.SetPlayerMovementState(PlayerMovementState.Falling);
        }

        if(isCrouching) {
            playerState.SetPlayerMovementState(PlayerMovementState.Crouching);
        }
    }

    private void UpdateControllerCollider() {
        Vector3 targetCenter = standCenter;
        float targetHeight = standHeight;

        if(isCrouching) {
            targetCenter = crouchCenter;
            targetHeight = crouchHeight;
        }

        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        controller.center = Vector3.Lerp(controller.center, targetCenter, crouchTransitionSpeed * Time.deltaTime);
    }

    private bool IsMovingLiterally() {
        Vector3 lateralVelocity = new Vector3(controller.velocity.x, 0f, controller.velocity.y);

        return lateralVelocity.magnitude > movingThreshold;
    }

    private void SetCharacter(bool mode) {
        if(!IsOwner) return;

        var renderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        if(mode){
            firstPersonPOV.Priority = 10;
            thirdPersonPOV.Priority = 0;
            firstPersonLook.enabled = true;
            thirdPersonLook.enabled = false;
            foreach (var r in renderers) {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }else{
            firstPersonPOV.Priority = 0;
            thirdPersonPOV.Priority = 10;
            firstPersonLook.enabled = false;
            thirdPersonLook.enabled = true;
            foreach (var r in renderers) {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }
    }

    // Update is called once per frame
    void Update() {
        if(!IsOwner) return;

        playerAnimation.UpdateAnimationState(moveInput, controller.isGrounded);

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
        if(shouldFaceMoveDirection && !isFirstPerson && moveDirection.sqrMagnitude > 0.001f) {
            Vector3 faceDirection = moveDirection;
            if(moveInput.y < -0.50f) faceDirection = forward;
            Quaternion rotation = Quaternion.LookRotation(onlyLookForward ? forward :  faceDirection, Vector3.up);
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
        
        if(moveInput.y < -0.50f) {
            playerSpeed = crouchSpeed;
        }else if(!isRunning && !isCrouching){
            playerSpeed = basePlayerSpeed;
        }

        if(controller.isGrounded && verticalVelocity < 0) verticalVelocity = 0;
        verticalVelocity += gravity * Time.deltaTime;

        velocity = new Vector3(moveDirection.x * playerSpeed, verticalVelocity, moveDirection.z * playerSpeed);
        controller.Move(velocity * Time.deltaTime);

        UpdateMovementState();
        UpdateControllerCollider();
    }

    private void FixedUpdate() {
        // controller.Move(movement * basePlayerSpeed * Time.deltaTime);
        // controller.Move(velocity * Time.deltaTime);
    }
}
