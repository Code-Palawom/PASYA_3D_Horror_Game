using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Visual representation of one inventory slot.
// Supports drag-and-drop between slots.
public class InventorySlotUI : MonoBehaviour,
    IBeginDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerClickHandler {
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image highlightBorder; // Active slot yellow border
    [SerializeField] private Image background;

    [SerializeField] private Color hotbarBgColor = new Color(0.75f, 0.75f, 0.75f, 0.9f);
    [SerializeField] private Color inventoryBgColor = new Color(0.55f, 0.55f, 0.55f, 0.9f);
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.1f, 1f);

    [Header("Active Item Name Popup (hotbar slots only)")]
    [Tooltip("Positioned above the icon in the prefab. Only wired up on hotbar " +
             "slots — leave empty on main inventory slot prefabs.")]
    [SerializeField] private TMP_Text itemNameLabel;
    [SerializeField] private float nameHoldDuration = 0.4f; // full-alpha hold before fading
    [SerializeField] private float nameFadeDuration = 1.6f; // fade-out length (hold + fade ≈ 2s total)

    public int SlotIndex { get; private set; }

    private bool _isHotbar;
    private InventoryUI _ui;
    private Coroutine _namePopupCoroutine;

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Init(int index, bool isHotbar, InventoryUI ui) {
        SlotIndex = index;
        _isHotbar = isHotbar;
        _ui = ui;

        background.color = isHotbar ? hotbarBgColor : inventoryBgColor;
        SetHighlight(false);
        ClearDisplay();
        HideNamePopupImmediate();
    }

    // ── Display ───────────────────────────────────────────────────────────────

    public void UpdateDisplay(InventoryItem item, int quantity) {
        bool hasItem = item != null && quantity > 0;

        iconImage.enabled = hasItem;
        quantityText.enabled = hasItem && quantity > 1;

        if (hasItem) {
            iconImage.sprite = item.icon;
            quantityText.text = quantity.ToString();
        }
    }

    private void ClearDisplay() {
        iconImage.enabled = false;
        quantityText.enabled = false;
    }

    public void SetHighlight(bool active) {
        if (highlightBorder == null) return;
        highlightBorder.enabled = active;
        highlightBorder.color = highlightColor;
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
    }

    // ── Drag & Drop ───────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
        => _ui.OnBeginDrag(SlotIndex);

    public void OnEndDrag(PointerEventData eventData) { /* handled by target OnDrop */ }

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
    // a drag still reorders as before. Main inventory slots (9–35) ignore
    // taps here since only the hotbar has an "active" concept.
    public void OnPointerClick(PointerEventData eventData) {
        if (!_isHotbar) return;
        _ui.OnHotbarSlotTapped(SlotIndex);
    }
}