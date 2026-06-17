using UnityEngine;

public interface IInteractable {
    string InteractPrompt { get; }
    bool IsLocked { get; }
    void OnInteract(GameObject interactor);
}

public interface IEnemy {
    void AlertTo(Vector3 position);
}