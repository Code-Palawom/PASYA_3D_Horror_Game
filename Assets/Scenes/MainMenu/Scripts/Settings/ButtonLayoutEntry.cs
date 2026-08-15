using System;

[Serializable]
public struct ButtonLayoutEntry {
    public float x, y, scale;

    public ButtonLayoutEntry(float x, float y, float scale) {
        this.x = x;
        this.y = y;
        this.scale = scale;
    }
}