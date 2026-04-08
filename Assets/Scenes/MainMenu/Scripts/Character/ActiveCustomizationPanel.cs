using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActiveCustomizationPanel : MonoBehaviour {
    [SerializeField] private Color textColorOnActive;

    [SerializeField] private ActiveSelection cosmetics;
    [SerializeField] private TextMeshProUGUI cosmeticsText;
    [SerializeField] private Image cosmeticsBorder;
    [SerializeField] private CanvasGroup cosmeticsBackground;

    [SerializeField] private ActiveSelection gender;
    [SerializeField] private TextMeshProUGUI genderText;
    [SerializeField] private Image genderBorder;
    [SerializeField] private CanvasGroup genderBackground;

    [SerializeField] private ActiveSelection skin;
    [SerializeField] private TextMeshProUGUI skinText;
    [SerializeField] private Image skinBorder;
    [SerializeField] private CanvasGroup skinBackground;

    [SerializeField] private ActiveSelection hair;
    [SerializeField] private TextMeshProUGUI hairText;
    [SerializeField] private Image hairBorder;
    [SerializeField] private CanvasGroup hairBackground;

    [SerializeField] private ActiveSelection head;
    [SerializeField] private TextMeshProUGUI headText;
    [SerializeField] private Image headBorder;
    [SerializeField] private CanvasGroup headBackground;

    [SerializeField] private ActiveSelection body;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Image bodyBorder;
    [SerializeField] private CanvasGroup bodyBackground;

    [SerializeField] private ActiveSelection pants;
    [SerializeField] private TextMeshProUGUI pantsText;
    [SerializeField] private Image pantsBorder;
    [SerializeField] private CanvasGroup pantsBackground;

    [SerializeField] private ActiveSelection shoes;
    [SerializeField] private TextMeshProUGUI shoesText;
    [SerializeField] private Image shoesBorder;
    [SerializeField] private CanvasGroup shoesBackground;

    private FontStyles textNormal;
    private Color textColor;

    private void Start() {
        textNormal = hairText.fontStyle;
        textColor = hairText.color;

        DecorateButton(cosmeticsText, cosmeticsBorder, cosmeticsBackground);
    }

    public void OpenCosmeticsSelection() {
        SetActiveCustomization("cosmetics", cosmeticsText, cosmeticsBorder, cosmeticsBackground);
    }

    public void OpenGenderSelection() {
        SetActiveCustomization("gender", genderText, genderBorder, genderBackground);
    }

    public void OpenSkinSelection() {
        SetActiveCustomization("skin", skinText, skinBorder, skinBackground);
    }

    public void OpenHairSelection() {
        SetActiveCustomization("hair", hairText, hairBorder, hairBackground);
    }

    public void OpenHeadSelection() {
        SetActiveCustomization("head", headText, headBorder, headBackground);
    }

    public void OpenBodySelection() {
        SetActiveCustomization("body", bodyText, bodyBorder, bodyBackground);
    }

    public void OpenPantsSelection() {
        SetActiveCustomization("pants", pantsText, pantsBorder, pantsBackground);
    }

    public void OpenShoesSelection() {
        SetActiveCustomization("shoes", shoesText, shoesBorder, shoesBackground);
    }

    public void SetActiveCustomization(string panel, TextMeshProUGUI text, Image border, CanvasGroup background) {
        cosmetics.HideSelection(); if(panel == "cosmetics") cosmetics.ShowSelection();
        gender.HideSelection(); if(panel == "gender") gender.ShowSelection();
        skin.HideSelection(); if(panel == "skin") skin.ShowSelection();
        hair.HideSelection(); if(panel == "hair") hair.ShowSelection();
        head.HideSelection(); if(panel == "head") head.ShowSelection();
        body.HideSelection(); if(panel == "body") body.ShowSelection();
        pants.HideSelection(); if(panel == "pants") pants.ShowSelection();
        shoes.HideSelection(); if(panel == "shoes") shoes.ShowSelection();

        UndecorateButton(cosmeticsText, cosmeticsBorder, cosmeticsBackground);
        UndecorateButton(genderText, genderBorder, genderBackground);
        UndecorateButton(skinText, skinBorder, skinBackground);
        UndecorateButton(hairText, hairBorder, hairBackground);
        UndecorateButton(headText, headBorder, headBackground);
        UndecorateButton(bodyText, bodyBorder, bodyBackground);
        UndecorateButton(pantsText, pantsBorder, pantsBackground);
        UndecorateButton(shoesText, shoesBorder, shoesBackground);

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
