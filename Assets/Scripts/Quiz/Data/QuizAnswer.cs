// Unified answer payload passed from the canvas to QuizManager.
// For MultipleChoice / TrueOrFalse: set SelectedIndex.
// For FillInTheBlank / ShortAnswer: set Text.
public class QuizAnswer {
    public int SelectedIndex = -1;   // MC and T/F
    public string Text = "";    // FillInBlank and ShortAnswer

    public static QuizAnswer FromIndex(int index) =>
        new QuizAnswer { SelectedIndex = index };

    public static QuizAnswer FromText(string text) =>
        new QuizAnswer { Text = text?.Trim() ?? "" };

    // Resolves the answer into readable text for chat/logs.
    public string ToDisplayString(QuestionRuntime question) {
        if (SelectedIndex >= 0) {
            var choices = question?.GetChoices();
            if (choices != null && SelectedIndex < choices.Count)
                return choices[SelectedIndex];
            return $"Option {SelectedIndex}";
        }
        return string.IsNullOrEmpty(Text) ? "(no answer)" : Text;
    }
}