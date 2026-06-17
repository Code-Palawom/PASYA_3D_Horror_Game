using System.Collections;
using UnityEngine;

// Base class for all wrong-answer side effects.
// Create new effects by extending this and adding [CreateAssetMenu].
public abstract class QuizSideEffect : ScriptableObject {
    public string effectName;
    [TextArea] public string description;
    public Sprite icon;
    public Color tintColor = Color.red;
    public float duration = 5f;

    // Apply gameplay effect. Only called on the local player's machine.
    public abstract void Apply(GameObject player);

    // Undo the effect after duration. Override if needed.
    public virtual void Remove(GameObject player) { }

    // Full lifecycle: apply → wait → remove.
    // isLocalPlayer controls whether gameplay effects fire.
    // Billboard is always shown regardless.
    public IEnumerator ApplyWithDuration(GameObject player, bool isLocalPlayer) {
        if (isLocalPlayer) Apply(player);

        SideEffectUIManager.Instance.AddEffect(this, player, isLocalPlayer);

        yield return new WaitForSeconds(duration);

        if (isLocalPlayer) Remove(player);

        SideEffectUIManager.Instance.RemoveEffect(this, player);
    }
}