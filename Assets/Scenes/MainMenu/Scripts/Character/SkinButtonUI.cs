using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkinButtonUI : MonoBehaviour {
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button button;
    [SerializeField] private Image selectedOutline; // now an Image so we can drive alpha
    [SerializeField] private Button selectButton;
    [SerializeField] private TMP_Text selectButtonLabel;
    [SerializeField] private GameObject lockedOverlay; // grayed-out / lock icon

    [Header("Outline Alpha")]
    [SerializeField] private float previewAlpha = 0.1f;
    [SerializeField] private float equippedAlpha = 0.3f;

    public CharacterSkinSO Skin => skin;

    private CharacterSkinSO skin;
    private Action<CharacterSkinSO> onPreview;
    private Action<CharacterSkinSO> onSelect;
    private Action<CharacterSkinSO> onPurchase;

    private bool isOwned;
    private bool isPreviewed;
    private bool isEquipped;
    private SkinAvailabilityStatus availability = SkinAvailabilityStatus.Available;

    public void Init(CharacterSkinSO skin, bool owned, SkinAvailabilityStatus availability, Action<CharacterSkinSO> onPreview, Action<CharacterSkinSO> onSelect, Action<CharacterSkinSO> onPurchase) {
        this.skin = skin;
        this.onPreview = onPreview;
        this.onSelect = onSelect;
        this.onPurchase = onPurchase;
        this.availability = availability;

        icon.sprite = skin.previewIcon;
        label.text = skin.displayName;

        SetOwned(owned);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => this.onPreview(this.skin)); // preview is always allowed, even locked/expired/loading

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(HandleSelectButtonClicked);

        SetOutlineAlpha(0f);
    }

    // Called by SkinSelectUI once the Firestore availability sync completes.
    public void SetAvailability(SkinAvailabilityStatus availability) {
        this.availability = availability;
        UpdateSelectButtonState();
    }

    private void HandleSelectButtonClicked() {
        if (isOwned) {
            onSelect(skin);
        } else if (availability == SkinAvailabilityStatus.Available) {
            onPurchase(skin); // locked skin, still purchasable — open the purchase popup
        }
        // else: still loading or expired and not owned — button is non-interactable, this shouldn't fire
    }

    public void SetOwned(bool owned) {
        isOwned = owned;

        if (lockedOverlay != null) lockedOverlay.SetActive(!owned);
        UpdateSelectButtonState();
    }

    // Called by SkinSelectUI when this skin is the one currently being previewed (dim outline).
    public void SetPreviewed(bool previewed) {
        isPreviewed = previewed;
        RefreshOutline();
    }

    // Called by SkinSelectUI when this skin is the equipped/saved skin (full outline).
    public void SetEquipped(bool equipped) {
        isEquipped = equipped;
        UpdateSelectButtonState();
        RefreshOutline();
    }

    private void UpdateSelectButtonState() {
        // Interactable when: equipping/previewing an owned skin, or purchasing an
        // unowned-but-currently-available one. Disabled when already equipped, still
        // loading its availability, or unowned with a closed/not-yet-open window.
        bool purchasableNow = availability == SkinAvailabilityStatus.Available;
        bool locked = !isOwned && !purchasableNow;
        Debug.Log($"LOCKED {isOwned} {purchasableNow} {skin.name}");
        selectButton.interactable = !(isOwned && isEquipped) && !locked;
        selectButtonLabel.text = GetSelectButtonLabel();
    }

    private string GetSelectButtonLabel() {
        if (isOwned)
            return isEquipped ? "Selected" : "Select";

        if (availability == SkinAvailabilityStatus.Loading)
            return "...";

        if (availability == SkinAvailabilityStatus.Expired)
            return "Time Limited";

        return skin.paywallType switch {
            SkinPaywallType.Currency => skin.EffectiveCurrencyCost.ToString(),
            SkinPaywallType.IAP => string.IsNullOrEmpty(skin.iapProductId) ? "Buy" : "Buy", // actual store price string is filled in by the popup/store lookup, not known here
            _ => "Locked"
        };
    }

    private void RefreshOutline() {
        float alpha = isEquipped ? equippedAlpha : (isPreviewed ? previewAlpha : 0f);
        SetOutlineAlpha(alpha);
    }

    private void SetOutlineAlpha(float a) {
        if (selectedOutline == null) return;
        var c = selectedOutline.color;
        c.a = a;
        selectedOutline.color = c;
        selectedOutline.gameObject.SetActive(a > 0f);
    }
}