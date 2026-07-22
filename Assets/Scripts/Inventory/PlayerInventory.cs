using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Host-authoritative inventory. Clients send ServerRpcs; the host validates
// and mutates the NetworkList which auto-syncs to all clients.
//
// Slot layout (indices are computed from hotbarSize, not hardcoded):
//   Index 0 .. hotbarSize-1              → Hotbar  (the "active/held" slot is hotbar[ActiveHotbarIndex])
//   Index hotbarSize .. TotalSlots-1     → Main inventory
//
// Implements IInventoryQuery so any InteractionRequirement (ItemRequirement,
// KeyRequirement, etc) can check this player's inventory without knowing
// about NetworkList/slot layout internals.
public class PlayerInventory : NetworkBehaviour, IInventoryQuery {
    [Header("Slot Counts")]
    [Tooltip("Number of hotbar slots (indices 0..hotbarSize-1). Adjustable per-prefab.")]
    [SerializeField] private int hotbarSize = 4;
    [Tooltip("Number of MAIN inventory slots, i.e. NOT counting the hotbar. " +
             "Total slots = hotbarSize + mainInventorySize.")]
    [SerializeField] private int mainInventorySize = 8;

    public int HotbarSize => hotbarSize;
    public int MainInventorySize => mainInventorySize;
    public int TotalSlots => hotbarSize + mainInventorySize;

    [Tooltip("Leave empty to auto-resolve the current scene's registry via " +
             "ItemRegistry.Instance (set by that scene's GameBootstrap). Only " +
             "assign this directly if this prefab should always use one fixed registry.")]
    [SerializeField] private ItemRegistry itemRegistry;
    private ItemRegistry Registry => itemRegistry != null ? itemRegistry : ItemRegistry.Instance;

    [Tooltip("Must have NetworkObject + WorldItem components (the same prefab WorldItemSpawner " +
             "uses). Instantiated server-side whenever a player drops an item from their inventory.")]
    [SerializeField] private GameObject worldItemPrefab;
    [Tooltip("Dropped items spawn this far in front of the player (transform.forward).")]
    [SerializeField] private float dropDistance = 1.5f;
    [Tooltip("Speed of the toss applied to the dropped item's Rigidbody, in m/s. Direction is " +
             "player-forward blended with an upward arc — set by dropTossUpwardRatio.")]
    [SerializeField] private float dropTossSpeed = 3f;
    [Tooltip("0 = flat toss straight forward, 1 = mostly upward. 0.5 gives a gentle arc.")]
    [SerializeField, Range(0f, 1f)] private float dropTossUpwardRatio = 0.5f;

    // ── Networked State ───────────────────────────────────────────────────────

    private NetworkList<NetworkInventorySlot> _slots;

    // Read by everyone, written only by server
    private NetworkVariable<int> _activeHotbarIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int ActiveHotbarIndex => _activeHotbarIndex.Value;
    public int SlotCount => _slots.Count;

    // ── Events (for InventoryUI) ──────────────────────────────────────────────

    public event System.Action<int> OnSlotChanged; // slot index
    public event System.Action<int> OnActiveSlotChanged; // new hotbar index

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake() {
        _slots = new NetworkList<NetworkInventorySlot>(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    }

    public override void OnNetworkSpawn() {
        if (IsServer) {
            // Fill all slots with empty data
            for (int i = 0; i < TotalSlots; i++)
                _slots.Add(NetworkInventorySlot.Empty);
        }

        _slots.OnListChanged += HandleSlotChanged;
        _activeHotbarIndex.OnValueChanged += (_, newVal) => OnActiveSlotChanged?.Invoke(newVal);
    }

    public override void OnNetworkDespawn() {
        _slots.OnListChanged -= HandleSlotChanged;
    }

    private void HandleSlotChanged(NetworkListEvent<NetworkInventorySlot> e)
        => OnSlotChanged?.Invoke(e.Index);

    // ── Server-Only: Slot Mutations ───────────────────────────────────────────

    // Add items to inventory. Returns true if ALL qty was placed.
    // Call only from server (e.g. inside a ServerRpc or host game logic).
    //
    // Priority order:
    //   1) Top off any EXISTING matching stack, wherever it is — hotbar or
    //      main inventory. Adding to a stack you already have always wins
    //      over where a brand new stack would land.
    //   2) Whatever's left needs a NEW slot. This is where hotbar gets
    //      priority: empty hotbar slots are filled first, and only the
    //      leftover that doesn't fit overflows into main inventory.
    //
    // If any of the item lands in a hotbar slot (existing stack topped off
    // OR a new stack placed there), that slot becomes the active slot —
    // picking something up effectively "equips" it. If it only touches main
    // inventory, the active slot is left untouched.
    public bool AddItem(string itemID, int qty = 1) {
        if (!IsServer) return false;
        var data = Registry.Get(itemID);
        if (data == null) {
            Debug.LogWarning($"[Inventory] Unknown itemID: {itemID}");
            return false;
        }

        int firstTouchedSlot = -1;
        int remaining = qty;

        // 1) Stack into any existing match, hotbar or main, wherever it is.
        if (data.stackable)
            remaining = StackIntoExisting(itemID, data, remaining, 0, TotalSlots, ref firstTouchedSlot);

        // 2) New slot needed for the rest — hotbar's empty slots first...
        if (remaining > 0)
            remaining = FillEmptySlots(itemID, data, remaining, 0, hotbarSize, ref firstTouchedSlot);

        // ...then overflow into main inventory's empty slots.
        if (remaining > 0)
            remaining = FillEmptySlots(itemID, data, remaining, hotbarSize, TotalSlots, ref firstTouchedSlot);

        bool success = remaining == 0;

        if (success && firstTouchedSlot >= 0 && firstTouchedSlot < hotbarSize)
            _activeHotbarIndex.Value = firstTouchedSlot;

        return success;
    }

    // Tops off existing stacks of itemID within [startIndex, endExclusive).
    // Returns whatever quantity didn't fit (0 if it all fit).
    private int StackIntoExisting(string itemID, InventoryItem data, int qty, int startIndex, int endExclusive,
        ref int firstTouchedSlot) {
        int remaining = qty;
        for (int i = startIndex; i < endExclusive && remaining > 0; i++) {
            var s = _slots[i];
            if (!s.IsEmpty && s.ItemID.ToString() == itemID && s.Quantity < data.maxStack) {
                int space = data.maxStack - s.Quantity;
                int toAdd = Mathf.Min(space, remaining);
                _slots[i] = new NetworkInventorySlot { ItemID = itemID, Quantity = s.Quantity + toAdd };
                remaining -= toAdd;
                if (firstTouchedSlot < 0) firstTouchedSlot = i;
            }
        }
        return remaining;
    }

    // Fills empty slots within [startIndex, endExclusive) with new stacks of
    // itemID. Returns whatever quantity didn't fit (0 if it all fit).
    private int FillEmptySlots(string itemID, InventoryItem data, int qty, int startIndex, int endExclusive,
        ref int firstTouchedSlot) {
        int remaining = qty;
        for (int i = startIndex; i < endExclusive && remaining > 0; i++) {
            if (_slots[i].IsEmpty) {
                int toPlace = data.stackable ? Mathf.Min(data.maxStack, remaining) : 1;
                _slots[i] = new NetworkInventorySlot { ItemID = itemID, Quantity = toPlace };
                remaining -= toPlace;
                if (firstTouchedSlot < 0) firstTouchedSlot = i;
            }
        }
        return remaining;
    }

    // Remove qty of itemID. Returns true if fully removed.
    // Call only from server. IInventoryQuery.RemoveItem routes here.
    public bool RemoveItem(string itemID, int qty = 1) {
        if (!IsServer) return false;

        int remaining = qty;
        for (int i = 0; i < TotalSlots && remaining > 0; i++) {
            var s = _slots[i];
            if (!s.IsEmpty && s.ItemID.ToString() == itemID) {
                int toRemove = Mathf.Min(s.Quantity, remaining);
                int newQty = s.Quantity - toRemove;
                _slots[i] = newQty <= 0
                    ? NetworkInventorySlot.Empty
                    : new NetworkInventorySlot { ItemID = itemID, Quantity = newQty };
                remaining -= toRemove;
            }
        }
        return remaining == 0;
    }

    // True if itemID exists anywhere in inventory (any slot, not just active).
    // Read-only — safe to call on client for UI/requirement previews, since
    // the NetworkList is Everyone-readable.
    public bool HasItem(string itemID) {
        if (string.IsNullOrEmpty(itemID)) return false;
        for (int i = 0; i < TotalSlots; i++) {
            var s = _slots[i];
            if (!s.IsEmpty && s.ItemID.ToString() == itemID) return true;
        }
        return false;
    }

    // Get the InventoryItem definition for the active hotbar slot.
    public InventoryItem GetActiveSlotItem() {
        var slot = GetSlot(_activeHotbarIndex.Value);
        return slot.IsEmpty ? null : Registry.Get(slot.ItemID.ToString());
    }

    // ── Read-Only (Client-Safe) ───────────────────────────────────────────────

    // Bounds-checked: returns Empty rather than throwing if called before the
    // server has populated slots, or before a client's NetworkList sync has
    // arrived (both can legitimately happen for a frame or two around spawn).
    public NetworkInventorySlot GetSlot(int index) {
        if (index < 0 || index >= _slots.Count) return NetworkInventorySlot.Empty;
        return _slots[index];
    }

    // ── Client → Server RPCs ──────────────────────────────────────────────────

    // Select a hotbar slot (0..hotbarSize-1). Only the owner may call this.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void SetActiveSlotServerRpc(int index) {
        if (index < 0 || index >= hotbarSize) return;
        _activeHotbarIndex.Value = index;
    }

    // Swap two inventory slots. Only the owner may call this.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void MoveSlotServerRpc(int fromIndex, int toIndex) {
        if (fromIndex < 0 || fromIndex >= TotalSlots) return;
        if (toIndex < 0 || toIndex >= TotalSlots) return;
        if (fromIndex == toIndex) return;

        var temp = _slots[fromIndex];
        _slots[fromIndex] = _slots[toIndex];
        _slots[toIndex] = temp;
    }

    // Drop `quantity` of the item at slotIndex into the world as a WorldItem,
    // in front of the player. Only the owner may call this. Dropping less
    // than the full stack leaves the remainder in place; dropping the whole
    // stack (or more, which is clamped) empties the slot.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void DropItemServerRpc(int slotIndex, int quantity) {
        if (slotIndex < 0 || slotIndex >= TotalSlots) return;

        var slot = _slots[slotIndex];
        if (slot.IsEmpty) return;

        int qtyToDrop = Mathf.Clamp(quantity, 1, slot.Quantity);
        string itemID = slot.ItemID.ToString();

        if (worldItemPrefab == null) {
            Debug.LogError("[PlayerInventory] No worldItemPrefab assigned — cannot drop items.");
            return;
        }

        Vector3 spawnPos = transform.position + transform.forward * dropDistance + Vector3.up * 0.5f;
        var go = Instantiate(worldItemPrefab, spawnPos, Quaternion.identity);
        var netObj = go.GetComponent<NetworkObject>();
        if (netObj == null) {
            Debug.LogError($"[PlayerInventory] '{worldItemPrefab.name}' has no NetworkObject.");
            Destroy(go);
            return;
        }

        // Note: dropped items skip the pickup quiz entirely — see
        // NetworkedQuizGate.SetSkipQuiz. Must be called before Spawn(), same
        // as WorldItemSpawner's SetDifficulty() call for spawner-placed items.
        var gate = go.GetComponent<NetworkedQuizGate>();
        gate?.SetSkipQuiz(true);

        netObj.Spawn();
        go.GetComponent<WorldItem>().Setup(itemID, qtyToDrop);

        // Gravity/physics: worldItemPrefab needs a Rigidbody + Collider + NetworkTransform +
        // NetworkRigidbody for this to actually fall and sync across clients. Toss it forward
        // and slightly up so it doesn't just spawn and drop straight down through the floor.
        // Must happen AFTER Spawn() — NetworkRigidbody keeps the Rigidbody kinematic until
        // network authority is established, and setting velocity on a still-kinematic body
        // throws ("Setting linear velocity of a kinematic body is not supported").
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic) {
            Vector3 tossDir = Vector3.Lerp(transform.forward, Vector3.up, dropTossUpwardRatio).normalized;
            rb.linearVelocity = tossDir * dropTossSpeed;
        }

        int remaining = slot.Quantity - qtyToDrop;
        _slots[slotIndex] = remaining <= 0
            ? NetworkInventorySlot.Empty
            : new NetworkInventorySlot { ItemID = slot.ItemID, Quantity = remaining };
    }
}