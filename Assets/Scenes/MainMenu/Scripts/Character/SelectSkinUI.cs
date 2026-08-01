using PrimeTween;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkinSelectUI : MonoBehaviour {
    [SerializeField] private SkinDatabaseSO database;
    [SerializeField] private CharacterAppearanceController appearance;
    [SerializeField] private Transform contentParent;
    [SerializeField] private SkinButtonUI buttonPrefab;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text characterLabel;
    [SerializeField] private MainMenuUI mainMenuUI;

    [Header("Label Animation")]
    [SerializeField] private float labelFadeDuration = 0.15f;
    [SerializeField] private Ease labelFadeEase = Ease.OutQuad;
    [SerializeField] private float labelMoveDistance = 15f; // pixels
    [SerializeField] private float labelEnableDelay = 1f; // delay before label fades in on panel open

    private readonly List<SkinButtonUI> spawnedButtons = new();
    private CharacterSkinSO previewSkin;
    private Sequence labelSequence;
    private RectTransform labelRect;
    private Vector2 labelRestPos;

    private PlayerProfile Profile => AuthManager.Instance.CurrentProfile;

    private void Awake() {
        labelRect = characterLabel.rectTransform;
        labelRestPos = labelRect.anchoredPosition;
    }

    private void OnEnable() {
        string savedId = SkinSaveSystem.Load();
        var savedSkin = database.GetById(savedId);
        var profile = AuthManager.Instance.CurrentProfile;

        // guard against a locally-cached skin the player no longer owns
        previewSkin = (savedSkin != null && PlayerProfile.IsSkinOwned(savedSkin, profile)) ? savedSkin : database.skins[0]; // guaranteed ownedByDefault
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
    }

    private void HandleProfileLoaded(PlayerProfile profile) {
        if (profile == null) return; // signed out mid-session; ignore here

        if (spawnedButtons.Count == 0) {
            BuildButtons(profile);
        } else {
            // profile changed (e.g. a skin was just granted) — refresh lock states in place
            foreach (var btn in spawnedButtons)
                btn.SetOwned(profile.OwnsSkin(btn.Skin));
        }
    }

    private void BuildButtons(PlayerProfile profile) {
        foreach (var skin in database.skins) {
            var btn = Instantiate(buttonPrefab, contentParent);
            btn.Init(skin, PlayerProfile.IsSkinOwned(skin, profile), OnSkinClicked);
            spawnedButtons.Add(btn);
        }

        RefreshHighlight();
    }

    private void Start() {
        confirmButton.onClick.AddListener(Confirm);
        AuthManager.Instance.OnPlayerStatsLoaded += HandleProfileLoaded;

        BuildButtons(AuthManager.Instance.CurrentProfile);
    }

    private void OnSkinClicked(CharacterSkinSO skin) {
        if (previewSkin == skin || !Profile.OwnsSkin(skin)) return; // ignore locked/no-op
        AnimateLabelChange(skin.name);
        previewSkin = skin;
        appearance.ApplySkin(skin); // live preview, not saved yet
        RefreshHighlight();
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
        for (int i = 0; i < spawnedButtons.Count; i++)
            spawnedButtons[i].SetSelected(database.skins[i] == previewSkin);
    }

    private void Confirm() {
        if (previewSkin != null && Profile.OwnsSkin(previewSkin)) { // defense in depth
            SkinSaveSystem.Save(previewSkin.skinId);
            mainMenuUI.HideCharacterPanel(true);
        }
    }

    private void OnDisable() {
        labelSequence.Stop();
    }
}