using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Uses the New Input System via a generated Player asset.
/// Raycast origin:    player body at eye height
/// Raycast direction: MainCamera forward (follows camera Y axis)
/// </summary>
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
    // Only owner runs this
    // ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn() {
        if (!IsOwner) {
            enabled = false;
            return;
        }

        // Create and enable input actions
        _input = new PlayerInput();
        _input.Interactions.Enable();

        // Subscribe to Interact action
        _input.Interactions.Interact.performed += OnInteractPerformed;
    }

    public override void OnNetworkDespawn() {
        if (_input == null) return;

        _input.Interactions.Interact.performed -= OnInteractPerformed;
        _input.Interactions.Disable();
        _input.Dispose();
    }

    // ─────────────────────────────────────────────────────────
    void Update() {
        DetectInteractable();
    }

    // ─────────────────────────────────────────────────────────
    void DetectInteractable() {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 direction = playerCamera.transform.forward;

        Debug.DrawRay(origin, direction * interactRange, Color.cyan);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, interactRange, interactLayer)) {
            if (hit.collider.TryGetComponent(out IInteractable interactable)) {
                _currentTarget = interactable;
                interactionUI.Show(_currentTarget.InteractPrompt);
                return;
            }
        }

        _currentTarget = null;
        interactionUI.Hide();
    }

    // ─────────────────────────────────────────────────────────
    // Fired by Input System when Interact is pressed
    // ─────────────────────────────────────────────────────────
    void OnInteractPerformed(InputAction.CallbackContext ctx) {
        _currentTarget?.OnInteract(gameObject);
    }

    // ─────────────────────────────────────────────────────────
    // Called from mobile UI button
    // ─────────────────────────────────────────────────────────
    public void TriggerInteract() {
        _currentTarget?.OnInteract(gameObject);
    }

    // ─────────────────────────────────────────────────────────
    void OnDrawGizmosSelected() {
        if (playerCamera == null) return;
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, 0.1f);
        Gizmos.DrawRay(origin, playerCamera.transform.forward * interactRange);
    }
}