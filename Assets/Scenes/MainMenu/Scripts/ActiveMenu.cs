using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ActiveMenu : MonoBehaviour {
    [SerializeField] private Animator menuAnimator;
    [SerializeField] private float duration;
    [SerializeField] private float delay;
    [SerializeField] private float currentAnimation;
    [SerializeField] private float targetAnimation;

    private static int status = Animator.StringToHash("Status");

    private Coroutine activeAnimation;

    private void Start() {
        activeAnimation = StartCoroutine(StartAnimation());
    }

    public void ShowBtn() {
        targetAnimation = 1f;
        StopCurrentAnimation();
        activeAnimation = StartCoroutine(StartAnimation());
    }

    public void HideBtn() {
        targetAnimation = 0f;
        StopCurrentAnimation();
        activeAnimation = StartCoroutine(StartAnimation());
    }

    private void StopCurrentAnimation() {
        if (activeAnimation != null) StopCoroutine(activeAnimation);
    }

    private IEnumerator StartAnimation() {
        yield return new WaitForSeconds(delay);

        float elapsed = 0;

        while(elapsed < duration){
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            currentAnimation = Mathf.Lerp(currentAnimation, targetAnimation, t);
            menuAnimator.SetFloat(status, currentAnimation);
            yield return null;
        }

        currentAnimation = targetAnimation;
        menuAnimator.SetFloat(status, currentAnimation);
    }
}
