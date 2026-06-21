using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lives on the PlayerCanvas (screen-space).
// Only shown to the LOCAL player when they are currently answering a gate.
//
// Shows:
//  - "You are currently answering..."
//  - Toggle: Allow other players to interact
public class InteractionStatusUI : MonoBehaviour {
    [Header("References")]
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text statusLabel;
    [SerializeField] Toggle allowOthersToggle;
    [SerializeField] TMP_Text allowOthersLabel;

    private NetworkedQuizGate _currentGate;

    // ─────────────────────────────────────────────────────────
    void Awake() {
        panel.SetActive(false);

        // Wire toggle — fires when player flips it
        allowOthersToggle.onValueChanged.RemoveAllListeners();
        allowOthersToggle.onValueChanged.AddListener(OnAllowOthersToggled);

        // Subscribe to QuizManager events
        QuizManager.OnQuizStarted += OnQuizStarted;
        QuizManager.OnQuizEnded += OnQuizEnded;
    }

    void OnDestroy() {
        QuizManager.OnQuizStarted -= OnQuizStarted;
        QuizManager.OnQuizEnded -= OnQuizEnded;
    }

    // ─────────────────────────────────────────────────────────
    // QuizManager events
    // ─────────────────────────────────────────────────────────
    void OnQuizStarted(NetworkedQuizGate gate) {
        _currentGate = gate;

        panel.SetActive(true);
        statusLabel.text = "You are currently answering a question.";
        allowOthersToggle.isOn = gate.AllowOthers;
        allowOthersLabel.text = "Allow others to interact";
    }

    void OnQuizEnded(bool wasCorrect) {
        _currentGate = null;
        panel.SetActive(false);
        allowOthersToggle.isOn = false;
    }

    // ─────────────────────────────────────────────────────────
    void OnAllowOthersToggled(bool allow) {
        _currentGate?.RequestSetAllowOthers(allow);
        allowOthersLabel.text = allow
            ? "Others can now interact"
            : "Allow others to interact";
    }
}