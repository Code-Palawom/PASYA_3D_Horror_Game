using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// Add clip entries in the Inspector to grow this list — no code changes needed elsewhere.
public enum SfxId {
    UINavigate,
    UINavigateBack,
    UIConfirm,
    UICancel,
    PurchaseSuccess,
    PurchaseFail,
    TrySkin,
    EquipSkin,
    Play,
    Tap,
}

[System.Serializable]
public class SfxEntry {
    public SfxId id;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
}

// Singleton SFX player. Call SfxManager.Play(SfxId.X) from anywhere, no reference needed.
// Uses a small round-robin AudioSource pool so overlapping sounds (e.g. rapid nav clicks) don't cut each other off.
public class SfxManager : MonoBehaviour {
    public static SfxManager Instance { get; private set; }

    [SerializeField] private SfxEntry[] sfxLibrary;
    [SerializeField] private int poolSize = 6;
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField] private AudioMixerGroup sfxMixerGroup; // assign the "SFX" group from your AudioMixer asset

    private Dictionary<SfxId, SfxEntry> lookup;
    private AudioSource[] pool;
    private int nextSource;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        lookup = new Dictionary<SfxId, SfxEntry>();
        foreach (SfxEntry entry in sfxLibrary) {
            if (entry.clip == null) continue;
            lookup[entry.id] = entry;
        }

        pool = new AudioSource[Mathf.Max(1, poolSize)];
        for (int i = 0; i < pool.Length; i++) {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f; // UI/SFX are 2D
            src.outputAudioMixerGroup = sfxMixerGroup;
            pool[i] = src;
        }
    }

    public static void Play(SfxId id, float volumeScale = 1f) {
        if (Instance == null) {
            Debug.LogWarning("SfxManager: no instance in scene — did you forget to add it?");
            return;
        }
        Instance.PlayInternal(id, volumeScale);
    }

    private void PlayInternal(SfxId id, float volumeScale) {
        if (!lookup.TryGetValue(id, out SfxEntry entry)) {
            Debug.LogWarning($"SfxManager: no clip assigned for {id}");
            return;
        }

        AudioSource src = pool[nextSource];
        nextSource = (nextSource + 1) % pool.Length;
        src.PlayOneShot(entry.clip, entry.volume * volumeScale * masterVolume);
    }
}