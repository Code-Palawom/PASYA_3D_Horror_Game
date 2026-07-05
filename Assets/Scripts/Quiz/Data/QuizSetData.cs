using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Quiz/QuizSet")]
public class QuizSetData : ScriptableObject {
    [Header("Set Info")]
    public string setName;
    public string category;

    [Header("Author")]
    public string authorId;
    public string authorName;

    [Header("Questions")]
    public List<QuestionData> questions;

    public QuizSetRuntime ToRuntime() {
        var runtime = new QuizSetRuntime {
            name = setName,
            source = QuizSetRuntime.SourceType.Local
            // category, questionCount, playCount left at defaults (0/"")
            // — local SOs are not tracked in Firestore _meta
        };

        foreach (var q in questions)
            if (q != null) runtime.questions.Add(q.ToRuntime());

        return runtime;
    }

    /// <summary>Converts this SO to a QuizSetMetaEntry for menu display.</summary>
    public QuizSetMetaEntry ToMetaEntry() {
        return new QuizSetMetaEntry {
            setId = name,    // use asset name as setId for local sets
            name = setName,
            category = category,
            questionCount = questions?.Count ?? 0,
            playCount = 0,
            lastUpdated = 0,
            hasLocalData = true,    // always available, no download needed
            isVerified = true,    // local SOs skip isVerified check
            authorId = authorId,
            authorName = authorName
        };
    }
}