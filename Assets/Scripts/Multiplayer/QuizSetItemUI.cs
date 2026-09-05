using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Selectable row for the Quiz Select panel.
// Displays name, category, question count, and play count from QuizSetMetaEntry.
// Author (id + name) is stored for future use.
// Supports show/hide for category filtering.
public class QuizSetItemUI : MonoBehaviour {
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] TMP_Text questionCountLabel;
    [SerializeField] TMP_Text playCountLabel;
    [SerializeField] Button button;
    [SerializeField] Image background;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color selectedColor = new Color(0.3f, 0.6f, 1f);

    [Header("Locking")]
    [SerializeField] GameObject lockIcon;
    [SerializeField] GameObject lockIcon2;
    [SerializeField] Color lockedColor = new Color(0.5f, 0.5f, 0.5f);

    public string SetId { get; private set; }
    public string SetName { get; private set; }
    public string Subject { get; private set; }
    public int Order { get; private set; }
    public bool IsLocked { get; private set; }

    // ── Author (stored for future use) ────────────────────────
    public string AuthorId { get; private set; }
    public string AuthorName { get; private set; }

    Action<string, string> _onSelected;

    public void Setup(QuizSetMetaEntry entry, Action<string, string> onSelected) {
        SetId = entry.setId;
        SetName = entry.name;
        Subject = entry.subject;
        Order = entry.order;
        AuthorId = entry.authorId;
        AuthorName = entry.authorName;
        _onSelected = onSelected;

        nameLabel.SetText(entry.name);
        questionCountLabel.SetText($"{entry.questionCount} questions");
        playCountLabel.SetText($"{entry.playCount} plays");

        SetSelected(false);
        SetLocked(false);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => {
            if (IsLocked) return; // guard even if something re-enabled the button externally
            _onSelected?.Invoke(SetName, SetId);
        });
    }

    public void SetSelected(bool selected) {
        if (selected) SfxManager.Play(SfxId.Tap);
        if (background != null && !IsLocked)
            background.color = selected ? selectedColor : normalColor;
    }

    // Locks/unlocks this card. previousSetName is shown in lockReasonLabel if provided.
    public void SetLocked(bool locked, string previousSetName = null) {
        IsLocked = locked;
        button.interactable = !locked;

        background.color = locked ? lockedColor : normalColor;

        lockIcon.SetActive(locked);
        lockIcon2.SetActive(locked);

        if (locked) {
            nameLabel.text = locked && !string.IsNullOrEmpty(previousSetName)
                ? $"Complete \"{previousSetName}\" first"
                : "Locked";
            questionCountLabel.SetText("");
            playCountLabel.SetText("");
        }
    }

    public bool MatchesSubject(string subject) => Subject == subject;
}