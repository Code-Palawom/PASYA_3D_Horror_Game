using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAnimation : MonoBehaviour {
    [SerializeField] private Animator animator;
    [SerializeField] private float motionBlendSpeed = 4f;
    [SerializeField] private float zeroBlendThreshold = 0.01f; // how close to 0 counts as "settled"

    private PlayerState playerState;

    private static int inputXHash = Animator.StringToHash("inputX");
    private static int inputYHash = Animator.StringToHash("inputY");
    private static int inputMagnitudeHash = Animator.StringToHash("inputMagnitude");
    private static int isGroundedHash = Animator.StringToHash("isGrounded");
    private static int isJumpingHash = Animator.StringToHash("isJumping");
    private static int isFallingHash = Animator.StringToHash("isFalling");
    private static int isCrouchingHash = Animator.StringToHash("isCrouching");

    Vector3 currentBlendInput = Vector3.zero;
    private bool isForcingZeroBlend = false;

    private void Awake() {
        playerState = GetComponent<PlayerState>();
    }

    private void Update() {
        if (!isForcingZeroBlend) return;

        UpdateAnimationState(Vector2.zero, true);

        if (currentBlendInput.sqrMagnitude <= zeroBlendThreshold * zeroBlendThreshold) {
            currentBlendInput = Vector3.zero;
            animator.SetFloat(inputXHash, 0f);
            animator.SetFloat(inputYHash, 0f);
            animator.SetFloat(inputMagnitudeHash, 0f);
            isForcingZeroBlend = false;
        }
    }

    public void ForceBlendToZero() {
        isForcingZeroBlend = true;
    }

    public void CancelForceBlend() {
        isForcingZeroBlend = false;
    }

    public void UpdateAnimationState(Vector2 playerInput, bool isGrounded) {
        bool isIdling = playerState.CurrentPlayerMovementState == PlayerMovementState.Idling;
        bool isRunning = playerState.CurrentPlayerMovementState == PlayerMovementState.Running;
        bool isJumping = playerState.CurrentPlayerMovementState == PlayerMovementState.Jumping;
        bool isFalling = playerState.CurrentPlayerMovementState == PlayerMovementState.Falling;
        bool isCrouching = playerState.CurrentPlayerMovementState == PlayerMovementState.Crouching;

        Vector2 moveInput = isRunning ? playerInput * 1.5f : playerInput;
        currentBlendInput = Vector3.Lerp(currentBlendInput, moveInput, motionBlendSpeed * Time.deltaTime);

        animator.SetBool(isGroundedHash, isGrounded);
        animator.SetBool(isFallingHash, isFalling);
        animator.SetBool(isJumpingHash, isJumping);
        animator.SetBool(isCrouchingHash, isCrouching);

        animator.SetFloat(inputXHash, currentBlendInput.x);
        animator.SetFloat(inputYHash, currentBlendInput.y);
        animator.SetFloat(inputMagnitudeHash, currentBlendInput.magnitude);
    }
}
