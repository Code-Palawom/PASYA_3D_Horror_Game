using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour {
    [SerializeField] private Transform cameraOrientation;
    [SerializeField] private float baseMoveSpeed = 5f;
    [SerializeField] private float crouchSpeed = 2f;
    [SerializeField] private float sprintSpeed = 9f;
    [SerializeField] private float jumpForce = 13f;
    [SerializeField] private CinemachineCamera firstPersonPOV;
    [SerializeField] private CinemachineCamera thirdPersonPOV;

    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioSource AudioSource;

    private bool isFirstPersonPOV = true;
    private bool isGrounded = true;

    private PlayerInput playerInput;

    private Rigidbody rb;
    private Vector2 moveInput;
    private float moveSpeed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        moveSpeed = baseMoveSpeed;
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        playerInput = GetComponent<PlayerInput>();
        playerInput.actions.FindActionMap("Camera").Enable();
    }

    public void OnMove(InputAction.CallbackContext context) {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnCrouch(InputAction.CallbackContext context) {
        if(context.started) {
            moveSpeed = crouchSpeed;
            Debug.Log("Crouching");
        }

        if(context.canceled) {
            moveSpeed = baseMoveSpeed;
            Debug.Log("Not crouching");
        }
    }

    public void OnSprint(InputAction.CallbackContext context) {
        if(context.started) {
            moveSpeed = sprintSpeed;
            Debug.Log("Sprinting");
        }

        if(context.canceled) {
            moveSpeed = baseMoveSpeed;
            Debug.Log("Not sprinting");
        }
    }

    public void OnJump(InputAction.CallbackContext context) {
        if(context.performed) {
            float distance = GetComponent<CapsuleCollider>().bounds.extents.y;
        
            if(Physics.Raycast(transform.position, Vector3.down, distance + 0.1f) || isGrounded) {
                rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
                AudioSource.PlayOneShot(jumpSound);
                Debug.Log("Jumped");
            }else{
                Debug.Log("Can't jump not grounded");
            }
        }
    }

    public void OnSwitchPOV(InputAction.CallbackContext context) {
        if(context.performed) {
            isFirstPersonPOV = !isFirstPersonPOV;

            if(isFirstPersonPOV) {
                firstPersonPOV.Priority = 10;
                thirdPersonPOV.Priority = 0;
            }else{
                firstPersonPOV.Priority = 0;
                thirdPersonPOV.Priority = 10;
            }

            Debug.Log($"POV Switched; is First Person {isFirstPersonPOV}");
        }
    }

    // Checks for collisions
    private void OnCollisionStay(Collision collision) {
        if(Vector3.Dot(collision.contacts[0].normal, Vector3.up) > 0.7f){
            isGrounded = true;
        }
    }

    void OnCollisionExit(Collision collision) {
        isGrounded = false;
        Debug.Log("Not grounded");
    }

    // Update is called once per frame
    void Update() {
        // RB Rotation for First Person POV
        if(isFirstPersonPOV) {
            Vector3 cameraForward = cameraOrientation.forward;
            cameraForward.y = 0f;
            
            if(cameraForward != Vector3.zero) {
                Quaternion newRotation = Quaternion.LookRotation(cameraForward);
                rb.MoveRotation(newRotation);
            }
        }
    }

    void FixedUpdate() {
        Vector3 move = transform.forward * moveInput.y + transform.right * moveInput.x;
        if(isFirstPersonPOV) {
            rb.AddForce(move.normalized * moveSpeed * 10f, ForceMode.Force);
        }else if(!isFirstPersonPOV && move.sqrMagnitude > 0.001f) {
            // Move character but only rotate to front camera direction
            //Vector3 cameraForward = cameraOrientation.forward;
            //cameraForward.y = 0f;

            //Quaternion newRotation = Quaternion.LookRotation(cameraForward);
            //rb.MoveRotation(Quaternion.Slerp(transform.rotation, newRotation, 10f * Time.deltaTime));
            //Vector3 move = transform.forward * moveInput.y + transform.right * moveInput.x;
            //rb.AddForce(move.normalized * moveSpeed * 10f, ForceMode.Force);

            // Move and rotate character based from inputs
            Vector3 forward = cameraOrientation.forward;
            Vector3 right = cameraOrientation.right;

            forward.y = 0;
            right.y = 0;

            forward.Normalize();
            right.Normalize();

            Vector3 cameraForward = forward * moveInput.y + right * moveInput.x;
            cameraForward.y = 0f;

            Quaternion newRotation = Quaternion.LookRotation(cameraForward, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, newRotation, 10f * Time.deltaTime));
            rb.AddForce(cameraForward.normalized * moveSpeed * 10f, ForceMode.Force);
        }
    }
}
