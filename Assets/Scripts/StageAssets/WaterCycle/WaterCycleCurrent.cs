using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A directional current inside a rotating water cycle. Only players are
/// carried by the current; bombs and enemies pass through unchanged.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class WaterCycleCurrent : MonoBehaviour
{
    public enum CurrentDirection { Up, Down, Left, Right }

    [Header("Current")]
    [SerializeField] private CurrentDirection direction = CurrentDirection.Right;
    [SerializeField, Min(0.1f)] private float tileSize = 1f;
    [SerializeField, Min(1)] private int againstCurrentDistanceTiles = 3;
    [SerializeField, Min(0.1f)] private float initialPushSpeed = 12f;
    [SerializeField, Min(0.1f)] private float brakeDeceleration = 14f;
    [SerializeField, Min(0.1f)] private float withCurrentBrakeDeceleration = 36f;

    [Header("SFX")]
    [SerializeField] private AudioClip pushSfx;
    [SerializeField, Range(0f, 1f)] private float pushSfxVolume = 1f;

    readonly HashSet<MovementController> pushedPlayers = new();
    Tilemap destructibleTilemap;
    Tilemap indestructibleTilemap;

    void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        ResolveReferences();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
            return;

        MovementController player = other.GetComponentInParent<MovementController>();
        if (player == null || player.isDead || player.IsEndingStage || player.ExternalMovementOverride)
            return;

        if (!pushedPlayers.Add(player))
            return;

        StartCoroutine(PushPlayerRoutine(player, EnteredWithCurrent(player)));
    }

    void OnTriggerExit2D(Collider2D other)
    {
        MovementController player = other != null ? other.GetComponentInParent<MovementController>() : null;
        if (player != null)
            pushedPlayers.Remove(player);
    }

    IEnumerator PushPlayerRoutine(MovementController player, bool enteredWithCurrent)
    {
        float size = GetTileSize(player.tileSize);
        Vector2 current = SnapToGrid(GetPlayerPosition(player), size);
        Vector2 currentDirection = ToVector(direction);
        float remainingDistance = enteredWithCurrent
            ? float.PositiveInfinity
            : Mathf.Max(1, againstCurrentDistanceTiles) * size;
        float speed = initialPushSpeed;

        if (IsBlockedAhead(current, currentDirection, size, player.gameObject))
            yield break;

        player.SetInputLocked(true, forceIdle: true, idleFacing: currentDirection);
        player.SetExternalMovementOverride(true);
        player.SetExternalMovementAllowsHazardDamage(true);
        player.ApplyDirectionFromVector(currentDirection);
        PlayPushSfx(player.GetComponent<AudioSource>() ?? player.GetComponentInChildren<AudioSource>(true));

        while (player != null && !player.isDead && !player.IsEndingStage)
        {
            if (IsOpposingDirectionHeld(player.PlayerId, currentDirection))
            {
                float deceleration = enteredWithCurrent
                    ? withCurrentBrakeDeceleration
                    : brakeDeceleration;
                speed = Mathf.MoveTowards(speed, 0f, deceleration * Time.fixedDeltaTime);
            }

            if (speed <= 0.001f)
                break;

            float step = Mathf.Min(speed * Time.fixedDeltaTime, remainingDistance);
            Vector2 next = QuantizeToPixelGrid(current + currentDirection * step);

            if (TryGetPassableBombStopPosition(current, next, currentDirection, size, player.gameObject, out Vector2 bombStopPosition))
            {
                MovePlayer(player, bombStopPosition);
                current = bombStopPosition;
                break;
            }

            if (IsBlockedAhead(next, currentDirection, size, player.gameObject))
                break;

            MovePlayer(player, next);
            current = next;

            if (!float.IsPositiveInfinity(remainingDistance))
            {
                remainingDistance -= step;
                if (remainingDistance <= 0.001f)
                    break;
            }

            yield return new WaitForFixedUpdate();
        }

        if (player != null)
        {
            player.SetExternalMovementAllowsHazardDamage(false);
            player.SetExternalMovementOverride(false);

            // A lethal hit can end the push loop on the same frame that the
            // death sequence selects its renderer. Do not restore the idle
            // movement visual in that case, otherwise it replaces the death
            // animation before it can be shown.
            if (!player.isDead)
            {
                player.SetInputLocked(false, forceIdle: false);
                player.ApplyDirectionFromVector(Vector2.zero);
            }
        }
    }

    bool EnteredWithCurrent(MovementController player)
    {
        Vector2 currentDirection = ToVector(direction);
        if (player.Direction != Vector2.zero)
            return Vector2.Dot(player.Direction.normalized, currentDirection) > 0f;

        Vector2 fromCycleCenter = GetPlayerPosition(player) - (Vector2)transform.position;
        return Vector2.Dot(fromCycleCenter, currentDirection) < 0f;
    }

    bool IsBlockedAhead(Vector2 position, Vector2 currentDirection, float size, GameObject playerObject)
    {
        ResolveReferences();
        Vector2 probe = position + currentDirection * (size * 0.5f);
        if (HasTileAt(destructibleTilemap, probe) || HasTileAt(indestructibleTilemap, probe))
            return true;

        Collider2D[] hits = Physics2D.OverlapBoxAll(probe, Vector2.one * (size * 0.55f), 0f, LayerMask.GetMask("Bomb"));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (hit != null && hit.gameObject != playerObject && !hit.transform.IsChildOf(playerObject.transform))
                return true;
        }

        return false;
    }

    bool TryGetPassableBombStopPosition(
        Vector2 current,
        Vector2 next,
        Vector2 currentDirection,
        float size,
        GameObject playerObject,
        out Vector2 stopPosition)
    {
        stopPosition = default;
        float stepDistance = Vector2.Dot(next - current, currentDirection);
        if (stepDistance <= 0.0001f)
            return false;

        Bomb[] bombs = FindObjectsByType<Bomb>();
        float nearestStopDistance = float.PositiveInfinity;
        Vector2 lateralDirection = new(-currentDirection.y, currentDirection.x);

        foreach (Bomb bomb in bombs)
        {
            if (bomb == null || bomb.IsSolid || bomb.gameObject == playerObject || bomb.transform.IsChildOf(playerObject.transform))
                continue;

            Collider2D bombCollider = bomb.GetComponent<Collider2D>();
            if (bombCollider == null || !bombCollider.isTrigger)
                continue;

            Vector2 bombTile = SnapToGrid(bombCollider.bounds.center, size);
            Vector2 offset = bombTile - current;
            float forwardDistance = Vector2.Dot(offset, currentDirection);
            float lateralDistance = Mathf.Abs(Vector2.Dot(offset, lateralDirection));
            float safeStopDistance = forwardDistance - size;

            if (lateralDistance > size * 0.25f || safeStopDistance < -0.001f || safeStopDistance > stepDistance + 0.001f)
                continue;

            if (safeStopDistance < nearestStopDistance)
            {
                nearestStopDistance = safeStopDistance;
                stopPosition = QuantizeToPixelGrid(current + currentDirection * Mathf.Max(0f, safeStopDistance));
            }
        }

        return !float.IsPositiveInfinity(nearestStopDistance);
    }

    static bool HasTileAt(Tilemap tilemap, Vector2 worldPosition)
        => tilemap != null && tilemap.HasTile(tilemap.WorldToCell(worldPosition));

    static bool IsOpposingDirectionHeld(int playerId, Vector2 currentDirection)
    {
        PlayerInputManager input = PlayerInputManager.Instance;
        if (input == null)
            return false;

        if (currentDirection == Vector2.up) return input.Get(playerId, PlayerAction.MoveDown);
        if (currentDirection == Vector2.down) return input.Get(playerId, PlayerAction.MoveUp);
        if (currentDirection == Vector2.left) return input.Get(playerId, PlayerAction.MoveRight);
        return input.Get(playerId, PlayerAction.MoveLeft);
    }

    void ResolveReferences()
    {
        if (destructibleTilemap != null && indestructibleTilemap != null)
            return;

        GameManager manager = FindAnyObjectByType<GameManager>();
        if (manager == null)
            return;

        destructibleTilemap = manager.destructibleTilemap;
        indestructibleTilemap = manager.indestructibleTilemap;
    }

    float GetTileSize(float candidate = 0f) => candidate > 0.0001f ? candidate : Mathf.Max(0.1f, tileSize);

    static Vector2 ToVector(CurrentDirection value) => value switch
    {
        CurrentDirection.Up => Vector2.up,
        CurrentDirection.Down => Vector2.down,
        CurrentDirection.Left => Vector2.left,
        _ => Vector2.right,
    };

    static Vector2 GetPlayerPosition(MovementController player)
        => player.Rigidbody != null ? player.Rigidbody.position : (Vector2)player.transform.position;

    static Vector2 SnapToGrid(Vector2 position, float size) => new(
        Mathf.Round(position.x / size) * size,
        Mathf.Round(position.y / size) * size);

    static Vector2 QuantizeToPixelGrid(Vector2 position) => new(
        Mathf.Round(position.x * 16f) / 16f,
        Mathf.Round(position.y * 16f) / 16f);

    static void MovePlayer(MovementController player, Vector2 position)
    {
        if (player.Rigidbody != null)
        {
            player.Rigidbody.linearVelocity = Vector2.zero;
            player.Rigidbody.MovePosition(position);
        }

        player.transform.position = position;
    }

    void PlayPushSfx(AudioSource source)
    {
        if (source != null && pushSfx != null)
            GameAudioSettings.PlaySfx(source, pushSfx, pushSfxVolume);
    }
}
