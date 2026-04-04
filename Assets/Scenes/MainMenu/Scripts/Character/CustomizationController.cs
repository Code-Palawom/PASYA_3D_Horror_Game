using TMPro;
using UnityEngine;

public class CustomizationController : MonoBehaviour {
    [SerializeField] private Color textColorOnActive;

    [SerializeField] private ActiveSelection cosmetics;
    [SerializeField] private TextMeshProUGUI cosmeticsText;

    [SerializeField] private ActiveSelection hair;
    [SerializeField] private TextMeshProUGUI hairText;

    [SerializeField] private ActiveSelection body;
    [SerializeField] private TextMeshProUGUI bodyText;

    private FontStyles textNormal;
    private Color textColor;

    private void Start() {
        textNormal = hairText.fontStyle;
        textColor = hairText.color;
    }

    public void OpenCosmeticsSelection() {
        cosmetics.ShowSelection();
        hair.HideSelection();
        body.HideSelection();

        setActiveCustomization(cosmeticsText);
    }

    public void OpenHairSelection() {
        cosmetics.HideSelection();
        hair.ShowSelection();
        body.HideSelection();

        setActiveCustomization(hairText);
    }

    public void OpenBodySelection() {
        cosmetics.HideSelection();
        hair.HideSelection();
        body.ShowSelection();

        setActiveCustomization(bodyText);
    }

    public void setActiveCustomization(TextMeshProUGUI text) {
        UndecorateText(cosmeticsText);
        UndecorateText(hairText);
        UndecorateText(bodyText);

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
