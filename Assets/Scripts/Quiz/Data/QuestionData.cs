using System.Collections.Generic;
using UnityEngine;

public enum QuestionDifficulty { Easy, Medium, Hard }
public enum QuestionType { MultipleChoice, TrueOrFalse, FillInTheBlank, ShortAnswer }

[CreateAssetMenu(menuName = "Quiz/Question")]
public class QuestionData : ScriptableObject {
    [Header("Common")]
    public string questionText;
    public QuestionType questionType;
    public QuestionDifficulty difficulty;
    public float timeLimit = 15f;
    public int pointValue = 100;

    [Header("Multiple Choice")]
    public List<string> choices;
    public int correctChoiceIndex;

    [Header("True or False")]
    public bool answer; // true = True, false = False

    [Header("Fill in the Blank")]
    public string correctAnswer;
    public List<string> alternativeAnswers;

    [Header("Short Answer")]
    public List<string> acceptableAnswers;
    public List<string> requiredKeywords;
    [Min(1)] public int requiredKeywordCount = 1;

    public QuestionRuntime ToRuntime() {
        var r = new QuestionRuntime {
            questionText = questionText,
            questionType = questionType,
            difficulty = difficulty,
            timeLimit = timeLimit,
            pointValue = pointValue,
        };

        switch (questionType) {
            case QuestionType.MultipleChoice:
                r.choices = new List<string>(choices ?? new List<string>());
                r.correctChoiceIndex = correctChoiceIndex;
                break;

            case QuestionType.TrueOrFalse:
                r.answerBool = answer;
                r.correctChoiceIndex = answer ? 0 : 1; // 0=True, 1=False
                break;

            case QuestionType.FillInTheBlank:
                r.correctAnswer = correctAnswer ?? "";
                r.alternativeAnswers = new List<string>(alternativeAnswers ?? new List<string>());
                break;

            case QuestionType.ShortAnswer:
                r.acceptableAnswers = new List<string>(acceptableAnswers ?? new List<string>());
                r.requiredKeywords = new List<string>(requiredKeywords ?? new List<string>());
                r.requiredKeywordCount = requiredKeywordCount;
                break;
        }

        return r;
    }
}