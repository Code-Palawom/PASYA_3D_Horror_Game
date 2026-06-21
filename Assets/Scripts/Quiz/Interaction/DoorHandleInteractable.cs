using UnityEngine;

// Sits on DoorHandle child.
// Passes cooldown info to the player's own UI when they look at the handle.
public class DoorHandleInteractable : MonoBehaviour, IInteractable {
    private NetworkedDoorController _door;
    private NetworkedQuizGate _gate;

    void Awake() {
        _door = GetComponentInParent<NetworkedDoorController>();
        _gate = GetComponentInParent<NetworkedQuizGate>();

        if (_door == null) Debug.LogError("[DoorHandleInteractable] Missing NetworkedDoorController.");
        if (_gate == null) Debug.LogError("[DoorHandleInteractable] Missing NetworkedQuizGate.");
    }

    // ─────────────────────────────────────────────────────────
    // Prompt text — used as fallback / console label
    // ─────────────────────────────────────────────────────────
    public string InteractPrompt {
        get {
            if (_gate.IsCooldownActive)
                return "Locked";
            if (_gate.HasInteractingPlayer && !_gate.AllowOthers)
                return "Someone is answering...";
            return _door.IsOpen ? "Close" : "Open";
        }
    }

    public bool IsLocked => !_gate.IsUnlocked;

    // ─────────────────────────────────────────────────────────
    // Called by InteractionController every frame the player
    // is looking at this handle — lets us push cooldown data
    // directly to the player's UI
    // ─────────────────────────────────────────────────────────
    public void OnFocus(PlayerInteractionUI ui) {
        if (_gate.IsCooldownActive) {
            ui.ShowWithCooldown(
                _gate.CooldownRemaining,
                _gate.WrongAnswerCooldown    // total duration for bar fill
            );
        } else {
            ui.Show(InteractPrompt);
        }
    }

    // ─────────────────────────────────────────────────────────
    public void OnInteract(GameObject interactor) {
        if (_door.IsOpen) {
            _door.RequestToggle();
            return;
        }

        _gate.Attempt(
            interactor,
            onSuccess: () => _door.RequestToggle(),
            onFail: () => { }
        );
    }
}