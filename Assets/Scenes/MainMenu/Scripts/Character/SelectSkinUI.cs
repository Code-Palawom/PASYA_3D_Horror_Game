using PrimeTween;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectSkinUI : MonoBehaviour {
    [SerializeField] private SkinDatabaseSO database;
    [SerializeField] private CharacterAppearanceController appearance;
    [SerializeField] private Transform contentParent;
    [SerializeField] private SkinButtonUI buttonPrefab;
    [SerializeField] private TMP_Text characterLabel;
    [SerializeField] private MainMenuUI mainMenuUI;
    [SerializeField] private SkinPurchasePopupUI purchasePopup;

    [Header("Label Animation")]
    [SerializeField] private float labelFadeDuration = 0.15f;
    [SerializeField] private Ease labelFadeEase = Ease.OutQuad;
    [SerializeField] private float labelMoveDistance = 15f; // pixels
    [SerializeField] private float labelEnableDelay = 1f; // delay before label fades in on panel open

    private readonly List<SkinButtonUI> spawnedButtons = new();
    private CharacterSkinSO previewSkin;
    private CharacterSkinSO selectedSkin;
    private Sequence labelSequence;
    private RectTransform labelRect;
    private Vector2 labelRestPos;
    private CancellationTokenSource syncCts;

    private PlayerProfile Profile => AuthManager.Instance.CurrentProfile;

    private void Awake() {
        labelRect = characterLabel.rectTransform;
        labelRestPos = labelRect.anchoredPosition;
    }

    private void OnEnable() {
        string savedId = SkinSaveSystem.Load();
        var savedSkin = database.GetById(savedId);
        var profile = AuthManager.Instance?.CurrentProfile;

        previewSkin = (savedSkin != null && PlayerProfile.IsSkinOwned(savedSkin, profile)) ? savedSkin : database.skins[0];
        selectedSkin = previewSkin; // whatever was saved is currently equipped
        appearance.ApplySkin(previewSkin);

        characterLabel.text = previewSkin != null ? previewSkin.name : string.Empty;

        labelSequence.Stop();
        SetLabelAlpha(0f);
        labelRect.anchoredPosition = labelRestPos + Vector2.up * labelMoveDistance;

        labelSequence = Sequence.Create()
            .ChainDelay(labelEnableDelay)
            .Chain(Tween.Alpha(characterLabel, endValue: 1f, duration: labelFadeDuration, ease: labelFadeEase))
            .Group(Tween.UIAnchoredPosition(labelRect, endValue: labelRestPos, duration: labelFadeDuration, ease: labelFadeEase));

        RefreshHighlight();
    }

    private void OnDestroy() {
        if (AuthManager.Instance != null)
            AuthManager.Instance.OnPlayerStatsLoaded -= HandleProfileLoaded;

        syncCts?.Cancel();
        syncCts?.Dispose();
    }

    private void HandleProfileLoaded(PlayerProfile profile) {
        if (profile == null) return; // signed out mid-session; ignore here

        if (spawnedButtons.Count == 0) {
            BuildButtons(profile);
        } else {
            // profile changed (e.g. a skin was just granted) — refresh lock states in place
            foreach (var btn in spawnedButtons)
                btn.SetOwned(PlayerProfile.IsSkinOwned(btn.Skin, profile));
        }
    }

    private void BuildButtons(PlayerProfile profile) {
        var nowUtc = DateTime.UtcNow;

        foreach (var skin in database.skins) {
            bool owned = PlayerProfile.IsSkinOwned(skin, profile);
            var availability = skin.GetAvailabilityStatus(nowUtc);

            var btn = Instantiate(buttonPrefab, contentParent);
            btn.Init(skin, owned, availability, OnSkinPreview, OnSkinSelected, OnSkinPurchase);
            spawnedButtons.Add(btn);
        }

        RefreshHighlight();
    }

    private async void Start() {
        AuthManager.Instance.OnPlayerStatsLoaded += HandleProfileLoaded;

        BuildButtons(AuthManager.Instance.CurrentProfile); // limited-time skins show "..." until the sync below lands

        syncCts = new CancellationTokenSource();
        try {
            // Keeps retrying for as long as the shop stays open — RefreshAvailability fires
            // via the callback the moment it finally succeeds, however long that takes.
            await TimeLimitedSkinSyncService.SyncPersistentAsync(database, RefreshAvailability, cancellationToken: syncCts.Token);
        } catch (OperationCanceledException) {
            // panel closed mid-retry — nothing to do, don't touch destroyed buttons
        }
    }

    private void RefreshAvailability() {
        var nowUtc = DateTime.UtcNow;
        foreach (var btn in spawnedButtons)
            btn.SetAvailability(btn.Skin.GetAvailabilityStatus(nowUtc));
    }

    private void OnSkinPreview(CharacterSkinSO skin) {
        if (previewSkin == skin) return; // ownership no longer gates preview
        AnimateLabelChange(skin.name);
        previewSkin = skin;
        appearance.ApplySkin(skin); // live preview, not saved yet — even for unowned skins
        RefreshHighlight();
    }

    private void OnSkinSelected(CharacterSkinSO skin) {
        if (selectedSkin == skin || !Profile.OwnsSkin(skin)) return; // ownership still required to equip

        selectedSkin = skin;
        previewSkin = skin;
        appearance.ApplySkin(skin);
        SkinSaveSystem.Save(skin.skinId);

        if (characterLabel.text != skin.name)
            AnimateLabelChange(skin.name);

        RefreshHighlight();
    }

    private void OnSkinPurchase(CharacterSkinSO skin) {
        if (skin.GetAvailabilityStatus(DateTime.UtcNow) != SkinAvailabilityStatus.Available) return; // loading/expired — shouldn't be reachable via UI, but guard anyway
        purchasePopup.Show(skin, () => HandlePurchaseSuccess(skin));
    }

    private void HandlePurchaseSuccess(CharacterSkinSO skin) {
        // Reflect the new ownership in every button in place — mirrors HandleProfileLoaded's refresh path.
        foreach (var btn in spawnedButtons)
            if (btn.Skin == skin)
                btn.SetOwned(true);

        // Equip it immediately, same as a normal Select tap on an owned skin.
        OnSkinSelected(skin);
    }

    private void AnimateLabelChange(string newText) {
        labelSequence.Stop();

        // Fade + move down out
        labelSequence = Sequence.Create()
            .Group(Tween.Alpha(characterLabel, endValue: 0f, duration: labelFadeDuration, ease: labelFadeEase))
            .Group(Tween.UIAnchoredPosition(labelRect, endValue: labelRestPos + Vector2.down * labelMoveDistance, duration: labelFadeDuration, ease: labelFadeEase))
            .ChainCallback(() => {
                characterLabel.text = newText;
                labelRect.anchoredPosition = labelRestPos + Vector2.up * labelMoveDistance;
            })
            // Fade + move down in, from above
            .Group(Tween.Alpha(characterLabel, endValue: 1f, duration: labelFadeDuration, ease: labelFadeEase))
            .Group(Tween.UIAnchoredPosition(labelRect, endValue: labelRestPos, duration: labelFadeDuration, ease: labelFadeEase));
    }

    private void SetLabelAlpha(float a) {
        var c = characterLabel.color;
        c.a = a;
        characterLabel.color = c;
    }

    private void RefreshHighlight() {
        foreach (var btn in spawnedButtons) {
            btn.SetPreviewed(btn.Skin == previewSkin);
            btn.SetEquipped(btn.Skin == selectedSkin);
        }
    }


    private void OnDisable() {
        labelSequence.Stop();
    }
}