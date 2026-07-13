using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public enum ToastType { Info, Success, Error }

[DefaultExecutionOrder(-100)]
public class ToastNotification : NetworkBehaviour {
    public static ToastNotification Instance { get; private set; }

    [SerializeField] private GameObject toastPrefab;
    [SerializeField] private Transform toastContainer;
    [SerializeField] private float displayDuration = 3f;
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private int maxVisibleToasts = 3;

    [Header("Type Colors")]
    [SerializeField] private Color infoColor = Color.white;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color errorColor = Color.red;

    private List<ActiveToast> activeToasts = new();

    private class ActiveToast {
        public GameObject GameObject;
        public CanvasGroup CanvasGroup;
        public RectTransform TimeBar;
        public Coroutine LifecycleCoroutine;
    }

    private void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ---------- LOCAL (no networking required — safe for MainMenu) ----------
    public void ShowLocalToast(string message, ToastType type = ToastType.Info) {
        SpawnToast(message, type);
    }

    // ---------- NETWORKED (server-only, requires active session) ----------
    public void BroadcastToast(string message, ToastType type = ToastType.Info) {
        if (!IsServer) return;
        ShowToastClientRpc(message, type);
    }

    public void SendToastToClient(string message, ulong targetClientId, ToastType type = ToastType.Info) {
        if (!IsServer) return;
        var clientRpcParams = new ClientRpcParams {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { targetClientId } }
        };
        ShowToastClientRpc(message, type, clientRpcParams);
    }

    [ClientRpc]
    private void ShowToastClientRpc(string message, ToastType type, ClientRpcParams clientRpcParams = default) {
        SpawnToast(message, type);
    }

    // ---------- Shared display logic ----------
    private void SpawnToast(string message, ToastType type) {
        if (activeToasts.Count >= maxVisibleToasts) {
            var oldest = activeToasts[0];
            activeToasts.RemoveAt(0);
            if (oldest.LifecycleCoroutine != null) StopCoroutine(oldest.LifecycleCoroutine);
            StartCoroutine(FadeOutAndDestroy(oldest));
        }

        GameObject toastObj = Instantiate(toastPrefab, toastContainer);
        toastObj.transform.SetAsLastSibling();

        TMP_Text text = toastObj.GetComponentInChildren<TMP_Text>();
        text.text = message;
        Color typeColor = type switch {
            ToastType.Success => successColor,
            ToastType.Error => errorColor,
            _ => infoColor
        };
        text.color = typeColor;

        CanvasGroup cg = toastObj.GetComponent<CanvasGroup>();
        cg.alpha = 0f;

        // Find and set up the time bar
        Transform barTransform = toastObj.transform.Find("TimeBar");
        RectTransform timeBar = barTransform.GetComponent<RectTransform>();
        timeBar.localScale = new Vector3(1f, 1f, 1f);
        Image barImage = barTransform.GetComponent<Image>();
        barImage.color = typeColor;

        var entry = new ActiveToast { GameObject = toastObj, CanvasGroup = cg, TimeBar = timeBar };
        entry.LifecycleCoroutine = StartCoroutine(ToastLifecycle(entry));
        activeToasts.Add(entry);
    }

    private IEnumerator ToastLifecycle(ActiveToast entry) {
        yield return Fade(entry.CanvasGroup, 0f, 1f);

        // Shrink the time bar over displayDuration
        float t = 0f;
        while (t < displayDuration) {
            t += Time.deltaTime;
            float remaining = 1f - (t / displayDuration);
            entry.TimeBar.localScale = new Vector3(remaining, 1f, 1f);
            yield return null;
        }
        entry.TimeBar.localScale = new Vector3(0f, 1f, 1f);

        yield return Fade(entry.CanvasGroup, 1f, 0f);
        activeToasts.Remove(entry);
        Destroy(entry.GameObject);
    }

    private IEnumerator FadeOutAndDestroy(ActiveToast entry) {
        yield return Fade(entry.CanvasGroup, entry.CanvasGroup.alpha, 0f);
        Destroy(entry.GameObject);
    }

    private IEnumerator Fade(CanvasGroup cg, float from, float to) {
        float t = 0f;
        while (t < fadeDuration) {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        cg.alpha = to;
    }
}