using UnityEngine;

// Sits on the DoorHandle child object.
// Handles the interaction logic:
//   - Door open   → close directly (no quiz)
//   - Door closed + unlocked → open directly
//   - Door closed + locked   → show quiz first, open on correct answer
public class DoorHandleInteractable : MonoBehaviour, IInteractable {
    // Walk up to parent to find these
    private NetworkedDoorController _door;
    private NetworkedQuizGate _gate;

    void Awake() {
        _door = GetComponentInParent<NetworkedDoorController>();
        _gate = GetComponentInParent<NetworkedQuizGate>();

        if (_door == null)
            Debug.LogError("[DoorHandleInteractable] No NetworkedDoorController found in parent.");
        if (_gate == null)
            Debug.LogError("[DoorHandleInteractable] No NetworkedQuizGate found in parent.");
    }

    // IInteractable

    // Prompt changes based on current door state
    public string InteractPrompt => _door.IsOpen ? "Press E to Close" : "Press E to Open";

    // IsLocked reflects quiz gate lock state
    public bool IsLocked => !_gate.IsUnlocked;

    public void OnInteract(GameObject interactor) {
        if (_door.IsOpen) {
            // Door is open — always allow closing, no quiz needed
            _door.RequestToggle();
            return;
        }

        // Door is closed — check if quiz gate is locked
        _gate.Attempt(
            interactor,
            onSuccess: () => _door.RequestToggle(),       // unlocked or correct answer → open
            onFail: () => { /* door stays shut, side effects already applied */ }
        );
    }
}