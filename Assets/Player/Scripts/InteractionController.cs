using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;


// Raycast origin:    player body at eye height
// Raycast direction: MainCamera forward

// Each frame the player looks at an IInteractable, calls OnFocus()
// so the interactable can push custom prompt/cooldown data to the UI.
// Disabled on non-owner clients so only the local player interacts.
public class InteractionController : NetworkBehaviour {
    [Header("Detection")]
    [SerializeField] float interactRange = 3f;
    [SerializeField] LayerMask interactLayer;

    [Header("References")]
    [SerializeField] Camera playerCamera;
    [SerializeField] PlayerInteractionUI interactionUI;

    [Header("Ray Origin")]
    [SerializeField] float eyeHeight = 1.6f;

    private PlayerInput _input;
    private IInteractable _currentTarget;

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
        Vector3 direction = playerCamera.transform.forward;

        Debug.DrawRay(origin, direction * interactRange, Color.cyan);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, interactRange, interactLayer)) {
            if (hit.collider.TryGetComponent(out IInteractable interactable)) {
                _currentTarget = interactable;

                // Let the interactable decide what to show
                // (normal prompt, cooldown, "someone answering", etc.)
                _currentTarget.OnFocus(interactionUI);
                return;
            }
        }

        _currentTarget = null;
        interactionUI.Hide();
    }

    // ─────────────────────────────────────────────────────────
    void OnInteractPerformed(InputAction.CallbackContext ctx) {
        _currentTarget?.OnInteract(gameObject);
    }

    public void TriggerInteract() =>
        _currentTarget?.OnInteract(gameObject);

    // ─────────────────────────────────────────────────────────
    void OnDrawGizmosSelected() {
        if (playerCamera == null) return;
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, 0.1f);
        Gizmos.DrawRay(origin, playerCamera.transform.forward * interactRange);
    }
}