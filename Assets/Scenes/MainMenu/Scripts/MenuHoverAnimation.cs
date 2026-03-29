using UnityEngine;
using UnityEngine.EventSystems;

public class MenuHoverAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    [SerializeField] private Animator animator;
    [SerializeField] private float animationSpeed = 10f;

    private static int slide = Animator.StringToHash("Slide");

    private float target = 0f;
    private float currentAnimation = 0f;
    void Update() {
        UpdateAnimationState();
    }

    public void OnPointerEnter(PointerEventData eventData) {
        target = 1f;
    }

    public void OnPointerExit(PointerEventData eventData) {
        target = 0f;
    }

    public void UpdateAnimationState() {
        currentAnimation = Mathf.Lerp(currentAnimation, target, animationSpeed * Time.deltaTime);

        animator.SetFloat(slide, currentAnimation);
    }
}

