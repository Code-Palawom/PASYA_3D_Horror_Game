using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

// Dims the screen using 4 panels (top/bottom/left/right) surrounding a
// highlighted RectTransform, leaving that target fully undimmed and
// interactable. Panels can pulse their alpha a set number of times.
public class TutorialSpotlight : MonoBehaviour {
    public RectTransform canvasRect;
    public RectTransform top, bottom, left, right;
    public float padding = 10f;

    [Header("Pulse")]
    public float minAlpha = 0.4f;
    public float maxAlpha = 0.75f;
    public float pulseSpeed = 2f; // cycles per second
    public Ease pulseEase = Ease.InOutSine;

    // Fired when the current Pulse() run finishes all its cycles.
    public event Action OnPulseComplete;

    public bool IsPulsing => pulseTween.isAlive;

    Image[] panels;
    Tween pulseTween;

    void Awake() {
        panels = new[]
        {
            top.GetComponent<Image>(),
            bottom.GetComponent<Image>(),
            left.GetComponent<Image>(),
            right.GetComponent<Image>()
        };
    }

    public void Highlight(RectTransform target) {
        Canvas.ForceUpdateCanvases();

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);

        Vector2 min = (Vector2)canvasRect.InverseTransformPoint(corners[0]) - Vector2.one * padding;
        Vector2 max = (Vector2)canvasRect.InverseTransformPoint(corners[2]) + Vector2.one * padding;

        float halfW = canvasRect.rect.width / 2f;
        float halfH = canvasRect.rect.height / 2f;

        SetPanel(top, new Vector2(-halfW, max.y), new Vector2(halfW, halfH));
        SetPanel(bottom, new Vector2(-halfW, -halfH), new Vector2(halfW, min.y));
        SetPanel(left, new Vector2(-halfW, min.y), new Vector2(min.x, max.y));
        SetPanel(right, new Vector2(max.x, min.y), new Vector2(halfW, max.y));
    }

    void SetPanel(RectTransform p, Vector2 bl, Vector2 tr) {
        p.anchorMin = p.anchorMax = p.pivot = new Vector2(0.5f, 0.5f);
        Vector2 size = tr - bl;
        p.sizeDelta = size;
        p.anchoredPosition = bl + size / 2f;
    }

    // Removes all dimming entirely (alpha 0) — nothing is darkened.
    // Call this when a step completes, before the next highlight is shown.
    public void HideHighlight() {
        if (pulseTween.isAlive) pulseTween.Stop();
        SetAlpha(0f);
    }

    // Pulses all 4 panels' alpha between minAlpha and maxAlpha, "times" full
    // up-and-down cycles, using PrimeTween's built-in Yoyo cycling.
    public void Pulse(int times) {
        if (pulseTween.isAlive) pulseTween.Stop();

        SetAlpha(minAlpha);
        float halfCycleDuration = 1f / pulseSpeed / 2f;

        pulseTween = Tween.Custom(
            minAlpha, maxAlpha, halfCycleDuration,
            onValueChange: SetAlpha,
            ease: pulseEase,
            cycles: times * 2,           // each cycle = one direction; *2 = one full up+down per pulse
            cycleMode: CycleMode.Yoyo
        ).OnComplete(() => OnPulseComplete?.Invoke());
    }

    void SetAlpha(float a) {
        foreach (var img in panels) {
            var c = img.color;
            c.a = a;
            img.color = c;
        }
    }
}