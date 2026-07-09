using UnityEngine;

// ScriptableObject defining a single item type.
// Create via: Assets > Create > Inventory > Item
[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class InventoryItem : ScriptableObject {
    [Header("Identity")]
    public string itemID; // Unique string, e.g. "key_red_door"
    public string displayName;
    public Sprite icon;

    [Header("Stacking")]
    public bool stackable = true;
    public int maxStack = 64;

    [Header("Key Settings")]
    public bool isKey = false;
    public string keyID; // Must match LockedDoor.requiredKeyID exactly
    public bool consumeOnUnlock = false;
}