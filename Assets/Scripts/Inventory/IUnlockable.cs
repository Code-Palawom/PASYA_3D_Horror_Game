// Anything that can be "unlocked" and queried for that state.
// Implemented by NetworkedQuizGate; implement on NetworkedDoorController too
// if you want doors to gate other doors directly (no quiz involved).
public interface IUnlockable {
    bool IsUnlocked { get; }
}