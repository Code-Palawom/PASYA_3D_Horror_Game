using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialFlow : MonoBehaviour {
    [SerializeField] private Button previousBtn;
    [SerializeField] private Button nextBtn;
    [SerializeField] private Button submitBtn;

    private string playerName;

    [SerializeField] GameObject NamePrompt;
    [SerializeField] GameObject SelectCharacterPrompt;

    [Header("Character Selection")]
    [SerializeField] private Button character1Btn;
    [SerializeField] private Image character1Icon;
    [SerializeField] private TMP_Text character1Label;
    [SerializeField] private GameObject character1SelectedOutline;
    [SerializeField] private CharacterSkinSO character1Skin;

    [SerializeField] private Button character2Btn;
    [SerializeField] private Image character2Icon;
    [SerializeField] private TMP_Text character2Label;
    [SerializeField] private GameObject character2SelectedOutline;
    [SerializeField] private CharacterSkinSO character2Skin;

    public RectTransform target;

    [Header("Edge Offset (keyboard visible)")]
    public float edgeOffsetLeft = 0f;
    public float edgeOffsetRight = 0f;
    public float edgeOffsetTop = 0f;
    public float edgeOffsetBottom = 0f;

    RectTransform canvasRect;
    bool initialized = false;

    Vector2 defaultAnchorMin, defaultAnchorMax, defaultOffsetMin, defaultOffsetMax;

    void Awake() {
        character1Icon.sprite = character1Skin.previewIcon;
        character1Label.text = character1Skin.displayName;
        character1Btn.onClick.AddListener(() => SelectCharacter(character1Skin));

        character2Icon.sprite = character2Skin.previewIcon;
        character2Label.text = character2Skin.displayName;
        character2Btn.onClick.AddListener(() => SelectCharacter(character2Skin));
    }

    void SelectCharacter(CharacterSkinSO skin) {
        character1SelectedOutline.SetActive(skin == character1Skin);
        character2SelectedOutline.SetActive(skin == character2Skin);

        submitBtn.interactable = true;
        SkinSaveSystem.Save(skin.skinId);
    }

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
        Init();

        bool keyboardVisible = TouchScreenKeyboard.visible;
        float keyboardHeightPx = TouchScreenKeyboard.area.height;

        if (keyboardVisible) {
            float canvasHeight = canvasRect != null ? canvasRect.rect.height : Screen.height;
            float kbHeightInCanvas = (keyboardHeightPx / Screen.height) * canvasHeight;

            target.anchorMin = new Vector2(0f, 0f);
            target.anchorMax = new Vector2(1f, 1f);
            target.offsetMin = new Vector2(edgeOffsetLeft, kbHeightInCanvas + edgeOffsetBottom);
            target.offsetMax = new Vector2(-edgeOffsetRight, -edgeOffsetTop);
        } else {
            target.anchorMin = defaultAnchorMin;
            target.anchorMax = defaultAnchorMax;
            target.offsetMin = defaultOffsetMin;
            target.offsetMax = defaultOffsetMax;
        }
    }

    public void OnNameChanged(string name) {
        nextBtn.interactable = !string.IsNullOrEmpty(name.Trim());
        playerName = name.Trim();
    }

    public void OnNext() {
        SettingsManager.Instance.Save(s => s.playerName = playerName);
        SelectCharacterPrompt.SetActive(true);
        NamePrompt.SetActive(false);
        nextBtn.gameObject.SetActive(false);
        previousBtn.gameObject.SetActive(true);
        submitBtn.gameObject.SetActive(true);
    }

    public void OnPrevious() {
        SelectCharacterPrompt.SetActive(false);
        submitBtn.gameObject.SetActive(false);
        previousBtn.gameObject.SetActive(false);
        nextBtn.gameObject.SetActive(true);
        NamePrompt.SetActive(true);
    }
}