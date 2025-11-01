using UnityEngine;

public static class MapRenderUtils
{
    // world [-half..+half] -> pixel [0..W/H)
    public static Vector2Int WorldToPixel(Vector2 world, float half, int texW, int texH)
    {
        float u = Mathf.InverseLerp(-half, +half, world.x);
        float v = Mathf.InverseLerp(-half, +half, world.y);
        int x = Mathf.Clamp(Mathf.RoundToInt(u * (texW - 1)), 0, texW - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(v * (texH - 1)), 0, texH - 1);
        return new Vector2Int(x, y);
    }

    public static Vector2 WorldToOverlayAnchored(Vector2 world, float half, RectTransform overlay)
    {
        var size = overlay.rect.size;
        var pivot = overlay.pivot; // 0..1
        float u = Mathf.InverseLerp(-half, +half, world.x);
        float v = Mathf.InverseLerp(-half, +half, world.y);
        Vector2 bottomLeftLocal = new Vector2(-pivot.x * size.x, -pivot.y * size.y);
        return bottomLeftLocal + new Vector2(u * size.x, v * size.y);
    }
}
