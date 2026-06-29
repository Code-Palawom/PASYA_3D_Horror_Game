using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Central manager for side effect HUD UI.
// Only runs on the local player's screen.
public class SideEffectUIManager : MonoBehaviour {
    public static SideEffectUIManager Instance { get; private set; }

    [Header("HUD (Screen-space)")]
    [SerializeField] Transform hudEffectContainer;
    [SerializeField] GameObject hudEffectEntryPrefab;

    private class ActiveEntry {
        public QuizSideEffect effect;
        public GameObject hudEntry;
    }

    private List<ActiveEntry> _active = new();

    void Awake() => Instance = this;

    public void AddEffect(QuizSideEffect effect, GameObject player, bool isLocalPlayer) {
        if (!isLocalPlayer) return;
        if (_active.Any(e => e.effect == effect)) return;

        Debug.Log("Added SideEFFCTSDAAAAAAAAAAA");

        var hudEntry = Instantiate(hudEffectEntryPrefab, hudEffectContainer);
        hudEntry.GetComponent<HUDEffectEntry>().Setup(effect);

        _active.Add(new ActiveEntry { effect = effect, hudEntry = hudEntry });
    }

    public void RemoveEffect(QuizSideEffect effect, GameObject player) {
        var entry = _active.FirstOrDefault(e => e.effect == effect);
        if (entry == null) return;

        if (entry.hudEntry != null) Destroy(entry.hudEntry);
        _active.Remove(entry);
    }
}