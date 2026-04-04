using UnityEngine;

[CreateAssetMenu(fileName = "character", menuName = "Configs/Character")]
public class CharacterData : ScriptableObject {
    //int health = 100;
    [Header("Mesh")]
    public Mesh cosmeticMesh;
    public Mesh hairMesh;
    public Mesh bodyMesh;
    
    [Header("Material")]
    public Material hairMaterial;
    public Material bodyMaterial;
    public Material[] cosmeticMaterial;
    //public RuntimeAnimatorController animatorController;
}