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
    public Sprite questionImage;

    [Header("Multiple Choice / True or False")]
    public List<string> choices;
    public int correctChoiceIndex;

    [Header("Fill in the Blank")]
    public string correctAnswer;
    public List<string> alternativeAnswers;

    [Header("Short Answer")]
    public List<string> acceptableAnswers;
    public List<string> requiredKeywords;
    [Min(1)] public int requiredKeywordCount = 1;

    public QuestionRuntime ToRuntime() {
        return new QuestionRuntime {
            questionText = questionText,
            questionType = questionType,
            difficulty = difficulty,
            timeLimit = timeLimit,
            pointValue = pointValue,
            choices = questionType == QuestionType.TrueOrFalse
                                   ? new List<string> { "True", "False" }
                                   : new List<string>(choices ?? new List<string>()),
            correctChoiceIndex = correctChoiceIndex,
            correctAnswer = correctAnswer ?? "",
            alternativeAnswers = new List<string>(alternativeAnswers ?? new List<string>()),
            acceptableAnswers = new List<string>(acceptableAnswers ?? new List<string>()),
            requiredKeywords = new List<string>(requiredKeywords ?? new List<string>()),
            requiredKeywordCount = requiredKeywordCount
        };
    }
}