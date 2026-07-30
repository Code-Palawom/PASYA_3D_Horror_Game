using System;
using System.Collections;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(MicPermission))]
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
    [SerializeField] private Transform cameraFollow;

    [Header("Player Setup")]
    [SerializeField] private UnityEngine.InputSystem.PlayerInput inputComponent;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Canvas playerCanvas;
    [SerializeField] private AudioListener audioListener;

    [Header("Distance Thresholds (hysteresis)")]
    [SerializeField] private float hideDistance = 0.5f;
    [SerializeField] private float showDistance = 0.7f;

    [Header("Player Inventory UI")]
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private PlayerItemActions playerItemActions;
    [SerializeField] private ItemActionButtonUI itemActionButtonUI;

    // ---------------- Noise (horror enemy hearing) ----------------
    [Header("Noise")]
    [Tooltip("Leave empty to auto-resolve via GetComponent in Start.")]
    [SerializeField] private PlayerNoiseEmitter noiseEmitter;
    [Tooltip("Leave empty to auto-resolve via GetComponent in Start. Used to amplify footstep noise while the flashlight is on.")]
    [SerializeField] private FlashlightController flashlightController;
    [SerializeField] private float walkNoiseLoudness = 3f;
    [SerializeField] private float crouchNoiseLoudness = 1f;
    [SerializeField] private float sprintNoiseLoudness = 8f;
    [SerializeField] private float walkFootstepInterval = 0.5f;
    [SerializeField] private float crouchFootstepInterval = 0.8f;
    [SerializeField] private float sprintFootstepInterval = 0.35f;
    [SerializeField] private float flashlightNoiseMultiplier = 1.5f; // applied to footstep loudness while flashlight is on
    private float noiseTimer = 0f;

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
    private bool isJumpscared = false;

    private float standHeight;
    private Vector3 standCenter;

    [Header("Camera Follow Crouch Offset")]
    [SerializeField] private float crouchYOffset = -0.6f;
    [SerializeField] private float transitionSpeed = 8f;
    private Vector3 cameraFollowOriginalLocalPos;

    private CharacterController controller;
    private Vector2 moveInput;
    private Vector3 velocity;
    private PlayerInput playerInput;
    private PlayerState playerState;
    private PlayerAnimation playerAnimation;

    Action _onVivoxLoggedInHandler;

    private bool isVisible = true;
    private SkinnedMeshRenderer[] renderers;

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
        cameraFollowOriginalLocalPos = cameraFollow.localPosition;

        renderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        if (noiseEmitter == null) noiseEmitter = GetComponent<PlayerNoiseEmitter>();
        if (flashlightController == null) flashlightController = GetComponent<FlashlightController>();
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
                playerItemActions.Init(inventory);
                itemActionButtonUI.Init(playerItemActions);
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
            SetCamera(isFirstPerson);
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
            if (GameModeManager.Instance.IsRelayMode) VivoxManager.Instance.UnregisterLocalPlayerTransform(cameraFollow);
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
            SettingsManager.Instance.Save(s => s.isFirstPerson = isFirstPerson);
        }

        SetCamera(isFirstPerson);
        //Debug.Log($"POV Switch is First Person: {isFirstPerson}");
    }

    private void SetCamera(bool mode) {
        if (!IsOwner) return;

        if (mode) {
            firstPersonPOV.Priority = 10;
            thirdPersonPOV.Priority = 0;
            firstPersonLook.enabled = true;
            thirdPersonLook.enabled = false;
        } else {
            firstPersonPOV.Priority = 0;
            thirdPersonPOV.Priority = 10;
            firstPersonLook.enabled = false;
            thirdPersonLook.enabled = true;
        }
    }

    private void SetCharacterVisibility(bool visibility) {
        if (isVisible == visibility) return;
        isVisible = visibility;

        ShadowCastingMode mode = visibility ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
        foreach (var r in renderers)
            r.shadowCastingMode = mode;
    }

    public void RefreshPOV() {
        if (!IsOwner) return;

        if (SettingsManager.Instance == null) return;
        isFirstPerson = SettingsManager.Instance.Current.isFirstPerson;
        SetCamera(isFirstPerson);
    }

    void Update() {
        if (!IsOwner || !controller.enabled) return;

        Vector3 camPos = playerCamera.transform.position;
        Vector3 targetPos = cameraFollow.position;
        float targetY = targetPos.y + 1f;

        bool isBelow = camPos.y < targetY;

        Vector2 camXZ = new Vector2(camPos.x, camPos.z);
        Vector2 targetXZ = new Vector2(targetPos.x, targetPos.z);
        float horizontalDist = Vector2.Distance(camXZ, targetXZ);

        if (isVisible && isBelow && horizontalDist < hideDistance) {
            SetCharacterVisibility(false);
        } else if (!isVisible && (!isBelow || horizontalDist > showDistance)) {
            SetCharacterVisibility(true);
        }

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
        HandleNoiseEmission();
    }

    // ---------------- Noise (horror enemy hearing) ----------------

    private void HandleNoiseEmission() {
        if (noiseEmitter == null) return;

        bool flashlightOn = flashlightController != null && flashlightController.IsActive;

        bool hasMoveInput = moveInput != Vector2.zero;
        bool isMovingLiterally = IsMovingLiterally();
        bool isMoving = hasMoveInput || isMovingLiterally;

        if (!stableGrounded || (!isMoving && !flashlightOn)) {
            // airborne, or standing still with the flashlight off: no noise, reset cadence
            noiseTimer = 0f;
            return;
        }

        float loudness;
        float interval;

        if (!isMoving) {
            // Standing still but flashlight on: the beam/hum is itself as
            // noticeable as a crouch-walk footstep, on the same cadence.
            loudness = crouchNoiseLoudness;
            interval = crouchFootstepInterval;
        } else if (isCrouching) {
            loudness = crouchNoiseLoudness;
            interval = crouchFootstepInterval;
        } else if (isRunning) {
            loudness = sprintNoiseLoudness;
            interval = sprintFootstepInterval;
        } else {
            loudness = walkNoiseLoudness;
            interval = walkFootstepInterval;
        }

        // Flashlight on = easier for enemies to notice/hear you approaching.
        // (Standing-still already uses the crouch-equivalent baseline above,
        // so this multiplier only applies while actually moving.)
        if (flashlightOn && isMoving) {
            loudness *= flashlightNoiseMultiplier;
        }

        noiseTimer += Time.deltaTime;
        if (noiseTimer >= interval) {
            noiseTimer = 0f;
            noiseEmitter.EmitNoise(transform.position, loudness);
        }
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

        Vector3 targetPos = cameraFollowOriginalLocalPos;
        if (isCrouching) targetPos.y += crouchYOffset;

        cameraFollow.localPosition = Vector3.Lerp(cameraFollow.localPosition, targetPos, Time.deltaTime * transitionSpeed);
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

    public void IsJumpscared(bool jumpscared) {
        isJumpscared = jumpscared;
    }

    [ClientRpc]
    public void TeleportClientRpc(Vector3 position, Quaternion rotation) {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        if (cc != null && !isJumpscared) cc.enabled = true;
        StartCoroutine(HideLoadingScreen());

        if (GameModeManager.Instance.IsRelayMode && string.IsNullOrEmpty(VivoxManager.Instance.CurrentChannelName)) {
            if (VivoxManager.Instance.IsLoggedIn) {
                JoinPositionalChannel($"{GameSessionManager.Instance.SessionId.Value}");
            } else {
                _onVivoxLoggedInHandler = () => JoinPositionalChannel(GameSessionManager.Instance.SessionId.Value.ToString());
                VivoxManager.Instance.OnVivoxLoggedIn += _onVivoxLoggedInHandler;
            }
        }else if(!string.IsNullOrEmpty(VivoxManager.Instance.CurrentChannelName)) {
            RegisterVivoxTransform();
        }
    }

    [ClientRpc]
    public void ShowLoadingScreenClientRpc() {
        LoadingScreenController.Instance.Show("Loading...");
    }

    void JoinPositionalChannel(string id) {
        if (!IsOwner) return;

        VivoxManager.Instance.OnVivoxLoggedIn -= _onVivoxLoggedInHandler;

        var micPermission = GetComponent<MicPermission>();

        micPermission.RequestMicThenJoin(
            onGranted: async () => {
                if(SceneManager.GetActiveScene().name == "Lobby") ActionbarToastNotification.Instance.ShowLocalToast("Initializing voice chat.");
                Debug.Log($"Initializing voice chat: ${id}");

                //await VivoxService.Instance.JoinEchoChannelAsync($"Channel_{id}", ChatCapability.AudioOnly);
                VivoxManager.Instance.SetLocalMute(true, true);
                if (await VivoxManager.Instance.JoinPositionalChannelAsync($"Channel_{id}")) {
                    if (SceneManager.GetActiveScene().name == "Lobby") ActionbarToastNotification.Instance.ShowLocalToast("Voice chat active.", ToastType.Success);
                    VivoxManager.Instance.RegisterLocalPlayerTransform(cameraFollow);
                    Debug.Log("Voice chat active.");
                } else {
                    ActionbarToastNotification.Instance.ShowLocalToast("An error occured initializing voice chat.", ToastType.Error);
                    Debug.Log("An error occured initializing voice chat.");
                }

                Debug.Log($"Yooo {VivoxManager.Instance.CurrentChannelName}");
            },
            onDenied: () => {
                ActionbarToastNotification.Instance.ShowLocalToast("Microphone permission denied cannot use voice chat.", ToastType.Error);
                Debug.Log("Microphone permission denied cannot use voice chat.");
            }
        );
    }

    void RegisterVivoxTransform() {
        if (IsOwner) VivoxManager.Instance.RegisterLocalPlayerTransform(cameraFollow);
    }

    IEnumerator HideLoadingScreen() {
        if (!IsOwner) yield break;
        yield return new WaitForSeconds(1f);
        LoadingScreenController.Instance.Hide();
    }
}