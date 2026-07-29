using System;
using Unity.Collections;
using Unity.Netcode;

// NGO-serializable struct holding all question data.
// Stored in a NetworkVariable on NetworkedQuizGate so all clients
// see the same question for a given gate.
//
// Strings use FixedString types (Unity.Collections).
// Multi-value fields (choices, keywords) are pipe-separated: "A|B|C|D"
public struct NetworkedQuestionData : INetworkSerializable, IEquatable<NetworkedQuestionData> {
    // ── Common ────────────────────────────────────────────────
    public int QuestionTypeInt;
    public int DifficultyInt;
    public FixedString512Bytes QuestionText;
    public float TimeLimit;
    public int PointValue;
    public FixedString512Bytes Description; // optional, shown in feedback toast

    // ── Multiple Choice ───────────────────────────────────────
    public FixedString512Bytes Choices;           // pipe-separated: "A|B|C|D"
    public int CorrectChoiceIndex;

    // ── True or False ─────────────────────────────────────────
    public bool AnswerBool; // true=True, false=False

    // ── Fill in the Blank ─────────────────────────────────────
    public FixedString512Bytes CorrectAnswer;
    public FixedString512Bytes AlternativeAnswers; // pipe-separated

    // ── Short Answer ──────────────────────────────────────────
    public FixedString512Bytes AcceptableAnswers;  // pipe-separated
    public FixedString512Bytes RequiredKeywords;   // pipe-separated
    public int RequiredKeywordCount;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref QuestionTypeInt);
        serializer.SerializeValue(ref DifficultyInt);
        serializer.SerializeValue(ref QuestionText);
        serializer.SerializeValue(ref TimeLimit);
        serializer.SerializeValue(ref PointValue);
        serializer.SerializeValue(ref Choices);
        serializer.SerializeValue(ref CorrectChoiceIndex);
        serializer.SerializeValue(ref AnswerBool);
        serializer.SerializeValue(ref CorrectAnswer);
        serializer.SerializeValue(ref AlternativeAnswers);
        serializer.SerializeValue(ref AcceptableAnswers);
        serializer.SerializeValue(ref RequiredKeywords);
        serializer.SerializeValue(ref RequiredKeywordCount);
        serializer.SerializeValue(ref Description);
    }

    public bool Equals(NetworkedQuestionData other) =>
        QuestionText.Equals(other.QuestionText) &&
        QuestionTypeInt == other.QuestionTypeInt;

    // ─────────────────────────────────────────────────────────
    // Convert to/from QuestionRuntime
    // ─────────────────────────────────────────────────────────

    public static NetworkedQuestionData FromRuntime(QuestionRuntime q) {
        var data = new NetworkedQuestionData {
            QuestionTypeInt = (int)q.questionType,
            DifficultyInt = (int)q.difficulty,
            QuestionText = new FixedString512Bytes(q.questionText ?? ""),
            TimeLimit = q.timeLimit,
            PointValue = q.pointValue,
            Description = new FixedString512Bytes(q.description ?? ""),
        };

        switch (q.questionType) {
            case global::QuestionType.MultipleChoice:
                data.Choices = new FixedString512Bytes(Join(q.GetChoices()));
                data.CorrectChoiceIndex = q.correctChoiceIndex;
                break;

            case global::QuestionType.TrueOrFalse:
                data.AnswerBool = q.answerBool;
                data.CorrectChoiceIndex = q.answerBool ? 0 : 1;
                break;

            case global::QuestionType.FillInTheBlank:
                data.CorrectAnswer = new FixedString512Bytes(q.correctAnswer ?? "");
                data.AlternativeAnswers = new FixedString512Bytes(Join(q.alternativeAnswers));
                break;

            case global::QuestionType.ShortAnswer:
                data.AcceptableAnswers = new FixedString512Bytes(Join(q.acceptableAnswers));
                data.RequiredKeywords = new FixedString512Bytes(Join(q.requiredKeywords));
                data.RequiredKeywordCount = q.requiredKeywordCount;
                break;
        }

        return data;
    }

    public QuestionRuntime ToRuntime() {
        var r = new QuestionRuntime {
            questionText = QuestionText.ToString(),
            questionType = (global::QuestionType)QuestionTypeInt,
            difficulty = (QuestionDifficulty)DifficultyInt,
            timeLimit = TimeLimit,
            pointValue = PointValue,
            description = Description.ToString(),
        };

        switch ((global::QuestionType)QuestionTypeInt) {
            case global::QuestionType.MultipleChoice:
                r.choices = Split(Choices.ToString());
                r.correctChoiceIndex = CorrectChoiceIndex;
                break;

            case global::QuestionType.TrueOrFalse:
                r.answerBool = AnswerBool;
                r.correctChoiceIndex = AnswerBool ? 0 : 1;
                break;

            case global::QuestionType.FillInTheBlank:
                r.correctAnswer = CorrectAnswer.ToString();
                r.alternativeAnswers = Split(AlternativeAnswers.ToString());
                break;

            case global::QuestionType.ShortAnswer:
                r.acceptableAnswers = Split(AcceptableAnswers.ToString());
                r.requiredKeywords = Split(RequiredKeywords.ToString());
                r.requiredKeywordCount = RequiredKeywordCount;
                break;
        }

        return r;
    }

    // ─────────────────────────────────────────────────────────
    static string Join(System.Collections.Generic.List<string> list) =>
        list == null ? "" : string.Join("|", list);

    static System.Collections.Generic.List<string> Split(string s) {
        var result = new System.Collections.Generic.List<string>();
        if (string.IsNullOrEmpty(s)) return result;
        foreach (var part in s.Split('|'))
            if (!string.IsNullOrWhiteSpace(part)) result.Add(part);
        return result;
    }
}