using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

    [Header("Drag Ghost")]
    [Tooltip("A plain UI Image that follows the pointer while dragging. Must sit on a " +
             "Screen Space - Overlay canvas (or one rendered above the inventory panel/hotbar) " +
             "so it's never occluded. Set its Raycast Target OFF in the Inspector — this is " +
             "also enforced in code at Init() — so it never steals the OnDrop hit test from " +
             "the slot underneath the cursor. Leave disabled/inactive by default in the scene.")]
    [SerializeField] private Image dragGhost;

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

        if (dragGhost != null) {
            dragGhost.raycastTarget = false; // belt-and-suspenders alongside the Inspector setting
            dragGhost.enabled = false;
        }

        RefreshAll();
        inventoryPanel.SetActive(false);
    }

    // ── Input ─────────────────────────────────────────────────────────────────

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

    public void OnBeginDrag(int slotIndex, Sprite icon) {
        _dragSourceIndex = slotIndex;

        if (dragGhost == null) return;
        if (icon == null) return; // empty slot — nothing to show, but the move itself still works

        dragGhost.sprite = icon;
        dragGhost.enabled = true;
    }

    // Called instead of OnBeginDrag when the slot being dragged from is
    // empty. Resets the source index so a stale value from an earlier,
    // successful drag can't accidentally be reused by OnDrop.
    public void CancelDrag() => _dragSourceIndex = -1;

    // Called every frame while dragging (InventorySlotUI.OnDrag). screenPosition
    // comes straight from PointerEventData.position, which is already in screen
    // space — this only lines up correctly if dragGhost's canvas is Screen Space
    // - Overlay. If you switch to Screen Space - Camera or World Space, convert
    // via RectTransformUtility.ScreenPointToLocalPointInRectangle first.
    public void UpdateDragVisual(Vector2 screenPosition) {
        if (dragGhost != null && dragGhost.enabled)
            dragGhost.rectTransform.position = screenPosition;
    }

    // Called on release regardless of whether the drop landed on a valid
    // target — always hide the ghost so it never gets stuck on screen.
    public void EndDragVisual() {
        if (dragGhost != null) dragGhost.enabled = false;
    }

    public void OnDrop(int targetIndex) {
        if (_dragSourceIndex < 0 || _dragSourceIndex == targetIndex) return;
        _inventory.MoveSlotServerRpc(_dragSourceIndex, targetIndex);

        // If the item landed in a hotbar slot, make that the active slot —
        // same "picking it up equips it" feel as AddItem's auto-select.
        // Server-side SetActiveSlotServerRpc already bounds-checks this too,
        // but checking here avoids sending a pointless RPC for main-inventory drops.
        if (targetIndex < PlayerInventory.HotbarSize)
            _inventory.SetActiveSlotServerRpc(targetIndex);

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