using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

// Single fixed on-screen text slot — no prefab spawning/pooling like the
// toast system, just one GameObject already sitting in your UI. Fades in
// on show, stays up until explicitly cleared. Showing new text while one
// is already up just swaps the string in place — no stacking, no colors.
[DefaultExecutionOrder(-100)]
public class TitleNotification : NetworkBehaviour {
    public static TitleNotification Instance { get; private set; }

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text displayText;
    [SerializeField] private float displayDuration = 3f;
    [SerializeField] private float fadeDuration = 0.3f;

    [Header("Type Colors")]
    [SerializeField] private Color infoColor = Color.white;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color errorColor = Color.red;

    private void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    // ---------- LOCAL (no networking required — safe for MainMenu) ----------
    public void ShowLocal(string message, ToastType type = ToastType.Info) => Display(message, type);

    // ---------- NETWORKED (server-only, requires active session) ----------
    public void Broadcast(string message, ToastType type = ToastType.Info) {
        if (!IsServer) return;
        ShowClientRpc(message, type);
    }

    public void SendToClient(string message, ulong targetClientId, ToastType type = ToastType.Info) {
        if (!IsServer) return;
        var clientRpcParams = new ClientRpcParams {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { targetClientId } }
        };
        ShowClientRpc(message, type, clientRpcParams);
    }

    [ClientRpc]
    private void ShowClientRpc(string message, ToastType type, ClientRpcParams clientRpcParams = default) => Display(message, type);

    // ---------- Shared display logic — single slot, no stacking ----------
    private void Display(string message, ToastType type) {
        StartCoroutine(ToastLifecycle());

        displayText.text = message;
        displayText.color = type switch {
            ToastType.Success => successColor,
            ToastType.Error => errorColor,
            _ => infoColor
        };

    }

    public void Clear() {
        StartCoroutine(Fade(canvasGroup.alpha, 0f));
    }

    public void BroadcastClear() {
        if (!IsServer) return;
        ClearClientRpc();
    }

    [ClientRpc]
    private void ClearClientRpc() => Clear();

    private IEnumerator Fade(float from, float to) {
        float t = 0f;
        while (t < fadeDuration) {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    private IEnumerator ToastLifecycle() {
        yield return Fade(0f, 1f);
        yield return new WaitForSeconds(displayDuration);
        yield return Fade(1f, 0f);
    }
}