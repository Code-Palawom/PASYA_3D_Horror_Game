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
    Sprinting = 3,
    Jumping = 4,
    Falling = 5,
    Strafing = 6,
    Crouching = 7,
}