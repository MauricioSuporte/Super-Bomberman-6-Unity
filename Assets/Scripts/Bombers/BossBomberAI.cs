using UnityEngine;

[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public class BossBomberAI : MonoBehaviour
{
    public Transform target;
    public float thinkInterval = 0.2f;
    public float maxChaseDistance = 20f;
    public float safeDistanceAfterBomb = 3f;

    private MovementController movement;
    private BombController bomb;

    private float thinkTimer;
    private Vector2 lastDirection = Vector2.zero;

    private void Awake()
    {
        movement = GetComponent<MovementController>();
        bomb = GetComponent<BombController>();

        movement.useAIInput = true;
        bomb.useAIInput = true;
    }

    private void Update()
    {
        if (GamePauseController.IsPaused)
            return;

        thinkTimer -= Time.deltaTime;
        if (thinkTimer <= 0f)
        {
            Think();
            thinkTimer = thinkInterval;
        }

        movement.SetAIDirection(lastDirection);
    }

    private void Think()
    {
        Vector2 myPos = RoundToTile(transform.position);

        Bomb[] bombs = FindObjectsOfType<Bomb>();
        if (IsInDangerFromBombs(myPos, bombs))
        {
            lastDirection = GetBestEscapeDirection(myPos, bombs);
            return;
        }

        if (target == null)
        {
            lastDirection = GetRandomDirection();
            return;
        }

        Vector2 playerPos = RoundToTile(target.position);
        Vector2 delta = playerPos - myPos;

        float manhattan = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        bool sameRow = Mathf.Abs(delta.y) < 0.1f;
        bool sameCol = Mathf.Abs(delta.x) < 0.1f;

        if (manhattan > maxChaseDistance)
        {
            lastDirection = GetStepTowards(delta);
            return;
        }

        if ((sameRow || sameCol) && manhattan <= bomb.explosionRadius + 1)
        {
            if (manhattan > 1.01f)
            {
                lastDirection = GetStepTowards(delta);
                return;
            }

            bomb.RequestBombFromAI();

            bombs = FindObjectsOfType<Bomb>();
            lastDirection = GetBestEscapeDirection(myPos, bombs);
            return;
        }

        lastDirection = GetStepTowards(delta);
    }

    private bool IsInDangerFromBombs(Vector2 myPos, Bomb[] bombs)
    {
        foreach (var b in bombs)
        {
            if (b == null || b.HasExploded)
                continue;

            if (IsTileInBombDanger(myPos, b))
                return true;
        }

        return false;
    }

    private bool IsTileInBombDanger(Vector2 tilePos, Bomb bomb)
    {
        Vector2 bombPos = RoundToTile(bomb.GetLogicalPosition());
        Vector2 delta = tilePos - bombPos;

        bool sameRow = Mathf.Abs(delta.y) < 0.1f;
        bool sameCol = Mathf.Abs(delta.x) < 0.1f;

        if (!sameRow && !sameCol)
            return false;

        int radius = 2;
        if (bomb.Owner != null)
            radius = bomb.Owner.explosionRadius;

        float dist = sameRow ? Mathf.Abs(delta.x) : Mathf.Abs(delta.y);

        return dist <= radius + safeDistanceAfterBomb;
    }

    private float GetSafetyScore(Vector2 candidatePos, Bomb[] bombs)
    {
        float minMargin = float.PositiveInfinity;

        foreach (var b in bombs)
        {
            if (b == null || b.HasExploded)
                continue;

            Vector2 bombPos = RoundToTile(b.GetLogicalPosition());
            Vector2 delta = candidatePos - bombPos;

            int radius = 2;
            if (b.Owner != null)
                radius = b.Owner.explosionRadius;

            bool sameRow = Mathf.Abs(delta.y) < 0.1f;
            bool sameCol = Mathf.Abs(delta.x) < 0.1f;

            float dist = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);

            float margin;

            if (sameRow || sameCol)
            {
                float linearDist = sameRow ? Mathf.Abs(delta.x) : Mathf.Abs(delta.y);
                margin = linearDist - (radius + safeDistanceAfterBomb);
            }
            else
            {
                margin = dist;
            }

            if (margin < minMargin)
                minMargin = margin;
        }

        if (float.IsPositiveInfinity(minMargin))
            minMargin = 999f;

        return minMargin;
    }

    private Vector2 GetBestEscapeDirection(Vector2 myPos, Bomb[] bombs)
    {
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        float bestScore = float.NegativeInfinity;
        Vector2 bestDir = Vector2.zero;

        foreach (var d in dirs)
        {
            Vector2 candidate = myPos + d;

            bool blocked = Physics2D.OverlapBox(
                candidate,
                Vector2.one * (movement.tileSize * 0.6f),
                0f,
                movement.obstacleMask);

            if (blocked)
                continue;

            float safety = GetSafetyScore(candidate, bombs);

            if (safety > bestScore)
            {
                bestScore = safety;
                bestDir = d;
            }
        }

        if (bestDir == Vector2.zero)
            bestDir = GetRandomDirection();

        return bestDir;
    }

    private Vector2 RoundToTile(Vector2 p)
    {
        return new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
    }

    private Vector2 GetStepTowards(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return new Vector2(Mathf.Sign(delta.x), 0f);
        else
            return new Vector2(0f, Mathf.Sign(delta.y));
    }

    private Vector2 GetRandomDirection()
    {
        int i = Random.Range(0, 4);
        return i switch
        {
            0 => Vector2.up,
            1 => Vector2.down,
            2 => Vector2.left,
            _ => Vector2.right,
        };
    }
}
