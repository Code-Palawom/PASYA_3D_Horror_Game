using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lives on PlayerCanvas (screen-space).
// Shows interaction prompt, cooldown countdown, and temporary messages.
// Only visible to the local player.
public class PlayerInteractionUI : MonoBehaviour {
    [Header("Prompt")]
    [SerializeField] GameObject panel;
    [SerializeField] Button interactButton;
    [SerializeField] TMP_Text promptText;
    [SerializeField] GameObject keyHintWrapper;
    [SerializeField] TMP_Text keyHintText;        // e.g. "[E]"

    [Header("Cooldown (replaces prompt text when gate is locked)")]
    [SerializeField] GameObject cooldownRow;
    [SerializeField] TMP_Text cooldownLabel;       // "Locked — 9.5s"
    [SerializeField] Image cooldownBar;         // Filled Horizontal

    [Header("Actionabr Cooldown Indicator")]
    [SerializeField] GameObject cooldownIndicator;
    [SerializeField] TMP_Text cooldownIndicatorLabel;
    [SerializeField] Image cooldownIndicatorBar;

    [Header("Temp Message")]
    [SerializeField] TMP_Text messageText;
    [SerializeField] float messageDuration = 3f;

    private Coroutine _messageClear;
    private float _cooldownEnd;
    private float _cooldownTotal;
    private bool _showingCooldown;

    public bool IsShowingCooldown => _showingCooldown;

    void Awake() => Hide();

    // ─────────────────────────────────────────────────────────
    // Show normal prompt (no cooldown)
    // ─────────────────────────────────────────────────────────
    public void Show(string message, string keyHint = "E") {
        panel.SetActive(true);
        if (_showingCooldown) {
            SetCooldownRowVisible(true);
            return;
        }


        // Prompt text is active, cooldown row is not — no overlap
        promptText.gameObject.SetActive(true);
        interactButton.gameObject.SetActive(true);
        promptText.text = message;

        if (keyHintText.CompareTag("PlatformPC")) {
            keyHintText.gameObject.SetActive(true);
            keyHintText.text = $"[{keyHint}]";
        }

        SetCooldownRowVisible(false);
        _showingCooldown = false;
    }

    // ─────────────────────────────────────────────────────────
    // Show ONLY the cooldown row — prompt text/key hint hidden
    // so "Locked" never appears twice
    // ─────────────────────────────────────────────────────────
    public void ShowWithCooldown(double cooldownRemaining, float cooldownTotal) {
        panel.SetActive(false);
        
        // Hide normal prompt text entirely — cooldown row covers messaging
        interactButton.gameObject.SetActive(false);
        if (keyHintText.CompareTag("PlatformPC")) keyHintWrapper.SetActive(false);

        _cooldownEnd = Time.time + (float)cooldownRemaining;
        _cooldownTotal = cooldownTotal;
        _showingCooldown = true;

        SetCooldownRowVisible(true);
    }

    // ─────────────────────────────────────────────────────────
    public void Hide() {
        panel.SetActive(false);
        SetCooldownRowVisible(false);
        //_showingCooldown = false;

        if (messageText != null)
            messageText.text = "";
    }

    // ─────────────────────────────────────────────────────────
    public void ShowMessage(string message) {
        if (messageText == null) return;
        messageText.text = message;

        if (_messageClear != null) StopCoroutine(_messageClear);
        _messageClear = StartCoroutine(ClearMessageAfter(messageDuration));
    }

    IEnumerator ClearMessageAfter(float delay) {
        yield return new WaitForSeconds(delay);
        if (messageText != null) messageText.text = "";
    }

    // ─────────────────────────────────────────────────────────
    void Update() {
        if (!_showingCooldown) return;

        float remaining = Mathf.Max(0f, _cooldownEnd - Time.time);
        float t = _cooldownTotal > 0f ? remaining / _cooldownTotal : 0f;

        cooldownLabel.text = $"Locked — {remaining:F1}s";
        cooldownBar.fillAmount = t;

        cooldownIndicator.SetActive(true);
        cooldownIndicatorLabel.text = $"Cannot interact for {remaining:F1}s";
        cooldownIndicatorBar.rectTransform.localScale = new Vector3(t, 1f, 1f);

        if (remaining <= 0f) {
            _showingCooldown = false;
            SetCooldownRowVisible(false);
            cooldownIndicator.SetActive(false);

            // Restore normal prompt visibility for next Show() call
            interactButton.gameObject.SetActive(true);
            if (keyHintText.CompareTag("PlatformPC")) keyHintWrapper.SetActive(true);
        }
    }

    // ─────────────────────────────────────────────────────────
    void SetCooldownRowVisible(bool visible) {
        cooldownRow.SetActive(visible);
    }

    // ─────────────────────────────────────────────────────────
    public static void ShowMessageForPlayer(GameObject player, string message) {
        player.GetComponentInChildren<PlayerInteractionUI>().ShowMessage(message);
    }
}