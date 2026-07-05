using System.Collections.Generic;

// JSON-serializable wrapper for quiz sets.
// Used for local disk save/load of fetched Firestore sets.
//
// Per-type required fields:
//   MultipleChoice  → choices, correctChoiceIndex
//   TrueOrFalse     → answer (bool)
//   FillInTheBlank  → correctAnswer, alternativeAnswers
//   ShortAnswer     → acceptableAnswers, requiredKeywords, requiredKeywordCount
//
// QuestionType: 0=MultipleChoice 1=TrueOrFalse 2=FillInTheBlank 3=ShortAnswer
// Difficulty:   0=Easy 1=Medium 2=Hard

[System.Serializable]
public class QuizSetJsonWrapper {
    public string name;
    public List<QuestionJsonEntry> questions;

    [System.Serializable]
    public class QuestionJsonEntry {
        // ── Common ────────────────────────────────────────────
        public string questionText;
        public int questionType;
        public int difficulty;
        public float timeLimit = 15f;
        public int pointValue = 100;

        // ── Multiple Choice ───────────────────────────────────
        public List<string> choices = new();
        public int correctChoiceIndex;

        // ── True or False ─────────────────────────────────────
        public bool answerBool;

        // ── Fill in the Blank ─────────────────────────────────
        public string correctAnswer = "";
        public List<string> alternativeAnswers = new();

        // ── Short Answer ──────────────────────────────────────
        public List<string> acceptableAnswers = new();
        public List<string> requiredKeywords = new();
        public int requiredKeywordCount = 1;
    }

    // ─────────────────────────────────────────────────────────
    public QuizSetRuntime ToRuntime() {
        var set = new QuizSetRuntime { name = name };
        if (questions == null) return set;

        foreach (var e in questions) {
            var r = new QuestionRuntime {
                questionText = e.questionText,
                questionType = (QuestionType)e.questionType,
                difficulty = (QuestionDifficulty)e.difficulty,
                timeLimit = e.timeLimit,
                pointValue = e.pointValue,
            };

            switch ((QuestionType)e.questionType) {
                case QuestionType.MultipleChoice:
                    r.choices = e.choices ?? new List<string>();
                    r.correctChoiceIndex = e.correctChoiceIndex;
                    break;

                case QuestionType.TrueOrFalse:
                    r.answerBool = e.answerBool;
                    r.correctChoiceIndex = e.answerBool ? 0 : 1;
                    break;

                case QuestionType.FillInTheBlank:
                    r.correctAnswer = e.correctAnswer ?? "";
                    r.alternativeAnswers = e.alternativeAnswers ?? new List<string>();
                    break;

                case QuestionType.ShortAnswer:
                    r.acceptableAnswers = e.acceptableAnswers ?? new List<string>();
                    r.requiredKeywords = e.requiredKeywords ?? new List<string>();
                    r.requiredKeywordCount = e.requiredKeywordCount;
                    break;
            }

            set.questions.Add(r);
        }

        return set;
    }

    public static QuizSetJsonWrapper FromRuntime(QuizSetRuntime set) {
        var wrapper = new QuizSetJsonWrapper { name = set.name, questions = new List<QuestionJsonEntry>() };

        foreach (var q in set.questions) {
            var e = new QuestionJsonEntry {
                questionText = q.questionText,
                questionType = (int)q.questionType,
                difficulty = (int)q.difficulty,
                timeLimit = q.timeLimit,
                pointValue = q.pointValue,
            };

            switch (q.questionType) {
                case QuestionType.MultipleChoice:
                    e.choices = q.choices ?? new List<string>();
                    e.correctChoiceIndex = q.correctChoiceIndex;
                    break;

                case QuestionType.TrueOrFalse:
                    e.answerBool = q.answerBool;
                    break;

                case QuestionType.FillInTheBlank:
                    e.correctAnswer = q.correctAnswer ?? "";
                    e.alternativeAnswers = q.alternativeAnswers ?? new List<string>();
                    break;

                case QuestionType.ShortAnswer:
                    e.acceptableAnswers = q.acceptableAnswers ?? new List<string>();
                    e.requiredKeywords = q.requiredKeywords ?? new List<string>();
                    e.requiredKeywordCount = q.requiredKeywordCount;
                    break;
            }

            wrapper.questions.Add(e);
        }

        return wrapper;
    }
}