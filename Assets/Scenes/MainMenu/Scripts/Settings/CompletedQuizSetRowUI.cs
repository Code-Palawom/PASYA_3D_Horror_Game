using Firebase.Firestore;
using TMPro;
using UnityEngine;

// One row in the completed-quiz-sets list. Populated by
// CompletedQuizSetListUI per entry in PlayerProfile.CompletedQuizSets.
public class CompletedQuizSetRowUI : MonoBehaviour {
    [Header("Refs")]
    [SerializeField] private TMP_Text nameText;
    //[SerializeField] private TMP_Text subjectText;      // hidden if unavailable (e.g. unknown set)
    [SerializeField] private TMP_Text questionCountText; // hidden if unavailable
    [SerializeField] private TMP_Text dateText;

    // Formats a Firestore Timestamp (UTC) as a local date for display.
    // Swap the format string here if you want a different style everywhere.
    private const string DateFormat = "MMM d, yyyy";

    public void Show(QuizSetMetaEntry meta, Timestamp completedAt, int correct, int incorrect) {
        if (nameText != null) nameText.text = meta.name;

        //if (subjectText != null) {
        //    bool hasSubject = !string.IsNullOrEmpty(meta.subject);
        //    subjectText.gameObject.SetActive(hasSubject);
        //    if (hasSubject) subjectText.text = meta.subject;
        //}

        if (questionCountText != null) {
            questionCountText.gameObject.SetActive(true);
            questionCountText.text = $"{meta.questionCount} questions";
        }

        SetDateText(completedAt);
    }

    // For a setId in CompletedQuizSets that no longer has catalog metadata —
    // removed from Firestore's quizSets collection, or a local SO set that's
    // since been removed from the build. Nothing preserved about what it used
    // to look like, so this only ever has the bare id and the completion date.
    public void ShowUnknown(string setId, Timestamp completedAt, int correct, int incorrect) {
        if (nameText != null) nameText.text = $"{setId} (Unknown Set)";
        //if (subjectText != null) subjectText.gameObject.SetActive(false);
        if (questionCountText != null) questionCountText.gameObject.SetActive(false);

        SetDateText(completedAt);
    }

    private void SetDateText(Timestamp completedAt) {
        if (dateText == null) return;
        dateText.text = completedAt.ToDateTime().ToLocalTime().ToString(DateFormat);
    }
}