using TMPro;
using UnityEngine;

public class CustomizationController : MonoBehaviour {
    [SerializeField] private Color textColorOnActive;

    [SerializeField] private ActiveSelection cosmetics;
    [SerializeField] private TextMeshProUGUI cosmeticsText;

    [SerializeField] private ActiveSelection gender;
    [SerializeField] private TextMeshProUGUI genderText;

    [SerializeField] private ActiveSelection skin;
    [SerializeField] private TextMeshProUGUI skinText;

    [SerializeField] private ActiveSelection hair;
    [SerializeField] private TextMeshProUGUI hairText;

    [SerializeField] private ActiveSelection head;
    [SerializeField] private TextMeshProUGUI headText;

    [SerializeField] private ActiveSelection body;
    [SerializeField] private TextMeshProUGUI bodyText;

    [SerializeField] private ActiveSelection pants;
    [SerializeField] private TextMeshProUGUI pantsText;

    [SerializeField] private ActiveSelection shoes;
    [SerializeField] private TextMeshProUGUI shoesText;

    private FontStyles textNormal;
    private Color textColor;

    private void Start() {
        textNormal = hairText.fontStyle;
        textColor = hairText.color;
    }

    public void OpenCosmeticsSelection() {
        setActiveCustomization("cosmetics", cosmeticsText);
    }

    public void OpenGenderSelection() {
        setActiveCustomization("gender", genderText);
    }

    public void OpenSkinSelection() {
        setActiveCustomization("skin", skinText);
    }

    public void OpenHairSelection() {
        setActiveCustomization("hair", hairText);
    }

    public void OpenHeadSelection() {
        setActiveCustomization("head", headText);
    }

    public void OpenBodySelection() {
        setActiveCustomization("body", bodyText);
    }

    public void OpenPantsSelection() {
        setActiveCustomization("pants", pantsText);
    }

    public void OpenShoesSelection() {
        setActiveCustomization("shoes", shoesText);
    }

    public void setActiveCustomization(string panel, TextMeshProUGUI text) {
        cosmetics.HideSelection(); if(panel == "cosmetics") cosmetics.ShowSelection();
        gender.HideSelection(); if(panel == "gender") gender.ShowSelection();
        skin.HideSelection(); if(panel == "skin") skin.ShowSelection();
        hair.HideSelection(); if(panel == "hair") hair.ShowSelection();
        head.HideSelection(); if(panel == "head") head.ShowSelection();
        body.HideSelection(); if(panel == "body") body.ShowSelection();
        pants.HideSelection(); if(panel == "pants") pants.ShowSelection();
        shoes.HideSelection(); if(panel == "shoes") shoes.ShowSelection();

        UndecorateText(cosmeticsText);
        UndecorateText(genderText);
        UndecorateText(skinText);
        UndecorateText(hairText);
        UndecorateText(headText);
        UndecorateText(bodyText);
        UndecorateText(pantsText);
        UndecorateText(shoesText);

        DecorateText(text);
    }

    private void DecorateText(TextMeshProUGUI text) {
        text.color = textColorOnActive;
        text.fontStyle = FontStyles.Bold;
    }

    private void UndecorateText(TextMeshProUGUI text) {
        text.color = textColor;
        text.fontStyle = textNormal;
    }
}
