using UnityEngine;
using UnityEngine.Audio;

// Drives the master AudioMixer's exposed BGM/SFX volume params from linear 0-1 values.
// Persistence lives in GameSettings (bgmVolume/sfxVolume) via SettingsManager, same as
// quality/VSync/frame rate — this class only applies values to the mixer, it doesn't save.
// A settings UI should call SettingsManager.Instance.Save(s => s.bgmVolume = v) on slider
// drag (see VideoSettings for the debounced-save pattern), which triggers Apply() -> here.
//
// SETUP (one-time, in the Unity Editor):
// 1. Create an AudioMixer asset (Assets > Create > Audio Mixer). Name it e.g. "MainMixer".
// 2. Inside it, add two child groups under Master: "BGM" and "SFX".
// 3. Select the BGM group, right-click its Volume slider in the Inspector > "Expose to script".
//    In the Mixer's Exposed Parameters list (top-left dropdown), rename it to "BGMVolume".
// 4. Do the same for SFX group's Volume -> expose and rename to "SFXVolume".
// 5. Assign this MainMixer asset to the `mixer` field below.
// 6. Assign the BGM group to BgmManager's `bgmMixerGroup`, and the SFX group to
//    SfxManager's `sfxMixerGroup`.
public class AudioSettingsManager : MonoBehaviour {
    public static AudioSettingsManager Instance { get; private set; }

    [SerializeField] private AudioMixer mixer;

    private const string BgmParam = "BGMVolume";
    private const string SfxParam = "SFXVolume";
    private const float MinDb = -80f; // effectively silent

    public bool isFirstLaunch = true;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Deliberately not in Awake: AudioMixer.SetFloat calls made during Awake can be silently
    // dropped/overridden because the mixer hasn't finished initializing yet — the value looked
    // "loaded" (GameSettings had it right) but never actually reached the mixer. Start() is
    // guaranteed to run after every object's Awake(), including SettingsManager's Load(), so
    // Current is always populated by the time this runs.
    private void Start() {
        if (SettingsManager.Instance != null) {
            SetBgmVolume(SettingsManager.Instance.Current.bgmVolume);
            SetSfxVolume(SettingsManager.Instance.Current.sfxVolume);
        }
    }

    public void SetBgmVolume(float linear01) {
        linear01 = Mathf.Clamp01(linear01);
        mixer.SetFloat(BgmParam, LinearToDb(linear01));
    }

    public void SetSfxVolume(float linear01) {
        linear01 = Mathf.Clamp01(linear01);
        mixer.SetFloat(SfxParam, LinearToDb(linear01));
    }

    private static float LinearToDb(float linear01) {
        // 0 maps to silence rather than log10(0) = -infinity.
        return linear01 <= 0.0001f ? MinDb : Mathf.Log10(linear01) * 20f;
    }
}