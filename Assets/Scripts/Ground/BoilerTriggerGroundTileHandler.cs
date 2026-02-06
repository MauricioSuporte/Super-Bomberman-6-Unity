using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class BoilerTriggerGroundTileHandler : MonoBehaviour, IGroundTileHandler, IGroundTileBombPlacedHandler
{
    [Header("Boiler Trigger")]
    [SerializeField, Min(0f)] private float pullDelaySeconds = 0.5f;
    [SerializeField] private int pullTilesUp = 1;
    [SerializeField, Min(0.01f)] private float pullMoveSeconds = 0.08f;

    private bool triggeredOnce;
    private Coroutine routine;

    private static readonly WaitForFixedUpdate waitFixed = new();

    private static FieldInfo bombLastPosField;

    private void Awake()
    {
        if (bombLastPosField == null)
            bombLastPosField = typeof(Bomb).GetField("lastPos", BindingFlags.Instance | BindingFlags.NonPublic);
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

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(PullAndExplodeRoutine(source, bombGo, bomb));
    }

    private IEnumerator PullAndExplodeRoutine(BombController source, GameObject bombGo, Bomb bomb)
    {
        if (pullDelaySeconds > 0f)
            yield return new WaitForSeconds(pullDelaySeconds);

        if (bombGo == null || bomb == null || source == null)
            yield break;

        if (bomb.HasExploded || bomb.IsBeingKicked || bomb.IsBeingPunched || bomb.IsBeingMagnetPulled)
            yield break;

        Vector2 from = bomb.GetLogicalPosition();
        from.x = Mathf.Round(from.x);
        from.y = Mathf.Round(from.y);

        Vector2 to = from + Vector2.up * pullTilesUp;
        to.x = Mathf.Round(to.x);
        to.y = Mathf.Round(to.y);

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
