using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class NetworkSetup : NetworkBehaviour {
    [SerializeField] private CinemachineCamera firstPersonCam;
    [SerializeField] private CinemachineCamera thirdPersonCam;
    [SerializeField] private AudioListener audioListener;

    public override void OnNetworkSpawn() {
        Debug.Log(IsOwner);
        if(IsOwner){
            gameObject.name = "LocalPlayer_" + OwnerClientId;
            firstPersonCam.enabled = true;
            thirdPersonCam.enabled = true;

            audioListener.enabled = true;
        }else{
            gameObject.name = "RemotePlayer_" + OwnerClientId;
            firstPersonCam.enabled = false;
            thirdPersonCam.enabled = false;

            audioListener.enabled = false;
        }
    }
}
