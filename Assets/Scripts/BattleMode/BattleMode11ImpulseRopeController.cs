using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class BattleMode11ImpulseRopeController : MonoBehaviour, IIndestructibleKickedBombHandler
{
    const string BattleMode11SceneName = "BattleMode_11";

    [System.Serializable]
    public sealed class RopeTileAnimation
    {
        public TileBase tile;
        public TileBase[] animationTiles;
    }

    [Header("SFX")]
    [SerializeField] private AudioClip ropeBounceSfx;
    [SerializeField, Range(0f, 1f)] private float ropeBounceSfxVolume = 1f;

    [Header("Rope Deformation")]
    [SerializeField, Min(0.01f)] private float ropeDeformationSeconds = 0.25f;
    [SerializeField] private RopeTileAnimation[] ropeTileAnimations;

    readonly Dictionary<Vector3Int, RunningTileSwap> runningSwaps = new();

    sealed class RunningTileSwap
    {
        public Coroutine routine;
        public TileBase originalTile;
        public Tilemap tilemap;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootstrapOnInitialScene()
    {
        EnsureForActiveScene();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForActiveScene();
    }

    static void EnsureForActiveScene()
    {
        if (!Application.isPlaying)
            return;

        if (!IsBattleMode11Active())
            return;

        if (FindAnyObjectByType<BattleMode11ImpulseRopeController>() != null)
            return;

        var host = new GameObject(nameof(BattleMode11ImpulseRopeController));
        host.AddComponent<BattleMode11ImpulseRopeController>();
    }

    void Awake()
    {
        if (!IsBattleMode11Active())
            Destroy(gameObject);
    }

    public bool TryHandleKickedBombBlocked(
        Bomb bomb,
        Vector2 currentWorldPos,
        Vector2 blockedWorldPos,
        Vector2 kickDirection,
        Tilemap indestructibleTilemap,
        Vector3Int blockedCell,
        TileBase blockedTile,
        out AudioClip bounceSfx,
        out float bounceSfxVolume)
    {
        bounceSfx = null;
        bounceSfxVolume = 1f;

        if (!IsBattleMode11Active() || bomb == null || blockedTile == null)
            return false;

        Vector2 dir = ToCardinal(kickDirection);
        if (!IsImpulseRopeImpact(blockedCell, dir))
            return false;

        StartRopeDeformation(indestructibleTilemap, blockedCell, dir);

        bounceSfx = ropeBounceSfx;
        bounceSfxVolume = ropeBounceSfxVolume;
        return true;
    }

    void StartRopeDeformation(Tilemap tilemap, Vector3Int hitCell, Vector2 impactDirection)
    {
        if (tilemap == null)
            return;

        if (!TryGetImpulseRopeSegment(hitCell, impactDirection, out Vector3Int startCell, out Vector3Int endCell))
            return;

        Vector3Int step = GetSegmentStep(startCell, endCell);
        if (step == Vector3Int.zero)
            return;

        Vector3Int cell = startCell;
        while (true)
        {
            TileBase originalTile = tilemap.GetTile(cell);
            if (!TryGetAnimationTiles(originalTile, out TileBase[] animationTiles))
                goto NEXT_CELL;

            StartTileSwap(tilemap, cell, originalTile, animationTiles);

        NEXT_CELL:
            if (cell == endCell)
                break;

            cell += step;
        }
    }

    void StartTileSwap(Tilemap tilemap, Vector3Int cell, TileBase originalTile, TileBase[] animationTiles)
    {
        if (runningSwaps.TryGetValue(cell, out RunningTileSwap running))
        {
            if (running.routine != null)
                StopCoroutine(running.routine);

            if (running.tilemap != null)
            {
                running.tilemap.SetTile(cell, running.originalTile);
                running.tilemap.RefreshTile(cell);
            }

            runningSwaps.Remove(cell);
        }

        RunningTileSwap swap = new()
        {
            originalTile = originalTile,
            tilemap = tilemap
        };

        swap.routine = StartCoroutine(TileSwapRoutine(cell, animationTiles, swap));
        runningSwaps[cell] = swap;
    }

    IEnumerator TileSwapRoutine(Vector3Int cell, TileBase[] animationTiles, RunningTileSwap swap)
    {
        float duration = Mathf.Max(0.01f, ropeDeformationSeconds);
        float elapsed = 0f;
        int lastFrame = -1;

        while (elapsed < duration)
        {
            if (swap.tilemap == null)
                yield break;

            float progress = Mathf.Clamp01(elapsed / duration);
            int frame = Mathf.Clamp(Mathf.FloorToInt(progress * animationTiles.Length), 0, animationTiles.Length - 1);

            if (frame != lastFrame)
            {
                swap.tilemap.SetTile(cell, animationTiles[frame]);
                swap.tilemap.RefreshTile(cell);
                lastFrame = frame;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (swap.tilemap != null)
        {
            swap.tilemap.SetTile(cell, swap.originalTile);
            swap.tilemap.RefreshTile(cell);
        }

        runningSwaps.Remove(cell);
    }

    bool TryGetAnimationTiles(TileBase tile, out TileBase[] animationTiles)
    {
        animationTiles = null;

        if (tile == null || ropeTileAnimations == null)
            return false;

        for (int i = 0; i < ropeTileAnimations.Length; i++)
        {
            RopeTileAnimation entry = ropeTileAnimations[i];
            if (entry == null || entry.tile != tile || entry.animationTiles == null || entry.animationTiles.Length == 0)
                continue;

            animationTiles = entry.animationTiles;
            return true;
        }

        return false;
    }

    static bool IsImpulseRopeImpact(Vector3Int cell, Vector2 direction)
    {
        if (direction == Vector2.up)
            return cell.y == 5 && cell.x >= -7 && cell.x <= 5;

        if (direction == Vector2.right)
            return cell.x == 6 && cell.y >= -6 && cell.y <= 4;

        if (direction == Vector2.down)
            return cell.y == -7 && cell.x >= -7 && cell.x <= 5;

        if (direction == Vector2.left)
            return cell.x == -8 && cell.y >= -6 && cell.y <= 4;

        return false;
    }

    static bool TryGetImpulseRopeSegment(
        Vector3Int hitCell,
        Vector2 direction,
        out Vector3Int startCell,
        out Vector3Int endCell)
    {
        startCell = default;
        endCell = default;

        if (direction == Vector2.up && hitCell.y == 5 && hitCell.x >= -7 && hitCell.x <= 5)
        {
            startCell = new Vector3Int(-7, 5, 0);
            endCell = new Vector3Int(5, 5, 0);
            return true;
        }

        if (direction == Vector2.right && hitCell.x == 6 && hitCell.y >= -6 && hitCell.y <= 4)
        {
            startCell = new Vector3Int(6, 4, 0);
            endCell = new Vector3Int(6, -6, 0);
            return true;
        }

        if (direction == Vector2.down && hitCell.y == -7 && hitCell.x >= -7 && hitCell.x <= 5)
        {
            startCell = new Vector3Int(5, -7, 0);
            endCell = new Vector3Int(-7, -7, 0);
            return true;
        }

        if (direction == Vector2.left && hitCell.x == -8 && hitCell.y >= -6 && hitCell.y <= 4)
        {
            startCell = new Vector3Int(-8, -6, 0);
            endCell = new Vector3Int(-8, 4, 0);
            return true;
        }

        return false;
    }

    static Vector3Int GetSegmentStep(Vector3Int startCell, Vector3Int endCell)
    {
        Vector3Int delta = endCell - startCell;

        if (delta.x != 0)
            return new Vector3Int(delta.x > 0 ? 1 : -1, 0, 0);

        if (delta.y != 0)
            return new Vector3Int(0, delta.y > 0 ? 1 : -1, 0);

        return Vector3Int.zero;
    }

    static Vector2 ToCardinal(Vector2 direction)
    {
        if (direction == Vector2.zero)
            return Vector2.zero;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            return direction.x > 0f ? Vector2.right : Vector2.left;

        return direction.y > 0f ? Vector2.up : Vector2.down;
    }

    static bool IsBattleMode11Active()
        => string.Equals(SceneManager.GetActiveScene().name, BattleMode11SceneName, System.StringComparison.Ordinal);
}
