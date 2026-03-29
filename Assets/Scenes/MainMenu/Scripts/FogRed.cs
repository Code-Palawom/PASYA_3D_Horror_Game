using System.Collections;
using UnityEngine;

public class FogRed : MonoBehaviour {
    [SerializeField] private Color fogColor;
    [SerializeField] private Color fogColorOnHover;
    [SerializeField] private float duration = 1f;

    public void ApplyRedFog() {
        StartCoroutine(ChangeColorOverTime(fogColor, fogColorOnHover, duration));
    }

    public void ResetFog() {
        StartCoroutine(ChangeColorOverTime(fogColorOnHover, fogColor, duration));
    }

    private IEnumerator ChangeColorOverTime(Color currentStartColor, Color currentEndColor, float currentDuration) {
        float elapsedTime = 0f;

        while (elapsedTime < currentDuration) {
            float t = elapsedTime / currentDuration;
            RenderSettings.fogColor = Color.Lerp(currentStartColor, currentEndColor, t);
            elapsedTime += Time.deltaTime;

            yield return null;
        }

        RenderSettings.fogColor = currentEndColor;
    }
}