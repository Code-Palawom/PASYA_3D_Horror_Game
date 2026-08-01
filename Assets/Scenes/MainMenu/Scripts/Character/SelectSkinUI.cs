using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SkinSelectUI : MonoBehaviour {
    [SerializeField] private SkinDatabaseSO database;
    [SerializeField] private CharacterAppearanceController appearance;
    [SerializeField] private Transform contentParent;
    [SerializeField] private SkinButtonUI buttonPrefab;
    [SerializeField] private Button confirmButton;
    [SerializeField] private MainMenuUI mainMenuUI;

    private readonly List<SkinButtonUI> spawnedButtons = new();
    private CharacterSkinSO previewSkin;

    private void OnEnable() {
        // Reset the preview skin to the saved one whenever the panel is opened.
        string savedId = SkinSaveSystem.Load();
        previewSkin = database.GetById(savedId) ?? (database.skins.Length > 0 ? database.skins[0] : null);
        appearance.ApplySkin(previewSkin);
        RefreshHighlight();
    }

    private void Start() {
        //string savedId = SkinSaveSystem.Load();
        //previewSkin = database.GetById(savedId) ?? (database.skins.Length > 0 ? database.skins[0] : null);
        //appearance.ApplySkin(previewSkin);

        foreach (var skin in database.skins) {
            var btn = Instantiate(buttonPrefab, contentParent);
            btn.Init(skin, OnSkinClicked);
            spawnedButtons.Add(btn);
        }

        RefreshHighlight();
        confirmButton.onClick.AddListener(Confirm);
    }

    private void OnSkinClicked(CharacterSkinSO skin) {
        previewSkin = skin;
        appearance.ApplySkin(skin); // live preview, not saved yet
        RefreshHighlight();
    }

    private void RefreshHighlight() {
        for (int i = 0; i < spawnedButtons.Count; i++)
            spawnedButtons[i].SetSelected(database.skins[i] == previewSkin);
    }

    private void Confirm() {
        if (previewSkin != null) {
            SkinSaveSystem.Save(previewSkin.skinId);
            mainMenuUI.HideCharacterPanel(true);
        }
    }
}