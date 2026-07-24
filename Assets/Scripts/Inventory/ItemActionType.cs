// Identifies which special "equipped item" behavior an InventoryItem grants,
// if any — drives whether/which HUD action button shows up while it's the
// active hotbar item (see InventoryItem.actionType, PlayerItemActions,
// ItemActionButtonUI). None means "no action button for this item".
//
// Add new entries here as you add new IItemAction implementations (e.g.
// Grapple, Radio) — nothing else needs to know about them; PlayerItemActions
// finds whichever component implements the matching ActionType automatically.
public enum ItemActionType {
    None,
    Flashlight,
}