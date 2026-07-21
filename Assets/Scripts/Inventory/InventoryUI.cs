using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PrimeTween;

// Client-side inventory UI. Wire this to the local player's PlayerInventory
// by calling Init() from PlayerSetup after network spawn.

// Scene setup:
//   - Create a Canvas with an "InventoryPanel" — its GameObject stays ACTIVE
//     at all times now (the popup toggle uses scale/alpha, not SetActive),
//     just position it in the scene at SCREEN CENTER (its anchor/pivot should
//     be 0.5, 0.5 so it scales outward from its own center, not a corner).
//   - Inside it: (hotbarSize + mainInventorySize) InventorySlotUI children —
//     indices 0..hotbarSize-1 = hotbar, the rest = main inventory
//   - Assign them to UISlots in order
//   - The hotbar itself should be a SEPARATE always-visible bar outside
//     InventoryPanel — only the main inventory portion pops
public class InventoryUI : MonoBehaviour {
    [Tooltip("Leave empty to auto-resolve the current scene's registry via " +
             "ItemRegistry.Instance (set by that scene's GameBootstrap).")]
    [SerializeField] private ItemRegistry itemRegistry;
    private ItemRegistry Registry => itemRegistry != null ? itemRegistry : ItemRegistry.Instance;
    [SerializeField] private InventorySlotUI[] UISlots; // (hotbarSize + mainInventorySize) elements, set in Inspector

    [Header("Main Inventory Popup Panel")]
    [Tooltip("The main inventory panel's RectTransform (NOT the hotbar — the hotbar stays " +
             "always visible). Position it in the scene at screen center with pivot (0.5, 0.5) " +
             "so it scales outward from its own middle.")]
    [SerializeField] private RectTransform inventoryPanel;
    [Tooltip("Prevents clicking/dragging through the panel while it's scaled down, and blocks " +
             "interaction during the pop animation itself.")]
    [SerializeField] private CanvasGroup inventoryPanelCanvasGroup;
    [SerializeField] private float popupInDuration = 0.35f;
    [SerializeField] private float popupOutDuration = 0.2f;

    [Header("Drag Ghost")]
    [Tooltip("A plain UI Image that follows the pointer while dragging. Must sit on a " +
             "Screen Space - Overlay canvas (or one rendered above the inventory panel/hotbar) " +
             "so it's never occluded. Set its Raycast Target OFF in the Inspector — this is " +
             "also enforced in code at Init() — so it never steals the OnDrop hit test from " +
             "the slot underneath the cursor. Leave disabled/inactive by default in the scene.")]
    [SerializeField] private Image dragGhost;

    [SerializeField] private InputAction action;

#if UNITY_EDITOR || UNITY_STANDALONE
    [SerializeField] Image backpackIcon;
#endif

    private PlayerInventory _inventory;
    private bool _isOpen;
    private int _dragSourceIndex = -1;
    private InventorySlotUI _hoveredDropTarget;
    private bool _hasInitialized;

    private PrimeTween.Tween _scaleTween;
    private PrimeTween.Tween _fadeTween;

    public bool IsOpen => _isOpen;

    // ── Initialization ────────────────────────────────────────────────────────

    void Awake() {
        action.performed += _ => SetOpen(!_isOpen);
        action.Enable();
    }

    void OnDestroy() {
        action.Disable();
        action.Dispose();
        if (_scaleTween.isAlive) _scaleTween.Stop();
        if (_fadeTween.isAlive) _fadeTween.Stop();
    }

    public void Init(PlayerInventory inventory) {
        _inventory = inventory;
        _inventory.OnSlotChanged += RefreshSlot;
        _inventory.OnActiveSlotChanged += RefreshActiveHighlight;

        for (int i = 0; i < UISlots.Length; i++)
            UISlots[i].Init(i, i < _inventory.HotbarSize, this);

        if (dragGhost != null) {
            dragGhost.raycastTarget = false; // belt-and-suspenders alongside the Inspector setting
            dragGhost.enabled = false;
        }

        // Start popped-down/invisible at whatever position the panel is
        // designed at (should be screen center) — no position math needed
        // now, since the popup scales in place instead of sliding in.
        inventoryPanel.localScale = Vector3.zero;
        if (inventoryPanelCanvasGroup != null) inventoryPanelCanvasGroup.alpha = 0f;
        SetPanelInteractable(false);

        RefreshAll();
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void SetOpen(bool open) {
        _isOpen = open;
        if (_scaleTween.isAlive) _scaleTween.Stop();
        if (_fadeTween.isAlive) _fadeTween.Stop();

        // Block interaction immediately for both directions — closing shouldn't
        // be draggable mid-shrink, and opening only becomes interactable once
        // the bounce settles (below).
        SetPanelInteractable(false);

        if (open) {
            _scaleTween = Tween.Scale(inventoryPanel, endValue: Vector3.one, duration: popupInDuration, ease: Ease.OutBack)
                .OnComplete(() => {
                    SetPanelInteractable(true);
                    SetBackpackIconAlpha(true);
                });
            if (inventoryPanelCanvasGroup != null)
                _fadeTween = Tween.Alpha(inventoryPanelCanvasGroup, endValue: 1f, duration: popupInDuration * 0.6f);
        } else {
            _scaleTween = Tween.Scale(inventoryPanel, endValue: Vector3.zero, duration: popupOutDuration, ease: Ease.InBack)
                .OnComplete(() => SetBackpackIconAlpha(false));
            if (inventoryPanelCanvasGroup != null)
                _fadeTween = Tween.Alpha(inventoryPanelCanvasGroup, endValue: 0f, duration: popupOutDuration * 0.8f);
        }
    }

    private void SetPanelInteractable(bool interactable) {
        if (inventoryPanelCanvasGroup == null) return;
        inventoryPanelCanvasGroup.interactable = interactable;
        inventoryPanelCanvasGroup.blocksRaycasts = interactable;
    }

    private void SetBackpackIconAlpha(bool open) {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (backpackIcon == null) return;
        var c = backpackIcon.color;
        backpackIcon.color = new Color(c.r, c.g, c.b, open ? 0.50f : 0.25f);
#endif
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

        // Skipped during the very first RefreshAll() so joining doesn't
        // flash names for every already-filled slot; see _hasInitialized.
        if (!_hasInitialized) return;

        bool isHotbar = index < _inventory.HotbarSize;
        if (isHotbar) {
            // Hotbar: only the currently-active slot pops — e.g. picking an
            // item up directly into your held slot, or its stack count
            // changing while held.
            if (index == _inventory.ActiveHotbarIndex)
                UISlots[index].PlayActiveNamePopup(item?.displayName);
        } else {
            // Main inventory: any slot pops whenever its contents change
            // (item added/removed/stack count changed), regardless of
            // whether the panel is currently open — it'll just be sitting
            // there ready when the player next opens it.
            UISlots[index].PlayActiveNamePopup(item?.displayName);
        }
    }

    private void RefreshActiveHighlight(int activeIndex) {
        for (int i = 0; i < _inventory.HotbarSize; i++) {
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

        // The pointer may end the drag sitting directly over a slot without
        // ever firing OnPointerExit on it (e.g. release without moving off),
        // so clear whatever's currently highlighted here too, not just via
        // NotifySlotHovered's own exit path.
        if (_hoveredDropTarget != null) {
            _hoveredDropTarget.SetDropTargetHighlight(false);
            _hoveredDropTarget = null;
        }
    }

    public void OnDrop(int targetIndex) {
        if (_dragSourceIndex < 0 || _dragSourceIndex == targetIndex) return;
        _inventory.MoveSlotServerRpc(_dragSourceIndex, targetIndex);
        _dragSourceIndex = -1;
    }

    // Called by InventorySlotUI on pointer enter/exit. Only shows the
    // "drop here" highlight while an actual drag is in progress, and never
    // on the slot being dragged FROM (dropping there is a no-op).
    public void NotifySlotHovered(InventorySlotUI slot, bool entered) {
        if (_dragSourceIndex < 0) return;

        if (entered) {
            if (slot.SlotIndex == _dragSourceIndex) return;
            if (_hoveredDropTarget != null && _hoveredDropTarget != slot)
                _hoveredDropTarget.SetDropTargetHighlight(false);
            _hoveredDropTarget = slot;
            slot.SetDropTargetHighlight(true);
        } else if (_hoveredDropTarget == slot) {
            slot.SetDropTargetHighlight(false);
            _hoveredDropTarget = null;
        }
    }

    // ── Touch/Click Slot Selection (called by InventorySlotUI) ─────────────────

    // Tapping a hotbar slot selects it — same effect as HotbarInput's number
    // keys, just routed through UI instead of Keyboard. If it's already the
    // active slot, no RPC is sent (nothing would change server-side), but we
    // still re-trigger the name popup locally using the slot's real current
    // data, so tapping the held slot lets you "peek" at the name again even
    // after the previous popup already faded out.
    public void OnHotbarSlotTapped(int slotIndex) {
        if (slotIndex == _inventory.ActiveHotbarIndex) {
            if (slotIndex < 0 || slotIndex >= _inventory.SlotCount) return;
            var slot = _inventory.GetSlot(slotIndex);
            var item = slot.IsEmpty ? null : Registry.Get(slot.ItemID.ToString());
            UISlots[slotIndex].PlayActiveNamePopup(item?.displayName);
            return;
        }
        _inventory.SetActiveSlotServerRpc(slotIndex);
    }
}