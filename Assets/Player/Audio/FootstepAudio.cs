using UnityEngine;

public class FootstepAudio : MonoBehaviour {
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float raycastDistance = 1.2f;
    [SerializeField] private LayerMask groundMask;

    [System.Serializable]
    public class SurfaceClipSet {
        public string surfaceTag = "Default";
        public AudioClip[] walkClips;
        public AudioClip[] runClips;
        public AudioClip[] crouchClips;
    }

    [SerializeField] private SurfaceClipSet[] surfaceSets;
    [SerializeField] private SurfaceClipSet defaultSet; // fallback if no tag matches

    [SerializeField] private float minPitch = 0.92f;
    [SerializeField] private float maxPitch = 1.08f;
    [SerializeField] private float crouchVolume = 0.4f;

    private PlayerState playerState;

    private void Awake() => playerState = GetComponent<PlayerState>();

    public void Footstep() {
        SurfaceClipSet set = GetSurfaceSet();
        Debug.Log($"[Footstep] Called.");
        if (set == null) return;

        AudioClip[] pool = playerState.CurrentPlayerMovementState switch {
            PlayerMovementState.Walking => set.walkClips,
            PlayerMovementState.Running => set.walkClips,
            PlayerMovementState.Crouching => set.crouchClips,
            _ => null
        };
        if (pool == null || pool.Length == 0) return;

        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.volume = playerState.CurrentPlayerMovementState == PlayerMovementState.Crouching
            ? crouchVolume : 1f;
        audioSource.PlayOneShot(pool[Random.Range(0, pool.Length)]);
        Debug.Log("[Footstep] Played.");
    }

    private SurfaceClipSet GetSurfaceSet() {
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastDistance, groundMask)) {
            foreach (var set in surfaceSets)
                if (hit.collider.CompareTag(set.surfaceTag))
                    return set;
        }
        return defaultSet;
    }
}