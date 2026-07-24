using System;

// Implement on a component attached to the player prefab to give an equipped
// item a special action triggered by the HUD action button — see
// PlayerItemActions (finds these automatically via GetComponents<IItemAction>)
// and ItemActionButtonUI (the button itself).
//
// One component per ActionType. Whichever InventoryItem has
// actionType == this.ActionType routes button presses here while equipped.
public interface IItemAction {
    // Must match an InventoryItem.actionType exactly for the button to route to this.
    ItemActionType ActionType { get; }

    // Called when the player presses the HUD action button while this
    // action's ActionType is the currently equipped item's action.
    void Activate();

    // Whether this action is currently "on" — used to pick between an
    // equipped item's actionIconOn/actionIconOff (see InventoryItem,
    // PlayerItemActions). Stateless actions (no on/off distinction) can just
    // always return false; their icon falls back to actionIcon regardless.
    bool IsActive { get; }

    // Raise whenever IsActive changes, so PlayerItemActions can refresh the
    // HUD icon immediately (e.g. right when the player presses the button)
    // instead of waiting for the equipped slot itself to change.
    event Action OnStateChanged;
}