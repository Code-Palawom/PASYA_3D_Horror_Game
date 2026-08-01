using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkinButtonUI : MonoBehaviour {
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button button;
    [SerializeField] private GameObject selectedOutline;
    [SerializeField] private GameObject lockedOverlay; // grayed-out / lock icon

    public CharacterSkinSO Skin => skin;

    private CharacterSkinSO skin;
    private Action<CharacterSkinSO> onClick;

    public void Init(CharacterSkinSO skin, bool owned, Action<CharacterSkinSO> onClick) {
        this.skin = skin;
        this.onClick = onClick;

        icon.sprite = skin.previewIcon;
        label.text = skin.displayName;

        Debug.Log("owned: " + owned);

        button.interactable = owned;
        if (lockedOverlay != null) lockedOverlay.SetActive(!owned);

        button.onClick.RemoveAllListeners();
        if (owned) button.onClick.AddListener(() => onClick(skin));
    }

    public void SetOwned(bool owned) {
        button.interactable = owned;
        if (lockedOverlay != null) lockedOverlay.SetActive(!owned);
        if (!owned) button.onClick.RemoveAllListeners();
    }

    public void SetSelected(bool selected) => selectedOutline.SetActive(selected);
}