using System.Collections.Generic;
using PrimeTween;
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

    private void Awake() {
        labelRect = characterLabel.rectTransform;
        labelRestPos = labelRect.anchoredPosition;
    }

    private void OnEnable() {
        string savedId = SkinSaveSystem.Load();
        previewSkin = database.GetById(savedId) ?? (database.skins.Length > 0 ? database.skins[0] : null);
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

    private void Start() {
        foreach (var skin in database.skins) {
            var btn = Instantiate(buttonPrefab, contentParent);
            btn.Init(skin, OnSkinClicked);
            spawnedButtons.Add(btn);
        }

        RefreshHighlight();
        confirmButton.onClick.AddListener(Confirm);
    }

    private void OnSkinClicked(CharacterSkinSO skin) {
        if(previewSkin == skin) return; // no change
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
        if (previewSkin != null) {
            SkinSaveSystem.Save(previewSkin.skinId);
            mainMenuUI.HideCharacterPanel(true);
        }
    }

    private void OnDisable() {
        labelSequence.Stop();
    }
}