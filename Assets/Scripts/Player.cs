using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour {
    [SerializeField] private float basePlayerSpeed = 5f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -30f;

    private CharacterController controller;
    private Vector2 moveInput;
    private Vector3 movement;
    private Vector3 velocity;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        controller = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update() {
        movement = new Vector3(moveInput.x, 0, moveInput.y);
        controller.Move(movement * basePlayerSpeed * Time.deltaTime);
        
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void FixedUpdate() {
        // controller.Move(movement * basePlayerSpeed * Time.deltaTime);
        // controller.Move(velocity * Time.deltaTime);
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
}
