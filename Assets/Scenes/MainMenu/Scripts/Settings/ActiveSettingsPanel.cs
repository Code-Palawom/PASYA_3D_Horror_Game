using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActiveSettingsPanel : MonoBehaviour {
    [SerializeField] private Color textColorOnActive;

    [SerializeField] private ActiveSettings profile;
    [SerializeField] private TextMeshProUGUI profileText;
    [SerializeField] private Image profileBorder;
    [SerializeField] private CanvasGroup profileBackground;

    [SerializeField] private ActiveSettings gameplay;
    [SerializeField] private TextMeshProUGUI gameplayText;
    [SerializeField] private Image gameplayBorder;
    [SerializeField] private CanvasGroup gameplayBackground;

    private FontStyles textNormal;
    private Color textColor;

    private void Start() {
        textNormal = profileText.fontStyle;
        textColor = profileText.color;

        DecorateButton(profileText, profileBorder, profileBackground);
    }

    public void OpenProfileSettings() {
        SetActiveCustomization("profile", profileText, profileBorder, profileBackground);
    }

    public void OpenGameplaySettings() {
        SetActiveCustomization("gameplay", gameplayText, gameplayBorder, gameplayBackground);
    }

    public void SetActiveCustomization(string panel, TextMeshProUGUI text, Image border, CanvasGroup background) {
        profile.HideSelection(); if(panel == "profile") profile.ShowSelection();
        gameplay.HideSelection(); if(panel == "gameplay") gameplay.ShowSelection();
        
        UndecorateButton(profileText, profileBorder, profileBackground);
        UndecorateButton(gameplayText, gameplayBorder, gameplayBackground);

        DecorateButton(text, border, background);
    }

    private void DecorateButton(TextMeshProUGUI text, Image border, CanvasGroup background) {
        text.color = textColorOnActive;
        text.fontStyle = FontStyles.Bold;

        border.enabled = true;
        background.alpha = 0.05f;
    }

    private void UndecorateButton(TextMeshProUGUI text, Image border, CanvasGroup background) {
        text.color = textColor;
        text.fontStyle = textNormal;

        border.enabled = false;
        background.alpha = 0.01f;
    }
}
