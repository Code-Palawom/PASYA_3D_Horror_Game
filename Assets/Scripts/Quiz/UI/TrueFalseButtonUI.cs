using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Two-button True / False panel.
// Index 0 = True, Index 1 = False.
public class TrueFalseButtonUI : MonoBehaviour {
    [SerializeField] Button trueButton;
    [SerializeField] Button falseButton;
    [SerializeField] Image trueBackground;
    [SerializeField] Image falseBackground;
    [SerializeField] Color defaultColor = Color.white;

    private Action<int> _onSelected;

    public void Setup(Action<int> onSelected) {
        _onSelected = onSelected;

        trueBackground.color = defaultColor;
        falseBackground.color = defaultColor;
        trueButton.interactable = true;
        falseButton.interactable = true;

        trueButton.onClick.RemoveAllListeners();
        falseButton.onClick.RemoveAllListeners();

        trueButton.onClick.AddListener(() => OnClicked(0));
        falseButton.onClick.AddListener(() => OnClicked(1));
    }

    void OnClicked(int index) {
        trueButton.interactable = false;
        falseButton.interactable = false;
        _onSelected?.Invoke(index);
    }

    public void SetInteractable(bool state) {
        trueButton.interactable = state;
        falseButton.interactable = state;
    }

    public void ResetVisual() {
        trueBackground.color = defaultColor;
        falseBackground.color = defaultColor;
    }
}