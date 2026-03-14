using UnityEngine;

public class Frame_Rate_Limiter : MonoBehaviour
{
    [SerializeField] private int frameRate = 60;

    private void Start()
    {
        #if UNITY_EDITOR
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = frameRate;
        #endif
    }
}
