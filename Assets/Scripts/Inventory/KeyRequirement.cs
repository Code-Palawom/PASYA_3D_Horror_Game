using UnityEngine;

// Requires the interacting player to have a matching key equipped in their
// ACTIVE hotbar slot — not just anywhere in inventory. Mirrors the
// keyID/isKey/consumeOnUnlock fields on InventoryItem, so the same key
// assets you already made for the old LockedDoor flow work here unchanged.
//
// Attach alongside InteractionRequirements on any door/gate that should be
// key-locked. Combine with a NetworkedQuizGate on the same object if you
// want "key AND correct quiz answer" — order doesn't matter, all
// requirements + the gate must pass.
public class KeyRequirement : InteractionRequirement {
    [Header("Key")]
    [Tooltip("Must match the keyID field on the InventoryItem SO for the key.")]
    [SerializeField] string requiredKeyID = "key_red_door";

    public override bool IsMet(GameObject interactor) {
        var inventory = interactor.GetComponent<IInventoryQuery>();
        if (inventory == null) {
            Debug.LogWarning($"[KeyRequirement] '{interactor.name}' has no IInventoryQuery component.");
            return false;
        }
        return inventory.HasKeyInActiveSlot(requiredKeyID);
    }

    // Consumes the key only if its InventoryItem asset has consumeOnUnlock set.
    public override void OnConsumed(GameObject interactor) {
        var inventory = interactor.GetComponent<IInventoryQuery>();
        var activeItem = inventory?.GetActiveSlotItem();
        if (activeItem != null && activeItem.isKey
            && activeItem.keyID == requiredKeyID && activeItem.consumeOnUnlock) {
            inventory.RemoveItem(activeItem.itemID, 1);
        }
    }

    public override string GetFailMessage(GameObject interactor) =>
        string.IsNullOrEmpty(failMessage) ? "You need the right key equipped." : failMessage;
}