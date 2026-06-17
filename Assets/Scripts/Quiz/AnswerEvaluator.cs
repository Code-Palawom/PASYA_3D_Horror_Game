using System;
using System.Linq;
using UnityEngine;

// Stateless evaluator. Determines whether a QuizAnswer is correct
// for a given QuestionData based on its QuestionType.
public static class AnswerEvaluator {
    public static bool Evaluate(QuestionData question, QuizAnswer answer) {
        return question.questionType switch {
            QuestionType.MultipleChoice => EvaluateMultipleChoice(question, answer),
            QuestionType.TrueOrFalse => EvaluateTrueOrFalse(question, answer),
            QuestionType.FillInTheBlank => EvaluateFillInTheBlank(question, answer),
            QuestionType.ShortAnswer => EvaluateShortAnswer(question, answer),
            _ => false
        };
    }

    // Multiple Choice
    static bool EvaluateMultipleChoice(QuestionData q, QuizAnswer a) =>
        a.SelectedIndex == q.correctChoiceIndex;

    // True or False
    static bool EvaluateTrueOrFalse(QuestionData q, QuizAnswer a) =>
        a.SelectedIndex == q.correctChoiceIndex;   // 0 = True, 1 = False

    // Fill in the Blank
    // Accepts: exact match (case-insensitive, trimmed) or any alternative answer
    static bool EvaluateFillInTheBlank(QuestionData q, QuizAnswer a) {
        if (string.IsNullOrWhiteSpace(a.Text)) return false;

        string submitted = a.Text.Trim().ToLowerInvariant();

        // Check primary correct answer
        if (string.Equals(submitted, q.correctAnswer?.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        // Check alternative answers
        if (q.alternativeAnswers != null) {
            foreach (var alt in q.alternativeAnswers) {
                if (string.Equals(submitted, alt?.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    // Short Answer
    // Two modes:
    //   1. acceptableAnswers defined → any full match wins
    //   2. requiredKeywords defined  → N keywords must be present
    static bool EvaluateShortAnswer(QuestionData q, QuizAnswer a) {
        if (string.IsNullOrWhiteSpace(a.Text)) return false;

        string submitted = a.Text.Trim().ToLowerInvariant();

        // Mode 1: acceptable full phrases
        if (q.acceptableAnswers != null && q.acceptableAnswers.Count > 0) {
            foreach (var phrase in q.acceptableAnswers) {
                if (submitted.Contains(phrase.Trim().ToLowerInvariant()))
                    return true;
            }
        }

        // Mode 2: keyword presence
        if (q.requiredKeywords != null && q.requiredKeywords.Count > 0) {
            int matchCount = q.requiredKeywords.Count(keyword =>
                submitted.Contains(keyword.Trim().ToLowerInvariant()));

            return matchCount >= Mathf.Max(1, q.requiredKeywordCount);
        }

        return false;
    }
}