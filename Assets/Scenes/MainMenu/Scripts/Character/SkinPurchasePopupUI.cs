using PrimeTween;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Confirmation popup shown when the player taps a locked skin.
// Branches into currency spend or IAP purchase depending on the skin's paywallType.
public class SkinPurchasePopupUI : MonoBehaviour {
    [SerializeField] private GameObject root; // the popup panel to show/hide
    [SerializeField] private CanvasGroup canvasGroup; // put on the same object as `root`
    [SerializeField] private RectTransform panelRect;  // the actual popup panel that scales in/out
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text priceLabel;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Animation")]
    [SerializeField] private float animDuration = 0.2f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InQuad;

    private CharacterSkinSO pendingSkin;
    private Action onPurchaseSuccess;
    private Sequence animSequence;

    private void Awake() {
        cancelButton.onClick.AddListener(Hide);
        confirmButton.onClick.AddListener(() => _ = HandleConfirmAsync());
        if (root != null) root.SetActive(false);
    }

    public void Show(CharacterSkinSO skin, Action onSuccess) {
        pendingSkin = skin;
        onPurchaseSuccess = onSuccess;

        icon.sprite = skin.previewIcon;
        titleLabel.text = skin.displayName;
        priceLabel.text = GetPriceText(skin);

        animSequence.Stop();
        if (root != null) root.SetActive(true);

        canvasGroup.alpha = 0f;
        panelRect.localScale = Vector3.one * 0.85f;

        animSequence = Sequence.Create()
            .Group(Tween.Alpha(canvasGroup, endValue: 1f, duration: animDuration, ease: Ease.OutQuad))
            .Group(Tween.Scale(panelRect, endValue: Vector3.one, duration: animDuration, ease: showEase));
    }

    public void Hide() {
        confirmButton.interactable = false; // prevent a tap during the exit animation window
        animSequence.Stop();

        animSequence = Sequence.Create()
            .Group(Tween.Alpha(canvasGroup, endValue: 0f, duration: animDuration, ease: hideEase))
            .Group(Tween.Scale(panelRect, endValue: Vector3.one * 0.85f, duration: animDuration, ease: hideEase))
            .ChainCallback(() => {
                if (root != null) root.SetActive(false);
                pendingSkin = null;
                onPurchaseSuccess = null;
                confirmButton.interactable = true; // reset for the next Show()
            });
    }

    private string GetPriceText(CharacterSkinSO skin) => skin.paywallType switch {
        SkinPaywallType.Currency => $"{skin.currencyCost} Coins",
        SkinPaywallType.IAP => PurchaseManager.GetIAPPriceString(skin.iapProductId),
        _ => string.Empty
    };

    private async System.Threading.Tasks.Task HandleConfirmAsync() {
        if (pendingSkin == null) return;

        confirmButton.interactable = false;

        SkinPurchaseResult result = pendingSkin.paywallType switch {
            SkinPaywallType.Currency => await PurchaseManager.PurchaseWithCurrency(pendingSkin),
            SkinPaywallType.IAP => await PurchaseManager.PurchaseWithIAP(pendingSkin),
            _ => SkinPurchaseResult.Error
        };

        confirmButton.interactable = true;

        if (result == SkinPurchaseResult.Success) {
            onPurchaseSuccess?.Invoke();
            ActionbarToastNotification.Instance.ShowLocalToast($"You purchased {pendingSkin.displayName}!", ToastType.Success);
            Hide();
        } else if (result == SkinPurchaseResult.OfferExpired) {
            ActionbarToastNotification.Instance.ShowLocalToast("This offer has ended.", ToastType.Error);
            priceLabel.text = "Expired";
        } else {
            ActionbarToastNotification.Instance.ShowLocalToast("Skin purchase failed", ToastType.Error);
            Debug.LogWarning($"Skin purchase failed: {result}");
        }
    }
}