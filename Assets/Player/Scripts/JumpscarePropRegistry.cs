using System.Collections.Generic;
using UnityEngine;

// Scene-side lookup from JumpscareLocationSet.Entry.propId to the Animator
// on the actual prop placed in this scene. Needed because the location data
// lives in a ScriptableObject asset, which can't reference scene objects.
// Drop one of these in each scene that has jumpscare props and fill in the
// list (propId -> Animator) in the inspector. propId values must match the
// ones used in JumpscareLocationSet entries for that scene.
public class JumpscarePropRegistry : MonoBehaviour {
    public static JumpscarePropRegistry Instance { get; private set; }

    [System.Serializable]
    public class PropEntry {
        public string propId;
        public Animator animator;
    }

    [SerializeField] private PropEntry[] props;

    private Dictionary<string, Animator> lookup;

    private void Awake() {
        Instance = this;
        lookup = new Dictionary<string, Animator>();
        foreach (var p in props) {
            if (!string.IsNullOrEmpty(p.propId) && p.animator != null)
                lookup[p.propId] = p.animator;
        }
    }

    private void OnDestroy() {
        if (Instance == this) Instance = null;
    }

    public Animator GetAnimator(string propId) {
        if (!string.IsNullOrEmpty(propId) && lookup.TryGetValue(propId, out var animator))
            return animator;
        return null;
    }
}