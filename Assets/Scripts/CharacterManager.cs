using UnityEditor.Overlays;
using UnityEngine;

public class CharacterManager : MonoBehaviour {
    public SaveManager saveManager;
    public ChangeCharacterHandler character;

    public CharacterData currentData;

    private CharacterData saveData;
    private CharacterData backupCharacterData;

    private Mesh rightHandCosmeticMesh;
    private Material[] rightHandCosmeticMaterial;
    private Material skin;
    private Material[] hair;
    private Material[] head;
    private Material[] body;
    private Material[] pants;
    private Material[] shoes;

    void Start() {
        string savedName = saveManager.Load();

        CharacterData data = Resources.Load<CharacterData>("Characters/character");
        
        if(data != null){
            saveData = Instantiate(data);
            saveData.name = data.name;

            character.ApplyCharacter(saveData, false);
        }
    }

    public void CustomizeCharacter() {
        backupCharacterData = Instantiate(saveData);
        backupCharacterData.name = saveData.name;
    }

    public void TryCosmetic(Mesh mesh, Material[] materials) {
        saveData.rightHandCosmeticMesh = mesh;
        saveData.rightHandCosmeticMaterial = materials;

        character.ApplyCharacter(saveData, true);
    }

    public void TryGender() { }

    public void TrySkinMaterial(Material material) {
        saveData.skinMaterial = material;
        saveData.headMaterial[0] = material;
        saveData.headMaterial[1] = material;
        saveData.headMaterial[5] = material;
        skin = material;

        character.ApplyCharacter(saveData, true);
    }

    public void TryHairMaterial(Material[] material) {
        saveData.hairMaterial = material;
        hair = material;
        character.ApplyCharacter(saveData, true);
    }

    public void TryHeadMaterial(Mesh mesh, Material[] material) {
        saveData.headMesh = mesh;
        saveData.headMaterial = material;

        character.ApplyCharacter(saveData, true);
    }

    public void TryBodyMaterial(Material[] material) {
        saveData.bodyMaterial = material;
        body = material;
        character.ApplyCharacter(saveData, true);
    }

    public void TryPantsMaterial(Material[] material) {
        saveData.pantsMaterial = material;
        pants = material;
        character.ApplyCharacter(saveData, true);
    }

    public void TryShoesMaterial(Material[] material) {
        saveData.shoesMaterial = material;
        shoes = material;
        character.ApplyCharacter(saveData, true);
    }

    public void SetCharacter() {
        Debug.Log("Character Saved!");

        currentData.rightHandCosmeticMesh = rightHandCosmeticMesh;
        currentData.rightHandCosmeticMaterial = rightHandCosmeticMaterial;
        if(skin != null) currentData.skinMaterial = skin;
        if(hair != null) currentData.hairMaterial = hair;
        if(head != null) currentData.headMaterial = head;
        if(body != null) currentData.bodyMaterial = body;
        if(pants != null) currentData.pantsMaterial = pants;
        if(shoes != null) currentData.shoesMaterial = shoes;
    }

    public void RevertCharacter() {
        saveData = backupCharacterData;
        backupCharacterData = Instantiate(saveData);
        backupCharacterData.name = saveData.name;

        character.ApplyCharacter(saveData, true);
    }

    private void OnDestroy() {
        if(backupCharacterData != null){
            Destroy(backupCharacterData);
            backupCharacterData = null;
        }
    }
}