using UnityEngine;

// Pickup item interactable.
// Implements OnFocus to show cooldown countdown when gate is locked.
[RequireComponent(typeof(NetworkedQuizGate))]
public class PickupInteractable : MonoBehaviour, IInteractable {
    [SerializeField] string itemName = "Item";

    private NetworkedQuizGate _gate;
    private InteractionRequirements _requirements;

    void Awake() {
        _gate = GetComponent<NetworkedQuizGate>();
        _requirements = GetComponent<InteractionRequirements>();
    }

    public string InteractPrompt {
        get {
            if (_gate.IsCooldownActive) return "Locked";
            if (_gate.HasInteractingPlayer
                && !_gate.AllowOthers) return "Someone is answering...";
            return $"Press E to pick up {itemName}";
        }
    }

    public bool IsLocked => !_gate.IsUnlocked;

    public void OnFocus(PlayerInteractionUI ui) {
        if (_gate.IsCooldownActive)
            ui.ShowWithCooldown(_gate.CooldownRemaining, _gate.WrongAnswerCooldown);
        else
            ui.Show(InteractPrompt);
    }

    public void OnInteract(GameObject interactor) {
        if (_requirements != null && _requirements.HasRequirements
            && !_requirements.CheckAll(interactor, out string failMsg)) {
            PlayerInteractionUI.ShowMessageForPlayer(interactor, failMsg);
            return;
        }

        _gate.Attempt(
            interactor,
            onSuccess: () => {
                _requirements?.NotifyConsumed(interactor);
                Destroy(gameObject);
            },
            onFail: () => { }
        );
    }
}