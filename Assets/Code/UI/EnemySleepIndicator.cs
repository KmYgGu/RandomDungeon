using UnityEngine;

/// <summary>종류별 그림 없이 공유하는 Zzz 표시. 좌표와 크기는 월드 기준으로 맞춘다.</summary>
public sealed class EnemySleepIndicator : MonoBehaviour
{
    private static Sprite sharedIcon;
    private Enemy owner;
    private SpriteRenderer marker;

    public void Bind(Enemy enemy)
    {
        owner = enemy;
        var icon = new GameObject("Zzz");
        icon.transform.SetParent(transform, false);
        marker = icon.AddComponent<SpriteRenderer>();
        marker.sprite = GetIcon();
        UpdateMarker();
    }

    private void LateUpdate() => UpdateMarker();

    private void UpdateMarker()
    {
        if (owner == null || marker == null) return;
        bool visible = owner.IsSleeping && !owner.IsInvisible && !owner.IsDisguised;
        marker.enabled = visible;
        if (!visible || owner.spriteRenderer == null) return;
        var body = owner.spriteRenderer;
        var scale = transform.lossyScale;
        marker.transform.localScale = new Vector3(0.65f / Mathf.Abs(scale.x),
            0.65f / Mathf.Abs(scale.y), 1f);
        marker.transform.position = new Vector3(body.bounds.center.x + 0.18f,
            body.bounds.max.y + 0.16f, body.transform.position.z);
        marker.sortingLayerID = body.sortingLayerID;
        marker.sortingOrder = body.sortingOrder + 10;
    }

    private static Sprite GetIcon()
    {
        if (sharedIcon != null) return sharedIcon;
        const int width = 30, height = 20;
        var ink = new bool[width * height];
        Stamp(ink, width, 2, 2, new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" });
        Stamp(ink, width, 12, 7, new[] { "11111", "00010", "00100", "01000", "11111" });
        Stamp(ink, width, 22, 12, new[] { "11111", "00010", "00100", "01000", "11111" });
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (ink[y * width + x])
                pixels[y * width + x] = new Color32(213, 247, 255, 255);
            else
            {
                bool border = false;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (x + dx >= 0 && x + dx < width && y + dy >= 0 && y + dy < height)
                        border |= ink[(y + dy) * width + x + dx];
                if (border) pixels[y * width + x] = new Color32(27, 39, 57, 255);
            }
        }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Shared enemy Zzz";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        sharedIcon = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 30f);
        return sharedIcon;
    }

    private static void Stamp(bool[] ink, int width, int x, int y, string[] rows)
    {
        for (int row = 0; row < rows.Length; row++)
        for (int col = 0; col < rows[row].Length; col++)
            if (rows[row][col] == '1') ink[(y + rows.Length - row - 1) * width + x + col] = true;
    }
}
