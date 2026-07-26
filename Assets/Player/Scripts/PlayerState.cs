using UnityEngine;

public class PlayerState : MonoBehaviour {

    [field: SerializeField] public PlayerMovementState CurrentPlayerMovementState { get; private set; }

    public void SetPlayerMovementState(PlayerMovementState playerMovementState) {
        CurrentPlayerMovementState = playerMovementState;
    }
}

public enum PlayerMovementState {
    Idling = 0,
    Walking = 1,
    Running = 2,
    Jumping = 3,
    Crouching = 4,
    Falling = 5,
}