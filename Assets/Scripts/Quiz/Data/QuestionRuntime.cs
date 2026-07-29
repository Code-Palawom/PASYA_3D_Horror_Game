using System.Collections.Generic;

// Universal runtime question class.
// Used by everything at runtime — canvas, evaluator, manager.
// Created from ScriptableObjects via QuestionData.ToRuntime()
// or from Firestore via QuizFetcher.
[System.Serializable]
public class QuestionRuntime {
    // ── Common ────────────────────────────────────────────────
    public string questionText;
    public QuestionType questionType;
    public QuestionDifficulty difficulty;
    public float timeLimit = 15f;
    public int pointValue = 100;
    public string description = ""; // optional, shown in feedback toast

    // ── Multiple Choice ───────────────────────────────────────
    public List<string> choices = new();
    public int correctChoiceIndex;

    // ── True or False ─────────────────────────────────────────
    public bool answerBool; // true=True, false=False
                            // correctChoiceIndex is also set: 0=True, 1=False

    // ── Fill in the Blank ─────────────────────────────────────
    public string correctAnswer;
    public List<string> alternativeAnswers = new();

    // ── Short Answer ──────────────────────────────────────────
    public List<string> acceptableAnswers = new();
    public List<string> requiredKeywords = new();
    public int requiredKeywordCount = 1;

    // Returns display choices.
    // TrueOrFalse always returns ["True", "False"].
    public List<string> GetChoices() {
        if (questionType == QuestionType.TrueOrFalse)
            return new List<string> { "True", "False" };
        return choices;
    }
}