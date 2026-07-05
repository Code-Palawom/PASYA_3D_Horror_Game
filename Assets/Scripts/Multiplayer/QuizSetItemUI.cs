using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Selectable row for the Quiz Select panel.
/// Displays name, category, question count, and play count from QuizSetMetaEntry.
/// Author (id + name) is stored for future use.
/// Supports show/hide for category filtering.
/// </summary>
public class QuizSetItemUI : MonoBehaviour {
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] TMP_Text categoryLabel;
    [SerializeField] TMP_Text questionCountLabel;
    [SerializeField] TMP_Text playCountLabel;
    [SerializeField] Button button;
    [SerializeField] Image background;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color selectedColor = new Color(0.3f, 0.6f, 1f);

    public string SetId { get; private set; }
    public string SetName { get; private set; }
    public string Category { get; private set; }

    // ── Author (stored for future use) ────────────────────────
    public string AuthorId { get; private set; }
    public string AuthorName { get; private set; }

    public void Setup(QuizSetMetaEntry entry, Action<string, string> onSelected) {
        SetId = entry.setId;
        SetName = entry.name;
        Category = entry.category;
        AuthorId = entry.authorId;
        AuthorName = entry.authorName;

        nameLabel?.SetText(entry.name);
        categoryLabel?.SetText(string.IsNullOrWhiteSpace(entry.category) ? "—" : entry.category);
        questionCountLabel?.SetText($"{entry.questionCount} questions");
        playCountLabel?.SetText($"{entry.playCount} plays");

        SetSelected(false);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onSelected?.Invoke(SetName, SetId));
    }

    public void SetSelected(bool selected) {
        if (background != null)
            background.color = selected ? selectedColor : normalColor;
    }

    /// <summary>
    /// Shows or hides this card based on the active category filter.
    /// Pass null or empty string to show all.
    /// </summary>
    public void ApplyFilter(string filterCategory) {
        bool show = string.IsNullOrEmpty(filterCategory) || Category == filterCategory;
        gameObject.SetActive(show);
    }
}