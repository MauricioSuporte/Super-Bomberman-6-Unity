using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;
using Assets.Scripts.Explosions;

[RequireComponent(typeof(BoilerPowderTrailIgniterPrefab))]
public sealed class BoilerTriggerGroundTileHandler : MonoBehaviour, IGroundTileHandler, IGroundTileBombAtHandler
{
    [SerializeField] private float pullDelaySeconds = 0.5f;
    [SerializeField] private int pullTilesUp = 1;
    [SerializeField] private float pullMoveSeconds = 0.08f;

    [Header("Explode Timing")]
    [SerializeField, Min(0.01f)] private float explodeAfterCapturedSeconds = 1f;

    [Header("SFX - Bomb Move")]
    [SerializeField] private AudioSource bombMoveSfxSource;
    [SerializeField] private AudioClip bombMoveClip;
    [SerializeField, Range(0f, 1f)] private float bombMoveVolume = 1f;

    [SerializeField] private Tilemap doorTilemapOverride;

    [SerializeField] private TileBase closedDoorTile;
    [SerializeField] private AnimatedTile openDoorAnimationTile;
    [SerializeField] private float openDoorAnimationSeconds = 0.5f;
    [SerializeField] private TileBase openedDoorTile;

    [SerializeField] private BoilerPowderTrailIgniterPrefab powderIgniter;

    private bool triggeredOnce;
    private bool finalizedOnce;

    private Coroutine routine;
    private Coroutine doorAllRoutine;

    private static readonly WaitForFixedUpdate waitFixed = new();
    private static FieldInfo bombLastPosField;

    private void Awake()
    {
        if (bombLastPosField == null)
            bombLastPosField = typeof(Bomb).GetField("lastPos", BindingFlags.Instance | BindingFlags.NonPublic);

        if (powderIgniter == null)
            powderIgniter = GetComponent<BoilerPowderTrailIgniterPrefab>();

        if (powderIgniter == null)
            powderIgniter = FindFirstObjectByType<BoilerPowderTrailIgniterPrefab>();

        if (bombMoveSfxSource == null)
        {
            bombMoveSfxSource = GetComponent<AudioSource>();
            if (bombMoveSfxSource == null)
            {
                bombMoveSfxSource = gameObject.AddComponent<AudioSource>();
                bombMoveSfxSource.playOnAwake = false;
            }
        }
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (doorAllRoutine != null)
        {
            StopCoroutine(doorAllRoutine);
            doorAllRoutine = null;
        }
    }

    public bool TryModifyExplosion(BombController source, Vector2 worldPos, TileBase groundTile, ref int radius, ref bool pierce)
        => false;

    public void OnBombAt(
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
        finalizedOnce = false;

        float explodeDelay = Mathf.Max(0.01f, explodeAfterCapturedSeconds);

        float minFuse =
            Mathf.Max(0.0f, pullDelaySeconds) +
            Mathf.Max(0.01f, pullMoveSeconds) +
            explodeDelay +
            0.05f;

        bomb.EnsureMinRemainingFuse(minFuse);

        Vector2 from = worldPos;
        from.x = Mathf.Round(from.x);
        from.y = Mathf.Round(from.y);

        Vector2 to = from + Vector2.up * pullTilesUp;
        to.x = Mathf.Round(to.x);
        to.y = Mathf.Round(to.y);

        StartDoorOpenSequenceAll(source);

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(PullAndExplodeRoutine(source, bombGo, bomb, from, to, explodeDelay));
    }

    private Tilemap ResolveDoorTilemap(BombController source)
    {
        if (doorTilemapOverride != null)
            return doorTilemapOverride;

        if (source != null && source.stageBoundsTiles != null)
            return source.stageBoundsTiles;

        if (source != null && source.groundTiles != null)
            return source.groundTiles;

        return null;
    }

    private void StartDoorOpenSequenceAll(BombController source)
    {
        Tilemap tm = ResolveDoorTilemap(source);
        if (tm == null)
            return;

        if (closedDoorTile == null || openDoorAnimationTile == null || openedDoorTile == null)
            return;

        if (doorAllRoutine != null)
            StopCoroutine(doorAllRoutine);

        doorAllRoutine = StartCoroutine(DoorOpenRoutineAll(tm));
    }

    private IEnumerator DoorOpenRoutineAll(Tilemap tm)
    {
        if (tm == null)
            yield break;

        tm.CompressBounds();
        var bounds = tm.cellBounds;

        List<Vector3Int> targets = new(64);

        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var c = new Vector3Int(x, y, 0);
                var t = tm.GetTile(c);
                if (t == closedDoorTile)
                    targets.Add(c);
            }
        }

        if (targets.Count == 0)
        {
            doorAllRoutine = null;
            yield break;
        }

        for (int i = 0; i < targets.Count; i++)
            tm.SetTile(targets[i], openDoorAnimationTile);

        tm.RefreshAllTiles();

        float dur = Mathf.Max(0.01f, openDoorAnimationSeconds);
        yield return new WaitForSeconds(dur);

        if (tm == null)
        {
            doorAllRoutine = null;
            yield break;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            var c = targets[i];
            if (tm.GetTile(c) == openDoorAnimationTile)
                tm.SetTile(c, openedDoorTile);
        }

        tm.RefreshAllTiles();
        doorAllRoutine = null;
    }

    private void PlayBombMoveSfx()
    {
        if (bombMoveClip == null)
            return;

        if (bombMoveSfxSource == null)
            return;

        bombMoveSfxSource.PlayOneShot(bombMoveClip, Mathf.Clamp01(bombMoveVolume));
    }

    private void FinalizeBoilerOnce()
    {
        if (finalizedOnce)
            return;

        finalizedOnce = true;

        if (powderIgniter != null)
            powderIgniter.IgniteSequence();

        routine = null;
    }

    private IEnumerator PullAndExplodeRoutine(
        BombController source,
        GameObject bombGo,
        Bomb bomb,
        Vector2 from,
        Vector2 to,
        float explodeDelay)
    {
        AnimatedSpriteRenderer frozenAnim = null;

        void RestoreAnimIfNeeded()
        {
            if (frozenAnim != null)
            {
                frozenAnim.SetFrozen(false);
                frozenAnim.RefreshFrame();
                frozenAnim = null;
            }
        }

        if (bomb != null)
            bomb.ForceStopExternalMovementAndSnap(from);

        if (pullDelaySeconds > 0f)
            yield return new WaitForSeconds(pullDelaySeconds);

        if (source == null)
        {
            RestoreAnimIfNeeded();
            FinalizeBoilerOnce();
            yield break;
        }

        if (bomb == null || bombGo == null || bomb.HasExploded)
        {
            RestoreAnimIfNeeded();
            FinalizeBoilerOnce();
            yield break;
        }

        float dur = Mathf.Max(0.01f, pullMoveSeconds);

        if (bombGo.TryGetComponent<AnimatedSpriteRenderer>(out var anim) && anim != null)
        {
            frozenAnim = anim;
            frozenAnim.SetFrozen(true);
        }

        if (bombGo.TryGetComponent<Rigidbody2D>(out var rb) && rb != null)
        {
            rb.position = from;
            bombGo.transform.position = from;

            PlayBombMoveSfx();

            float t = 0f;

            while (t < dur)
            {
                if (source == null)
                {
                    RestoreAnimIfNeeded();
                    FinalizeBoilerOnce();
                    yield break;
                }

                if (bomb == null || bombGo == null || bomb.HasExploded)
                {
                    RestoreAnimIfNeeded();
                    FinalizeBoilerOnce();
                    yield break;
                }

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
            PlayBombMoveSfx();

            if (bomb == null || bombGo == null || bomb.HasExploded)
            {
                RestoreAnimIfNeeded();
                FinalizeBoilerOnce();
                yield break;
            }

            bomb.ForceSetLogicalPosition(to);
        }

        if (source == null)
        {
            RestoreAnimIfNeeded();
            FinalizeBoilerOnce();
            yield break;
        }

        if (bomb == null || bombGo == null || bomb.HasExploded)
        {
            RestoreAnimIfNeeded();
            FinalizeBoilerOnce();
            yield break;
        }

        bomb.ForceSetLogicalPosition(to);

        RestoreAnimIfNeeded();

        if (explodeDelay > 0f)
            yield return new WaitForSeconds(explodeDelay);

        if (source == null)
        {
            FinalizeBoilerOnce();
            yield break;
        }

        if (bomb == null || bombGo == null || bomb.HasExploded)
        {
            FinalizeBoilerOnce();
            yield break;
        }

        source.ExplodeBomb(bombGo);

        FinalizeBoilerOnce();
    }
}
