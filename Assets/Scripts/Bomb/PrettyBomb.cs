using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Bomb))]
public sealed class PrettyBomb : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float movementSpeedMultiplier = 0.3f;

    private static readonly Vector2[] Directions =
        { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
    private Bomb bomb;
    private Vector2 direction;
    private bool moving;
    private float retryAt;

    private void Awake()
    {
        bomb = GetComponent<Bomb>();
        direction = Directions[Random.Range(0, Directions.Length)];
    }

    private void Update()
    {
        if (GamePauseController.IsPaused || bomb.HasExploded || bomb.Owner == null)
            return;

        if (bomb.IsBeingMagnetPulled)
            return;

        if (!bomb.CanBeMagnetPulled || Time.time < retryAt)
            return;

        var owner = bomb.Owner;
        var movement = owner.GetComponent<MovementController>();
        float tileSize = movement != null ? movement.tileSize : 1f;
        LayerMask obstacles = LayerMask.GetMask("Stage", "Bomb", "Player", "Enemy", "Louie");

        // Reuse Bomb's grid movement, occupancy notifications, terrain handling,
        // and collision checks. A stopped segment means its path was blocked.
        Vector2 previous = direction;
        int first = Random.Range(0, Directions.Length);
        for (int attempt = 0; attempt < Directions.Length + 1; attempt++)
        {
            Vector2 candidate = attempt == 0 ? previous : Directions[(first + attempt - 1) % Directions.Length];
            if (moving && candidate == previous)
                continue;

            if (!bomb.StartMagnetPull(candidate, tileSize, 0, obstacles,
                    owner.destructibleTiles, movementSpeedMultiplier,
                    obstacles, 0.6f, 0.9f, true))
                continue;

            direction = candidate;
            moving = true;
            return;
        }

        // A surrounded bomb keeps its fuse and retries when a route opens.
        moving = false;
        retryAt = Time.time + 0.08f;
    }
}
