using UnityEngine;
using UnityEngine.InputSystem;

// Client-side inventory UI. Wire this to the local player's PlayerInventory
// by calling Init() from PlayerSetup after network spawn.

// Scene setup:
//   - Create a Canvas with an "InventoryPanel" (hidden by default)
//   - Inside it: 36 InventorySlotUI children (index 0–8 = hotbar, 9–35 = main)
//   - Assign them to slotUIs in order
//   - The hotbar can be a separate always-visible bar outside InventoryPanel
public class InventoryUI : MonoBehaviour {
    [SerializeField] private ItemRegistry itemRegistry;
    [SerializeField] private InventorySlotUI[] slotUIs; // 36 elements, set in Inspector
    [SerializeField] private GameObject inventoryPanel; // Shown/hidden with Tab

    private PlayerInventory _inventory;
    private bool _isOpen;
    private int _dragSourceIndex = -1;

    // ── Initialization ────────────────────────────────────────────────────────

    public void Init(PlayerInventory inventory) {
        _inventory = inventory;
        _inventory.OnSlotChanged += RefreshSlot;
        _inventory.OnActiveSlotChanged += RefreshActiveHighlight;

        for (int i = 0; i < slotUIs.Length; i++)
            slotUIs[i].Init(i, i < PlayerInventory.HotbarSize, this);

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
    }

    private void RefreshSlot(int index) {
        if (index < 0 || index >= slotUIs.Length) return;
        var slot = _inventory.GetSlot(index);
        var item = slot.IsEmpty ? null : itemRegistry.Get(slot.ItemID.ToString());
        slotUIs[index].UpdateDisplay(item, slot.Quantity);
    }

    private void RefreshActiveHighlight(int activeIndex) {
        for (int i = 0; i < PlayerInventory.HotbarSize; i++)
            slotUIs[i].SetHighlight(i == activeIndex);
    }

    // ── Drag & Drop (called by InventorySlotUI) ───────────────────────────────

    public void OnBeginDrag(int slotIndex)
        => _dragSourceIndex = slotIndex;

    public void OnDrop(int targetIndex) {
        if (_dragSourceIndex < 0 || _dragSourceIndex == targetIndex) return;
        _inventory.MoveSlotServerRpc(_dragSourceIndex, targetIndex);
        _dragSourceIndex = -1;
    }
}