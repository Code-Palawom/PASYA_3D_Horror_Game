using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAnimation : MonoBehaviour {
    [SerializeField] private Animator animator;
    [SerializeField] private float motionBlendSpeed = 4f;

    private PlayerState playerState;
    
    private static int inputXHash = Animator.StringToHash("inputX");
    private static int inputYHash = Animator.StringToHash("inputY");
    private static int inputMagnitudeHash = Animator.StringToHash("inputMagnitude");

    Vector3 currentBlendInput = Vector3.zero;

    private void Awake() {
        playerState = GetComponent<PlayerState>();
    }
    
    public void UpdateAnimationState(Vector2 playerInput) {
        bool isSprinting = playerState.CurrentPlayerMovementState == PlayerMovementState.Sprinting;
        Vector2 moveInput = isSprinting ? playerInput * 1.5f : playerInput;

        currentBlendInput = Vector3.Lerp(currentBlendInput, moveInput, motionBlendSpeed * Time.deltaTime);

        float inputMagnitude = currentBlendInput.magnitude;

        animator.SetFloat(inputXHash, currentBlendInput.x);
        animator.SetFloat(inputYHash, currentBlendInput.y);
        animator.SetFloat(inputMagnitudeHash, inputMagnitude);
    }
}
