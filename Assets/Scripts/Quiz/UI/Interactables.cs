using UnityEngine;

// Door
[RequireComponent(typeof(NetworkedQuizGate))]
public class DoorInteractable : MonoBehaviour, IInteractable {
    [SerializeField] Animator doorAnimator;

    private NetworkedQuizGate _gate;

    public string InteractPrompt => IsLocked ? "Press E to answer question" : "Press E to open";
    public bool IsLocked => !_gate.IsUnlocked;

    void Awake() => _gate = GetComponent<NetworkedQuizGate>();

    public void OnInteract(GameObject interactor) {
        _gate.Attempt(
            interactor,
            onSuccess: () => doorAnimator.SetTrigger("Open"),
            onFail: () => { /* door stays shut, side effects handled by gate */ }
        );
    }
}

// Pickup Item
[RequireComponent(typeof(NetworkedQuizGate))]
public class PickupInteractable : MonoBehaviour, IInteractable {
    [SerializeField] string itemName = "Item";

    private NetworkedQuizGate _gate;

    public string InteractPrompt => IsLocked ? "Press E to answer question" : $"Press E to pick up {itemName}";
    public bool IsLocked => !_gate.IsUnlocked;

    void Awake() => _gate = GetComponent<NetworkedQuizGate>();

    public void OnInteract(GameObject interactor) {
        _gate.Attempt(
            interactor,
            onSuccess: () => Destroy(gameObject),
            onFail: () => { /* item stays, side effects handled by gate */ }
        );
    }
}