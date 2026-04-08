using System.Collections;
using UnityEngine;

public class SettingsPanel : MonoBehaviour {
    [SerializeField] private GameObject panel;
    [SerializeField] private float fadeDuration = 1f;

    private CanvasGroup canvas;
    private Coroutine activeAnimation;

    void Start () {
        canvas = GetComponent<CanvasGroup>();
        panel.SetActive(false);
        canvas.alpha = 0f;
    }

    public void OpenSettingsPanel() {
        panel.SetActive(true);

        if(activeAnimation != null) StopCoroutine(activeAnimation);
        activeAnimation = StartCoroutine(Fade(true, 1f));
    }

    public void CloseSettingsPanel() {
        if(activeAnimation != null) StopCoroutine(activeAnimation);
        activeAnimation = StartCoroutine(Fade(false, 0f));
    }

    private IEnumerator Fade (bool setActive, float alpha) {
        float startAlpha = canvas.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration) {
            elapsedTime += Time.deltaTime;
            canvas.alpha = Mathf.Lerp(startAlpha, alpha, elapsedTime / fadeDuration);
            yield return null;
        }

        if(!setActive) panel.SetActive(false);
        canvas.alpha = alpha;
    }
}
