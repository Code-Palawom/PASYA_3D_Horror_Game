using System.Collections.Generic;
using UnityEngine;

// Sits above a player's head (spawned by SideEffectUIManager).
// Shows active side effect icons visible to all players.
public class WorldEffectBillboard : MonoBehaviour {
    [SerializeField] Transform iconContainer;           // Horizontal Layout Group
    [SerializeField] GameObject worldIconPrefab;

    private Dictionary<QuizSideEffect, GameObject> _icons = new();

    public void AddEffect(QuizSideEffect effect) {
        if (_icons.ContainsKey(effect)) return;

        iconContainer.gameObject.SetActive(true);
        var icon = Instantiate(worldIconPrefab, iconContainer);
        icon.GetComponent<WorldEffectIcon>().Setup(effect);
        _icons[effect] = icon;
    }

    public void RemoveEffect(QuizSideEffect effect) {
        if (!_icons.TryGetValue(effect, out var icon)) return;

        Destroy(icon);
        _icons.Remove(effect);

        if (_icons.Count == 0)
            iconContainer.gameObject.SetActive(false);
    }

    void LateUpdate() {
        if (Camera.main == null) return;
        transform.LookAt(Camera.main.transform);
        transform.Rotate(0, 180f, 0);
    }
}