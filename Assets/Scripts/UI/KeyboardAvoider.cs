using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

[ExecuteAlways]
public class KeyboardAvoider : MonoBehaviour {
    public RectTransform target;

    [Header("Layout Swap")]
    public VerticalLayoutGroup verticalLayout;
    public ContentSizeFitter contentSizeFitter;

    [Header("Horizontal Split (keyboard visible)")]
    public RectTransform textRect;           // Text child's RectTransform
    public RectTransform inputContainerRect; // InputContainer GameObject's RectTransform (holds InputField + Button)
    [Range(0f, 1f)] public float textWidthRatio = 0.7f;

    [Header("Text Rect Offset")]
    public float textOffsetLeft = 0f;
    public float textOffsetRight = 0f;
    public float textOffsetTop = 0f;
    public float textOffsetBottom = 0f;

    [Header("Input Container Rect Offset")]
    public float inputContainerOffsetLeft = 0f;
    public float inputContainerOffsetRight = 0f;
    public float inputContainerOffsetTop = 0f;
    public float inputContainerOffsetBottom = 0f;

    [Header("Edge Offset (keyboard visible)")]
    public float edgeOffsetLeft = 0f;
    public float edgeOffsetRight = 0f;
    public float edgeOffsetTop = 0f;
    public float edgeOffsetBottom = 0f; // gap between target's bottom and the keyboard top

    bool wasKeyboardVisible = false;
    RectTransform canvasRect;
    bool initialized = false;

    private bool showOverlay;

    // Cached each Update() for the debug overlay
    bool debugKeyboardVisible;
    float debugKeyboardHeightPx;
    float debugKbHeightInCanvas;
    float debugCanvasHeight;

    Vector2 defaultAnchorMin, defaultAnchorMax, defaultOffsetMin, defaultOffsetMax;

#if UNITY_EDITOR
    [Header("Editor Testing (Play Mode)")]
    public Key toggleFakeKeyboardKey = Key.K;
    public float fakeKeyboardHeight = 300f;
    bool fakeKeyboardVisible = false;

    [Header("Scene View Preview (Edit Mode)")]
    [Tooltip("Toggle this in the Inspector, without pressing Play, to preview the keyboard-visible layout in the Scene view.")]
    public bool previewKeyboardInEditor = false;
#endif

    void OnEnable() => Init();

    void Init() {
        if (initialized || target == null) return;

        Canvas canvas = target.GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? canvas.rootCanvas.GetComponent<RectTransform>() : null;

        defaultAnchorMin = target.anchorMin;
        defaultAnchorMax = target.anchorMax;
        defaultOffsetMin = target.offsetMin;
        defaultOffsetMax = target.offsetMax;

        initialized = true;
    }

    void Update() {
        if (target == null) return;
        Init(); // safe no-op if already initialized; needed since edit mode may not call OnEnable reliably after script recompiles

        bool keyboardVisible;
        float keyboardHeightPx;

#if UNITY_EDITOR
        if (!Application.isPlaying) {
            // Scene view / edit mode preview — driven by Inspector checkbox only
            keyboardVisible = previewKeyboardInEditor;
            keyboardHeightPx = previewKeyboardInEditor ? fakeKeyboardHeight : 0f;
        } else {
            if (Keyboard.current != null && Keyboard.current[toggleFakeKeyboardKey].wasPressedThisFrame)
                fakeKeyboardVisible = !fakeKeyboardVisible;
            keyboardVisible = fakeKeyboardVisible;
            keyboardHeightPx = fakeKeyboardVisible ? fakeKeyboardHeight : 0f;
        }
#elif UNITY_ANDROID
        // TouchScreenKeyboard.area.height is unreliable on Android (often 0 or full-screen
        // depending on OEM keyboard/Android version). Use the decor view visible frame instead.
        keyboardHeightPx = AndroidKeyboardHeight.GetHeightPx();
        keyboardVisible = keyboardHeightPx > 0f;
#else
        keyboardVisible = TouchScreenKeyboard.visible;
        keyboardHeightPx = TouchScreenKeyboard.area.height;
#endif

        float canvasHeight = canvasRect != null ? canvasRect.rect.height : Screen.height;
        float kbHeightInCanvas = (keyboardHeightPx / Screen.height) * canvasHeight;

        // Cache for debug overlay
        debugKeyboardVisible = keyboardVisible;
        debugKeyboardHeightPx = keyboardHeightPx;
        debugKbHeightInCanvas = kbHeightInCanvas;
        debugCanvasHeight = canvasHeight;

        if (keyboardVisible) {
            // Stretch target to fill remaining space, minus edge offsets, above the keyboard
            target.anchorMin = new Vector2(0f, 0f);
            target.anchorMax = new Vector2(1f, 1f);
            target.offsetMin = new Vector2(edgeOffsetLeft, kbHeightInCanvas + edgeOffsetBottom);
            target.offsetMax = new Vector2(-edgeOffsetRight, -edgeOffsetTop);
        } else {
            // Restore original rect
            target.anchorMin = defaultAnchorMin;
            target.anchorMax = defaultAnchorMax;
            target.offsetMin = defaultOffsetMin;
            target.offsetMax = defaultOffsetMax;
        }

        if (keyboardVisible != wasKeyboardVisible) {
            SetLayoutMode(keyboardVisible);
            wasKeyboardVisible = keyboardVisible;
        }
    }

    void SetLayoutMode(bool keyboardVisible) {
        if (keyboardVisible) {
            verticalLayout.enabled = false;
            contentSizeFitter.enabled = false;

            // Text: left portion (e.g. 70%)
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(textWidthRatio, 1f);
            textRect.offsetMin = new Vector2(textOffsetLeft, textOffsetBottom);
            textRect.offsetMax = new Vector2(-textOffsetRight, -textOffsetTop);

            // InputContainer: right portion (e.g. 30%)
            inputContainerRect.anchorMin = new Vector2(textWidthRatio, 0f);
            inputContainerRect.anchorMax = new Vector2(1f, 1f);
            inputContainerRect.offsetMin = new Vector2(inputContainerOffsetLeft, inputContainerOffsetBottom);
            inputContainerRect.offsetMax = new Vector2(-inputContainerOffsetRight, -inputContainerOffsetTop);
        } else {
            // Re-enabling VerticalLayoutGroup recomputes anchors/positions on its own
            verticalLayout.enabled = true;
            contentSizeFitter.enabled = true;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(target);
    }

    public void RefreshDebugMode() {
        if (SettingsManager.Instance == null) {
            Debug.LogWarning("[KeyboardAvoider] SettingsManager not ready yet.");
            return;
        }
        showOverlay = SettingsManager.Instance.Current.showDebugOverlay;
    }

    private void OnGUI() {
        if (!showOverlay) return;
        GUI.Label(new Rect(10, 110, 420, 20), $"[KB] Visible : {debugKeyboardVisible}");
        GUI.Label(new Rect(10, 130, 420, 20), $"[KB] Height px : {debugKeyboardHeightPx:F0}  (Screen.height={Screen.height})");
        GUI.Label(new Rect(10, 150, 420, 20), $"[KB] Height in canvas : {debugKbHeightInCanvas:F1}  (canvasHeight={debugCanvasHeight:F0})");
    }
}

#if UNITY_ANDROID && !UNITY_EDITOR
public static class AndroidKeyboardHeight {
    public static float GetHeightPx() {
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        using var decorView = activity.Call<AndroidJavaObject>("getWindow")
                                       .Call<AndroidJavaObject>("getDecorView");
        using var rect = new AndroidJavaObject("android.graphics.Rect");
        decorView.Call("getWindowVisibleDisplayFrame", rect);

        int screenHeight = decorView.Call<int>("getHeight");
        int visibleHeight = rect.Call<int>("height");
        int diff = screenHeight - visibleHeight;

        // Ignore small diffs (status/nav bar) — keyboard is usually well over 100px
        return diff > 100 ? diff : 0f;
    }
}
#elif UNITY_ANDROID
public static class AndroidKeyboardHeight {
    public static float GetHeightPx() => 0f;
}
#endif