using System;
using System.Collections;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : NetworkBehaviour {
    [Header("Movement")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private bool shouldFaceMoveDirection = false;
    [SerializeField] private bool onlyLookForward = false;
    [SerializeField] private float basePlayerSpeed = 4f;
    //[SerializeField] private float sprintTreshold = 0.70f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float crouchSpeed = 1.5f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float movingThreshold = 0.01f;
    [SerializeField] private float crouchHeight = 1.4f;
    [SerializeField] private Vector3 crouchCenter = new Vector3(0, -0.3f, 0);
    [SerializeField] private float crouchTransitionSpeed = 2f;
    [SerializeField] private float groundedGraceTime = 0.15f;

    [Header("POV")]
    [SerializeField] private CinemachineCamera thirdPersonPOV;
    [SerializeField] private CinemachineCamera firstPersonPOV;
    [SerializeField] private ThirdPersonCameraLook thirdPersonLook;
    [SerializeField] private FirstPersonCameraLook firstPersonLook;

    [Header("Player Setup")]
    [SerializeField] private UnityEngine.InputSystem.PlayerInput inputComponent;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Canvas playerCanvas;
    [SerializeField] private AudioListener audioListener;

    [Header("Player Inventory UI")]
    [SerializeField] private InventoryUI inventoryUI;

    private float originalBasePlayerSpeed;
    private float originalSprintSpeed;
    private float originalCrouchSpeed;

    private bool isFirstPerson = true;
    private float ungroundedTimer = 0f;
    private bool stableGrounded = true;
    private float playerSpeed;
    private float verticalVelocity = 0f;
    private bool isRunning = false;
    private bool isCrouching = false;
    private bool isToggledRunning = false;

    private float standHeight;
    private Vector3 standCenter;

    private CharacterController controller;
    private Vector2 moveInput;
    private Vector3 velocity;
    private PlayerInput playerInput;
    private PlayerState playerState;
    private PlayerAnimation playerAnimation;

    void Awake() {
        originalBasePlayerSpeed = basePlayerSpeed;
        originalSprintSpeed = sprintSpeed;
        originalCrouchSpeed = crouchSpeed;
    }

    // Runs on all instances — only grab components here, no owner-specific logic
    void Start() {
        controller = GetComponent<CharacterController>();
        playerState = GetComponent<PlayerState>();
        playerAnimation = GetComponent<PlayerAnimation>();

        standCenter = controller.center;
        standHeight = controller.height;
    }

    // Replaces owner-specific Start() logic; guaranteed IsOwner is valid here
    public override void OnNetworkSpawn() {
        if (IsOwner) {
            gameObject.tag = "LocalPlayer";

            // --- Camera & Audio setup ---
            playerCamera.enabled = true;
            playerCamera.tag = "MainCamera";
            audioListener.enabled = true;

            // Assign canvas to local player's camera explicitly — never rely on Camera.main
            playerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            playerCanvas.worldCamera = playerCamera;
            playerCanvas.gameObject.SetActive(true);

            var inventory = GetComponent<PlayerInventory>();
            if (inventoryUI != null && inventory != null) {
                inventoryUI.Init(inventory);
            } else {
                Debug.LogWarning("[NetworkSetup] Missing InventoryUI or PlayerInventory reference.");
            }

            // --- Input ---
            inputComponent.enabled = true;
            playerInput = new PlayerInput();
            playerInput.POV.Enable();
            playerInput.POV.SwitchPOV.performed += OnSwitchPOV;
            playerInput.Interactions.Enable();
            playerInput.Movements.Enable();

            // --- POV restore from SettingsManager ---
            playerSpeed = basePlayerSpeed;
            isFirstPerson = SettingsManager.Instance != null
                ? SettingsManager.Instance.Current.isFirstPerson
                : true;
            SetCharacter(isFirstPerson);
        } else {
            // Remote instances on this machine: disable camera, audio, and UI
            playerCamera.enabled = false;
            playerCamera.tag = "Untagged"; // Prevents hijacking Camera.main
            audioListener.enabled = false;
            playerCanvas.gameObject.SetActive(false);

            inputComponent.enabled = false;
            playerInput = new PlayerInput();
            playerInput.POV.Disable();
            playerInput.Interactions.Disable();
            playerInput.Movements.Disable();
        }
    }

    public override void OnNetworkDespawn() {
        // Clean up input when this player leaves to avoid dangling subscriptions
        if (IsOwner && playerInput != null) {
            playerInput.POV.SwitchPOV.performed -= OnSwitchPOV;
            playerInput.POV.Disable();
            playerInput.Interactions.Disable();
            playerInput.Movements.Disable();
        }
    }

    public void OnMove(InputAction.CallbackContext context) {
        if (!IsOwner) return;

        moveInput = context.ReadValue<Vector2>();

        if (context.control.device is Gamepad) moveInput = moveInput.normalized;

        //Debug.Log($"Move Input: {moveInput}");
    }

    public void OnJump(InputAction.CallbackContext context) {
        if (context.performed && controller.isGrounded) {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            Debug.Log("Jump!");
        }
        //Debug.Log($"Jumping {context.performed} - Is on ground: {controller.isGrounded}");
    }

    public void OnSprint(InputAction.CallbackContext context) {
        if (!IsOwner) return;

        if (moveInput.y == -1) return;

#if UNITY_STANDALONE || UNITY_EDITOR
        if (context.started) {
            isToggledRunning = true;
            isRunning = true;
            if (isCrouching == false) playerSpeed = sprintSpeed;
            Debug.Log("Sprinting!");
        }

        if (context.canceled) {
            isToggledRunning = false;
            isRunning = false;
            if (isCrouching == false) playerSpeed = basePlayerSpeed;
            Debug.Log("Done sprinting!");
        }
#else
        if (context.performed) {
            isToggledRunning = !isToggledRunning;

            if (isToggledRunning) {
                if (isCrouching == false) {
                    isRunning = true;
                    playerSpeed = sprintSpeed;
                    Debug.Log("Sprint!");
                }
            } else {
                if (isCrouching == false) {
                    isRunning = false;
                    playerSpeed = basePlayerSpeed;
                    Debug.Log("Not Sprint!");
                }
            }
        }
#endif
    }

    public void OnCrouch(InputAction.CallbackContext context) {
        if (!IsOwner) return;

        if (context.started) {
            playerSpeed = crouchSpeed;
            isCrouching = true;
        }

        if (context.canceled) {
            playerSpeed = basePlayerSpeed;
            isCrouching = false;
            if (isRunning) playerSpeed = sprintSpeed;
        }
    }

    public void OnSwitchPOV(InputAction.CallbackContext context) {
        if (!IsOwner) return;

        isFirstPerson = !isFirstPerson;

        if (SettingsManager.Instance != null) {
            var s = SettingsManager.Instance.Current;
            s.isFirstPerson = isFirstPerson;
            SettingsManager.Instance.Save(s);
        }

        SetCharacter(isFirstPerson);
        //Debug.Log($"POV Switch is First Person: {isFirstPerson}");
    }

    private void SetCharacter(bool mode) {
        if (!IsOwner) return;

        var renderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        if (mode) {
            firstPersonPOV.Priority = 10;
            thirdPersonPOV.Priority = 0;
            firstPersonLook.enabled = true;
            thirdPersonLook.enabled = false;
            foreach (var r in renderers)
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        } else {
            firstPersonPOV.Priority = 0;
            thirdPersonPOV.Priority = 10;
            firstPersonLook.enabled = false;
            thirdPersonLook.enabled = true;
            foreach (var r in renderers)
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }

    public void RefreshPOV() {
        if (!IsOwner) return;

        if (SettingsManager.Instance == null) return;
        isFirstPerson = SettingsManager.Instance.Current.isFirstPerson;
        SetCharacter(isFirstPerson);
    }

    void Update() {
        if (!IsOwner) return;

        playerAnimation.UpdateAnimationState(moveInput, controller.isGrounded);

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
        if (shouldFaceMoveDirection && !isFirstPerson && moveDirection.sqrMagnitude > 0.001f) {
            Vector3 faceDirection = moveDirection;
            if (moveInput.y < -0.50f) faceDirection = forward;
            Quaternion rotation = Quaternion.LookRotation(onlyLookForward ? forward : faceDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 10f * Time.deltaTime);
        }

        if (isFirstPerson) {
            Vector3 camForward = firstPersonPOV.transform.forward;
            camForward.y = 0;

            if (camForward.sqrMagnitude > 0.01f) {
                Quaternion rotation = Quaternion.LookRotation(camForward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 10f * Time.deltaTime);
            }
        }
        if (isCrouching) {
            playerSpeed = crouchSpeed;
        } else {
            if (moveInput.y < -0.50f) {
                if (isToggledRunning) isRunning = false;
                playerSpeed = crouchSpeed;
            } else if (!isRunning && !isCrouching && !isToggledRunning) {
                playerSpeed = basePlayerSpeed;
            } else if (isToggledRunning) {
                isRunning = true;
                playerSpeed = sprintSpeed;
            }
        }

        if (controller.isGrounded && verticalVelocity < 0) verticalVelocity = 0;
        verticalVelocity += gravity * Time.deltaTime;

        velocity = new Vector3(moveDirection.x * playerSpeed, verticalVelocity, moveDirection.z * playerSpeed);
        controller.Move(velocity * Time.deltaTime);

        if (controller.isGrounded) {
            ungroundedTimer = 0f;
            stableGrounded = true;
        } else {
            ungroundedTimer += Time.deltaTime;
            if (ungroundedTimer > groundedGraceTime) stableGrounded = false;
        }

        UpdateMovementState();
        UpdateControllerCollider();
    }

    private void UpdateMovementState() {
        bool isMovementInput = moveInput != Vector2.zero;
        bool isMovingLiterally = IsMovingLiterally();

        PlayerMovementState lateralState = isRunning
            ? PlayerMovementState.Running
            : isMovingLiterally || isMovementInput
                ? PlayerMovementState.Walking
                : PlayerMovementState.Idling;

        playerState.SetPlayerMovementState(lateralState);

        if (!stableGrounded && controller.velocity.y > 0f)
            playerState.SetPlayerMovementState(PlayerMovementState.Jumping);
        else if (!stableGrounded && controller.velocity.y <= 0f)
            playerState.SetPlayerMovementState(PlayerMovementState.Falling);

        if (isCrouching)
            playerState.SetPlayerMovementState(PlayerMovementState.Crouching);
    }

    private void UpdateControllerCollider() {
        Vector3 targetCenter = isCrouching ? crouchCenter : standCenter;
        float targetHeight = isCrouching ? crouchHeight : standHeight;

        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        controller.center = Vector3.Lerp(controller.center, targetCenter, crouchTransitionSpeed * Time.deltaTime);
    }

    private bool IsMovingLiterally() {
        Vector3 lateralVelocity = new Vector3(controller.velocity.x, 0f, controller.velocity.y);
        return lateralVelocity.magnitude > movingThreshold;
    }

    public void SetSpeedMultiplier(float multiplier) {
        if (!IsOwner) return;

        basePlayerSpeed *= multiplier;
        sprintSpeed *= multiplier;
        crouchSpeed *= multiplier;
    }

    public void RestoreSpeedMultiplier() {
        if (!IsOwner) return;

        basePlayerSpeed = originalBasePlayerSpeed;
        sprintSpeed = originalSprintSpeed;
        crouchSpeed = originalCrouchSpeed;
    }

    [ClientRpc]
    public void TeleportClientRpc(Vector3 position, Quaternion rotation) {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        if (cc != null) cc.enabled = true;
        StartCoroutine(HideLoadingScreen());
    }

    [ClientRpc]
    public void ShowLoadingScreenClientRpc() {
        LoadingScreenController.Instance.Show("Loading...");
    }

    IEnumerator HideLoadingScreen() {
        if (!IsOwner) yield break;
        yield return new WaitForSeconds(1f);
        LoadingScreenController.Instance.Hide();
    }
}