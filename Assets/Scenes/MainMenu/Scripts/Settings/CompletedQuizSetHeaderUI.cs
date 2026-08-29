using TMPro;
using UnityEngine;

// A subject group header in the completed-quiz-sets list. Interleaved with
// CompletedQuizSetRowUI instances in the same scrolling content by
// CompletedQuizSetListUI — the shared Vertical Layout Group stacks whatever
// order their sibling indices are set to.
public class CompletedQuizSetSectionHeaderUI : MonoBehaviour {
    [SerializeField] private TMP_Text subjectText;
    [SerializeField] private TMP_Text countText; // optional — e.g. "(3)"

    public void Show(string subject, int count) {
        if (subjectText != null) subjectText.text = subject;
        if (countText != null) countText.text = $"({count})";
    }
}