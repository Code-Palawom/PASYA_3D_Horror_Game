using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Quiz/QuizSet")]
public class QuizSetData : ScriptableObject {
    public string setName;
    public List<QuestionData> questions;

    public QuizSetRuntime ToRuntime() {
        var runtime = new QuizSetRuntime { name = setName };
        foreach (var q in questions)
            if (q != null) runtime.questions.Add(q.ToRuntime());
        return runtime;
    }
}