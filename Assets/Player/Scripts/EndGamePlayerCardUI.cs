using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One player's row on the end-game podium screen. Pure display component —
// PodiumScreenUI decides what data goes in and drives the local player's
// XP count-up; this just holds the widgets.
public class EndGamePlayerCardUI : MonoBehaviour {
    [Header("Widgets")]
    //[SerializeField] TMP_Text rankText;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text scoreText;
    [SerializeField] TMP_Text xpText;               // local card: running total; other cards: "+180 XP"
    [SerializeField] TMP_Text accuracyText;          // "7/9 correct"
    [SerializeField] TMP_Text statusText;            // "Left the game" — disconnected only
    [SerializeField] TMP_Text coinsText;
    [SerializeField] Image characterIcon;             // CharacterSkinSO.previewIcon for this player's equipped skin
    //[SerializeField] GameObject statsGroup;          // score/xp/accuracy container, hidden when disconnected
    [SerializeField] GameObject disconnectedOverlay; // gray tint / icon
    [SerializeField] GameObject localPlayerHighlight; // border/background for "you"

    public void Setup(int rank, GameSessionManager.PlayerSessionStats stats, bool isLocalPlayer, Sprite characterIconSprite) {
        //rankText.text = $"#{rank}";
        nameText.text = stats.PlayerName;
        if (localPlayerHighlight != null) localPlayerHighlight.SetActive(isLocalPlayer);

        // Set regardless of disconnected state — the gray overlay tints it,
        // but "who this was" still reads better with the icon than without.
        if (characterIcon != null) {
            characterIcon.enabled = characterIconSprite != null;
            characterIcon.sprite = characterIconSprite;
        }

        bool disconnected = stats.Disconnected;
        if (disconnectedOverlay != null) disconnectedOverlay.SetActive(disconnected);
        //if (statsGroup != null) statsGroup.SetActive(!disconnected);
        if (statusText != null) statusText.gameObject.SetActive(disconnected);

        if (disconnected) {
            if (statusText != null) statusText.text = "Left the game";

            scoreText.text = "Score: -";
            accuracyText.text = "Accuracy: -";
            coinsText.text = "Coins Earned: -";

            // Local player's xpText gets driven frame-by-frame by
            // PodiumScreenUI's count-up coroutine via SetXpValue(); everyone
            // else just gets a static delta since we don't know their totals.
            if (!isLocalPlayer)
                xpText.text = $"+0 XP";
        } else {
            scoreText.text = $"Score: {stats.Score} pts";
            accuracyText.text = $"Accuracy: {stats.QuestionsCorrect}/{stats.QuestionsAnswered} correct";
            coinsText.text = $"Coins Earned: +{Mathf.RoundToInt(GameSessionManager.CalculateXp(stats.Score) * 0.01f)}";

            // Local player's xpText gets driven frame-by-frame by
            // PodiumScreenUI's count-up coroutine via SetXpValue(); everyone
            // else just gets a static delta since we don't know their totals.
            if (!isLocalPlayer)
                xpText.text = $"+{GameSessionManager.CalculateXp(stats.Score)} XP";
        }
    }

    // Called every frame by PodiumScreenUI's count-up coroutine, local player only.
    public void SetXpValue(long currentXp) => xpText.text = $"+{currentXp:N0} XP";
}