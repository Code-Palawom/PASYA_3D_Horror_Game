using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;


// Detection origin:  player body at eye height
// Detection method:  OverlapBox offset forward from eye position
// Box direction:     camera forward, but pitch is clamped so looking
//                     straight up/down doesn't swing the box to detect
//                     something directly above/below the player.

// Each frame the player looks at an IInteractable, calls OnFocus()
// so the interactable can push custom prompt/cooldown data to the UI.
// Disabled on non-owner clients so only the local player interacts.
public class InteractionController : NetworkBehaviour {
    [Header("Detection")]
    [SerializeField] Vector3 boxHalfExtents = new Vector3(0.5f, 0.5f, 0.5f);
    [SerializeField] float boxForwardOffset = 0.25f;
    [SerializeField] float boxVerticalOffset = 0f; // additional offset along world up, applied after eye height
    [SerializeField] float maxPitchClamp = 30f; // degrees, clamps how far the box tilts up/down with camera
    [SerializeField] LayerMask interactLayer;

    [Header("References")]
    [SerializeField] Camera playerCamera;
    [SerializeField] PlayerInteractionUI interactionUI;

    [Header("Ray Origin")]
    [SerializeField] float eyeHeight = 1.6f;

    private PlayerInput _input;
    private IInteractable _currentTarget;

    // cached each frame for gizmos + reuse
    private Vector3 _lastBoxCenter;
    private Quaternion _lastBoxRotation = Quaternion.identity;

    // ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn() {
        if (!IsOwner) { enabled = false; return; }

        _input = new PlayerInput();
        _input.Interactions.Enable();
        _input.Interactions.Interact.performed += OnInteractPerformed;
    }

    public override void OnNetworkDespawn() {
        if (_input == null) return;
        _input.Interactions.Interact.performed -= OnInteractPerformed;
        _input.Interactions.Disable();
        _input.Dispose();
    }

    // ─────────────────────────────────────────────────────────
    void Update() => DetectInteractable();

    void DetectInteractable() {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 clampedForward = GetClampedForward();
        Vector3 flatForward = GetFlatForward();

        Vector3 center = origin + clampedForward * boxForwardOffset + Vector3.up * boxVerticalOffset;
        Quaternion rotation = Quaternion.LookRotation(flatForward, Vector3.up); // yaw only, box stays upright

        _lastBoxCenter = center;
        _lastBoxRotation = rotation;

        Collider[] hits = Physics.OverlapBox(center, boxHalfExtents, rotation, interactLayer);

        IInteractable nearest = null;
        float nearestSqrDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++) {
            if (!hits[i].TryGetComponent(out IInteractable interactable)) continue;

            float sqrDist = (hits[i].transform.position - origin).sqrMagnitude;
            if (sqrDist < nearestSqrDist) {
                nearestSqrDist = sqrDist;
                nearest = interactable;
            }
        }

        if (nearest != null) {
            _currentTarget = nearest;
            _currentTarget.OnFocus(interactionUI);
            return;
        }

        _currentTarget = null;
        interactionUI.Hide();
    }

    // Clamps the camera's forward pitch so extreme up/down looks don't
    // swing the box to cover something directly overhead/underfoot.
    // Used only to offset the box's position, not its rotation.
    Vector3 GetClampedForward() {
        Vector3 forward = playerCamera.transform.forward;
        Vector3 flatDir = GetFlatForward();

        float pitchAngle = Vector3.SignedAngle(flatDir, forward, Vector3.Cross(Vector3.up, flatDir));
        float clampedPitch = Mathf.Clamp(pitchAngle, -maxPitchClamp, maxPitchClamp);

        return Quaternion.AngleAxis(clampedPitch, Vector3.Cross(Vector3.up, flatDir)) * flatDir;
    }

    // Camera forward flattened onto the horizontal plane (yaw only).
    // Used for the box's rotation so it always stays upright, regardless of pitch.
    Vector3 GetFlatForward() {
        Vector3 forward = playerCamera.transform.forward;
        Vector3 flatDir = new Vector3(forward.x, 0f, forward.z).normalized;
        if (flatDir.sqrMagnitude < 0.0001f) flatDir = transform.forward; // guard: looking straight up/down
        return flatDir;
    }

    // ─────────────────────────────────────────────────────────
    void OnInteractPerformed(InputAction.CallbackContext ctx) {
        if (interactionUI.IsShowingCooldown) return;
        _currentTarget?.OnInteract(gameObject);
    }

    public void TriggerInteract() {
        _currentTarget?.OnInteract(gameObject);
    }

    // ─────────────────────────────────────────────────────────
    void OnDrawGizmosSelected() {
        if (playerCamera == null) return;
        Gizmos.color = Color.cyan;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Gizmos.DrawWireSphere(origin, 0.05f);

        Vector3 center = _lastBoxCenter;
        Quaternion rotation = _lastBoxRotation;

        // Update() doesn't run in edit mode, so compute a live preview instead
        // of relying on stale/default cached values.
        if (!Application.isPlaying) {
            Vector3 clampedForward = GetClampedForward();
            center = origin + clampedForward * boxForwardOffset + Vector3.up * boxVerticalOffset;
            rotation = Quaternion.LookRotation(GetFlatForward(), Vector3.up);
        }

        Matrix4x4 prevMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, boxHalfExtents * 2f);
        Gizmos.matrix = prevMatrix;
    }
}