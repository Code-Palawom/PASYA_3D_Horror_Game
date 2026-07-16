using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

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

    [Header("Active Item Name Popup (hotbar slots only)")]
    [Tooltip("Positioned above the icon in the prefab. Only wired up on hotbar " +
             "slots — leave empty on main inventory slot prefabs.")]
    [SerializeField] private TMP_Text itemNameLabel;
    [SerializeField] private float nameHoldDuration = 0.4f; // full-alpha hold before fading
    [SerializeField] private float nameFadeDuration = 1.6f; // fade-out length (hold + fade ≈ 2s total)

    [Header("Drag Feedback")]
    [Tooltip("Icon alpha on the slot you're dragging FROM, while the drag is in progress.")]
    [SerializeField] private float draggingIconOpacity = 0.35f;

    public int SlotIndex { get; private set; }

    private bool _isHotbar;
    private bool _isCurrentlyActive; // only meaningful when _isHotbar is true
    private InventoryUI _ui;
    private Coroutine _namePopupCoroutine;
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

    // ── Active Item Name Popup ───────────────────────────────────────────────
    // Called by InventoryUI whenever this slot becomes the active hotbar slot
    // (or its contents change while already active). Shows the item's name
    // at full alpha, holds briefly, then fades out over ~2s total. Switching
    // to a DIFFERENT slot calls HideNamePopupImmediate on this one instead,
    // so only one name is ever visible/fading at a time.

    public void PlayActiveNamePopup(string itemDisplayName) {
        if (itemNameLabel == null) return;

        if (_namePopupCoroutine != null) {
            StopCoroutine(_namePopupCoroutine);
            _namePopupCoroutine = null;
        }

        if (string.IsNullOrEmpty(itemDisplayName)) {
            HideNamePopupImmediate();
            return;
        }

        itemNameLabel.text = itemDisplayName;
        var c = itemNameLabel.color;
        itemNameLabel.color = new Color(c.r, c.g, c.b, 1f);
        itemNameLabel.enabled = true;

        if (gameObject.activeInHierarchy)
            _namePopupCoroutine = StartCoroutine(FadeNameRoutine());
    }

    public void HideNamePopupImmediate() {
        if (_namePopupCoroutine != null) {
            StopCoroutine(_namePopupCoroutine);
            _namePopupCoroutine = null;
        }
        if (itemNameLabel != null) itemNameLabel.enabled = false;
    }

    private System.Collections.IEnumerator FadeNameRoutine() {
        yield return new WaitForSeconds(nameHoldDuration);

        float t = 0f;
        Color start = itemNameLabel.color;
        while (t < nameFadeDuration) {
            t += Time.deltaTime;
            itemNameLabel.color = new Color(start.r, start.g, start.b,
                Mathf.Lerp(1f, 0f, t / nameFadeDuration));
            yield return null;
        }

        itemNameLabel.enabled = false;
        _namePopupCoroutine = null;
    }

    private void OnDisable() {
        // Panel toggled off (e.g. Tab menu) or object pooled — drop any
        // in-flight coroutine so it doesn't try to run against a disabled object.
        if (_namePopupCoroutine != null) {
            StopCoroutine(_namePopupCoroutine);
            _namePopupCoroutine = null;
        }
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
        // if itemNameLabel isn't assigned (main inventory slots), so this is
        // safe to call unconditionally regardless of slot type.
        PlayActiveNamePopup(_currentItemName);

        _ui.OnBeginDrag(SlotIndex, iconImage.sprite);
    }

    // Required for uGUI to populate pointerEventData.pointerDrag at all (see
    // previous explanation) — also moves the ghost icon to follow the pointer
    // every frame while dragging. Deliberately does NOT re-trigger the name
    // popup here — that already happened once in OnBeginDrag above; calling
    // it again every frame would restart the fade coroutine 60+ times/sec
    // and would throw on main-inventory slots where itemNameLabel is null
    // if read directly instead of through PlayActiveNamePopup's own guard.
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
        if (!_isHotbar) return;
        _ui.OnHotbarSlotTapped(SlotIndex);
    }
}