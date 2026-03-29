using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuHoverAnimation : MonoBehaviour, IPointerEnterHandler {
    [SerializeField] private Animator animator;
    [SerializeField] private float duration = 2f;
    [SerializeField] private int buttonIndex;

    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private Color textColor;
    [SerializeField] private Color textColorOnHover;

    private static int slide = Animator.StringToHash("Slide");

    private FontStyles textNormal;
    private float target = 0f;
    private float currentAnimation = 0f;

    public ActiveMenuButtonState activeMenuBtn;

    private Coroutine activeAnimation;

    void Start() {
        textNormal = text.fontStyle;
        if(buttonIndex == 0){
            activeMenuBtn.SetActiveMenuButton(buttonIndex);
            StopCurrentAnimation();
            target = 1f;
            DecorateText();
            activeAnimation = StartCoroutine(StartAnimation());
        }
    }

    public void SlideOut(int btnIndex) {
        if(btnIndex != buttonIndex) {
            UndecorateText();
            StopCurrentAnimation();
            target = 0f;
            activeAnimation = StartCoroutine(StartAnimation());
        }
    }

    public void OnPointerEnter(PointerEventData eventData) {
        activeMenuBtn.SetActiveMenuButton(buttonIndex);
        StopCurrentAnimation();
        target = 1f;
        DecorateText();
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

    private void DecorateText() {
        text.color = textColorOnHover;
        text.fontStyle = FontStyles.Bold;
    }

    private void UndecorateText() {
        text.color = textColor;
        text.fontStyle = textNormal;
    }
}
