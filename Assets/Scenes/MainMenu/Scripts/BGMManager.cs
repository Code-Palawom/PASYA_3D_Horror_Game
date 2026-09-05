using UnityEngine;
using UnityEngine.Audio;

[System.Serializable]
public class BgmEntry {
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
}

// Picks one random track from `tracks` and loops it. Call PlayRandomTrack() again
// (e.g. on a scene/state change) to pick a new random track.
public class BGMManager : MonoBehaviour {
    public static BGMManager Instance { get; private set; }

    [SerializeField] private BgmEntry[] tracks;
    [SerializeField] private AudioMixerGroup bgmMixerGroup; // assign the "BGM" group from your AudioMixer asset
    [SerializeField] private bool playOnStart = true;

    private AudioSource source;
    private int lastTrackIndex = -1;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = bgmMixerGroup;
    }

    private void Start() {
        if (playOnStart) PlayRandomTrack();
    }

    // Picks a new random track (avoids repeating the one currently playing, if more than one is available),
    // applies that track's own volume, and loops it. Per-track volume stacks with the mixer's BGM volume slider.
    public void PlayRandomTrack() {
        if (tracks == null || tracks.Length == 0) {
            Debug.LogWarning("BGMManager: no tracks assigned.");
            return;
        }

        int index = Random.Range(0, tracks.Length);
        if (tracks.Length > 1) {
            while (index == lastTrackIndex) {
                index = Random.Range(0, tracks.Length);
            }
        }
        lastTrackIndex = index;

        BgmEntry entry = tracks[index];
        if (entry.clip == null) {
            Debug.LogWarning($"BGMManager: track at index {index} has no clip assigned.");
            return;
        }

        source.clip = entry.clip;
        source.volume = entry.volume;
        source.Play();
    }

    public void Stop() => source.Stop();
}