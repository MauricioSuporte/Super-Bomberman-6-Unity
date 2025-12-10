using UnityEngine;

[RequireComponent(typeof(AIMovementController))]
[RequireComponent(typeof(BombController))]
public class BossBomberAI : MonoBehaviour
{
    public Transform target;
    public float thinkInterval = 0.2f;
    public float maxChaseDistance = 20f;
    public float safeDistanceAfterBomb = 3f;
    public float bombChainCooldown = 0.3f;

    AIMovementController movement;
    BombController bomb;

    float thinkTimer;
    Vector2 lastDirection = Vector2.zero;
    bool isEvading;
    float lastBombTime;

    void Awake()
    {
        movement = GetComponent<AIMovementController>();
        bomb = GetComponent<BombController>();

        bomb.useAIInput = true;
        movement.isBoss = true;
    }

    void Start()
    {
        if (target == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                target = player.transform;
        }
    }

    void Update()
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

    void Think()
    {
        Vector2 myPos = RoundToTile(transform.position);
        Bomb[] bombs = FindObjectsByType<Bomb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        bool inDanger = IsInDangerFromBombs(myPos, bombs);

        if (inDanger)
        {
            isEvading = true;
            lastDirection = GetBestSafeDirectionOrStay(myPos, bombs);
            return;
        }

        if (isEvading)
        {
            float safetyHere = GetSafetyScore(myPos, bombs);

            if (bombs.Length > 0 && safetyHere < 5f)
            {
                lastDirection = GetBestSafeDirectionOrStay(myPos, bombs);
                return;
            }

            isEvading = false;
        }

        if (target == null)
        {
            lastDirection = GetBestDirectionAvoidingExplosion(myPos);
            return;
        }

        Vector2 playerPos = RoundToTile(target.position);
        Vector2 delta = playerPos - myPos;

        float manhattan = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        bool sameRow = Mathf.Abs(delta.y) < 0.1f;
        bool sameCol = Mathf.Abs(delta.x) < 0.1f;

        if (manhattan > maxChaseDistance)
        {
            Vector2 dirFar = GetStepTowards(delta);
            Vector2 targetTileFar = myPos + dirFar;

            lastDirection = IsTileWithExplosion(targetTileFar)
                ? GetBestDirectionAvoidingExplosion(myPos)
                : dirFar;
            return;
        }

        if ((sameRow || sameCol) && manhattan <= bomb.explosionRadius + 1)
        {
            if (manhattan > 1.01f)
            {
                Vector2 dirTo = GetStepTowards(delta);
                Vector2 targetTileTo = myPos + dirTo;

                lastDirection = IsTileWithExplosion(targetTileTo)
                    ? GetBestDirectionAvoidingExplosion(myPos)
                    : dirTo;
                return;
            }

            TryPlaceBombChain(myPos);

            bombs = FindObjectsByType<Bomb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            isEvading = true;
            lastDirection = GetBestSafeDirectionOrStay(myPos, bombs);
            return;
        }

        Vector2 dir = GetStepTowards(delta);
        Vector2 targetTile = myPos + dir;

        lastDirection = IsTileWithExplosion(targetTile)
            ? GetBestDirectionAvoidingExplosion(myPos)
            : dir;
    }

    void TryPlaceBombChain(Vector2 myPos)
    {
        if (bomb == null)
            return;

        if (bomb.BombsRemaining <= 0)
            return;

        if (Time.time - lastBombTime < bombChainCooldown)
            return;

        if (IsTileWithBomb(myPos))
            return;

        bomb.RequestBombFromAI();
        lastBombTime = Time.time;
    }

    bool IsInDangerFromBombs(Vector2 myPos, Bomb[] bombs)
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

    bool IsTileInBombDanger(Vector2 tilePos, Bomb bombInstance)
    {
        Vector2 bombPos = RoundToTile(bombInstance.GetLogicalPosition());
        Vector2 delta = tilePos - bombPos;

        bool sameRow = Mathf.Abs(delta.y) < 0.1f;
        bool sameCol = Mathf.Abs(delta.x) < 0.1f;

        if (!sameRow && !sameCol)
            return false;

        int radius = bombInstance.Owner != null ? bombInstance.Owner.explosionRadius : 2;

        float dist = sameRow ? Mathf.Abs(delta.x) : Mathf.Abs(delta.y);

        return dist <= radius + safeDistanceAfterBomb;
    }

    float GetSafetyScore(Vector2 candidatePos, Bomb[] bombs)
    {
        float minMargin = float.PositiveInfinity;

        foreach (var b in bombs)
        {
            if (b == null || b.HasExploded)
                continue;

            Vector2 bombPos = RoundToTile(b.GetLogicalPosition());
            Vector2 delta = candidatePos - bombPos;

            int radius = b.Owner != null ? b.Owner.explosionRadius : 2;

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

    Vector2 GetBestSafeDirectionOrStay(Vector2 myPos, Bomb[] bombs)
    {
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        float bestScore = GetSafetyScore(myPos, bombs);
        Vector2 bestDir = Vector2.zero;

        foreach (var d in dirs)
        {
            Vector2 candidate = myPos + d;

            if (IsTileWithExplosion(candidate))
                continue;

            bool blocked = Physics2D.OverlapBox(
                candidate,
                Vector2.one * (movement.tileSize * 0.6f),
                0f,
                movement.obstacleMask);

            if (blocked)
                continue;

            float safety = GetSafetyScore(candidate, bombs);

            if (safety > bestScore + 0.1f)
            {
                bestScore = safety;
                bestDir = d;
            }
        }

        return bestDir;
    }

    Vector2 GetBestDirectionAvoidingExplosion(Vector2 myPos)
    {
        return GetBestSafeDirectionOrStay(myPos, System.Array.Empty<Bomb>());
    }

    bool IsTileWithExplosion(Vector2 tilePos)
    {
        int explosionLayer = LayerMask.NameToLayer("Explosion");
        int mask = 1 << explosionLayer;

        return Physics2D.OverlapBox(
            tilePos,
            Vector2.one * 0.4f,
            0f,
            mask) != null;
    }

    bool IsTileWithBomb(Vector2 tilePos)
    {
        int bombLayer = LayerMask.NameToLayer("Bomb");
        int mask = 1 << bombLayer;

        return Physics2D.OverlapBox(
            tilePos,
            Vector2.one * 0.4f,
            0f,
            mask) != null;
    }

    Vector2 RoundToTile(Vector2 p)
    {
        return new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
    }

    Vector2 GetStepTowards(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return new Vector2(Mathf.Sign(delta.x), 0f);
        else
            return new Vector2(0f, Mathf.Sign(delta.y));
    }
}
