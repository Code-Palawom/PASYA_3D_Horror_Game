using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour {
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private bool shouldFaceMoveDirection = false;
    [SerializeField] private float basePlayerSpeed = 4f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float crouchSpeed = 1.5f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -30f;

    [SerializeField] private CinemachineCamera thirdPersonPOV;
    [SerializeField] private CinemachineCamera firstPersonPOV;
    private Boolean isFirstPerson = true;

    private float playerSpeed;
    private Boolean isSprinting = false;
    private Boolean isCrouching = false;

    private CharacterController controller;
    private Vector2 moveInput;
    private Vector3 velocity;
    private PlayerInput playerInput;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        playerInput.actions.FindActionMap("POV").Enable();

        playerSpeed = basePlayerSpeed;
    }

    public void onMove(InputAction.CallbackContext context) {
        moveInput = context.ReadValue<Vector2>();
        Debug.Log($"Move Input: {moveInput}");
    }

    public void onJump(InputAction.CallbackContext context) {
        if(context.performed && controller.isGrounded) {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            Debug.Log("Jump!");
        }
        Debug.Log($"Jumping {context.performed} - Is on ground: {controller.isGrounded}");
    }

    public void onSprint(InputAction.CallbackContext context) {
        if(context.started) {
            isSprinting = true;
            if(isCrouching == false) {
                playerSpeed = sprintSpeed;
                
            }
            Debug.Log("Sprinting!");
        }

        if(context.canceled) {
            if(isCrouching == false) {
                playerSpeed = basePlayerSpeed;
            }

            isSprinting = false;
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

            if(isSprinting) {
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

    // Update is called once per frame
    void Update() {
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
        controller.Move(moveDirection * playerSpeed * Time.deltaTime);

        if(shouldFaceMoveDirection && !isFirstPerson && moveDirection.sqrMagnitude > 0.001f) {
            Quaternion rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 10f * Time.deltaTime);
        }

        if(isFirstPerson) {
            Vector3 camForward = firstPersonPOV.transform.forward;

            if(camForward.sqrMagnitude > 0.01f) {
                Quaternion rotation = Quaternion.LookRotation(camForward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 10f * Time.deltaTime);
            }
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void FixedUpdate() {
        // controller.Move(movement * basePlayerSpeed * Time.deltaTime);
        // controller.Move(velocity * Time.deltaTime);
    }
}
