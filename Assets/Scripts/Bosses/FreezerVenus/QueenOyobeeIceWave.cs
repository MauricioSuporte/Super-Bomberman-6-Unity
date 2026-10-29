using System.Collections.Generic;
using UnityEngine;

// The coronation wave sweeps the whole encounter without changing bomb fuses or health immunity.
[DisallowMultipleComponent]
public sealed class QueenOyobeeIceWave : MonoBehaviour
{
    private QueenOyobeeController owner;
    private Vector2 origin;
    private float radius;
    private float maximumRadius;
    private float fadeSeconds;
    private Texture2D texture;
    private Sprite sprite;
    private SpriteRenderer visual;
    private Color32[] pixels;
    private float[] pixelDistances;
    private int width;
    private readonly HashSet<int> frozenPlayers = new();
    public const float Speed = 11f;

    public static QueenOyobeeIceWave Create(QueenOyobeeController owner)
    {
        var go = new GameObject("Queen coronation ice wave");
        var wave = go.AddComponent<QueenOyobeeIceWave>();
        wave.Initialize(owner);
        return wave;
    }

    public void Initialize(QueenOyobeeController queen)
    {
        owner = queen;
        origin = FreezerVenusArena.Snap(queen.Boss.GroundPosition);
        Vector2 min = origin, max = origin;
        var arena = queen.Boss.Arena;
        if (arena.Ground != null)
        {
            foreach (var cell in arena.Ground.cellBounds.allPositionsWithin)
            {
                if (!arena.IsFloor(cell)) continue;
                Vector2 point = arena.Center(cell);
                min = Vector2.Min(min, point - Vector2.one * 0.5f);
                max = Vector2.Max(max, point + Vector2.one * 0.5f);
                maximumRadius = Mathf.Max(maximumRadius, Vector2.Distance(origin, point) + 1f);
            }
        }
        foreach (var player in Object.FindObjectsByType<MovementController>())
        {
            if (!IsLivingPlayer(player)) continue;
            Vector2 point = player.transform.position;
            min = Vector2.Min(min, point - Vector2.one);
            max = Vector2.Max(max, point + Vector2.one);
            maximumRadius = Mathf.Max(maximumRadius, Vector2.Distance(origin, point) + 1f);
        }
        min = (Vector2)FreezerVenusArena.Snap(min - Vector2.one * 0.5f);
        max += Vector2.one * 0.5f;
        width = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) * 16f) + 1);
        int height = Mathf.Max(1, Mathf.CeilToInt((max.y - min.y) * 16f) + 1);
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        pixels = new Color32[width * height];
        pixelDistances = new float[pixels.Length];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixelDistances[y * width + x] = Vector2.Distance(origin, min + new Vector2(x, y) / 16f);
        sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.zero, 16f);
        visual = gameObject.AddComponent<SpriteRenderer>();
        visual.sprite = sprite;
        visual.sortingLayerID = queen.Boss.body.sortingLayerID;
        visual.sortingOrder = queen.Boss.body.sortingOrder + 3;
        transform.position = min;
        Draw(0f);
    }

    private static bool IsLivingPlayer(MovementController player)
        => player != null && player.isActiveAndEnabled && player.CompareTag("Player") && !player.isDead && !player.IsEndingStage;

    public static bool HasReached(Vector2 origin, Vector2 playerPosition, float radius)
        => Vector2.Distance(origin, playerPosition) <= radius + 0.25f;

    private void FixedUpdate()
    {
        if (!Assets.Scripts.Netcode.NetSync.ShouldSimulateLocally) return;
        if (GamePauseController.IsPaused) return;
        if (owner == null || !owner.Boss.CombatActive) { Destroy(gameObject); return; }
        radius += Speed * Time.fixedDeltaTime;
        float displayedRadius = Mathf.Floor(radius * 16f) / 16f;
        foreach (var player in Object.FindObjectsByType<MovementController>())
        {
            if (!IsLivingPlayer(player)) continue;
            maximumRadius = Mathf.Max(maximumRadius, Vector2.Distance(origin, player.transform.position) + 0.5f);
            int id = player.GetEntityId().GetHashCode();
            if (frozenPlayers.Contains(id) || !HasReached(origin, player.transform.position, displayedRadius)) continue;
            var freeze = player.GetComponent<QueenOyobeeFreeze>();
            if (freeze == null) freeze = player.gameObject.AddComponent<QueenOyobeeFreeze>();
            // Unlike ordinary snow, the transformation must freeze every living player.
            if (freeze.TryFreeze(3.2f, force: true)) frozenPlayers.Add(id);
        }
        Draw(displayedRadius);
        if (radius < maximumRadius) return;
        fadeSeconds += Time.fixedDeltaTime;
        visual.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - fadeSeconds / 0.3f));
        if (fadeSeconds >= 0.3f) Destroy(gameObject);
    }

    public void Draw(float displayedRadius)
    {
        // Native-size pixels: a white-blue crest, cyan body and sparse frosty trailing sparks.
        for (int i = 0; i < pixels.Length; i++)
        {
            float behind = displayedRadius - pixelDistances[i];
            Color32 color = default;
            if (behind >= -0.0625f && behind <= 0.0625f) color = new Color32(219, 249, 255, 245);
            else if (behind > 0.0625f && behind <= 0.1875f) color = new Color32(117, 210, 244, 205);
            else if (behind > 0.1875f && behind <= 0.25f) color = new Color32(48, 115, 166, 150);
            else if (behind > 0.25f && behind < 0.65f && ((i % width) * 7 + (i / width) * 11) % 23 < 2)
                color = new Color32(166, 236, 255, 135);
            pixels[i] = color;
        }
        texture.SetPixels32(pixels);
        texture.Apply();
    }

    private void OnDestroy()
    {
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
    }
}
