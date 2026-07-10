using UnityEngine;

// Requires another door/gate in the scene to already be unlocked.
// Drag any component that implements IUnlockable into targetUnlockable
// (e.g. a NetworkedQuizGate, or a NetworkedDoorController once it implements
// IUnlockable too).
public class UnlockRequirement : InteractionRequirement {
    [Header("Target")]
    [Tooltip("Must be a component implementing IUnlockable.")]
    [SerializeField] MonoBehaviour targetUnlockable;

    IUnlockable _target;

    void Awake() {
        _target = targetUnlockable as IUnlockable;
        if (_target == null)
            Debug.LogError($"[DoorUnlockedRequirement] '{name}': " +
                            $"'{targetUnlockable}' does not implement IUnlockable.");
    }

    public override bool IsMet(GameObject interactor) => _target != null && _target.IsUnlocked;
}