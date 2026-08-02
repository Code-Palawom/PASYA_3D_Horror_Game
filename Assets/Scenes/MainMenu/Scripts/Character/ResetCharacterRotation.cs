using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class ResetCharacterRotation : MonoBehaviour {
    [SerializeField] public Transform rotateTarget;   // defaults to this transform if unassigned
    [SerializeField] private float resetDuration = 0.4f;
    [SerializeField] private AnimationCurve resetEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Yaw the model snaps to on reset, regardless of its starting rotation.")]
    [SerializeField] private float resetYaw = 180f;

    [Header("Drag Feel")]
    [Tooltip("How quickly the visual rotation catches up to the raw drag input. Lower = snappier, higher = more lag/smoothness.")]
    [SerializeField] private float dragSmoothTime = 0.06f;

    [Header("Momentum (after release)")]
    [Tooltip("How fast the spin decelerates after letting go, in degrees/sec^2. Higher = stops sooner.")]
    [SerializeField] private float momentumFriction = 500f;
    [Tooltip("Momentum stops once angular speed drops below this (deg/sec).")]
    [SerializeField] private float minMomentumSpeed = 5f;
    [Tooltip("Optional cap on release speed so a fast flick can't send it spinning absurdly fast. Set to a big number to effectively disable.")]
    [SerializeField] private float maxMomentumSpeed = 1200f;

    private Quaternion initialRotation;

    // currentYaw = what's actually applied to the transform (smoothed)
    // targetYaw  = where the raw drag input wants us to be (unsmoothed, accumulates instantly)
    private float currentYaw;
    private float targetYaw;
    private float yawVelocity; // deg/sec, driven by SmoothDampAngle while dragging, then reused as momentum velocity

    private bool isDragging;

    public Coroutine resetRoutine;
    private Coroutine momentumRoutine;

    private void Awake() {
        if (rotateTarget == null)
            rotateTarget = transform;

        initialRotation = rotateTarget.localRotation;
        currentYaw = initialRotation.eulerAngles.y;
        targetYaw = currentYaw;
    }

    private void Update() {
        // While dragging, continuously ease currentYaw toward targetYaw for a smooth, lagged feel.
        // While momentum is running, the momentum routine drives currentYaw directly, so skip this.
        if (isDragging) {
            // Using SmoothDamp (not SmoothDampAngle) on purpose: targetYaw/currentYaw are unwrapped
            // continuous values now, so we don't want shortest-path angle wrapping here. SmoothDampAngle
            // would sometimes pick the wrong direction on a fast flick (e.g. treat +150 forward as -210),
            // causing a visible snap. Plain linear smoothing is correct since direction is already unambiguous.
            currentYaw = Mathf.SmoothDamp(currentYaw, targetYaw, ref yawVelocity, dragSmoothTime);
            rotateTarget.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
        }
    }

    public void BeginDrag(PointerEventData eventData) {
        // Cancel any in-progress reset/momentum if the player grabs the model mid-animation.
        if (resetRoutine != null) {
            StopCoroutine(resetRoutine);
            resetRoutine = null;
        }
        if (momentumRoutine != null) {
            StopCoroutine(momentumRoutine);
            momentumRoutine = null;
        }

        isDragging = true;
        targetYaw = currentYaw; // sync so there's no jump when drag starts
        yawVelocity = 0f;
    }

    public void UpdateRotation(PointerEventData eventData, float dragSpeed, bool invertX) {
        float delta = eventData.delta.x * dragSpeed * (invertX ? -1f : 1f);
        targetYaw += delta; // raw input target; Update() smooths currentYaw toward this every frame
    }

    public void EndDrag(PointerEventData eventData) {
        isDragging = false;

        // yawVelocity currently holds the deg/sec SmoothDampAngle was moving at when released.
        float releaseSpeed = Mathf.Clamp(Mathf.Abs(yawVelocity), 0f, maxMomentumSpeed);
        float releaseDirection = Mathf.Sign(yawVelocity);
        float startVelocity = releaseSpeed * releaseDirection;

        if (momentumRoutine != null)
            StopCoroutine(momentumRoutine);

        momentumRoutine = StartCoroutine(MomentumRoutine(startVelocity));
    }

    private IEnumerator MomentumRoutine(float velocity) {
        while (Mathf.Abs(velocity) > minMomentumSpeed) {
            // Decelerate toward zero at a constant rate (friction).
            velocity = Mathf.MoveTowards(velocity, 0f, momentumFriction * Time.deltaTime);

            currentYaw += velocity * Time.deltaTime;
            targetYaw = currentYaw; // keep target in sync in case drag resumes right after

            rotateTarget.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
            yield return null;
        }

        momentumRoutine = null;
    }

    public void ResetRotation() {
        if (resetRoutine != null)
            StopCoroutine(resetRoutine);
        if (momentumRoutine != null) {
            StopCoroutine(momentumRoutine);
            momentumRoutine = null;
        }

        isDragging = false;
        resetRoutine = StartCoroutine(ResetRotationRoutine());
    }

    private IEnumerator ResetRotationRoutine() {
        float startYaw = currentYaw;

        // Take the shortest angular path (works fine even though currentYaw itself is unwrapped/unclamped).
        float delta = Mathf.DeltaAngle(startYaw, resetYaw);
        float targetYawReset = startYaw + delta; // shortest-path target expressed in the same unwrapped space as currentYaw

        float elapsed = 0f;
        while (elapsed < resetDuration) {
            elapsed += Time.deltaTime;
            float t = resetEase.Evaluate(Mathf.Clamp01(elapsed / resetDuration));
            currentYaw = startYaw + delta * t;
            targetYaw = currentYaw;
            rotateTarget.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
            yield return null;
        }

        currentYaw = targetYawReset;
        targetYaw = currentYaw;
        rotateTarget.localRotation = Quaternion.Euler(0f, resetYaw, 0f);
        resetRoutine = null;
    }
}