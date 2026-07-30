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
    [Tooltip("Optional. TaskDefinition (SpecificGate type) matches against this. Leave blank to " +
             "just use itemID — only set this if you need the gate id to differ from the item's " +
             "own id (e.g. two spawners share this item but should count as separate tasks).")]
    public string pickupGateId;
    [Tooltip("Optional. TaskDefinition (DifficultyOrTag type) matches against these. Applied to " +
             "the spawned WorldItem's NetworkedQuizGate before it spawns, same as pickupDifficulty.")]
    public System.Collections.Generic.List<string> pickupTags = new();

    [Header("World Physics")]
    [Tooltip("Fallback only — used if worldModelPrefab has no Collider on it. Prefer adding a " +
             "Box/Sphere/Capsule Collider directly to worldModelPrefab sized to fit the actual " +
             "mesh; WorldItem copies its shape automatically. This field just covers items whose " +
             "model doesn't bother with one.")]
    public Vector3 worldColliderSize = new Vector3(0.3f, 0.3f, 0.3f);
    [Tooltip("Fallback only — see worldColliderSize.")]
    public Vector3 worldColliderCenter = Vector3.zero;

    [Header("Equipped Action")]
    [Tooltip("If not None, equipping this item (making it the active hotbar slot) shows a HUD " +
             "action button. Pressing it calls Activate() on whichever player component implements " +
             "IItemAction with a matching ActionType (see PlayerItemActions, e.g. FlashlightController " +
             "for Flashlight). None means no button.")]
    public ItemActionType actionType = ItemActionType.None;
    [Tooltip("Fallback icon used when actionIconOn/actionIconOff don't apply (e.g. this action has " +
             "no on/off state) or aren't assigned. Leave empty to fall back further to this item's " +
             "normal icon.")]
    public Sprite actionIcon;
    [Tooltip("Icon shown on the HUD action button while the equipped IItemAction.IsActive is true " +
             "(e.g. flashlight ON). Leave empty to fall back to actionIcon.")]
    public Sprite actionIconOn;
    [Tooltip("Icon shown while the equipped IItemAction.IsActive is false (e.g. flashlight OFF). " +
             "Leave empty to fall back to actionIcon.")]
    public Sprite actionIconOff;
}