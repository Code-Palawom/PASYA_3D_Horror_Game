using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Host-authoritative inventory. Clients send ServerRpcs; the host validates
// and mutates the NetworkList which auto-syncs to all clients.
//
// Slot layout:
//   Index 0–8   → Hotbar  (the "active/held" slot is hotbar[ActiveHotbarIndex])
//   Index 9–35  → Main inventory
public class PlayerInventory : NetworkBehaviour {
    public const int HotbarSize = 9;
    public const int InventorySize = 36;

    [SerializeField] private ItemRegistry itemRegistry;

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
            for (int i = 0; i < InventorySize; i++)
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
    public bool AddItem(string itemID, int qty = 1) {
        if (!IsServer) return false;
        var data = itemRegistry.Get(itemID);
        if (data == null) {
            Debug.LogWarning($"[Inventory] Unknown itemID: {itemID}");
            return false;
        }

        int remaining = qty;

        // 1) Stack into existing slots
        if (data.stackable) {
            for (int i = 0; i < InventorySize && remaining > 0; i++) {
                var s = _slots[i];
                if (!s.IsEmpty && s.ItemID.ToString() == itemID && s.Quantity < data.maxStack) {
                    int space = data.maxStack - s.Quantity;
                    int toAdd = Mathf.Min(space, remaining);
                    _slots[i] = new NetworkInventorySlot { ItemID = itemID, Quantity = s.Quantity + toAdd };
                    remaining -= toAdd;
                }
            }
        }

        // 2) Place remainder into empty slots
        for (int i = 0; i < InventorySize && remaining > 0; i++) {
            if (_slots[i].IsEmpty) {
                int toPlace = data.stackable ? Mathf.Min(data.maxStack, remaining) : 1;
                _slots[i] = new NetworkInventorySlot { ItemID = itemID, Quantity = toPlace };
                remaining -= toPlace;
            }
        }

        return remaining == 0;
    }

    // Remove qty of itemID. Returns true if fully removed.
    public bool RemoveItem(string itemID, int qty = 1) {
        if (!IsServer) return false;

        int remaining = qty;
        for (int i = 0; i < InventorySize && remaining > 0; i++) {
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

    // Returns true if the item in the active hotbar slot is a key
    // matching the given keyID.
    public bool HasKeyInActiveSlot(string keyID) {
        if (!IsServer) return false;
        var slot = _slots[_activeHotbarIndex.Value];
        if (slot.IsEmpty) return false;
        var item = itemRegistry.Get(slot.ItemID.ToString());
        return item != null && item.isKey && item.keyID == keyID;
    }

    // Get the InventoryItem definition for the active hotbar slot.
    public InventoryItem GetActiveSlotItem() {
        var slot = _slots[_activeHotbarIndex.Value];
        return slot.IsEmpty ? null : itemRegistry.Get(slot.ItemID.ToString());
    }

    // ── Read-Only (Client-Safe) ───────────────────────────────────────────────

    public NetworkInventorySlot GetSlot(int index) => _slots[index];

    // ── Client → Server RPCs ──────────────────────────────────────────────────

    // Select a hotbar slot (0–8). Only the owner may call this.
    [ServerRpc(RequireOwnership = true)]
    public void SetActiveSlotServerRpc(int index) {
        if (index < 0 || index >= HotbarSize) return;
        _activeHotbarIndex.Value = index;
    }

    // Swap two inventory slots. Only the owner may call this.
    [ServerRpc(RequireOwnership = true)]
    public void MoveSlotServerRpc(int fromIndex, int toIndex) {
        if (fromIndex < 0 || fromIndex >= InventorySize) return;
        if (toIndex < 0 || toIndex >= InventorySize) return;
        if (fromIndex == toIndex) return;

        var temp = _slots[fromIndex];
        _slots[fromIndex] = _slots[toIndex];
        _slots[toIndex] = temp;
    }

    // Drop item at slotIndex into the world. Only the owner may call this.
    [ServerRpc(RequireOwnership = true)]
    public void DropItemServerRpc(int slotIndex) {
        if (slotIndex < 0 || slotIndex >= InventorySize) return;
        if (_slots[slotIndex].IsEmpty) return;

        // TODO: Instantiate a WorldItem prefab at player's position here
        // var go = Instantiate(worldItemPrefab, transform.position + transform.forward, Quaternion.identity);
        // go.GetComponent<NetworkObject>().Spawn();
        // go.GetComponent<WorldItem>().Setup(_slots[slotIndex].ItemID.ToString(), _slots[slotIndex].Quantity);

        _slots[slotIndex] = NetworkInventorySlot.Empty;
    }
}