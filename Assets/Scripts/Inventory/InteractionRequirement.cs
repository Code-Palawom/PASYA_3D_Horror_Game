using UnityEngine;

// Base class for a single interaction requirement.
//
// Concrete requirements are plain components — write one per "kind" of
// requirement (item, other-door-unlocked, quest-flag, whatever) and drop
// them on the same GameObject as InteractionRequirements. No manual list
// wiring needed; InteractionRequirements finds them automatically.
public abstract class InteractionRequirement : MonoBehaviour {

    [Tooltip("Shown to the player when this requirement isn't met.")]
    [SerializeField] protected string failMessage = "You can't do that yet.";

    // Pure read — no side effects. Called on whichever side is doing the
    // check (the interacting client, for immediate feedback).
    public abstract bool IsMet(GameObject interactor);

    // Called once, only after ALL requirements on the object passed and the
    // interaction is actually going through (e.g. to consume a key item).
    // Override if this requirement needs to react to success. Default: no-op.
    public virtual void OnConsumed(GameObject interactor) { }

    public virtual string GetFailMessage(GameObject interactor) => failMessage;
}