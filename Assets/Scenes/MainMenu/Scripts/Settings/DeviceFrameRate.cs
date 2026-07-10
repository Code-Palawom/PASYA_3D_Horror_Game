using UnityEngine;

public static class DeviceFrameRate {
    // Returns the maximum refresh rate (Hz) supported by the device's default display.
    // Falls back to Screen.currentResolution.refreshRateRatio on non-Android or on failure.
    public static float GetMaxRefreshRate() {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject windowManager = activity.Call<AndroidJavaObject>("getWindowManager"))
            using (AndroidJavaObject display = windowManager.Call<AndroidJavaObject>("getDefaultDisplay"))
            {
                // Display.getSupportedModes() -> Display.Mode[]
                AndroidJavaObject[] modes = display.Call<AndroidJavaObject[]>("getSupportedModes");

                float maxHz = 0f;
                foreach (AndroidJavaObject mode in modes)
                {
                    float hz = mode.Call<float>("getRefreshRate");
                    if (hz > maxHz) maxHz = hz;
                    mode.Dispose();
                }

                if (maxHz > 0f)
                    return maxHz;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AndroidDisplayInfo] Failed to query supported modes: {e.Message}");
        }
#endif
        // Editor / fallback: current refresh rate
        RefreshRate rr = Screen.currentResolution.refreshRateRatio;
        return (float)rr.numerator / rr.denominator;
    }
}