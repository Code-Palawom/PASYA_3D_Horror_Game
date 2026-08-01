using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Character/Skin Database")]
public class SkinDatabaseSO : ScriptableObject {
    public CharacterSkinSO[] skins;

    public CharacterSkinSO GetById(string id) => Array.Find(skins, s => s.skinId == id);
}