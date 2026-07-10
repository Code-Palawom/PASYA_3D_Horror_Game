using UnityEngine;

// Drop this on any interactable's GameObject alongside one or more
// InteractionRequirement components (ItemRequirement, DoorUnlockedRequirement,
// your own custom ones, etc). It auto-collects them — no manual list to fill in.
//
// Existing interactables (DoorHandleInteractable, PickupInteractable,
// NetworkedQuizGate) check this optionally: if there's no
// InteractionRequirements component present, they behave exactly as before.
public class InteractionRequirements : MonoBehaviour {
    InteractionRequirement[] _requirements;

    void Awake() => _requirements = GetComponents<InteractionRequirement>();

    public bool HasRequirements => _requirements != null && _requirements.Length > 0;

    // Checks every requirement in order. Stops and reports the first failure.
    public bool CheckAll(GameObject interactor, out string failMessage) {
        foreach (var req in _requirements) {
            if (req == null) continue;
            if (!req.IsMet(interactor)) {
                failMessage = req.GetFailMessage(interactor);
                return false;
            }
        }
        failMessage = null;
        return true;
    }

    // Call once, only after the interaction actually succeeds, so items get
    // consumed exactly once (e.g. after a correct quiz answer, not before).
    public void NotifyConsumed(GameObject interactor) {
        foreach (var req in _requirements)
            req?.OnConsumed(interactor);
    }
}