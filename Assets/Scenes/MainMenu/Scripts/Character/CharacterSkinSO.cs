using UnityEngine;

[CreateAssetMenu(menuName = "Character/Skin")]
public class CharacterSkinSO : ScriptableObject {
    public string skinId;
    public string displayName;
    public GameObject modelPrefab; // Humanoid Avatar assigned, own SkinnedMeshRenderer + rig
    public Sprite previewIcon;
    public bool ownedByDefault; // free/starter skin, no unlock required
}