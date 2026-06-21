using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Generic single-line selectable row, stacked vertically in a list.
// Used for the Quiz Select screen (text-only — no preview image).
// Level Select uses its own LevelSelectItemUI instead, since it needs
// a thumbnail alongside the label.
// Click to select; the controlling panel deselects siblings to keep
// only one highlighted at a time (single-select behavior).
public class SelectableListItemUI : MonoBehaviour {
    [SerializeField] TMP_Text label;
    [SerializeField] Button button;
    [SerializeField] Image background;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color selectedColor = new Color(0.3f, 0.6f, 1f);

    // The underlying value this row represents (the quiz set name). </summary>
    public string Value { get; private set; }

    public void Setup(string displayText, string value, Action<string> onSelected) {
        label.text = displayText;
        Value = value;
        SetSelected(false);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onSelected?.Invoke(Value));
    }

    public void SetSelected(bool selected) {
        if (background != null)
            background.color = selected ? selectedColor : normalColor;
    }
}