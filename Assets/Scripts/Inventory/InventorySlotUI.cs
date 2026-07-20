using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using PrimeTween;

// Visual representation of one inventory slot.
// Supports drag-and-drop between slots.
public class InventorySlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerClickHandler {
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image background;

    [SerializeField] private Color hotbarBgColor = new Color(0.75f, 0.75f, 0.75f, 0.9f);
    [SerializeField] private Color inventoryBgColor = new Color(0.55f, 0.55f, 0.55f, 0.9f);

    [Header("Hotbar Active/Inactive Opacity")]
    [Tooltip("Only hotbar slots ever show an active/inactive state — main inventory " +
             "slots (isHotbar == false) always stay at full opacity regardless of these values.")]
    [SerializeField] private float activeOpacity = 0.5f;
    [SerializeField] private float inactiveOpacity = 0.25f;

    [Header("Item Name Popup")]
    [Tooltip("Positioned above the icon in the prefab. Wire this up on BOTH hotbar and " +
             "main inventory slot prefabs — main inventory slots now show the popup too " +
             "whenever their contents change, not just the active hotbar slot.")]
    [SerializeField] private TMP_Text itemNameLabel;
    [SerializeField] private float namePunchDuration = 0.15f;   // quick scale+fade in
    [SerializeField] private float namePunchStartScale = 0.7f;  // starting scale before the OutBack pop
    [SerializeField] private float nameHoldDuration = 0.4f;     // full-alpha hold before fading
    [SerializeField] private float nameFadeDuration = 1.6f;     // fade-out length (in + hold + fade ≈ 2.15s total)

    [Header("Drag Feedback")]
    [Tooltip("Icon alpha on the slot you're dragging FROM, while the drag is in progress.")]
    [SerializeField] private float draggingIconOpacity = 0.35f;

    public int SlotIndex { get; private set; }

    private bool _isHotbar;
    private bool _isCurrentlyActive; // only meaningful when _isHotbar is true
    private InventoryUI _ui;
    private Sequence _namePopupSequence;
    private string _currentItemName; // authoritative source for "what's actually in this slot right now"

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Init(int index, bool isHotbar, InventoryUI ui) {
        SlotIndex = index;
        _isHotbar = isHotbar;
        _ui = ui;

        background.color = isHotbar ? hotbarBgColor : inventoryBgColor;
        ClearDisplay();
        HideNamePopupImmediate();
        SetHighlight(false); // applies correct base opacity (25% hotbar / 100% main) via ApplyBaseOpacity
    }

    // ── Display ───────────────────────────────────────────────────────────────

    public void UpdateDisplay(InventoryItem item, int quantity) {
        bool hasItem = item != null && quantity > 0;

        iconImage.enabled = hasItem;
        quantityText.enabled = hasItem && quantity > 1;
        _currentItemName = hasItem ? item.displayName : null;

        if (hasItem) {
            iconImage.sprite = item.icon;
            quantityText.text = quantity.ToString();
        }
    }

    private void ClearDisplay() {
        iconImage.enabled = false;
        quantityText.enabled = false;
        _currentItemName = null;
    }

    // Only hotbar slots have an "active" concept at all — main inventory
    // slots always sit at full opacity regardless of what's passed in here.
    public void SetHighlight(bool active) {
        _isCurrentlyActive = active;
        ApplyBaseOpacity();
    }

    // The opacity a hotbar slot's icon should sit at when NOT being dragged —
    // used both by SetHighlight and to restore the correct value after a
    // drag ends (rather than hardcoding a flat 1f, which would be wrong for
    // an inactive hotbar slot that should settle back at 25%, not 100%).
    // Main inventory slots always resolve to 1f here (their icon just isn't
    // touched by the active/inactive system at all).
    private float BaseOpacity => !_isHotbar ? 1f : (_isCurrentlyActive ? activeOpacity : inactiveOpacity);

    // Applies the active/inactive dimming to a hotbar slot's bg+icon.
    // No-op for main inventory slots — they're never touched by this at all,
    // so their designed background alpha (inventoryBgColor) is left exactly
    // as configured rather than being forced to 1f.
    private void ApplyBaseOpacity() {
        if (!_isHotbar) return;
        float alpha = BaseOpacity;
        SetIconOpacity(alpha);
        SetBackgroundOpacity(alpha);
    }

    private void SetBackgroundOpacity(float alpha) {
        if (background == null) return;
        var c = background.color;
        background.color = new Color(c.r, c.g, c.b, alpha);
    }

    // ── Item Name Popup ──────────────────────────────────────────────────────
    // Called by InventoryUI whenever this slot's contents are worth calling
    // out — the active hotbar slot changing/refreshing, or (now) ANY main
    // inventory slot's contents changing. Pops the label in with a quick
    // scale+fade (OutBack), holds briefly at full alpha, then fades out.
    // Switching away from a hotbar slot calls HideNamePopupImmediate on it
    // instead, so only one hotbar name is ever visible/fading at a time —
    // main inventory slots each animate independently since several can
    // change at once (e.g. a stack split across slots).

    public void PlayActiveNamePopup(string itemDisplayName) {
        if (itemNameLabel == null) return;

        if (_namePopupSequence.isAlive) _namePopupSequence.Stop();

        if (string.IsNullOrEmpty(itemDisplayName)) {
            HideNamePopupImmediate();
            return;
        }

        itemNameLabel.text = itemDisplayName;
        itemNameLabel.enabled = true;
        itemNameLabel.rectTransform.localScale = Vector3.one * namePunchStartScale;
        var c = itemNameLabel.color;
        itemNameLabel.color = new Color(c.r, c.g, c.b, 0f);

        if (!gameObject.activeInHierarchy) return;

        _namePopupSequence = Sequence.Create()
            .Group(Tween.Alpha(itemNameLabel, endValue: 1f, duration: namePunchDuration))
            .Group(Tween.Scale(itemNameLabel.rectTransform, endValue: Vector3.one, duration: namePunchDuration, ease: Ease.OutBack))
            .ChainDelay(nameHoldDuration)
            .Chain(Tween.Alpha(itemNameLabel, endValue: 0f, duration: nameFadeDuration))
            .OnComplete(() => itemNameLabel.enabled = false);
    }

    public void HideNamePopupImmediate() {
        if (_namePopupSequence.isAlive) _namePopupSequence.Stop();
        if (itemNameLabel != null) itemNameLabel.enabled = false;
    }

    private void OnDisable() {
        // Panel toggled off (e.g. Tab menu) or object pooled — drop any
        // in-flight tween so it doesn't try to run against a disabled object.
        if (_namePopupSequence.isAlive) _namePopupSequence.Stop();
        // Also guards against a slot staying dimmed forever if it's disabled
        // mid-drag (e.g. main inventory panel closed while dragging).
        SetIconOpacity(BaseOpacity);
    }

    // ── Drag & Drop ───────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData) {
        if (!iconImage.enabled) {
            // No item here — explicitly cancel rather than silently letting
            // InventoryUI's _dragSourceIndex carry over a stale value from a
            // previous drag. UpdateDragVisual/EndDragVisual still get called
            // every frame after this (interfaces require the methods exist),
            // but they no-op safely since the ghost was never enabled below.
            _ui.CancelDrag();
            return;
        }
        SetIconOpacity(draggingIconOpacity);

        // Show the name once at drag-start. PlayActiveNamePopup no-ops safely
        // if itemNameLabel isn't assigned, so this is safe to call
        // unconditionally regardless of slot type.
        PlayActiveNamePopup(_currentItemName);

        _ui.OnBeginDrag(SlotIndex, iconImage.sprite);
    }

    // Required for uGUI to populate pointerEventData.pointerDrag at all (see
    // previous explanation) — also moves the ghost icon to follow the pointer
    // every frame while dragging. Deliberately does NOT re-trigger the name
    // popup here — that already happened once in OnBeginDrag above; calling
    // it again every frame would restart the tween 60+ times/sec.
    public void OnDrag(PointerEventData eventData)
        => _ui.UpdateDragVisual(eventData.position);

    public void OnEndDrag(PointerEventData eventData) {
        // Fires on THIS slot (the source) regardless of whether the drop
        // landed on a valid target. Restores to BaseOpacity rather than a
        // flat 1f, since an inactive hotbar slot should settle back at 25%,
        // not full opacity.
        SetIconOpacity(BaseOpacity);
        _ui.EndDragVisual();
    }

    private void SetIconOpacity(float alpha) {
        var c = iconImage.color;
        iconImage.color = new Color(c.r, c.g, c.b, alpha);
    }

    public void OnDrop(PointerEventData eventData) {
        // The slot that was dragged from
        var source = eventData.pointerDrag?.GetComponent<InventorySlotUI>();
        if (source != null && source != this)
            _ui.OnDrop(SlotIndex);
    }

    public void OnPointerEnter(PointerEventData eventData) {
        // TODO: show item tooltip
    }

    // Tap/click a hotbar slot to make it active — this is the touch
    // equivalent of pressing 1–9 on desktop. Fires alongside the drag
    // handlers with no conflict: uGUI only raises OnPointerClick if the
    // pointer didn't move past the drag threshold, so a tap selects and
    // a drag still reorders as before. Main inventory slots ignore
    // taps here since only the hotbar has an "active" concept.
    public void OnPointerClick(PointerEventData eventData) {
        if (!_isHotbar) {
            // Main inventory slots have no "active" concept to route through
            // InventoryUI — just pop the name directly off this slot's own
            // current contents. No-ops safely on an empty slot.
            PlayActiveNamePopup(_currentItemName);
            return;
        }
        _ui.OnHotbarSlotTapped(SlotIndex);
    }
}