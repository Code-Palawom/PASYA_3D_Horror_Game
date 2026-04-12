using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActiveModePanel : MonoBehaviour {
    [SerializeField] private Color textColorOnActive;

    [SerializeField] private ActiveMode host;
    [SerializeField] private TextMeshProUGUI hostText;
    [SerializeField] private Image hostBorder;
    [SerializeField] private CanvasGroup hostBackground;

    [SerializeField] private ActiveMode join;
    [SerializeField] private TextMeshProUGUI joinText;
    [SerializeField] private Image joinBorder;
    [SerializeField] private CanvasGroup joinBackground;

    private FontStyles textNormal;
    private Color textColor;

    private void Start() {
        textNormal = hostText.fontStyle;
        textColor = hostText.color;

        DecorateButton(hostText, hostBorder, hostBackground);
    }

    public void OpenHostPanel() {
        //GameModeSettings.CurrentMode = GameModeSettings.GameMode.HostMultiplayer;
        SetActiveCustomization("host", hostText, hostBorder, hostBackground);
    }

    public void OpenJoinPanel() {
        //GameModeSettings.CurrentMode = GameModeSettings.GameMode.JoinMultiplayer;
        SetActiveCustomization("join", joinText, joinBorder, joinBackground);
    }

    public void SetActiveCustomization(string panel, TextMeshProUGUI text, Image border, CanvasGroup background) {
        host.HideSelection(); if(panel == "host") host.ShowSelection();
        join.HideSelection(); if(panel == "join") join.ShowSelection();
        
        UndecorateButton(hostText, hostBorder, hostBackground);
        UndecorateButton(joinText, joinBorder, joinBackground);

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
