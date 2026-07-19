#if PLATFORM_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
using System;
using UnityEngine;

public class MicPermission : MonoBehaviour {
    public void RequestMicThenJoin(Action onGranted, Action onDenied) {
#if PLATFORM_ANDROID && !UNITY_EDITOR
        if (Permission.HasUserAuthorizedPermission(Permission.Microphone)) {
            onGranted?.Invoke();
            return;
        }

        var callbacks = new PermissionCallbacks();
        callbacks.PermissionGranted += (permissionName) => {
            Debug.Log($"Granted: {permissionName}");
            onGranted?.Invoke();
        };
        callbacks.PermissionDenied += (permissionName) => {
            Debug.Log($"Denied: {permissionName}");
            onDenied?.Invoke();
        };

        Permission.RequestUserPermission(Permission.Microphone, callbacks);
#else
        // iOS/other platforms: permission is handled by the OS prompt
        // automatically when Vivox first touches the mic
        onGranted?.Invoke();
#endif
    }
}