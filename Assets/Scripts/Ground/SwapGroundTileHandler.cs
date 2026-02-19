using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class SwapGroundTileHandler : MonoBehaviour, IGroundTileExplosionHitHandler, IGroundTileHandler
{
    [Header("Swap Tiles (Ground Tilemap)")]
    [SerializeField] private TileBase tileA;
    [SerializeField] private TileBase tileB;

    [Header("Behavior")]
    [SerializeField] private bool onlyIfMatchesAorB = true;

    [Header("Auto Collect Puzzle Cells")]
    [SerializeField] private Tilemap groundTilemapOverride;
    [SerializeField] private bool autoCollectAllTileAOnStart = true;

    [Header("Completion Action")]
    [SerializeField] private GameObject floatingPlatformToEnable;

    [Header("Completion SFX")]
    [SerializeField] private AudioClip puzzleCompleteSfx;
    [SerializeField, Range(0f, 1f)] private float puzzleCompleteVolume = 1f;

    private readonly HashSet<Vector3Int> _puzzleCells = new();
    private int _remainingA;
    private bool _completedOnce;

    private void Awake()
    {
        if (!autoCollectAllTileAOnStart)
            return;

        Tilemap tm = ResolveTilemapEarly();
        if (tm == null)
            return;

        CachePuzzleCellsFromTileA(tm);
    }

    private Tilemap ResolveTilemapEarly()
    {
        if (groundTilemapOverride != null)
            return groundTilemapOverride;

        var tms = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        for (int i = 0; i < tms.Length; i++)
        {
            var tm = tms[i];
            if (tm == null) continue;

            string n = tm.name.ToLowerInvariant();
            if (n.Contains("ground"))
            {
                groundTilemapOverride = tm;
                return tm;
            }
        }

        return null;
    }

    private void CachePuzzleCellsFromTileA(Tilemap tm)
    {
        _puzzleCells.Clear();
        _completedOnce = false;

        if (tm == null || tileA == null)
        {
            _remainingA = 0;
            return;
        }

        tm.CompressBounds();
        var bounds = tm.cellBounds;

        int found = 0;

        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var c = new Vector3Int(x, y, 0);
                TileBase t = tm.GetTile(c);
                if (t == tileA)
                {
                    _puzzleCells.Add(c);
                    found++;
                }
            }
        }

        _remainingA = found;

        if (_remainingA == 0)
            TryCompleteNow();
    }

    public void OnExplosionHit(
        BombController source,
        Vector2 worldPos,
        Vector3Int cell,
        TileBase groundTile)
    {
        if (source == null)
            return;

        if (tileA == null || tileB == null)
            return;

        Tilemap tm =
            groundTilemapOverride != null ? groundTilemapOverride :
            (source.groundTiles != null ? source.groundTiles : source.stageBoundsTiles);

        if (tm == null)
            return;

        if (autoCollectAllTileAOnStart && _puzzleCells.Count == 0 && !_completedOnce)
            CachePuzzleCellsFromTileA(tm);

        TileBase next;

        if (groundTile == tileA)
            next = tileB;
        else if (groundTile == tileB)
            next = tileA;
        else
        {
            if (onlyIfMatchesAorB)
                return;

            return;
        }

        tm.SetTile(cell, next);
        tm.RefreshTile(cell);

        if (!_completedOnce && _puzzleCells.Count > 0 && _puzzleCells.Contains(cell))
        {
            if (groundTile == tileA && next == tileB)
            {
                _remainingA = Mathf.Max(0, _remainingA - 1);
                if (_remainingA == 0)
                    TryCompleteNow();
            }
            else if (groundTile == tileB && next == tileA)
            {
                _remainingA += 1;
            }
        }
    }

    private void TryCompleteNow()
    {
        if (_completedOnce)
            return;

        _completedOnce = true;

        if (floatingPlatformToEnable != null)
            floatingPlatformToEnable.SetActive(true);

        PlayPuzzleCompleteSfx();
    }

    private void PlayPuzzleCompleteSfx()
    {
        if (puzzleCompleteSfx == null)
            return;

        if (TryGetComponent<AudioSource>(out var src))
        {
            src.PlayOneShot(puzzleCompleteSfx, puzzleCompleteVolume);
            return;
        }

        GameObject temp = new("PuzzleCompleteSFX");
        temp.transform.position = transform.position;

        var audio = temp.AddComponent<AudioSource>();
        audio.clip = puzzleCompleteSfx;
        audio.volume = puzzleCompleteVolume;
        audio.Play();

        Destroy(temp, puzzleCompleteSfx.length);
    }

    public bool TryModifyExplosion(
        BombController source,
        Vector2 worldPos,
        TileBase groundTile,
        ref int radius,
        ref bool pierce)
    {
        return false;
    }
}
