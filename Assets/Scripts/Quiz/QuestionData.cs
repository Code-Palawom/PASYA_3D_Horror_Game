using System.Collections.Generic;
using UnityEngine;

public enum QuestionDifficulty { Easy, Medium, Hard }

public enum QuestionType {
    MultipleChoice,     // A B C D — one correct index
    TrueOrFalse,        // True / False — index 0 or 1
    FillInTheBlank,     // typed answer, exact/close match
    ShortAnswer         // typed answer, keyword or acceptable-phrase match
}

[CreateAssetMenu(menuName = "Quiz/Question")]
public class QuestionData : ScriptableObject {
    [Header("Common")]
    public string questionText;
    public QuestionType questionType;
    public QuestionDifficulty difficulty;
    public float timeLimit = 15f;
    public int pointValue = 100;
    public Sprite questionImage;                // optional visual

    // Multiple Choice
    [Header("Multiple Choice / True or False")]
    [Tooltip("4 entries for MultipleChoice. Leave empty for TrueOrFalse (auto-filled).")]
    public string[] choices;
    public int correctChoiceIndex;

    // Fill in the Blank
    [Header("Fill in the Blank")]
    [Tooltip("The exact correct word or phrase (case-insensitive).")]
    public string correctAnswer;

    [Tooltip("Extra spellings or synonyms also accepted as correct.")]
    public List<string> alternativeAnswers;

    // Short Answer
    [Header("Short Answer")]
    [Tooltip("Full acceptable responses. Any one match = correct.")]
    public List<string> acceptableAnswers;

    [Tooltip("Keywords that must appear in the response.")]
    public List<string> requiredKeywords;

    [Tooltip("How many of the above keywords must be present to count as correct.")]
    [Min(1)] public int requiredKeywordCount = 1;

    // Helper: returns display choices for TrueOrFalse
    public string[] GetChoices() {
        if (questionType == QuestionType.TrueOrFalse)
            return new[] { "True", "False" };
        return choices;
    }
}