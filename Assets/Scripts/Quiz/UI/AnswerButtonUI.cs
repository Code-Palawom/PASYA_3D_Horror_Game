using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AnswerButtonUI : MonoBehaviour {
    [SerializeField] Button button;
    [SerializeField] TMP_Text label;
    [SerializeField] Image background;
    [SerializeField] Color defaultColor = Color.white;

    private Action<int> _onClicked;
    private int _index;

    public void Setup(int index, string text, Action<int> onClicked) {
        _index = index;
        _onClicked = onClicked;
        label.text = text;
        background.color = defaultColor;
        button.interactable = true;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => _onClicked?.Invoke(_index));
    }

    public void SetInteractable(bool state) => button.interactable = state;

    public void ResetVisual() => background.color = defaultColor;
}