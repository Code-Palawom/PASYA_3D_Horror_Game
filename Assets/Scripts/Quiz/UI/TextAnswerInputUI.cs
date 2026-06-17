using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Input field panel shared by FillInTheBlank and ShortAnswer question types.
// Short answer gets a multiline field and a higher character limit.
public class TextAnswerInputUI : MonoBehaviour {
    [SerializeField] TMP_InputField inputField;
    [SerializeField] Button submitButton;
    [SerializeField] TMP_Text placeholderText;
    [SerializeField] TMP_Text characterCountLabel;  // optional

    [Header("Config")]
    [SerializeField] int fillInBlankCharLimit = 50;
    [SerializeField] int shortAnswerCharLimit = 300;

    private Action<string> _onSubmit;

    public void Setup(QuestionType type, Action<string> onSubmit) {
        _onSubmit = onSubmit;

        inputField.text = "";
        inputField.interactable = true;
        submitButton.interactable = true;

        if (type == QuestionType.FillInTheBlank) {
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.characterLimit = fillInBlankCharLimit;
            placeholderText.text = "Type your answer...";
        } else // ShortAnswer
          {
            inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
            inputField.characterLimit = shortAnswerCharLimit;
            placeholderText.text = "Write your answer here...";
        }

        inputField.onValueChanged.RemoveAllListeners();
        inputField.onValueChanged.AddListener(OnValueChanged);

        submitButton.onClick.RemoveAllListeners();
        submitButton.onClick.AddListener(OnSubmitClicked);

        UpdateCharCount();
        inputField.ActivateInputField();
    }

    void OnValueChanged(string value) {
        UpdateCharCount();
        // Allow submit only if something is typed
        submitButton.interactable = !string.IsNullOrWhiteSpace(value);
    }

    void OnSubmitClicked() {
        string text = inputField.text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        inputField.interactable = false;
        submitButton.interactable = false;
        _onSubmit?.Invoke(text);
    }

    void UpdateCharCount() {
        if (characterCountLabel == null) return;
        int limit = inputField.characterLimit;
        characterCountLabel.text = $"{inputField.text.Length} / {limit}";
    }

    public void Clear() {
        inputField.text = "";
        inputField.interactable = true;
        submitButton.interactable = false;
    }
}