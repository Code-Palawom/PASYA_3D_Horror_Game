using UnityEngine;

public class DebugSetting : MonoBehaviour {
    public SaveManager saveManager;
    public FirstPersonCameraLook firstPersonCameraLook;
    public ThirdPersonCameraLook thirdPersonCameraLook;

    public bool showOverlay = false;

    public int fontSize = 13;
    public Color textColor = new Color(1f, 1f, 1f, 0.9f);
    public Color bgColor = new Color(0f, 0f, 0f, 0.55f);
    public float padding = 10f;

    private float fps;
    private float fpsTimer;
    private int frameCount;

    private GUIStyle labelStyle;
    private bool stylesInitialized = false;

    void Start() {
        RefreshDebugMode();
    }

    public void RefreshDebugMode() {
        showOverlay = saveManager.GetOneData(0);
        firstPersonCameraLook.RefreshDebugMode();
        thirdPersonCameraLook.RefreshDebugMode();
    }

    void Update() {
        frameCount++;
        fpsTimer += Time.unscaledDeltaTime;
        if(fpsTimer >= 0.5f){
            fps = frameCount / fpsTimer;
            frameCount = 0;
            fpsTimer = 0f;
        }
    }

    void OnGUI() {
        if(!showOverlay) return;

        InitStyles();

        string info = BuildInfo();

        Vector2 size = labelStyle.CalcSize(new GUIContent(info));
        float w = size.x + padding * 2;
        float h = size.y + padding * 2;
        float x = Screen.width - w - 10f;
        float y = 10f;

        GUI.color = bgColor;
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(x + padding, y + padding, size.x, size.y), info, labelStyle);
    }

    string BuildInfo() {
        var q = QualitySettings.names;
        int level = QualitySettings.GetQualityLevel();

        string fpsColor = fps >= 60 ? "lime" : fps >= 30 ? "yellow" : "red";

        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"<b>Quality: {QualitySettings.names[level]} (Level {level})</b>");
        sb.AppendLine($"FPS: <color={fpsColor}>{fps:F0}</color>   Frame: {(Time.unscaledDeltaTime * 1000f):F1}ms");
        sb.AppendLine("──────────────────────");

        sb.AppendLine($"Mipmap Limit: {QualitySettings.globalTextureMipmapLimit}");
        sb.AppendLine($"Aniso Textures: {QualitySettings.anisotropicFiltering}");

        sb.AppendLine($"Shadows: {QualitySettings.shadows}");
        sb.AppendLine($"Shadow Res: {QualitySettings.shadowResolution}");
        sb.AppendLine($"Shadow Distance: {QualitySettings.shadowDistance:F0}m");
        sb.AppendLine($"Shadow Cascades: {QualitySettings.shadowCascades}");
        sb.AppendLine($"Shadowmask Mode: {QualitySettings.shadowmaskMode}");

        sb.AppendLine($"Pixel Lights: {QualitySettings.pixelLightCount}");
        sb.AppendLine($"Anti Aliasing: {(QualitySettings.antiAliasing == 0 ? "Off" : QualitySettings.antiAliasing + "x")}");
        sb.AppendLine($"Soft Particles: {(QualitySettings.softParticles ? "On" : "Off")}");
        sb.AppendLine($"RT Reflections: {(QualitySettings.realtimeReflectionProbes ? "On" : "Off")}");
        sb.AppendLine($"VSync: {QualitySettings.vSyncCount}");

        sb.AppendLine($"LOD Bias: {QualitySettings.lodBias:F2}");
        sb.AppendLine($"Max LOD Level: {QualitySettings.maximumLODLevel}");

        sb.AppendLine($"Skin Weights: {QualitySettings.skinWeights}");

        sb.AppendLine($"Particle Raycast: {QualitySettings.particleRaycastBudget}");

        return sb.ToString();
    }

    void InitStyles() {
        if(stylesInitialized) return;

        labelStyle = new GUIStyle(GUI.skin.label){
            fontSize = fontSize,
            richText = true,
            wordWrap = false,
        };
        labelStyle.normal.textColor = textColor;
        stylesInitialized = true;
    }
}