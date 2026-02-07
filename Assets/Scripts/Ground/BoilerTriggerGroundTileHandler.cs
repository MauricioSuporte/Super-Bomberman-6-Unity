using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class BoilerTriggerGroundTileHandler : MonoBehaviour, IGroundTileHandler, IGroundTileBombPlacedHandler
{
    [Header("Boiler Trigger")]
    [SerializeField, Min(0f)] private float pullDelaySeconds = 0.5f;
    [SerializeField] private int pullTilesUp = 1;
    [SerializeField, Min(0.01f)] private float pullMoveSeconds = 0.08f;

    [Header("Door (Tilemap)")]
    [SerializeField] private Tilemap doorTilemapOverride;

    [Header("Door Tiles")]
    [SerializeField] private TileBase closedDoorTile;
    [SerializeField] private AnimatedTile openDoorAnimationTile;
    [SerializeField, Min(0.01f)] private float openDoorAnimationSeconds = 0.5f;
    [SerializeField] private TileBase openedDoorTile;

    private bool triggeredOnce;
    private Coroutine routine;

    private readonly Dictionary<Vector3Int, Coroutine> _doorPending = new();

    private static readonly WaitForFixedUpdate waitFixed = new();

    private static FieldInfo bombLastPosField;

    private void Awake()
    {
        if (bombLastPosField == null)
            bombLastPosField = typeof(Bomb).GetField("lastPos", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        foreach (var kv in _doorPending)
            if (kv.Value != null)
                StopCoroutine(kv.Value);

        _doorPending.Clear();
    }

    public bool TryModifyExplosion(BombController source, Vector2 worldPos, TileBase groundTile, ref int radius, ref bool pierce)
        => false;

    public void OnBombPlaced(
        BombController source,
        Vector2 worldPos,
        Vector3Int cell,
        TileBase groundTile,
        GameObject bombGo)
    {
        if (triggeredOnce)
            return;

        if (source == null || bombGo == null)
            return;

        if (!bombGo.TryGetComponent<Bomb>(out var bomb) || bomb == null)
            return;

        if (bomb.HasExploded)
            return;

        triggeredOnce = true;

        Vector2 from = bomb.GetLogicalPosition();
        from.x = Mathf.Round(from.x);
        from.y = Mathf.Round(from.y);

        Vector2 to = from + Vector2.up * pullTilesUp;
        to.x = Mathf.Round(to.x);
        to.y = Mathf.Round(to.y);

        StartDoorOpenSequence(source, to);

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(PullAndExplodeRoutine(source, bombGo, bomb, from, to));
    }

    private void StartDoorOpenSequence(BombController source, Vector2 doorWorldPos)
    {
        Tilemap tm = doorTilemapOverride != null ? doorTilemapOverride : source.groundTiles;
        if (tm == null)
            return;

        if (openDoorAnimationTile == null || openedDoorTile == null)
            return;

        Vector3Int doorCell = tm.WorldToCell(doorWorldPos);

        if (_doorPending.TryGetValue(doorCell, out var c) && c != null)
            StopCoroutine(c);

        _doorPending[doorCell] = StartCoroutine(DoorOpenRoutine(tm, doorCell));
    }

    private IEnumerator DoorOpenRoutine(Tilemap tm, Vector3Int cell)
    {
        if (tm == null)
            yield break;

        TileBase original = tm.GetTile(cell);

        if (openedDoorTile != null && original == openedDoorTile)
        {
            _doorPending.Remove(cell);
            yield break;
        }

        if (closedDoorTile != null && original != null && original != closedDoorTile)
        {
            _doorPending.Remove(cell);
            yield break;
        }

        tm.SetTile(cell, openDoorAnimationTile);
        tm.RefreshTile(cell);

        float dur = Mathf.Max(0.01f, openDoorAnimationSeconds);
        yield return new WaitForSeconds(dur);

        if (tm == null)
        {
            _doorPending.Remove(cell);
            yield break;
        }

        if (tm.GetTile(cell) == openDoorAnimationTile)
        {
            tm.SetTile(cell, openedDoorTile);
            tm.RefreshTile(cell);
        }

        _doorPending.Remove(cell);
    }

    private IEnumerator PullAndExplodeRoutine(BombController source, GameObject bombGo, Bomb bomb, Vector2 from, Vector2 to)
    {
        if (pullDelaySeconds > 0f)
            yield return new WaitForSeconds(pullDelaySeconds);

        if (bombGo == null || bomb == null || source == null)
            yield break;

        if (bomb.HasExploded || bomb.IsBeingKicked || bomb.IsBeingPunched || bomb.IsBeingMagnetPulled)
            yield break;

        float dur = Mathf.Max(0.01f, pullMoveSeconds);

        if (bombGo.TryGetComponent<AnimatedSpriteRenderer>(out var anim) && anim != null)
            anim.SetFrozen(true);

        if (bombGo.TryGetComponent<Rigidbody2D>(out var rb) && rb != null)
        {
            rb.position = from;
            bombGo.transform.position = from;

            float t = 0f;

            while (t < dur)
            {
                if (bombGo == null || bomb == null || source == null)
                    yield break;

                if (bomb.HasExploded)
                    yield break;

                t += Time.fixedDeltaTime;
                float a = Mathf.Clamp01(t / dur);

                Vector2 p = Vector2.Lerp(from, to, a);

                bombLastPosField?.SetValue(bomb, p);

                rb.MovePosition(p);
                bombGo.transform.position = p;

                yield return waitFixed;
            }

            bombLastPosField?.SetValue(bomb, to);

            rb.position = to;
            bombGo.transform.position = to;
        }
        else
        {
            bomb.ForceSetLogicalPosition(to);
        }

        if (bombGo == null || bomb == null || source == null)
            yield break;

        if (bomb.HasExploded)
            yield break;

        bomb.ForceSetLogicalPosition(to);

        if (bombGo.TryGetComponent<AnimatedSpriteRenderer>(out var anim2) && anim2 != null)
        {
            anim2.SetFrozen(false);
            anim2.RefreshFrame();
        }

        source.ExplodeBomb(bombGo);

        routine = null;
    }
}
