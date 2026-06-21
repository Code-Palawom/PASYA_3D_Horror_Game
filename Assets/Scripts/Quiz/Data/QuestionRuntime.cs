using System.Collections.Generic;

// Universal runtime question class.
// Used by everything at runtime — canvas, evaluator, manager.
// Created from ScriptableObjects via QuestionData.ToRuntime()
// or from JSON via QuizFetcher.
[System.Serializable]
public class QuestionRuntime {
    public string questionText;
    public QuestionType questionType;
    public QuestionDifficulty difficulty;
    public float timeLimit = 15f;
    public int pointValue = 100;

    // Multiple Choice / True or False
    public List<string> choices = new();
    public int correctChoiceIndex;

    // Fill in the Blank
    public string correctAnswer;
    public List<string> alternativeAnswers = new();

    // Short Answer
    public List<string> acceptableAnswers = new();
    public List<string> requiredKeywords = new();
    public int requiredKeywordCount = 1;

    // Returns display choices.
    // TrueOrFalse auto-returns ["True","False"] if choices list is empty.
    public List<string> GetChoices() {
        if (questionType == QuestionType.TrueOrFalse &&
            (choices == null || choices.Count == 0))
            return new List<string> { "True", "False" };

        return choices;
    }
}