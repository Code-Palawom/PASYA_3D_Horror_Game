using PrimeTween;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum CompletionType { ButtonClick, CustomEvent, DialogClick }

[System.Serializable]
public class TutorialStep {
    public string id;
    public RectTransform highlightTarget;
    [TextArea] public string instructionText;
    public CompletionType completionType;
    public Button targetButton;   // used only if ButtonClick
    public int pulseCount = 3;    // how many times the dim panels pulse on this step
    public DialogPosition dialogPosition = DialogPosition.Auto; // Auto = relative to highlightTarget
}

public class TutorialManager : MonoBehaviour {
    public static TutorialManager Instance { get; private set; }

    public List<TutorialStep> steps;
    public TutorialSpotlight spotlight;
    public TutorialDialog dialog;

    [Header("Timing")]
    public float delayBetweenSteps = 0.5f; // seconds to wait after a step completes, before showing the next

    int currentIndex = -1;
    bool completionRequested; // task done, but waiting on the pulse to finish

    void Awake() {
        if (Instance != null && Instance != this) {
            Debug.LogWarning("Multiple TutorialManager instances found, destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        spotlight.OnPulseComplete += HandlePulseComplete;
    }

    void OnDestroy() {
        spotlight.OnPulseComplete -= HandlePulseComplete;
    }

    void Start() => StartCoroutine(StartTutorial());

    IEnumerator StartTutorial() {
        yield return new WaitForSeconds(delayBetweenSteps);

        ShowStep();
    }

    // Displays the current step's highlight and completion requirement.
    // Call this to show the very first step; every step after that is
    // advanced to automatically via CompleteCurrentStep().
    void ShowStep() {
        currentIndex++;
        if (currentIndex >= steps.Count) {
            gameObject.SetActive(false);
            return;
        }

        completionRequested = false;

        var step = steps[currentIndex];
        spotlight.Highlight(step.highlightTarget);
        spotlight.Pulse(step.pulseCount);

        bool clickToContinue = step.completionType == CompletionType.DialogClick;
        dialog.Show(step.instructionText, step.highlightTarget, step.dialogPosition, clickToContinue,
            clickToContinue ? RequestCompletion : null);

        switch (step.completionType) {
            case CompletionType.ButtonClick:
                step.targetButton.onClick.AddListener(OnButtonStepDone);
                break;

            case CompletionType.CustomEvent:
                // External gameplay code should call CompleteCustomStep(step.id)
                break;
        }
    }

    // Called when the step's task is done (button clicked, dialog clicked, or
    // custom event fired). If the pulse is still animating, waits for it to
    // finish before actually advancing; otherwise advances immediately.
    void RequestCompletion() {
        if (completionRequested) return;
        completionRequested = true;

        if (!spotlight.IsPulsing)
            CompleteCurrentStep();
        // else: HandlePulseComplete will pick this up once the pulse finishes
    }

    void HandlePulseComplete() {
        if (completionRequested)
            CompleteCurrentStep();
    }

    // Actually advances: hides the dialog and the highlight cutout immediately,
    // waits delayBetweenSteps, then reveals the next step.
    void CompleteCurrentStep() {
        dialog.Hide();
        spotlight.HideHighlight();
        Tween.Delay(delayBetweenSteps, ShowStep);
    }

    void OnButtonStepDone() {
        var step = steps[currentIndex];
        step.targetButton.onClick.RemoveListener(OnButtonStepDone);
        RequestCompletion();
    }

    public void CompleteCustomStep(string id) {
        if (currentIndex >= 0 && currentIndex < steps.Count && steps[currentIndex].id == id)
            RequestCompletion();
    }
}