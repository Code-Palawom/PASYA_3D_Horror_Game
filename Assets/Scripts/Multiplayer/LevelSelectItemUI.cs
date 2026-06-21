using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Selectable row for the Level Select list — includes a preview image
// thumbnail alongside the label, unlike the plain text-only rows used
// for Quiz Select (see SelectableListItemUI).
public class LevelSelectItemUI : MonoBehaviour {
    [SerializeField] Image previewImage;
    [SerializeField] TMP_Text label;
    [SerializeField] Button button;
    [SerializeField] Image background;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color selectedColor = new Color(0.3f, 0.6f, 1f);

    // The level's scene name — what gets passed back on selection. </summary>
    public string Value { get; private set; }

    public void Setup(LevelOption level, Action<string> onSelected) {
        label.text = level.displayName;
        Value = level.sceneName;

        if (previewImage != null) {
            previewImage.sprite = level.previewImage;
            previewImage.gameObject.SetActive(level.previewImage != null);
        }

        SetSelected(false);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onSelected?.Invoke(Value));
    }

    public void SetSelected(bool selected) {
        if (background != null) {
            background.color = selected ? selectedColor : normalColor;
            label.color = selected ? selectedColor : normalColor;
        }
    }
}