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
}