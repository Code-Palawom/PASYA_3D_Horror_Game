using UnityEngine;
using UnityEngine.InputSystem;

// Client-side inventory UI. Wire this to the local player's PlayerInventory
// by calling Init() from PlayerSetup after network spawn.

// Scene setup:
//   - Create a Canvas with an "InventoryPanel" (hidden by default)
//   - Inside it: 36 InventorySlotUI children (index 0–8 = hotbar, 9–35 = main)
//   - Assign them to UISlots in order
//   - The hotbar can be a separate always-visible bar outside InventoryPanel
public class InventoryUI : MonoBehaviour {
    [Tooltip("Leave empty to auto-resolve the current scene's registry via " +
             "ItemRegistry.Instance (set by that scene's GameBootstrap).")]
    [SerializeField] private ItemRegistry itemRegistry;
    private ItemRegistry Registry => itemRegistry != null ? itemRegistry : ItemRegistry.Instance;
    [SerializeField] private InventorySlotUI[] UISlots; // 36 elements, set in Inspector
    [SerializeField] private GameObject inventoryPanel; // Shown/hidden with Tab

    [SerializeField] private InputAction action;

    private PlayerInventory _inventory;
    private bool _isOpen;
    private int _dragSourceIndex = -1;
    private bool _hasInitialized;

    // ── Initialization ────────────────────────────────────────────────────────

    void Awake() {
        action.performed += _ => SetOpen(!_isOpen);
        action.Enable();
    }

    void OnDestroy() {
        action.Disable();
        action.Dispose();
    }

    public void Init(PlayerInventory inventory) {
        _inventory = inventory;
        _inventory.OnSlotChanged += RefreshSlot;
        _inventory.OnActiveSlotChanged += RefreshActiveHighlight;

        for (int i = 0; i < UISlots.Length; i++)
            UISlots[i].Init(i, i < PlayerInventory.HotbarSize, this);

        RefreshAll();
        inventoryPanel.SetActive(false);
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void Update() {
        if (Keyboard.current.tabKey.wasPressedThisFrame)
            SetOpen(!_isOpen);
    }

    private void SetOpen(bool open) {
        _isOpen = open;
        inventoryPanel.SetActive(open);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private void RefreshAll() {
        for (int i = 0; i < _inventory.SlotCount; i++)
            RefreshSlot(i);
        RefreshActiveHighlight(_inventory.ActiveHotbarIndex);
        _hasInitialized = true;
    }

    private void RefreshSlot(int index) {
        if (index < 0 || index >= UISlots.Length) return;
        var slot = _inventory.GetSlot(index);
        var item = slot.IsEmpty ? null : Registry.Get(slot.ItemID.ToString());
        UISlots[index].UpdateDisplay(item, slot.Quantity);

        // Also re-trigger the popup if this is the slot you're currently
        // holding — e.g. picking an item up directly into your active slot,
        // or its stack count changing. Skipped during the very first
        // RefreshAll() so joining doesn't flash a name immediately; see
        // _hasInitialized below.
        if (_hasInitialized && index < PlayerInventory.HotbarSize
            && index == _inventory.ActiveHotbarIndex) {
            UISlots[index].PlayActiveNamePopup(item?.displayName);
        }
    }

    private void RefreshActiveHighlight(int activeIndex) {
        for (int i = 0; i < PlayerInventory.HotbarSize; i++) {
            bool isActive = i == activeIndex;
            UISlots[i].SetHighlight(isActive);

            // i < _inventory.SlotCount guards against this firing before slots
            // are populated/synced (e.g. before PlayerInventory's own
            // OnNetworkSpawn has run, or before a client's initial NetworkList
            // sync arrives) — GetSlot is bounds-safe now too, but checking
            // here avoids doing the Registry lookup on data we know is stale.
            if (isActive && i < _inventory.SlotCount) {
                var slot = _inventory.GetSlot(i);
                var item = slot.IsEmpty ? null : Registry.Get(slot.ItemID.ToString());
                if (_hasInitialized) UISlots[i].PlayActiveNamePopup(item?.displayName);
            } else {
                // Immediately kills any name still fading on the slot we just
                // switched away from — only one name is ever visible at a time.
                UISlots[i].HideNamePopupImmediate();
            }
        }
    }

    // ── Drag & Drop (called by InventorySlotUI) ───────────────────────────────

    public void OnBeginDrag(int slotIndex)
        => _dragSourceIndex = slotIndex;

    public void OnDrop(int targetIndex) {
        if (_dragSourceIndex < 0 || _dragSourceIndex == targetIndex) return;
        _inventory.MoveSlotServerRpc(_dragSourceIndex, targetIndex);
        _dragSourceIndex = -1;
    }

    // ── Touch/Click Slot Selection (called by InventorySlotUI) ─────────────────

    // Tapping a hotbar slot selects it — same effect as HotbarInput's number
    // keys, just routed through UI instead of Keyboard. Safe to call even if
    // it's already the active slot (PlayerInventory's RPC is idempotent).
    public void OnHotbarSlotTapped(int slotIndex) {
        if (slotIndex == _inventory.ActiveHotbarIndex) return;
        _inventory.SetActiveSlotServerRpc(slotIndex);
    }
}