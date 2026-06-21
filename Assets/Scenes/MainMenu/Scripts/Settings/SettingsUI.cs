using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsUI : MonoBehaviour {
    [Header("Name")]
    [SerializeField] private TMP_InputField nameField;

    [Header("Quality Carousel")]
    [SerializeField] private Button btnQualityPrev;
    [SerializeField] private Button btnQualityNext;
    [SerializeField] private TMP_Text qualityLabel;

    [Header("POV")]
    [SerializeField] private Button btnFirstPerson;
    [SerializeField] private Button btnThirdPerson;

    [Header("POV Button Colors")]
    [SerializeField] private Color activeColor = new Color(0.20f, 0.60f, 1.00f);
    [SerializeField] private Color inactiveColor = new Color(0.30f, 0.30f, 0.30f);

    // ── State ────────────────────────────────────────────────
    private bool _isFirstPerson;
    private int _qualityIndex;
    private string[] _qualityNames;

    // ── Init ────────────────────────────────────────────────
    void Awake() {
        _qualityNames = QualitySettings.names;
    }

    void Start() {

        // Quality carousel
        btnQualityPrev.onClick.AddListener(() => StepQuality(-1));
        btnQualityNext.onClick.AddListener(() => StepQuality(+1));

        // POV toggle
        btnFirstPerson.onClick.AddListener(() => SetPOV(true));
        btnThirdPerson.onClick.AddListener(() => SetPOV(false));

        // Name — auto-save on field exit, not on every keystroke
        nameField.onEndEdit.AddListener(_ => AutoSave());

        if (SettingsManager.Instance != null)
            Populate(SettingsManager.Instance.Current);
    }

    // ── Called by your panel manager ─────────────────────────
    public void Show() {
        gameObject.SetActive(true);
        Refresh();
    }

    public void Hide() => gameObject.SetActive(false);

    public void Refresh() {
        if (SettingsManager.Instance != null)
            Populate(SettingsManager.Instance.Current);
    }

    // ── Quality carousel ─────────────────────────────────────
    private void StepQuality(int direction) {
        // Infinite wrap
        _qualityIndex = (_qualityIndex + direction + _qualityNames.Length) % _qualityNames.Length;
        qualityLabel.text = _qualityNames[_qualityIndex];
        AutoSave();
    }

    // ── POV toggle ───────────────────────────────────────────
    private void SetPOV(bool firstPerson) {
        _isFirstPerson = firstPerson;
        SetButtonColor(btnFirstPerson, firstPerson);
        SetButtonColor(btnThirdPerson, !firstPerson);
        AutoSave();
    }

    // ── Auto-save ────────────────────────────────────────────
    private void AutoSave() {
        if (SettingsManager.Instance == null) return;

        SettingsManager.Instance.Save(new GameSettings {
            playerName = nameField.text.Trim(),
            isFirstPerson = _isFirstPerson,
            qualityLevel = _qualityIndex
        });
    }

    // ── Populate from loaded settings ────────────────────────
    private void Populate(GameSettings s) {
        nameField.text = s.playerName;

        _qualityIndex = Mathf.Clamp(s.qualityLevel, 0, _qualityNames.Length - 1);
        qualityLabel.text = _qualityNames[_qualityIndex];

        // Apply POV without triggering AutoSave during populate
        _isFirstPerson = s.isFirstPerson;
        SetButtonColor(btnFirstPerson, s.isFirstPerson);
        SetButtonColor(btnThirdPerson, !s.isFirstPerson);
    }

    // ── Helpers ──────────────────────────────────────────────
    private void SetButtonColor(Button btn, bool isActive) {
        var colors = btn.colors;
        colors.normalColor = isActive ? activeColor : inactiveColor;
        btn.colors = colors;
    }
}