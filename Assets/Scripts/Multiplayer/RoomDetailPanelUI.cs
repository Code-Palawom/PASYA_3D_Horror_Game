using NUnit.Framework;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Right-hand detail panel in the Join tab — shows the selected room's
// preview image, level name, quiz info, and connected player names.
// Mirrors the reference layout but swaps gamemode/weapon-preset/etc.
// for the fields relevant to a quiz game: quiz name + question count.
public class RoomDetailPanelUI : MonoBehaviour {
    [Header("Preview")]
    [SerializeField] Image previewImage;
    [SerializeField] TMP_Text levelNameLabel;

    [Header("Info")]
    [SerializeField] TMP_Text quizNameLabel;
    [SerializeField] TMP_Text questionCountLabel;

    [Header("Connected Players")]
    [SerializeField] Transform playerListContainer;
    [SerializeField] MenuPlayerItemUI playerItemPrefab;

    private readonly List<MenuPlayerItemUI> _spawnedItems = new();

    // ─────────────────────────────────────────────────────────
    public void Show(DiscoveredHost host, string levelDisplayName, Sprite preview) {
        gameObject.SetActive(true);

        if (previewImage != null) {
            previewImage.sprite = preview;
            previewImage.gameObject.SetActive(preview != null);
        }

        if (levelNameLabel != null)
            levelNameLabel.text = levelDisplayName;

        if (quizNameLabel != null)
            quizNameLabel.text = host.QuizSetName;

        if (questionCountLabel != null)
            questionCountLabel.text = $"{host.QuestionCount} questions";

        RefreshPlayerList(host.PlayerNames);
    }

    public void ShowOnline(OnlineDiscoveredSession session, string levelDisplayName, Sprite preview) {
        gameObject.SetActive(true);

        if (previewImage != null) {
            previewImage.sprite = preview;
            previewImage.gameObject.SetActive(preview != null);
        }

        if (levelNameLabel != null)
            levelNameLabel.text = levelDisplayName;

        if (quizNameLabel != null)
            quizNameLabel.text = session.QuizSetName;

        if (questionCountLabel != null)
            questionCountLabel.text = $"{session.QuestionCount} questions";

        RefreshPlayerList(new List<string>());
    }

    public void Hide() => gameObject.SetActive(false);

    // Visible-by-default placeholder state — shown before any room
    // is selected, instead of leaving the panel empty/hidden.
    public void ShowEmpty() {
        gameObject.SetActive(true);

        //if (previewImage != null)
        //    previewImage.gameObject.SetActive(false);

        if (levelNameLabel != null)
            levelNameLabel.text = "Select a room";

        if (quizNameLabel != null)
            quizNameLabel.text = "—";

        if (questionCountLabel != null)
            questionCountLabel.text = "—";

        RefreshPlayerList(null);
    }

    // ─────────────────────────────────────────────────────────
    void RefreshPlayerList(List<string> names) {
        foreach (var item in _spawnedItems) Destroy(item.gameObject);
        _spawnedItems.Clear();

        if (names == null) return;

        foreach (var name in names) {
            var item = Instantiate(playerItemPrefab, playerListContainer);
            item.Setup(name);
            _spawnedItems.Add(item);
        }
    }
}