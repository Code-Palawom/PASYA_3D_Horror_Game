using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkinButtonUI : MonoBehaviour {
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button button;
    [SerializeField] private GameObject selectedOutline;

    private CharacterSkinSO skin;
    private Action<CharacterSkinSO> onClick;

    public void Init(CharacterSkinSO skin, Action<CharacterSkinSO> onClick) {
        this.skin = skin;
        this.onClick = onClick;

        icon.sprite = skin.previewIcon;
        label.text = skin.displayName;

        button.onClick.AddListener(() => onClick(skin));
    }

    public void SetSelected(bool selected) => selectedOutline.SetActive(selected);
}