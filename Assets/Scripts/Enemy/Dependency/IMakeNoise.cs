using UnityEngine;

// Implement on any component that can emit a detectable noise
// (player footsteps, sprinting, dropped items, broken glass, etc).
public interface IMakesNoise {
    // Called to report a noise event. loudness roughly maps to
    // detection radius (e.g. 3 = footstep, 8 = sprint, 12 = gunshot).
    void EmitNoise(Vector3 position, float loudness);
}