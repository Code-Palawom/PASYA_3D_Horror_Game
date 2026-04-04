using UnityEditor.Overlays;
using UnityEngine;

public class CharacterManager : MonoBehaviour {
    public SaveManager saveManager;
    public ChangeCharacterHandler character;

    public CharacterData currentData;

    private CharacterData saveData;
    private CharacterData backupCharacterData;

    [Header("Cosmetics")]
    [SerializeField] private Mesh cosmeticMeshWatch;
    [SerializeField] private Material[] cosmeticMaterialWatch;

    private Mesh cosmeticMesh;
    private Material[] cosmeticMaterial;

    private Material hair;
    private Material body;

    void Start() {
        string savedName = saveManager.Load();

        CharacterData data = Resources.Load<CharacterData>("Characters/character");
        
        if(data != null){
            saveData = Instantiate(data);
            saveData.name = data.name;

            character.ApplyCharacter(saveData);
        }
    }

    public void CustomizeCharacter() {
        backupCharacterData = Instantiate(saveData);
        backupCharacterData.name = saveData.name;
    }

    public void TryCosmetic(string uid) {
        GetCosmetics(uid);

        character.ApplyCharacter(saveData);
    }

    public void TryHairMaterial(Material material) {
        saveData.hairMaterial = material;
        hair = material;
        character.ApplyCharacter(saveData);
    }

    public void TryBodyMaterial(Material material) {
        saveData.bodyMaterial = material;
        body = material;
        character.ApplyCharacter(saveData);
    }

    public void SetCharacter() {
        Debug.Log("Character Saved!");

        currentData.cosmeticMesh = cosmeticMesh;
        currentData.cosmeticMaterial = cosmeticMaterial;
        if(hair != null) currentData.hairMaterial = hair;
        if(body != null) currentData.bodyMaterial = body;
    }

    public void RevertCharacter() {
        saveData = backupCharacterData;
        backupCharacterData = Instantiate(saveData);
        backupCharacterData.name = saveData.name;

        character.ApplyCharacter(saveData);
    }

    private void GetCosmetics(string uid) {
        switch(uid) {
            case "watch":
                cosmeticMesh = cosmeticMeshWatch;
                cosmeticMaterial = cosmeticMaterialWatch;
                saveData.cosmeticMesh = cosmeticMeshWatch;
                saveData.cosmeticMaterial = cosmeticMaterialWatch;
                break;
            default:
                cosmeticMesh = null;
                cosmeticMaterial = null;
                saveData.cosmeticMesh = null;
                saveData.cosmeticMaterial = null;
                break;
        }
    }

    private void OnDestroy() {
        if(backupCharacterData != null){
            Destroy(backupCharacterData);
            backupCharacterData = null;
        }
    }
}