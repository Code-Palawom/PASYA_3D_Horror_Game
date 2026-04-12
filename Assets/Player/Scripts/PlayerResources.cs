using Unity.Netcode;
using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;

public class CharacterAppearanceSync : NetworkBehaviour
{
    [Header("Renderers (Assign in Inspector)")]
    public SkinnedMeshRenderer headR;
    public SkinnedMeshRenderer hairR;
    public SkinnedMeshRenderer bodyR;
    public SkinnedMeshRenderer armsR;
    public SkinnedMeshRenderer legsR;
    public SkinnedMeshRenderer pantsR;
    public SkinnedMeshRenderer shoesR;
    public SkinnedMeshRenderer handCosmeticR;

    private static Dictionary<string, Mesh> meshCache;
    private static Dictionary<string, Material> matCache;

    private NetworkVariable<FixedString64Bytes> n_headMesh = new();
    private NetworkVariable<FixedString64Bytes> n_hairMesh = new();
    private NetworkVariable<FixedString64Bytes> n_bodyMesh = new();
    private NetworkVariable<FixedString64Bytes> n_armsMesh = new();
    private NetworkVariable<FixedString64Bytes> n_legsMesh = new();
    private NetworkVariable<FixedString64Bytes> n_pantsMesh = new();
    private NetworkVariable<FixedString64Bytes> n_shoesMesh = new();
    private NetworkVariable<FixedString64Bytes> n_handMesh = new();

    private NetworkVariable<FixedString64Bytes> n_skinMat = new();
    private NetworkVariable<FixedString128Bytes> n_headMat = new();
    private NetworkVariable<FixedString128Bytes> n_hairMat = new();
    private NetworkVariable<FixedString128Bytes> n_bodyMat = new();
    private NetworkVariable<FixedString128Bytes> n_pantsMat = new();
    private NetworkVariable<FixedString128Bytes> n_shoesMat = new();
    private NetworkVariable<FixedString128Bytes> n_handMat = new();

    private void Awake() {
        if(meshCache == null){
            meshCache = new Dictionary<string, Mesh>();
            foreach (var m in Resources.LoadAll<Mesh>(""))
                meshCache[m.name] = m;

            Debug.Log($"[CharacterAppearanceSync] Cached {meshCache.Count} meshes.");
        }

        if(matCache == null){
            matCache = new Dictionary<string, Material>();
            foreach (var m in Resources.LoadAll<Material>(""))
                matCache[m.name] = m;

            Debug.Log($"[CharacterAppearanceSync] Cached {matCache.Count} materials.");
        }
    }

    public override void OnNetworkSpawn() {
        n_headMesh.OnValueChanged += (_, _) => ApplyAll();
        n_hairMesh.OnValueChanged += (_, _) => ApplyAll();
        n_bodyMesh.OnValueChanged += (_, _) => ApplyAll();
        n_armsMesh.OnValueChanged += (_, _) => ApplyAll();
        n_legsMesh.OnValueChanged += (_, _) => ApplyAll();
        n_pantsMesh.OnValueChanged += (_, _) => ApplyAll();
        n_shoesMesh.OnValueChanged += (_, _) => ApplyAll();
        n_handMesh.OnValueChanged += (_, _) => ApplyAll();
        n_skinMat.OnValueChanged += (_, _) => ApplyAll();
        n_headMat.OnValueChanged += (_, _) => ApplyAll();
        n_hairMat.OnValueChanged += (_, _) => ApplyAll();
        n_bodyMat.OnValueChanged += (_, _) => ApplyAll();
        n_pantsMat.OnValueChanged += (_, _) => ApplyAll();
        n_shoesMat.OnValueChanged += (_, _) => ApplyAll();
        n_handMat.OnValueChanged += (_, _) => ApplyAll();

        if(IsOwner){
            CharacterData data = Resources.Load<CharacterData>("Characters/character");
            if(data == null){
                Debug.LogError("[CharacterAppearanceSync] CharacterData not found in Resources!");
            }else{
                UpdateAppearanceServerRpc(
                    data.headMesh?.name ?? "",
                    data.hairMesh?.name ?? "",
                    data.bodyMesh?.name ?? "",
                    data.armsMesh?.name ?? "",
                    data.legsMesh?.name ?? "",
                    data.pantsMesh?.name ?? "",
                    data.shoesMesh?.name ?? "",
                    data.rightHandCosmeticMesh?.name ?? "",
                    data.skinMaterial?.name ?? "",
                    JoinMatNames(data.headMaterial),
                    JoinMatNames(data.hairMaterial),
                    JoinMatNames(data.bodyMaterial),
                    JoinMatNames(data.pantsMaterial),
                    JoinMatNames(data.shoesMaterial),
                    JoinMatNames(data.rightHandCosmeticMaterial)
                );
            }
        }

        ApplyAll();
    }

    [ServerRpc]
    private void UpdateAppearanceServerRpc(
        FixedString64Bytes headM, FixedString64Bytes hairM, FixedString64Bytes bodyM,
        FixedString64Bytes armsM, FixedString64Bytes legsM, FixedString64Bytes pantsM,
        FixedString64Bytes shoesM, FixedString64Bytes handM,
        FixedString64Bytes skinMat,
        FixedString128Bytes headMat, FixedString128Bytes hairMat, FixedString128Bytes bodyMat,
        FixedString128Bytes pantsMat, FixedString128Bytes shoesMat, FixedString128Bytes handMat)
    {
        n_headMesh.Value = headM;
        n_hairMesh.Value = hairM;
        n_bodyMesh.Value = bodyM;
        n_armsMesh.Value = armsM;
        n_legsMesh.Value = legsM;
        n_pantsMesh.Value = pantsM;
        n_shoesMesh.Value = shoesM;
        n_handMesh.Value = handM;
        n_skinMat.Value = skinMat;
        n_headMat.Value = headMat;
        n_hairMat.Value = hairMat;
        n_bodyMat.Value = bodyMat;
        n_pantsMat.Value = pantsMat;
        n_shoesMat.Value = shoesMat;
        n_handMat.Value = handMat;
    }

    private void ApplyAll() {
        ApplyMesh(headR, n_headMesh.Value.ToString());
        ApplyMesh(hairR, n_hairMesh.Value.ToString());
        ApplyMesh(bodyR, n_bodyMesh.Value.ToString());
        ApplyMesh(armsR, n_armsMesh.Value.ToString());
        ApplyMesh(legsR, n_legsMesh.Value.ToString());
        ApplyMesh(pantsR, n_pantsMesh.Value.ToString());
        ApplyMesh(shoesR, n_shoesMesh.Value.ToString());
        ApplyMesh(handCosmeticR, n_handMesh.Value.ToString());

        ApplySingleMaterial(armsR, n_skinMat.Value.ToString());
        ApplySingleMaterial(legsR, n_skinMat.Value.ToString());

        ApplyMaterialArray(headR, n_headMat.Value.ToString());
        ApplyMaterialArray(hairR, n_hairMat.Value.ToString());
        ApplyMaterialArray(bodyR, n_bodyMat.Value.ToString());
        ApplyMaterialArray(pantsR, n_pantsMat.Value.ToString());
        ApplyMaterialArray(shoesR, n_shoesMat.Value.ToString());
        ApplyMaterialArray(handCosmeticR, n_handMat.Value.ToString());
    }

    private void ApplyMesh(SkinnedMeshRenderer r, string meshName) {
        if(r == null) return;

        if(string.IsNullOrEmpty(meshName)){
            r.enabled = false;
            return;
        }

        if(meshCache.TryGetValue(meshName, out var mesh)){
            r.sharedMesh = mesh;
            r.enabled = true;
        }else{
            Debug.LogWarning($"[CharacterAppearanceSync] Mesh not in cache: '{meshName}'");
        }
    }

    private void ApplySingleMaterial(SkinnedMeshRenderer r, string matName) {
        if(r == null || string.IsNullOrEmpty(matName)) return;

        if(matCache.TryGetValue(matName, out var mat)){
            r.material = mat;
        }else{
            Debug.LogWarning($"[CharacterAppearanceSync] Material not in cache: '{matName}'");
        }
    }

    private void ApplyMaterialArray(SkinnedMeshRenderer r, string joined) {
        if(r == null || string.IsNullOrEmpty(joined)) return;

        string[] names = joined.Split(';');
        Material[] result = new Material[names.Length];

        for(int i = 0; i < names.Length; i++){
            if(string.IsNullOrEmpty(names[i])) continue;

            if(matCache.TryGetValue(names[i], out var mat)){
                result[i] = mat;
            }else{
                Debug.LogWarning($"[CharacterAppearanceSync] Material not in cache: '{names[i]}'");
            }
        }

        r.materials = result;
    }

    private static string JoinMatNames(Material[] mats){
        if(mats == null || mats.Length == 0) return "";
        return string.Join(";", System.Array.ConvertAll(mats, m => m != null ? m.name : ""));
    }
}