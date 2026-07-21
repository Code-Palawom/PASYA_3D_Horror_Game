using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrimeTween;

// Popup asking "how many of this stack do you want to drop?". Shown by
// InventoryUI when a stack of more than 1 is dragged out of the
// inventory/hotbar and released in open space.
//
// Scene setup:
//   - A small panel (Image + CanvasGroup) containing an item icon, a name
//     label, a Slider (or +/- buttons — wire whichever you use to
//     SetQuantity), a quantity label, and Confirm/Cancel buttons.
//   - Wire slider.onValueChanged -> SetQuantity, confirmButton.onClick ->
//     Confirm, cancelButton.onClick -> Cancel in the Inspector, OR leave
//     them unwired and call SetQuantity/Confirm/Cancel from your own script.
//   - Leave the GameObject active with the panel's CanvasGroup alpha at 0
//     (Awake sets this up for you) so it can pop in/out without a
//     SetActive flicker, matching InventoryUI's popup panel.
//   - Assign this component to InventoryUI's dropQuantityPrompt field.
public class DropQuantityPrompt : MonoBehaviour {
    [Header("Panel")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float popupInDuration = 0.2f;
    [SerializeField] private float popupOutDuration = 0.15f;

    [Header("Content")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI itemNameLabel;
    [SerializeField] private TextMeshProUGUI quantityLabel;
    [Tooltip("Range is set to [1, maxQuantity] each time Show() is called.")]
    [SerializeField] private Slider quantitySlider;

    [Header("Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action<int> _onConfirm;
    private int _quantity = 1;
    private Tween _scaleTween;
    private Tween _fadeTween;

    void Awake() {
        if (panel != null) panel.localScale = Vector3.zero;
        if (canvasGroup != null) {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (quantitySlider != null) quantitySlider.onValueChanged.AddListener(v => SetQuantity(Mathf.RoundToInt(v)));
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
    }

    void OnDestroy() {
        if (_scaleTween.isAlive) _scaleTween.Stop();
        if (_fadeTween.isAlive) _fadeTween.Stop();
    }

    // Opens the prompt for `item`, defaulting to the full stack selected.
    // onConfirm is invoked with the chosen quantity (1..maxQuantity) once
    // the player confirms; nothing is invoked on cancel.
    public void Show(InventoryItem item, int maxQuantity, Action<int> onConfirm) {
        _onConfirm = onConfirm;

        if (iconImage != null) {
            iconImage.enabled = item != null && item.icon != null;
            iconImage.sprite = item != null ? item.icon : null;
        }
        if (itemNameLabel != null) itemNameLabel.text = item != null ? item.displayName : string.Empty;

        if (quantitySlider != null) {
            quantitySlider.minValue = 1;
            quantitySlider.maxValue = Mathf.Max(1, maxQuantity);
            quantitySlider.value = quantitySlider.maxValue; // default to dropping the whole stack
        }
        SetQuantity(Mathf.Max(1, maxQuantity));

        Open();
    }

    public void SetQuantity(int qty) {
        _quantity = Mathf.Max(1, qty);
        if (quantityLabel != null) quantityLabel.text = _quantity.ToString();
    }

    public void Confirm() {
        var callback = _onConfirm;
        int qty = _quantity;
        Close();
        callback?.Invoke(qty);
    }

    public void Cancel() => Close();

    private void Open() {
        if (panel == null) return;
        if (_scaleTween.isAlive) _scaleTween.Stop();
        if (_fadeTween.isAlive) _fadeTween.Stop();

        SetInteractable(false);
        _scaleTween = Tween.Scale(panel, endValue: Vector3.one, duration: popupInDuration, ease: Ease.OutBack)
            .OnComplete(() => SetInteractable(true));
        if (canvasGroup != null)
            _fadeTween = Tween.Alpha(canvasGroup, endValue: 1f, duration: popupInDuration * 0.6f);
    }

    private void Close() {
        _onConfirm = null;
        if (panel == null) return;
        if (_scaleTween.isAlive) _scaleTween.Stop();
        if (_fadeTween.isAlive) _fadeTween.Stop();

        SetInteractable(false);
        _scaleTween = Tween.Scale(panel, endValue: Vector3.zero, duration: popupOutDuration, ease: Ease.InBack);
        if (canvasGroup != null)
            _fadeTween = Tween.Alpha(canvasGroup, endValue: 0f, duration: popupOutDuration * 0.8f);
    }

    private void SetInteractable(bool interactable) {
        if (canvasGroup == null) return;
        canvasGroup.interactable = interactable;
        canvasGroup.blocksRaycasts = interactable;
    }
}