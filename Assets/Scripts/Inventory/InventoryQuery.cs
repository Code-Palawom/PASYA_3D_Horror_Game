// Thin adapter over PlayerInventory so requirement components stay decoupled
// from the concrete inventory implementation. PlayerInventory implements
// this directly (see PlayerInventory.cs).
public interface IInventoryQuery {
    // True if itemID exists anywhere in inventory (any slot).
    bool HasItem(string itemID);

    // The InventoryItem definition currently in the active hotbar slot, or null.
    InventoryItem GetActiveSlotItem();

    // Removes qty of itemID from inventory. Returns true if fully removed.
    bool RemoveItem(string itemID, int qty);
}