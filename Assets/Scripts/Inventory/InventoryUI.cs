using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Client-side inventory UI. Wire this to the local player's PlayerInventory
// by calling Init() from PlayerSetup after network spawn.

// Scene setup:
//   - Create a Canvas with an "InventoryPanel" — its GameObject stays ACTIVE
//     at all times now (the slide toggle uses position, not SetActive), just
//     position it in the scene at its OPEN/shown position — Init() captures
//     that as the shown position and computes the hidden position by
//     shifting right by the panel's own width.
//   - Inside it: (hotbarSize + mainInventorySize) InventorySlotUI children —
//     indices 0..hotbarSize-1 = hotbar, the rest = main inventory
//   - Assign them to UISlots in order
//   - The hotbar itself should be a SEPARATE always-visible bar outside
//     InventoryPanel — only the main inventory portion slides
public class InventoryUI : MonoBehaviour {
    [Tooltip("Leave empty to auto-resolve the current scene's registry via " +
             "ItemRegistry.Instance (set by that scene's GameBootstrap).")]
    [SerializeField] private ItemRegistry itemRegistry;
    private ItemRegistry Registry => itemRegistry != null ? itemRegistry : ItemRegistry.Instance;
    [SerializeField] private InventorySlotUI[] UISlots; // (hotbarSize + mainInventorySize) elements, set in Inspector

    [Header("Main Inventory Slide Panel")]
    [Tooltip("The main inventory panel's RectTransform (NOT the hotbar — the hotbar stays " +
             "always visible). Position it in the scene at its OPEN position; Init() reads " +
             "that as the shown position and computes the hidden position from it.")]
    [SerializeField] private RectTransform inventoryPanel;
    [Tooltip("Optional but recommended: prevents clicking/dragging through the panel while " +
             "it's slid off-screen, and blocks interaction during the slide itself.")]
    [SerializeField] private CanvasGroup inventoryPanelCanvasGroup;
    [SerializeField] private float slideDuration = 0.25f;
    [SerializeField] private AnimationCurve slideEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

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
    private bool _hasInitialized;

    private Vector2 _shownPos;
    private Vector2 _hiddenPos;
    private Coroutine _slideCoroutine;

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
            UISlots[i].Init(i, i < _inventory.HotbarSize, this);

        if (dragGhost != null) {
            dragGhost.raycastTarget = false; // belt-and-suspenders alongside the Inspector setting
            dragGhost.enabled = false;
        }

        // Capture the panel's designed position as "shown", then compute
        // "hidden" by shifting it fully off-screen to the right by its own
        // width — this is what makes it slide in from off-screen right ->
        // into view (right-to-left) when opened.
        _shownPos = inventoryPanel.anchoredPosition;
        _hiddenPos = _shownPos + new Vector2(inventoryPanel.rect.width, 0f);
        inventoryPanel.anchoredPosition = _hiddenPos;
        SetPanelInteractable(false);

        RefreshAll();
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void SetOpen(bool open) {
        _isOpen = open;
        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(SlidePanel(open));
    }

    private System.Collections.IEnumerator SlidePanel(bool open) {
        // Block interaction immediately when CLOSING so items can't be
        // dragged mid-slide; only allow interaction once fully OPEN (below).
        if (!open) SetPanelInteractable(false);

        Vector2 start = inventoryPanel.anchoredPosition;
        Vector2 end = open ? _shownPos : _hiddenPos;

        float t = 0f;
        while (t < slideDuration) {
            t += Time.deltaTime;
            float eased = slideEase.Evaluate(Mathf.Clamp01(t / slideDuration));
            inventoryPanel.anchoredPosition = Vector2.LerpUnclamped(start, end, eased);
            yield return null;
        }
        inventoryPanel.anchoredPosition = end;

        if (open) SetPanelInteractable(true);
#if UNITY_EDITOR || UNITY_STANDALONE
        var c = backpackIcon.color;
        if (open) {
            backpackIcon.color = new Color(c.r, c.g, c.b, 0.50f);
        } else {
            backpackIcon.color = new Color(c.r, c.g, c.b, 0.25f);
        }
#endif
        _slideCoroutine = null;
    }

    private void SetPanelInteractable(bool interactable) {
        if (inventoryPanelCanvasGroup == null) return;
        inventoryPanelCanvasGroup.interactable = interactable;
        inventoryPanelCanvasGroup.blocksRaycasts = interactable;
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
        if (_hasInitialized && index < _inventory.HotbarSize
            && index == _inventory.ActiveHotbarIndex) {
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
    }

    public void OnDrop(int targetIndex) {
        if (_dragSourceIndex < 0 || _dragSourceIndex == targetIndex) return;
        _inventory.MoveSlotServerRpc(_dragSourceIndex, targetIndex);
        _dragSourceIndex = -1;
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