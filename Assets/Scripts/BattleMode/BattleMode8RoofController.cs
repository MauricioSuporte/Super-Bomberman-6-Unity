using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class BattleMode8RoofController : MonoBehaviour
{
    const string BattleMode8SceneName = "BattleMode_8";

    [System.Serializable]
    public struct RoofTileSwap
    {
        public TileBase closedTile;
        public TileBase firstTransitionTile;
        public TileBase secondTransitionTile;
        public TileBase openTile;
    }

    [Header("Tilemap")]
    [SerializeField] private Tilemap roofTilemap;

    [Header("Tiles")]
    [SerializeField] private RoofTileSwap[] roofTiles;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float toggleSeconds = 5f;
    [SerializeField, Min(0f)] private float transitionSeconds = 1f;
    [SerializeField] private bool startClosed = true;

    readonly Dictionary<TileBase, int> swapIndexByTile = new();
    readonly Dictionary<Vector3Int, TileBase> closedTileByClearedCell = new();

    Coroutine transitionRoutine;
    float elapsed;
    bool closed;

    void Awake()
    {
        closed = startClosed;
        ResolveReferences();
        RebuildTileMaps();
        ApplyCurrentState();
    }

    void OnEnable()
    {
        elapsed = 0f;
        closed = startClosed;
        ResolveReferences();
        RebuildTileMaps();
        ApplyCurrentState();
    }

    void OnValidate()
    {
        toggleSeconds = Mathf.Max(0.01f, toggleSeconds);
        transitionSeconds = Mathf.Max(0f, transitionSeconds);
    }

    void Update()
    {
        if (!IsBattleMode8Active())
            return;

        ResolveReferences();
        if (transitionRoutine != null || roofTilemap == null || roofTiles == null || roofTiles.Length == 0)
            return;

        if (GamePauseController.IsPaused)
            return;

        elapsed += Time.deltaTime;
        if (elapsed < toggleSeconds)
            return;

        elapsed -= toggleSeconds;
        transitionRoutine = StartCoroutine(TransitionRoutine(opening: closed));
    }

    [ContextMenu("Apply Current Roof State")]
    void ApplyCurrentState()
    {
        ResolveReferences();
        RebuildTileMaps();

        if (roofTilemap == null)
            return;

        bool changed = false;
        BoundsInt bounds = roofTilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            TileBase currentTile = roofTilemap.GetTile(cell);
            if (!TryGetReplacementTile(cell, currentTile, closed ? RoofTilePhase.Closed : RoofTilePhase.Open, out TileBase replacementTile))
                continue;

            roofTilemap.SetTile(cell, replacementTile);
            changed = true;
        }

        if (changed)
            roofTilemap.RefreshAllTiles();
    }

    IEnumerator TransitionRoutine(bool opening)
    {
        if (transitionSeconds <= 0f)
        {
            closed = !opening;
            ApplyCurrentState();
            transitionRoutine = null;
            yield break;
        }

        RoofTilePhase firstPhase = opening ? RoofTilePhase.FirstTransition : RoofTilePhase.SecondTransition;
        RoofTilePhase secondPhase = opening ? RoofTilePhase.SecondTransition : RoofTilePhase.FirstTransition;
        RoofTilePhase finalPhase = opening ? RoofTilePhase.Open : RoofTilePhase.Closed;

        float stepSeconds = transitionSeconds / 3f;
        ApplyPhase(firstPhase);
        yield return WaitTransitionStep(stepSeconds);

        ApplyPhase(secondPhase);
        yield return WaitTransitionStep(stepSeconds);

        yield return WaitTransitionStep(stepSeconds);
        closed = !opening;
        ApplyPhase(finalPhase);
        transitionRoutine = null;
    }

    IEnumerator WaitTransitionStep(float seconds)
    {
        float waited = 0f;
        while (waited < seconds)
        {
            if (!GamePauseController.IsPaused)
                waited += Time.deltaTime;

            yield return null;
        }
    }

    void ApplyPhase(RoofTilePhase phase)
    {
        ResolveReferences();
        RebuildTileMaps();

        if (roofTilemap == null)
            return;

        bool changed = false;
        BoundsInt bounds = roofTilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            TileBase currentTile = roofTilemap.GetTile(cell);
            if (!TryGetReplacementTile(cell, currentTile, phase, out TileBase replacementTile))
                continue;

            roofTilemap.SetTile(cell, replacementTile);
            changed = true;
        }

        if (changed)
            roofTilemap.RefreshAllTiles();
    }

    bool TryGetReplacementTile(Vector3Int cell, TileBase currentTile, RoofTilePhase targetPhase, out TileBase replacementTile)
    {
        replacementTile = null;

        if (targetPhase == RoofTilePhase.Closed)
        {
            if (currentTile == null && closedTileByClearedCell.TryGetValue(cell, out replacementTile))
            {
                closedTileByClearedCell.Remove(cell);
                return replacementTile != null;
            }

            if (currentTile == null)
                return false;

            if (!TryGetSwapForTile(currentTile, out RoofTileSwap swap))
                return false;

            replacementTile = swap.closedTile;
            return replacementTile != currentTile;
        }

        if (currentTile == null)
        {
            if (!closedTileByClearedCell.TryGetValue(cell, out TileBase rememberedClosedTile) ||
                !TryGetSwapForTile(rememberedClosedTile, out RoofTileSwap rememberedSwap))
                return false;

            replacementTile = GetTileForPhase(rememberedSwap, targetPhase);
            return replacementTile != currentTile;
        }

        if (!TryGetSwapForTile(currentTile, out RoofTileSwap currentSwap))
            return false;

        replacementTile = GetTileForPhase(currentSwap, targetPhase);

        if (replacementTile == null)
            closedTileByClearedCell[cell] = currentSwap.closedTile;

        return replacementTile != currentTile;
    }

    void RebuildTileMaps()
    {
        swapIndexByTile.Clear();

        if (roofTiles == null)
            return;

        for (int i = 0; i < roofTiles.Length; i++)
        {
            RegisterSwapTile(roofTiles[i].closedTile, i);
            RegisterSwapTile(roofTiles[i].firstTransitionTile, i);
            RegisterSwapTile(roofTiles[i].secondTransitionTile, i);
            RegisterSwapTile(roofTiles[i].openTile, i);
        }
    }

    void RegisterSwapTile(TileBase tile, int index)
    {
        if (tile != null)
            swapIndexByTile[tile] = index;
    }

    bool TryGetSwapForTile(TileBase tile, out RoofTileSwap swap)
    {
        if (tile != null && swapIndexByTile.TryGetValue(tile, out int index) && index >= 0 && index < roofTiles.Length)
        {
            swap = roofTiles[index];
            return swap.closedTile != null;
        }

        swap = default;
        return false;
    }

    static TileBase GetTileForPhase(RoofTileSwap swap, RoofTilePhase phase)
    {
        return phase switch
        {
            RoofTilePhase.Closed => swap.closedTile,
            RoofTilePhase.FirstTransition => swap.firstTransitionTile != null ? swap.firstTransitionTile : swap.closedTile,
            RoofTilePhase.SecondTransition => swap.secondTransitionTile != null ? swap.secondTransitionTile : swap.openTile,
            RoofTilePhase.Open => swap.openTile,
            _ => swap.closedTile,
        };
    }

    void ResolveReferences()
    {
        if (roofTilemap != null)
            return;

        roofTilemap = FindTilemapByName("Roof");
    }

    static bool IsBattleMode8Active()
        => string.Equals(SceneManager.GetActiveScene().name, BattleMode8SceneName, System.StringComparison.Ordinal);

    static Tilemap FindTilemapByName(string tilemapName)
    {
        var tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            if (tilemaps[i] != null && string.Equals(tilemaps[i].name, tilemapName, System.StringComparison.OrdinalIgnoreCase))
                return tilemaps[i];
        }

        return null;
    }

    enum RoofTilePhase
    {
        Closed,
        FirstTransition,
        SecondTransition,
        Open
    }
}
