using UnityEngine;

// Requires the interacting player to hold a given inventory item — either
// anywhere in their inventory, or actively equipped in the hotbar, depending
// on requireEquipped. Whether the item gets consumed on a successful use is
// controlled by the item itself (InventoryItem.consumeOnUnlock), not by this
// component — so every requirement referencing the same itemID behaves
// consistently regardless of where any individual instance checks for it.
public class ItemRequirement : InteractionRequirement {
    [Header("Item")]
    [SerializeField] string itemId = "torch";
    [Tooltip("If true, the item must be in the currently active hotbar slot (equipped) — not " +
             "just anywhere in inventory. Use this for door-key style checks.")]
    [SerializeField] bool requireEquipped = false;

    [Tooltip("Leave empty to auto-resolve the current scene's registry via " +
             "ItemRegistry.Instance (set by that scene's GameBootstrap).")]
    [SerializeField] private ItemRegistry itemRegistry;
    private ItemRegistry Registry => itemRegistry != null ? itemRegistry : ItemRegistry.Instance;

    public override bool IsMet(GameObject interactor) {
        var inventory = interactor.GetComponent<IInventoryQuery>();
        if (inventory == null) {
            Debug.LogWarning($"[ItemRequirement] '{interactor.name}' has no IInventoryQuery component.");
            return false;
        }
        return requireEquipped ? IsEquipped(inventory) : inventory.HasItem(itemId);
    }

    private bool IsEquipped(IInventoryQuery inventory) {
        var active = inventory.GetActiveSlotItem();
        return active != null && active.itemID == itemId;
    }

    // Removes from wherever the item actually is — works the same whether
    // requireEquipped is on or off, since RemoveItem searches all slots.
    public override void OnConsumed(GameObject interactor) {
        var item = Registry?.Get(itemId);
        if (item == null || !item.consumeOnUnlock) return;
        interactor.GetComponent<IInventoryQuery>()?.RemoveItem(itemId, 1);
    }

    public override string GetFailMessage(GameObject interactor) {
        if (!string.IsNullOrEmpty(failMessage)) return failMessage;
        return requireEquipped ? $"You need {itemId} equipped." : $"Requires: {itemId}";
    }
}