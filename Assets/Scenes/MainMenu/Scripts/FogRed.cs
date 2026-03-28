using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class FogRed : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    [SerializeField] private Color fogColor;
    [SerializeField] private Color fogColorOnHover;
    [SerializeField] private float duration = 1f;

    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private Color textColor;
    [SerializeField] private Color textColorOnHover;

    public void OnPointerEnter(PointerEventData eventData) {
        text.color = textColorOnHover;
        StartCoroutine(ChangeColorOverTime(fogColor, fogColorOnHover, duration));
    }

    public void OnPointerExit(PointerEventData eventData) {
        text.color = textColor;
        StartCoroutine(ChangeColorOverTime(fogColorOnHover, fogColor, duration));
    }

    private IEnumerator ChangeColorOverTime(Color currentStartColor, Color currentEndColor, float currentDuration)
    {
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