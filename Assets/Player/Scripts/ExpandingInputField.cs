using UnityEngine;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class ExpandingInputField : MonoBehaviour {
    [SerializeField] private TMP_InputField inputField;

    [Header("Limits")]
    [SerializeField] private float minWidth = 120f;
    [SerializeField] private float maxWidth = 400f;
    [SerializeField] private float minHeight = 40f;
    [SerializeField] private float maxHeight = 200f;

    [Header("Padding (text area vs full field)")]
    [SerializeField] private float paddingX = 20f;
    [SerializeField] private float paddingY = 16f;

    private RectTransform rt;
    private TMP_Text textComp;

    void Awake() {
        if (!inputField) inputField = GetComponent<TMP_InputField>();
        rt = GetComponent<RectTransform>();
        textComp = inputField.textComponent;
        inputField.onValueChanged.AddListener(_ => Resize());
    }

    void Start() => Resize();

    void Resize() {
        string content = string.IsNullOrEmpty(inputField.text) ? " " : inputField.text;
        bool hasNewline = content.Contains("\n");

        Vector2 singleLine = textComp.GetPreferredValues(content, 0f, 0f);
        float desiredWidth = singleLine.x + paddingX;

        if (!hasNewline && desiredWidth <= maxWidth) {
            textComp.textWrappingMode = TextWrappingModes.NoWrap;
            rt.sizeDelta = new Vector2(Mathf.Clamp(desiredWidth, minWidth, maxWidth), minHeight);
        } else {
            textComp.textWrappingMode = TextWrappingModes.Normal;
            Vector2 wrapped = textComp.GetPreferredValues(content, maxWidth - paddingX, 0f);
            float desiredHeight = wrapped.y + paddingY;
            rt.sizeDelta = new Vector2(maxWidth, Mathf.Clamp(desiredHeight, minHeight, maxHeight));
        }
    }
}