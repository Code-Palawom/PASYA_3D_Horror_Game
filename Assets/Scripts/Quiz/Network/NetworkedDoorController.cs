using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Manages open/close state of a door, synced across all clients.
// Attach to the Door root GameObject.
// The Hinge child is what actually rotates.
public class NetworkedDoorController : NetworkBehaviour {
    [Header("References")]
    [SerializeField] Transform hinge;               // the empty Hinge child

    [Header("Animation")]
    [SerializeField] float openAngle = 90f;      // degrees to rotate when open
    [SerializeField] float animDuration = 0.5f;     // seconds to complete swing
    [SerializeField] bool invertAngle = false;    // flip direction if needed

    // ── Networked state ───────────────────────────────────────
    private NetworkVariable<bool> _isOpen = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsOpen => _isOpen.Value;

    private Coroutine _animCoroutine;

    // ─────────────────────────────────────────────────────────
    // Sync animation when NetworkVariable changes
    // ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn() {
        _isOpen.OnValueChanged += OnDoorStateChanged;
    }

    public override void OnNetworkDespawn() {
        _isOpen.OnValueChanged -= OnDoorStateChanged;
    }

    void OnDoorStateChanged(bool previous, bool current) {
        // Runs on all clients when server changes _isOpen
        PlayAnimation(current);
    }

    // ─────────────────────────────────────────────────────────
    // Called by DoorHandleInteractable
    // ─────────────────────────────────────────────────────────
    public void RequestToggle() {
        ToggleDoorRpc();
    }

    [Rpc(SendTo.Server)]
    void ToggleDoorRpc() {
        Debug.Log("[RPC][Server] ToggleDoor");
        _isOpen.Value = !_isOpen.Value;
    }

    // ─────────────────────────────────────────────────────────
    // Animation — runs locally on every client via OnValueChanged
    // ─────────────────────────────────────────────────────────
    void PlayAnimation(bool opening) {
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateHinge(opening));
    }

    IEnumerator AnimateHinge(bool opening) {
        float target = opening ? (invertAngle ? -openAngle : openAngle) : 0f;
        float start = hinge.localEulerAngles.y;

        // Normalize start angle to -180..180 range for correct Lerp direction
        if (start > 180f) start -= 360f;

        float elapsed = 0f;

        while (elapsed < animDuration) {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
            float angle = Mathf.LerpAngle(start, target, t);
            hinge.localEulerAngles = new Vector3(0f, angle, 0f);
            yield return null;
        }

        hinge.localEulerAngles = new Vector3(0f, target, 0f);
        _animCoroutine = null;
    }
}