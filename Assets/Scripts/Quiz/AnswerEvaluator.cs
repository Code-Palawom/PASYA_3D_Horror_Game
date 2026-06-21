using System.Linq;
using UnityEngine;

public static class AnswerEvaluator {
    public static bool Evaluate(QuestionRuntime question, QuizAnswer answer) {
        return question.questionType switch {
            QuestionType.MultipleChoice => answer.SelectedIndex == question.correctChoiceIndex,
            QuestionType.TrueOrFalse => answer.SelectedIndex == question.correctChoiceIndex,
            QuestionType.FillInTheBlank => EvaluateFillInBlank(question, answer),
            QuestionType.ShortAnswer => EvaluateShortAnswer(question, answer),
            _ => false
        };
    }

    static bool EvaluateFillInBlank(QuestionRuntime q, QuizAnswer a) {
        if (string.IsNullOrWhiteSpace(a.Text)) return false;
        string submitted = a.Text.Trim().ToLowerInvariant();

        if (string.Equals(submitted, q.correctAnswer?.Trim(), System.StringComparison.OrdinalIgnoreCase))
            return true;

        return q.alternativeAnswers?.Any(alt =>
            string.Equals(submitted, alt?.Trim(), System.StringComparison.OrdinalIgnoreCase)) ?? false;
    }

    static bool EvaluateShortAnswer(QuestionRuntime q, QuizAnswer a) {
        if (string.IsNullOrWhiteSpace(a.Text)) return false;
        string submitted = a.Text.Trim().ToLowerInvariant();

        if (q.acceptableAnswers?.Count > 0)
            if (q.acceptableAnswers.Any(p => submitted.Contains(p.Trim().ToLowerInvariant())))
                return true;

        if (q.requiredKeywords?.Count > 0) {
            int matched = q.requiredKeywords.Count(k =>
                submitted.Contains(k.Trim().ToLowerInvariant()));
            return matched >= Mathf.Max(1, q.requiredKeywordCount);
        }

        return false;
    }
}