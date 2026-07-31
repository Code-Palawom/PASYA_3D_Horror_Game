using PrimeTween;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
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

    [Tooltip("If false, this step won't display automatically after the previous one completes. " +
             "Call TutorialManager.Instance.BeginStep(id) to reveal it when you're ready (e.g. from a trigger).")]
    public bool autoStart = true;
}

public class TutorialManager : MonoBehaviour {
    public static TutorialManager Instance { get; private set; }

    public List<TutorialStep> steps;
    public TutorialSpotlight spotlight;
    public TutorialDialog dialog;

    [Header("Timing")]
    public float delayBetweenSteps = 0.5f; // seconds to wait after a step completes, before showing the next

    [SerializeField] GameObject completeTutorialPanel;

    int currentIndex = -1;
    bool completionRequested;   // task done, but waiting on the pulse to finish
    bool awaitingManualStart;   // sitting on a non-auto step, waiting for BeginStep()

    // Steps requested (begin or complete) before the tutorial actually reached
    // them yet — e.g. an item picked up early, on a step several steps ahead
    // of the one currently showing. Applied automatically the moment that
    // step becomes current.
    readonly HashSet<string> pendingBegins = new HashSet<string>();
    readonly HashSet<string> pendingCompletions = new HashSet<string>();

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

    void Start() => StartCoroutine(WaitForLocalPlayer());

    IEnumerator WaitForLocalPlayer() {
        // Wait until Netcode has spawned the local player's object.
        while (NetworkManager.Singleton == null
               || NetworkManager.Singleton.LocalClient == null
               || NetworkManager.Singleton.LocalClient.PlayerObject == null) {
            yield return null;
        }

        yield return new WaitForSeconds(delayBetweenSteps);
        AdvanceToNextStep();
    }

    // Moves the index to the next step. If that step is set to autoStart, it
    // displays immediately; otherwise it waits until BeginStep(id) is called
    // externally (e.g. from a collider trigger) — unless that step was
    // already requested early, in which case it displays right away.
    void AdvanceToNextStep() {
        currentIndex++;
        if (currentIndex >= steps.Count) {
            //gameObject.SetActive(false);
            CompleteTutorial();
            return;
        }

        var step = steps[currentIndex];

        if (!step.autoStart) {
            if (pendingBegins.Remove(step.id)) {
                // Was already requested early (e.g. item picked up before we got here)
                DisplayStep(step);
            } else {
                awaitingManualStart = true;
            }
            return;
        }

        DisplayStep(step);
    }

    // Call this to reveal a step that has autoStart = false. If the tutorial
    // hasn't reached that step yet, the request is remembered and applied
    // automatically once it does.
    public void BeginStep(string id) {
        bool isCurrentAndWaiting = awaitingManualStart
            && currentIndex >= 0 && currentIndex < steps.Count
            && steps[currentIndex].id == id;

        if (isCurrentAndWaiting) {
            awaitingManualStart = false;
            DisplayStep(steps[currentIndex]);
        } else {
            pendingBegins.Add(id);
        }
    }

    void DisplayStep(TutorialStep step) {
        completionRequested = false;

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
                if (pendingCompletions.Remove(step.id))
                    RequestCompletion(); // was already requested early
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
    // waits delayBetweenSteps, then reveals (or arms) the next step.
    void CompleteCurrentStep() {
        dialog.Hide();
        spotlight.HideHighlight();
        Tween.Delay(delayBetweenSteps, AdvanceToNextStep);
    }

    void OnButtonStepDone() {
        var step = steps[currentIndex];
        step.targetButton.onClick.RemoveListener(OnButtonStepDone);
        RequestCompletion();
    }

    // Marks a CustomEvent step as complete. If the tutorial hasn't reached
    // that step yet, the request is remembered and applied automatically
    // the moment that step becomes current.
    public void CompleteCustomStep(string id) {
        bool isCurrent = currentIndex >= 0 && currentIndex < steps.Count && steps[currentIndex].id == id;

        if (isCurrent)
            RequestCompletion();
        else
            pendingCompletions.Add(id);
    }

    private void CompleteTutorial() {
        completeTutorialPanel.SetActive(true);
    }

    public void ExitTutorial() {
        SettingsManager.Instance.Save(s => s.completedTutorial = true);
        completeTutorialPanel.SetActive(false);
        NetworkSessionManager.Instance.LeaveSession();
    }
}