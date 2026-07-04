using System.Collections;
using TMPro;
using UnityEngine;

public class GoogleSignInLoading : MonoBehaviour {
    public static GoogleSignInLoading Instance { get; private set; }

    [Header("References")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private CanvasGroup backgroundGroup;

    [Header("Animation")]
    [SerializeField] private float slideDuration = 0.35f;
    [SerializeField] private float dotInterval = 0.4f;

    private Vector2 hiddenPos;   // off-screen below
    private Vector2 shownPos;    // resting position
    private Coroutine dotCoroutine;

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Hide panel just below screen on start
        float panelHeight = panel.rect.height;
        hiddenPos = new Vector2(0, -panelHeight - 100f);
        shownPos = new Vector2(0, 0f);  // tweak this for vertical position
        panel.anchoredPosition = hiddenPos;
        backgroundGroup.alpha = 0f;
        panel.gameObject.SetActive(false);
    }

    // Public API
    public void Show(string message = "Signing in") {
        panel.gameObject.SetActive(true);
        statusText.text = message + "...";
        StopAllCoroutines();
        StartCoroutine(AnimateIn());
        dotCoroutine = StartCoroutine(AnimateDots(message));
    }

    public void Hide() {
        StopAllCoroutines();
        StartCoroutine(AnimateOut());
    }

    // Animations
    private IEnumerator AnimateIn() {
        float t = 0f;
        Vector2 startPos = panel.anchoredPosition;

        while (t < slideDuration) {
            t += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, t / slideDuration);
            panel.anchoredPosition = Vector2.Lerp(startPos, shownPos, progress);
            backgroundGroup.alpha = Mathf.Lerp(0f, 1f, progress);
            yield return null;
        }

        panel.anchoredPosition = shownPos;
        backgroundGroup.alpha = 1f;
    }

    private IEnumerator AnimateOut() {
        float t = 0f;
        Vector2 startPos = panel.anchoredPosition;

        while (t < slideDuration) {
            t += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, t / slideDuration);
            panel.anchoredPosition = Vector2.Lerp(startPos, hiddenPos, progress);
            backgroundGroup.alpha = Mathf.Lerp(1f, 0f, progress);
            yield return null;
        }

        panel.gameObject.SetActive(false);
    }

    private IEnumerator AnimateDots(string baseMessage) {
        string[] states = { ".", "..", "..." };
        int i = 0;

        while (true) {
            yield return new WaitForSeconds(dotInterval);
            statusText.text = baseMessage + states[i % 3];
            i++;
        }
    }
}