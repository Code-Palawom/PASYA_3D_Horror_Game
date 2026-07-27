using Unity.Netcode;
using UnityEngine;

public class FootstepAudio : NetworkBehaviour {
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

    // Called via Animation Event. Only reliable on the owner's own local
    // Animator playback (NetworkAnimator can skip event frames on remote
    // clients when it snaps synced playback time) — so instead of playing
    // audio directly here, the owner resolves which surface it's on and
    // broadcasts that + the movement state, and every client (including
    // the owner) resolves clips and plays locally off the RPC.
    public void Footstep() {
        if (!IsOwner) return;

        int surfaceIndex = GetSurfaceIndex();
        FootstepServerRpc(surfaceIndex, (int)playerState.CurrentPlayerMovementState);
    }

    [ServerRpc]
    private void FootstepServerRpc(int surfaceIndex, int state) =>
        FootstepClientRpc(surfaceIndex, state);

    [ClientRpc]
    private void FootstepClientRpc(int surfaceIndex, int state) {
        SurfaceClipSet set = ResolveSurfaceSet(surfaceIndex);
        if (set == null) return;

        PlayerMovementState movementState = (PlayerMovementState)state;
        AudioClip[] pool = movementState switch {
            PlayerMovementState.Walking => set.walkClips,
            PlayerMovementState.Running => set.walkClips,
            PlayerMovementState.Crouching => set.crouchClips,
            _ => null
        };
        if (pool == null || pool.Length == 0) return;

        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.volume = movementState == PlayerMovementState.Crouching ? crouchVolume : 1f;
        audioSource.PlayOneShot(pool[Random.Range(0, pool.Length)]);
    }

    // Returns the index into surfaceSets, or -1 for defaultSet.
    private int GetSurfaceIndex() {
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastDistance, groundMask)) {
            for (int i = 0; i < surfaceSets.Length; i++)
                if (hit.collider.CompareTag(surfaceSets[i].surfaceTag))
                    return i;
        }
        return -1;
    }

    private SurfaceClipSet ResolveSurfaceSet(int index) =>
        index >= 0 && index < surfaceSets.Length ? surfaceSets[index] : defaultSet;
}