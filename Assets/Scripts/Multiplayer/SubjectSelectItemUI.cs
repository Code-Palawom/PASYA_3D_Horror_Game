using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SubjectSelectItemUI : MonoBehaviour {
    [SerializeField] TMP_Text subjectLabel;
    [SerializeField] TMP_Text progressLabel; // e.g. "2 / 5 completed" — optional
    [SerializeField] TMP_Text totalPlayCountLabel;
    [SerializeField] Button button;
    [SerializeField] Image background;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color selectedColor = new Color(0.3f, 0.6f, 1f);

    public string Subject { get; private set; }

    public void Setup(string subject, int completedCount, int totalCount, int totalPlayCount, Action<string> onSelected) {
        Subject = subject;
        subjectLabel.SetText(subject);
        progressLabel.SetText($"{completedCount} / {totalCount} completed");
        totalPlayCountLabel.SetText($"{totalPlayCount}");

        SetSelected(false);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onSelected?.Invoke(Subject));
    }

    public void SetSelected(bool selected) {
        if(selected) SfxManager.Play(SfxId.Tap);
        if (background != null)
            background.color = selected ? selectedColor : normalColor;
    }

    // Shows or hides this card based on the active category filter.
    // Pass null or empty string to show all.
    public void ApplyFilter(string filterSubject) {
        bool show = string.IsNullOrEmpty(filterSubject) || Subject == filterSubject;
        gameObject.SetActive(show);
    }
}