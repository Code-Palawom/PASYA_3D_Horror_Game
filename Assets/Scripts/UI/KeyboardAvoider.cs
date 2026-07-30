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

    [Header("Edge Offset (keyboard visible)")]
    public float edgeOffsetLeft = 0f;
    public float edgeOffsetRight = 0f;
    public float edgeOffsetTop = 0f;
    public float edgeOffsetBottom = 0f; // gap between target's bottom and the keyboard top

    bool wasKeyboardVisible = false;
    RectTransform canvasRect;
    bool initialized = false;

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
#else
        keyboardVisible = TouchScreenKeyboard.visible;
        keyboardHeightPx = TouchScreenKeyboard.area.height;
#endif
        if (keyboardVisible) {
            float canvasHeight = canvasRect != null ? canvasRect.rect.height : Screen.height;
            float kbHeightInCanvas = (keyboardHeightPx / Screen.height) * canvasHeight;

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
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            // InputContainer: right portion (e.g. 30%)
            inputContainerRect.anchorMin = new Vector2(textWidthRatio, 0f);
            inputContainerRect.anchorMax = new Vector2(1f, 1f);
            inputContainerRect.offsetMin = Vector2.zero;
            inputContainerRect.offsetMax = Vector2.zero;
        } else {
            // Re-enabling VerticalLayoutGroup recomputes anchors/positions on its own
            verticalLayout.enabled = true;
            contentSizeFitter.enabled = true;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(target);
    }
}