using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuHoverAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    [SerializeField] private Animator animator;
    [SerializeField] private float duration = 2f;

    private static int slide = Animator.StringToHash("Slide");

    private float target = 0f;
    private float currentAnimation = 0f;

    private Coroutine activeAnimation;

    public void OnPointerEnter(PointerEventData eventData) {
        StopCurrentAnimation();
        target = 1f;
        activeAnimation = StartCoroutine(StartAnimation());
    }

    public void OnPointerExit(PointerEventData eventData) {
        StopCurrentAnimation();
        target = 0f;
        activeAnimation = StartCoroutine(StartAnimation());
    }


    private void StopCurrentAnimation() {
        if (activeAnimation != null) StopCoroutine(activeAnimation);
    }

    private IEnumerator StartAnimation() {
        float elapsed = 0;

        while(elapsed < duration){
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            currentAnimation = Mathf.Lerp(currentAnimation, target, t);
            animator.SetFloat(slide, currentAnimation);
            yield return null;
        }

        currentAnimation = target;
        animator.SetFloat(slide, currentAnimation);
    }
}
