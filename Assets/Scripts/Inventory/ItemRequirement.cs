using UnityEngine;

// Requires the interacting player to hold a given inventory item anywhere
// in their inventory (not necessarily equipped). For a door-key style check
// that must be in the active hotbar slot, use KeyRequirement instead.
public class ItemRequirement : InteractionRequirement {
    [Header("Item")]
    [SerializeField] string itemId = "torch";
    [SerializeField] bool consumeOnUse = true;

    public override bool IsMet(GameObject interactor) {
        var inventory = interactor.GetComponent<IInventoryQuery>();
        if (inventory == null) {
            Debug.LogWarning($"[ItemRequirement] '{interactor.name}' has no IInventoryQuery component.");
            return false;
        }
        return inventory.HasItem(itemId);
    }

    public override void OnConsumed(GameObject interactor) {
        if (!consumeOnUse) return;
        interactor.GetComponent<IInventoryQuery>()?.RemoveItem(itemId, 1);
    }

    public override string GetFailMessage(GameObject interactor) =>
        string.IsNullOrEmpty(failMessage) ? $"Requires: {itemId}" : failMessage;
}