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
    public int QuestionType;
    public int Difficulty;
    public FixedString512Bytes QuestionText;
    public FixedString512Bytes Choices;            // pipe-separated: "A|B|C|D"
    public int CorrectChoiceIndex;
    public FixedString128Bytes CorrectAnswer;
    public FixedString512Bytes AlternativeAnswers; // pipe-separated
    public FixedString512Bytes AcceptableAnswers;  // pipe-separated
    public FixedString512Bytes RequiredKeywords;   // pipe-separated
    public int RequiredKeywordCount;
    public float TimeLimit;
    public int PointValue;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref QuestionType);
        serializer.SerializeValue(ref Difficulty);
        serializer.SerializeValue(ref QuestionText);
        serializer.SerializeValue(ref Choices);
        serializer.SerializeValue(ref CorrectChoiceIndex);
        serializer.SerializeValue(ref CorrectAnswer);
        serializer.SerializeValue(ref AlternativeAnswers);
        serializer.SerializeValue(ref AcceptableAnswers);
        serializer.SerializeValue(ref RequiredKeywords);
        serializer.SerializeValue(ref RequiredKeywordCount);
        serializer.SerializeValue(ref TimeLimit);
        serializer.SerializeValue(ref PointValue);
    }

    public bool Equals(NetworkedQuestionData other) =>
        QuestionText.Equals(other.QuestionText) &&
        QuestionType == other.QuestionType;

    // ─────────────────────────────────────────────────────────
    // Convert to/from QuestionRuntime
    // ─────────────────────────────────────────────────────────

    public static NetworkedQuestionData FromRuntime(QuestionRuntime q) {
        return new NetworkedQuestionData {
            QuestionType = (int)q.questionType,
            Difficulty = (int)q.difficulty,
            QuestionText = new FixedString512Bytes(q.questionText ?? ""),
            Choices = new FixedString512Bytes(Join(q.GetChoices())),
            CorrectChoiceIndex = q.correctChoiceIndex,
            CorrectAnswer = new FixedString128Bytes(q.correctAnswer ?? ""),
            AlternativeAnswers = new FixedString512Bytes(Join(q.alternativeAnswers)),
            AcceptableAnswers = new FixedString512Bytes(Join(q.acceptableAnswers)),
            RequiredKeywords = new FixedString512Bytes(Join(q.requiredKeywords)),
            RequiredKeywordCount = q.requiredKeywordCount,
            TimeLimit = q.timeLimit,
            PointValue = q.pointValue
        };
    }

    public QuestionRuntime ToRuntime() {
        return new QuestionRuntime {
            questionText = QuestionText.ToString(),
            questionType = (QuestionType)QuestionType,
            difficulty = (QuestionDifficulty)Difficulty,
            timeLimit = TimeLimit,
            pointValue = PointValue,
            choices = Split(Choices.ToString()),
            correctChoiceIndex = CorrectChoiceIndex,
            correctAnswer = CorrectAnswer.ToString(),
            alternativeAnswers = Split(AlternativeAnswers.ToString()),
            acceptableAnswers = Split(AcceptableAnswers.ToString()),
            requiredKeywords = Split(RequiredKeywords.ToString()),
            requiredKeywordCount = RequiredKeywordCount
        };
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