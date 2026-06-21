using System.Collections.Generic;

// JSON-serializable wrapper for quiz sets.
//
// The endpoint returns a TOP-LEVEL ARRAY of quiz sets:
// [ { "name": "...", "questions": [...] }, { "name": "...", "questions": [...] } ]
//
// JsonUtility does not support top-level arrays, so we wrap it:
//   string wrapped = $"{{\"items\":{json}}}";
//   var result = JsonUtility.FromJson<QuizSetArrayWrapper>(wrapped);
//
// QuestionType: 0=MultipleChoice 1=TrueOrFalse 2=FillInTheBlank 3=ShortAnswer
// Difficulty:   0=Easy 1=Medium 2=Hard

// ── Array wrapper (top-level) ─────────────────────────────────
[System.Serializable]
public class QuizSetArrayWrapper {
    public List<QuizSetJsonWrapper> items;
}

// ── Single quiz set ───────────────────────────────────────────
[System.Serializable]
public class QuizSetJsonWrapper {
    public string name;
    public List<QuestionJsonEntry> questions;

    // ── Single question ───────────────────────────────────────
    [System.Serializable]
    public class QuestionJsonEntry {
        public string questionText;
        public int questionType;
        public int difficulty;
        public float timeLimit = 15f;
        public int pointValue = 100;
        public List<string> choices = new();
        public int correctChoiceIndex;
        public string correctAnswer = "";
        public List<string> alternativeAnswers = new();
        public List<string> acceptableAnswers = new();
        public List<string> requiredKeywords = new();
        public int requiredKeywordCount = 1;
    }

    // ─────────────────────────────────────────────────────────
    public QuizSetRuntime ToRuntime() {
        var set = new QuizSetRuntime { name = name };

        if (questions == null) return set;

        foreach (var entry in questions) {
            set.questions.Add(new QuestionRuntime {
                questionText = entry.questionText,
                questionType = (QuestionType)entry.questionType,
                difficulty = (QuestionDifficulty)entry.difficulty,
                timeLimit = entry.timeLimit,
                pointValue = entry.pointValue,
                choices = entry.choices ?? new(),
                correctChoiceIndex = entry.correctChoiceIndex,
                correctAnswer = entry.correctAnswer ?? "",
                alternativeAnswers = entry.alternativeAnswers ?? new(),
                acceptableAnswers = entry.acceptableAnswers ?? new(),
                requiredKeywords = entry.requiredKeywords ?? new(),
                requiredKeywordCount = entry.requiredKeywordCount
            });
        }

        return set;
    }

    public static QuizSetJsonWrapper FromRuntime(QuizSetRuntime set) {
        var wrapper = new QuizSetJsonWrapper { name = set.name, questions = new() };

        foreach (var q in set.questions) {
            wrapper.questions.Add(new QuestionJsonEntry {
                questionText = q.questionText,
                questionType = (int)q.questionType,
                difficulty = (int)q.difficulty,
                timeLimit = q.timeLimit,
                pointValue = q.pointValue,
                choices = q.choices ?? new(),
                correctChoiceIndex = q.correctChoiceIndex,
                correctAnswer = q.correctAnswer ?? "",
                alternativeAnswers = q.alternativeAnswers ?? new(),
                acceptableAnswers = q.acceptableAnswers ?? new(),
                requiredKeywords = q.requiredKeywords ?? new(),
                requiredKeywordCount = q.requiredKeywordCount
            });
        }

        return wrapper;
    }
}