using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Quiz/QuizSet")]
public class QuizSetData : ScriptableObject {
    public string setName;
    public List<QuestionData> questions;

    // Returns a random question index (within the full list) that matches the given difficulty.
    // Returns -1 if none found.
    public int GetRandomIndexByDifficulty(QuestionDifficulty difficulty) {
        var filtered = questions
            .Select((q, i) => (q, i))
            .Where(x => x.q.difficulty == difficulty)
            .ToList();

        if (filtered.Count == 0) {
            Debug.LogWarning($"[QuizSetData] No questions found for difficulty '{difficulty}' in set '{setName}'. Falling back to any.");
            filtered = questions.Select((q, i) => (q, i)).ToList();
        }

        if (filtered.Count == 0) return -1;

        return filtered[Random.Range(0, filtered.Count)].i;
    }

    public QuestionData GetByIndex(int index) {
        if (index < 0 || index >= questions.Count) return null;
        return questions[index];
    }
}