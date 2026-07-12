using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generic, reusable full-screen flash effect, attached to the player prefab.
/// Only active for the owning client (IsOwner) — local-only, no RPCs.
/// Access via ScreenFlashController.Local.Flash(...) from anywhere on the owning client.
/// </summary>
public class ScreenFlashController : NetworkBehaviour {
    /// <summary>
    /// Reference to the local player's flash controller. Null on remote/non-owner instances.
    /// </summary>
    public static ScreenFlashController Local { get; private set; }

    [Header("References")]
    [Tooltip("A child Canvas (Screen Space - Overlay) with a full-screen stretched Image.")]
    [SerializeField] private Image flashImage;

    [Header("Default Settings")]
    [SerializeField] private float defaultDuration = 0.35f;
    [SerializeField] private float defaultMaxAlpha = 0.6f;
    [SerializeField] private AnimationCurve defaultCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));

    private Coroutine _activeFlash;

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        if (!IsOwner) {
            enabled = false;
            return;
        }

        Local = this;


        if (flashImage != null) {
            SetAlpha(0f);
            flashImage.raycastTarget = false;
        }
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();
        if (Local == this)
            Local = null;
    }

    public void Flash(Color color, float duration = -1f, float maxAlpha = -1f, AnimationCurve curve = null) {
        if (!IsOwner || flashImage == null) return;

        if (duration <= 0f) duration = defaultDuration;
        if (maxAlpha < 0f) maxAlpha = defaultMaxAlpha;
        if (curve == null) curve = defaultCurve;

        if (_activeFlash != null)
            StopCoroutine(_activeFlash);

        _activeFlash = StartCoroutine(FlashRoutine(color, duration, maxAlpha, curve));
    }

    public void FlashDamage() => Flash(Color.red, 0.25f, 0.5f);
    public void FlashCorrect() => Flash(Color.green, 0.3f, 0.4f);
    public void FlashWrong() => Flash(Color.red, 0.3f, 0.4f);
    public void FlashHeal() => Flash(Color.green, 0.3f, 0.35f);

    private IEnumerator FlashRoutine(Color color, float duration, float maxAlpha, AnimationCurve curve) {
        flashImage.gameObject.SetActive(true);
        color.a = 1f;
        flashImage.color = color;

        float t = 0f;
        while (t < duration) {
            t += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(t / duration);
            float curveValue = curve.Evaluate(normalized);
            SetAlpha(curveValue * maxAlpha);
            yield return null;
        }

        SetAlpha(0f);
        flashImage.gameObject.SetActive(false);
        _activeFlash = null;
    }

    private void SetAlpha(float alpha) {
        Color c = flashImage.color;
        c.a = alpha;
        flashImage.color = c;
    }
}