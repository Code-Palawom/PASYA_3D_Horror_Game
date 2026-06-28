using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Handles a full-screen black fade + loading panel.

// Public API:
//   LoadingScreenController.Instance.Show("Loading...");
//   LoadingScreenController.Instance.Hide();

// Canvas hierarchy expected:
//   [LoadingScreenController GameObject]
//     └── BlackOverlay          (Image, full screen, black) + CanvasGroup
//           └── LoadingPanel    (child GameObject)
//                 ├── Image     (progress bar, filled)
//                 ├── TMP_Text  (status e.g. "Loading...")
//                 └── TMP_Text  (tip text)

public class LoadingScreenController : MonoBehaviour {
    public static LoadingScreenController Instance { get; private set; }

    [Header("Fade")]
    [SerializeField] private CanvasGroup blackOverlay;
    [SerializeField] private float fadeInDuration = 0.4f;
    [SerializeField] private float fadeOutDuration = 0.4f;

    [Header("Loading Panel")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Image progressBar;
    [SerializeField] private TMP_Text statusText;

    [Header("Tips")]
    [SerializeField] private TMP_Text tipText;

    private Coroutine _fakeProgressCoroutine;
    private bool isFirstShow = true;
    
    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    void Awake() {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        blackOverlay.alpha = 1f;
        blackOverlay.blocksRaycasts = true;
    }


    void Start() {
        if (isFirstShow) {
            blackOverlay.blocksRaycasts = false;
            StartCoroutine(FadeOverlay(1f, 0f, 1f));
            isFirstShow = false;
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void Show(string message = "Loading...") {
        StartCoroutine(ShowSequence(message));
    }

    public void Hide(float delay = 0.2f) {
        StartCoroutine(HideSequence(delay));
    }

    public void SetMessage(string message, MessageColor type = MessageColor.Normal) {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = type switch {
            MessageColor.Error => new Color32(220, 80, 80, 255),
            _ => Color.white
        };
    }

    // -------------------------------------------------------------------------
    // Show: fade black in → reveal loading panel
    // -------------------------------------------------------------------------

    private IEnumerator ShowSequence(string message = "Loading...") {
        StopFakeProgress();

        blackOverlay.blocksRaycasts = true;

        yield return StartCoroutine(FadeOverlay(0f, 1f, fadeInDuration));

        loadingPanel.SetActive(true);
        progressBar.fillAmount = 0f;
        if (statusText != null) {
            statusText.color = Color.white;
            statusText.text = message;
        }

        ShowRandomTip();
        _fakeProgressCoroutine = StartCoroutine(FakeLoadProgress());
    }

    // -------------------------------------------------------------------------
    // Hide: snap progress → pause → fade black out
    // -------------------------------------------------------------------------

    private IEnumerator HideSequence(float delay) {
        StopFakeProgress();

        progressBar.fillAmount = 1f;
        yield return new WaitForSeconds(delay);

        loadingPanel.SetActive(false);

        yield return StartCoroutine(FadeOverlay(1f, 0f, fadeOutDuration));

        blackOverlay.blocksRaycasts = false;
    }

    // -------------------------------------------------------------------------
    // Tips
    // -------------------------------------------------------------------------

    private void ShowRandomTip() {
        if (tipText == null || TipsManager.Instance == null) return;
        tipText.text = TipsManager.Instance.GetRandomTip();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private IEnumerator FadeOverlay(float from, float to, float duration) {
        float elapsed = 0f;
        blackOverlay.alpha = from;

        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            blackOverlay.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        blackOverlay.alpha = to;
    }

    private IEnumerator FakeLoadProgress(float duration = 2f) {
        float elapsed = 0f;

        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            progressBar.fillAmount = Mathf.Lerp(0f, 0.9f, elapsed / duration);
            yield return null;
        }
    }

    private void StopFakeProgress() {
        if (_fakeProgressCoroutine == null) return;
        StopCoroutine(_fakeProgressCoroutine);
        _fakeProgressCoroutine = null;
    }

    public enum MessageColor { Normal, Ok, Error }
}