#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

// Captures the current skybox (RenderSettings.skybox) as a 360 equirectangular PNG.
// Requires the companion "Hidden/CubemapToEquirect" shader (CubemapToEquirect.shader)
// to be present anywhere in the project.
public static class SkyboxPanoramaCapture
{
    private const int CubemapFaceSize = 2048;
    private const int OutputWidth = 4096;
    private const int OutputHeight = 2048; // keep 2:1 for a valid equirect panorama

    [MenuItem("Tools/Skybox/Capture 360 Panorama PNG")]
    public static void CapturePanorama()
    {
        if (RenderSettings.skybox == null)
        {
            Debug.LogError("No skybox material assigned in Lighting > Environment > Skybox Material.");
            return;
        }

        // Temporary camera used purely to render the cubemap
        GameObject camGO = new GameObject("TempSkyboxCaptureCam");
        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.cullingMask = 0; // don't render any scene geometry, only sky
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;

        Cubemap cubemap = new Cubemap(CubemapFaceSize, TextureFormat.RGBA32, false);

        bool success = cam.RenderToCubemap(cubemap);
        Object.DestroyImmediate(camGO);

        if (!success)
        {
            Debug.LogError("RenderToCubemap failed. Check that your GPU/render pipeline supports cubemap rendering.");
            Object.DestroyImmediate(cubemap);
            return;
        }

        Shader convertShader = Shader.Find("Hidden/CubemapToEquirect");
        if (convertShader == null)
        {
            Debug.LogError("Could not find shader 'Hidden/CubemapToEquirect'. Make sure CubemapToEquirect.shader is in the project.");
            Object.DestroyImmediate(cubemap);
            return;
        }

        Material convertMat = new Material(convertShader);
        convertMat.SetTexture("_Cube", cubemap);

        RenderTexture rt = new RenderTexture(OutputWidth, OutputHeight, 0, RenderTextureFormat.ARGB32);
        rt.Create();

        Graphics.Blit(null, rt, convertMat);

        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D result = new Texture2D(OutputWidth, OutputHeight, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, OutputWidth, OutputHeight), 0, 0);
        result.Apply();

        RenderTexture.active = prevActive;

        byte[] pngBytes = result.EncodeToPNG();

        string folder = Path.Combine(Application.dataPath, "SkyboxCaptures");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string fileName = $"skybox_panorama_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        string fullPath = Path.Combine(folder, fileName);
        File.WriteAllBytes(fullPath, pngBytes);

        // Cleanup
        Object.DestroyImmediate(cubemap);
        Object.DestroyImmediate(convertMat);
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(result);

        AssetDatabase.Refresh();

        Debug.Log($"Skybox panorama saved to: {fullPath}");
        EditorUtility.RevealInFinder(fullPath);
    }
}
#endif
