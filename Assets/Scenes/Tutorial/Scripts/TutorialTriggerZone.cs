using UnityEngine;

// Drop this on any GameObject with a trigger Collider to advance a tutorial
// step when something enters it. Set the step's CompletionType to CustomEvent
// and give it the same id as stepId here — this just calls
// TutorialManager.Instance.CompleteCustomStep(stepId) under the hood.
[RequireComponent(typeof(Collider))]
public class TutorialTriggerZone : MonoBehaviour {
    [Tooltip("Must match the id of a TutorialStep with CompletionType.CustomEvent")]
    public string stepId;

    [Tooltip("Only objects with this tag trigger completion. Leave empty to accept any collider.")]
    public string requiredTag = "Player";

    [Tooltip("If true, this zone disables itself after firing once so it can't retrigger the step.")]
    public bool triggerOnce = true;

    bool fired;

    void Reset() {
        // Make sure the collider is actually usable as a trigger by default.
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other) {
        if (fired && triggerOnce) return;

        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
            return;

        if (TutorialManager.Instance == null) {
            Debug.LogWarning($"TutorialTriggerZone '{name}' fired but no TutorialManager.Instance found.");
            return;
        }

        fired = true;
        TutorialManager.Instance.CompleteCustomStep(stepId);

        if (triggerOnce)
            enabled = false;
    }
}