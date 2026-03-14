using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAnimation : MonoBehaviour {
    [SerializeField] private Animator animator;
    [SerializeField] private float motionBlendSpeed = 4f;

    private PlayerState playerState;
    
    private static int inputXHash = Animator.StringToHash("inputX");
    private static int inputYHash = Animator.StringToHash("inputY");
    private static int inputMagnitudeHash = Animator.StringToHash("inputMagnitude");
    private static int isGroundedHash = Animator.StringToHash("isGrounded");
    private static int isJumpingHash = Animator.StringToHash("isJumping");
    private static int isFallingHash = Animator.StringToHash("isFalling");

    Vector3 currentBlendInput = Vector3.zero;

    private void Awake() {
        playerState = GetComponent<PlayerState>();
    }
    
    public void UpdateAnimationState(Vector2 playerInput, bool isGrounded) {

        bool isIdling = playerState.CurrentPlayerMovementState == PlayerMovementState.Idling;
        bool isRunning = playerState.CurrentPlayerMovementState == PlayerMovementState.Running;
        bool isSprinting = playerState.CurrentPlayerMovementState == PlayerMovementState.Sprinting;
        bool isJumping = playerState.CurrentPlayerMovementState == PlayerMovementState.Jumping;
        bool isFalling = playerState.CurrentPlayerMovementState == PlayerMovementState.Falling;

        Vector2 moveInput = isRunning ? playerInput * 1.5f : playerInput;
        currentBlendInput = Vector3.Lerp(currentBlendInput, moveInput, motionBlendSpeed * Time.deltaTime);

        animator.SetBool(isGroundedHash, isGrounded);
        animator.SetBool(isFallingHash, isFalling);
        animator.SetBool(isJumpingHash, isJumping);

        animator.SetFloat(inputXHash, currentBlendInput.x);
        animator.SetFloat(inputYHash, currentBlendInput.y);
        animator.SetFloat(inputMagnitudeHash, currentBlendInput.magnitude);
    }
}
