using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour {
    [SerializeField] private Transform orientation;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private CinemachineCamera firstPersonPOV;
    [SerializeField] private CinemachineCamera thirdPersonPOV;

    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioSource AudioSource;

    private bool isFirstPersonPOV = true;

    private PlayerInput playerInput;

    private Rigidbody rb;
    private Vector2 moveInput;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        playerInput = GetComponent<PlayerInput>();
        playerInput.actions.FindActionMap("Camera").Enable();
    }

    public void OnMove(InputAction.CallbackContext context) {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context) {
        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
        AudioSource.PlayOneShot(jumpSound);
    }

    public void OnSwitchPOV(InputAction.CallbackContext context) {
        isFirstPersonPOV = !isFirstPersonPOV;

        if(isFirstPersonPOV) {
            firstPersonPOV.Priority = 10;
            thirdPersonPOV.Priority = 0;
        }else{
            firstPersonPOV.Priority = 0;
            thirdPersonPOV.Priority = 10;
        }
    }

    // Update is called once per frame
    void Update() {
        // Movements for First Person POV
        if(isFirstPersonPOV) {
            Vector3 cameraForward = orientation.forward;
            cameraForward.y = 0f;

            if(cameraForward != Vector3.zero) {
                Quaternion newRotation = Quaternion.LookRotation(cameraForward);
                rb.MoveRotation(newRotation);
            }
        }
    }

    void FixedUpdate() {
        // Movements General
        Vector3 move = transform.forward * moveInput.y + transform.right * moveInput.x;
        rb.AddForce(move.normalized * moveSpeed * 10f, ForceMode.Force);

        if(!isFirstPersonPOV && move.sqrMagnitude > 0.001f) {
            Vector3 cameraForward = orientation.forward;
            cameraForward.y = 0f;

            Quaternion newRotation = Quaternion.LookRotation(cameraForward);
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, newRotation, 10f * Time.deltaTime));

            // Borkoloy nga move
            //Vector3 forward = orientation.forward;
            //Vector3 right = orientation.right;

            //forward.y = 0;
            //right.y = 0;

            //forward.Normalize();
            //right.Normalize();

            //Vector3 cameraForward = forward * moveInput.y + right * moveInput.x;
            //cameraForward.y = 0f;

            //Quaternion newRotation = Quaternion.LookRotation(cameraForward, Vector3.up);
            //rb.MoveRotation(Quaternion.Slerp(transform.rotation, newRotation, 10f * Time.deltaTime));
        }
    }
}
