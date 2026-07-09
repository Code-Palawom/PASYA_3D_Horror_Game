using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Visual representation of one inventory slot.
// Supports drag-and-drop between slots.
public class InventorySlotUI : MonoBehaviour,
    IBeginDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler {
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image highlightBorder; // Active slot yellow border
    [SerializeField] private Image background;

    [SerializeField] private Color hotbarBgColor = new Color(0.75f, 0.75f, 0.75f, 0.9f);
    [SerializeField] private Color inventoryBgColor = new Color(0.55f, 0.55f, 0.55f, 0.9f);
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.1f, 1f);

    public int SlotIndex { get; private set; }

    private bool _isHotbar;
    private InventoryUI _ui;

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Init(int index, bool isHotbar, InventoryUI ui) {
        SlotIndex = index;
        _isHotbar = isHotbar;
        _ui = ui;

        background.color = isHotbar ? hotbarBgColor : inventoryBgColor;
        SetHighlight(false);
        ClearDisplay();
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
}