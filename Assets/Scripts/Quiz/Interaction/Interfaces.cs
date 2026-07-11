using UnityEngine;

public interface IInteractable {
    string InteractPrompt { get; }
    bool IsLocked { get; }

    void OnInteract(GameObject interactor);

    // Called every frame by InteractionController while the player
    // is looking at this interactable. Lets the interactable push
    // custom data (e.g. cooldown) to the player's UI directly.
    // Default implementation just calls ui.Show(InteractPrompt).
    void OnFocus(PlayerInteractionUI ui);
}

public interface IEnemy {
    void AlertTo(Vector3 position);
}

// Anything that can be "unlocked" and queried for that state.
// Implemented by NetworkedQuizGate; implement on NetworkedDoorController too
// if you want doors to gate other doors directly (no quiz involved).