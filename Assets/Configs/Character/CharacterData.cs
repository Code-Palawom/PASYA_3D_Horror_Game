using UnityEngine;

[CreateAssetMenu(fileName = "character", menuName = "Configs/Character")]
public class CharacterData : ScriptableObject {
    //int health = 100;
    [Header("Mesh")]
    public Mesh rightHandCosmeticMesh;
    public Mesh headMesh;
    public Mesh hairMesh;
    public Mesh bodyMesh;
    public Mesh armsMesh;
    public Mesh legsMesh;
    public Mesh pantsMesh;
    public Mesh shoesMesh;
    
    [Header("Material")]
    public Material[] rightHandCosmeticMaterial;
    public Material skinMaterial;
    public Material[] hairMaterial;
    public Material[] headMaterial;
    public Material[] bodyMaterial;
    public Material[] pantsMaterial;
    public Material[] shoesMaterial;
    //public RuntimeAnimatorController animatorController;
}