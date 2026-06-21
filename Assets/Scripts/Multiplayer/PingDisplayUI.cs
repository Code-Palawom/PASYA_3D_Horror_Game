using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reusable ping display — 3 signal bars + ms label below.
// 3 bars = good (<100ms), 2 bars = medium (100-200ms), 1 bar = bad (>200ms).
// Attach this component to any GameObject that needs ping display.
// Used by both LobbyListItemUI (room list row) and RoomDetailPanelUI (detail panel).
public class PingDisplayUI : MonoBehaviour {
    [Header("Bars (tallest to shortest — index 0 = bar 1 leftmost/shortest)")]
    [SerializeField] Image bar1;   // short  — always lit when any signal
    [SerializeField] Image bar2;   // medium — lit when medium or good
    [SerializeField] Image bar3;   // tall   — lit only when good

    [Header("Ms Label")]
    [SerializeField] TMP_Text msLabel;

    [Header("Colors")]
    [SerializeField] Color goodColor = Color.green;
    [SerializeField] Color mediumColor = Color.yellow;
    [SerializeField] Color badColor = Color.red;
    [SerializeField] Color dimColor = new Color(1f, 1f, 1f, 0.2f);

    [Header("Thresholds (ms)")]
    [SerializeField] int goodThreshold = 100;   // <100ms  → 3 bars
    [SerializeField] int mediumThreshold = 200;   // <200ms  → 2 bars, >200ms → 1 bar

    // ─────────────────────────────────────────────────────────
    public void SetPing(int pingMs) {
        if (pingMs < 0) {
            // Unknown / not measured yet
            SetBars(0, Color.white);
            if (msLabel != null) msLabel.text = "—";
            return;
        }

        Color barColor;
        int activeBars;

        if (pingMs < goodThreshold) {
            activeBars = 3;
            barColor = goodColor;
        } else if (pingMs < mediumThreshold) {
            activeBars = 2;
            barColor = mediumColor;
        } else {
            activeBars = 1;
            barColor = badColor;
        }

        SetBars(activeBars, barColor);

        if (msLabel != null)
            msLabel.text = $"{pingMs}ms";
    }

    void SetBars(int activeBars, Color activeColor) {
        SetBar(bar1, activeBars >= 1, activeColor);
        SetBar(bar2, activeBars >= 2, activeColor);
        SetBar(bar3, activeBars >= 3, activeColor);
    }

    void SetBar(Image bar, bool active, Color activeColor) {
        if (bar == null) return;
        bar.color = active ? activeColor : dimColor;
    }
}