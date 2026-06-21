using System.Collections.Generic;
using UnityEngine;

// Global registry of all side effects. Assign the same asset on all prefabs.
// Used to reference effects by index over the network.
[CreateAssetMenu(menuName = "Quiz/SideEffectRegistry")]
public class SideEffectRegistry : ScriptableObject {
    public List<QuizSideEffect> effects;

    public int IndexOf(QuizSideEffect effect) => effects.IndexOf(effect);

    public QuizSideEffect GetByIndex(int index) {
        if (index < 0 || index >= effects.Count) return null;
        return effects[index];
    }
}