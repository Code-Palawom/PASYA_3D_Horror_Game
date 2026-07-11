using UnityEngine;

// ScriptableObject defining a single item type.
// Create via: Assets > Create > Inventory > Item
[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class InventoryItem : ScriptableObject {
    [Header("Identity")]
    public string itemID; // Unique string, e.g. "key_red_door"
    public string displayName;

    [Header("Visuals")]
    [Tooltip("2D icon shown in inventory/UI slots.")]
    public Sprite icon;
    [Tooltip("3D model prefab instantiated for the physical pickup in the world (WorldItem).")]
    public GameObject worldModelPrefab;

    [Header("Stacking")]
    public bool stackable = true;
    public int maxStack = 64;

    [Header("Key Settings")]
    public bool isKey = false;
    public string keyID; // Must match LockedDoor.requiredKeyID exactly
    public bool consumeOnUnlock = false;

    [Header("Pickup Quiz")]
    [Tooltip("Quiz difficulty required to pick this item up via WorldItem. " +
             "Applied to the spawned WorldItem's NetworkedQuizGate before it spawns.")]
    public QuestionDifficulty pickupDifficulty = QuestionDifficulty.Easy;
}