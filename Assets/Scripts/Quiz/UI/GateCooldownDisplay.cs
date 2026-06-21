using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// World-space countdown display that sits above a gate object.
// Shows remaining cooldown time to all players after a wrong answer.

// Setup: add a World Space Canvas as a child of the gate,
// build a panel with TMP_Text + Image fill bar, assign here.
public class GateCooldownDisplay : MonoBehaviour {
    [Header("UI References")]
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text countdownLabel;
    [SerializeField] Image fillBar;            // Filled, Horizontal
    [SerializeField] TMP_Text lockedLabel;        // e.g. "Locked"

    [Header("Colors")]
    [SerializeField] Color fullColor = Color.red;
    [SerializeField] Color emptyColor = Color.grey;

    private float _totalDuration;
    private float _endTime;
    private bool _active;

    // ─────────────────────────────────────────────────────────
    // Called by NetworkedQuizGate when _cooldownEndTime changes
    // ─────────────────────────────────────────────────────────
    public void SetCooldown(bool active, float remaining, float total) {
        _active = active;
        _totalDuration = total;
        _endTime = Time.time + remaining;

        panel.SetActive(active);

        if (!active) {
            if (countdownLabel != null) countdownLabel.text = "";
            if (fillBar != null) fillBar.fillAmount = 0f;
        }
    }

    // ─────────────────────────────────────────────────────────
    void Update() {
        if (!_active) return;

        float remaining = Mathf.Max(0f, _endTime - Time.time);
        float t = _totalDuration > 0 ? remaining / _totalDuration : 0f;

        if (countdownLabel != null)
            countdownLabel.text = $"{remaining:F1}s";

        if (fillBar != null) {
            fillBar.fillAmount = t;
            fillBar.color = Color.Lerp(emptyColor, fullColor, t);
        }

        // Hide when expired (server clears NetworkVariable, but handle locally too)
        if (remaining <= 0f) {
            _active = false;
            panel.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────
    // Always face camera
    // ─────────────────────────────────────────────────────────
    void LateUpdate() {
        if (!_active || Camera.main == null) return;
        transform.LookAt(Camera.main.transform);
        transform.Rotate(0, 180f, 0);
    }
}