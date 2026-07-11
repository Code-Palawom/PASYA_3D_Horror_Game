using Unity.Netcode;
using UnityEngine;

// Sits on DoorHandle child.
// Passes cooldown info to the player's own UI when they look at the handle.
public class DoorHandleInteractable : MonoBehaviour, IInteractable {
    private NetworkedDoorController _door;
    private NetworkedQuizGate _gate;
    private InteractionRequirements _requirements;

    void Awake() {
        _door = GetComponentInParent<NetworkedDoorController>();
        _gate = GetComponentInParent<NetworkedQuizGate>();
        _requirements = GetComponentInParent<InteractionRequirements>();

        if (_door == null) Debug.LogError("[DoorHandleInteractable] Missing NetworkedDoorController.");
        if (_gate == null) Debug.LogError("[DoorHandleInteractable] Missing NetworkedQuizGate.");
    }

    // IInteractable.OnFocus/InteractPrompt don't receive the interactor, so
    // we resolve the local player directly to preview requirement state.
    // Only ever meaningful for the local client anyway — remote players'
    // interactables aren't focused/prompted on your screen.
    private static GameObject LocalPlayer =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null
            ? NetworkManager.Singleton.LocalClient.PlayerObject?.gameObject
            : null;

    // ─────────────────────────────────────────────────────────
    // Prompt text — used as fallback / console label
    // ─────────────────────────────────────────────────────────
    public string InteractPrompt {
        get {
            if (_gate.IsCooldownActive)
                return "Locked";
            if (_gate.HasInteractingPlayer && !_gate.AllowOthers)
                return "Someone is answering...";

            // Preview requirement state so the prompt doesn't just say "Open"
            // when the player can't actually pass the requirement check yet.
            if (_requirements != null && _requirements.HasRequirements) {
                var local = LocalPlayer;
                if (local != null && !_requirements.CheckAll(local, out string failMsg))
                    return failMsg;
            }

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

        // Requirement check (key items, other doors unlocked, etc) happens
        // BEFORE the quiz starts — no point answering a question you can't
        // actually use the result of yet.
        if (_requirements != null && _requirements.HasRequirements
            && !_requirements.CheckAll(interactor, out string failMsg)) {
            PlayerInteractionUI.ShowMessageForPlayer(interactor, failMsg);
            return;
        }

        _gate.Attempt(
            interactor,
            onSuccess: () => {
                _requirements?.NotifyConsumed(interactor);
                _door.RequestToggle();
            },
            onFail: () => { }
        );
    }
}