using UnityEngine;
using UnityEngine.Audio;

// Picks one random track from `tracks` and loops it. Call PlayRandomTrack() again
// (e.g. on a scene/state change) to pick a new random track.
public class BGMManager : MonoBehaviour {
    public static BGMManager Instance { get; private set; }

    [SerializeField] private AudioClip[] tracks;
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

    // Picks a new random track (avoids repeating the one currently playing, if more than one is available) and loops it.
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

        source.clip = tracks[index];
        source.Play();
    }

    public void Stop() => source.Stop();
}