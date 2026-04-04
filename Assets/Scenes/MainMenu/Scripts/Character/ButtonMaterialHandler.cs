using UnityEngine;

public class ButtonMaterialHandler : MonoBehaviour {
    [SerializeField] CharacterManager characterManager;
    [SerializeField] Mesh mesh = null;
    [SerializeField] Material[] material = null;

    public void HandleClick(string type) {
        if(type == "cosmetic") {
            characterManager.TryCosmetic(mesh, material);
        }else if(type == "gender") {

        }else if(type == "skin") {
            characterManager.TrySkinMaterial(material[0]);
        }else if(type == "hair") {
            characterManager.TryHairMaterial(material);
        }else if(type == "head") {
            characterManager.TryHeadMaterial(mesh, material);
        }else if(type == "body") {
            characterManager.TryBodyMaterial(material);
        }else if(type == "pants") {
            characterManager.TryPantsMaterial(material);
        }else if(type == "shoes") {
            characterManager.TryShoesMaterial(material);
        }
    }
}
