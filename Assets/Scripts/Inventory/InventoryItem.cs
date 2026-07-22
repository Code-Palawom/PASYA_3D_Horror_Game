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

    [Header("Usage")]
    [Tooltip("If true, one of this item is consumed (removed from inventory) whenever it's used " +
             "to satisfy an ItemRequirement — see ItemRequirement.OnConsumed. Lives on the item " +
             "itself so every requirement referencing this itemID behaves consistently, rather " +
             "than each requirement instance deciding separately.")]
    public bool consumeOnUnlock = false;

    [Header("Pickup Quiz")]
    [Tooltip("Quiz difficulty required to pick this item up via WorldItem. " +
             "Applied to the spawned WorldItem's NetworkedQuizGate before it spawns.")]
    public QuestionDifficulty pickupDifficulty = QuestionDifficulty.Easy;

    [Header("World Physics")]
    [Tooltip("Fallback only — used if worldModelPrefab has no Collider on it. Prefer adding a " +
             "Box/Sphere/Capsule Collider directly to worldModelPrefab sized to fit the actual " +
             "mesh; WorldItem copies its shape automatically. This field just covers items whose " +
             "model doesn't bother with one.")]
    public Vector3 worldColliderSize = new Vector3(0.3f, 0.3f, 0.3f);
    [Tooltip("Fallback only — see worldColliderSize.")]
    public Vector3 worldColliderCenter = Vector3.zero;
}