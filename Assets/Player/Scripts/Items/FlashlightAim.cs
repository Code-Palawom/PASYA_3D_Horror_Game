using Unity.Netcode;
using UnityEngine;

// Combines horizontal yaw from the camera-follow rig (which typically
// drives body/rig rotation) with vertical pitch from the main camera
// (which usually only tilts locally, e.g. Cinemachine POV/FreeLook).
// Written to a networked pivot so remote clients see correct aim.
public class FlashlightAim : NetworkBehaviour {
    [SerializeField] Transform cameraFollowRig; // source of yaw
    [SerializeField] Camera playerCamera;       // source of pitch

    void LateUpdate() {
        if (!IsOwner) return;

        float yaw = cameraFollowRig.eulerAngles.y;
        float pitch = playerCamera.transform.eulerAngles.x;

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}