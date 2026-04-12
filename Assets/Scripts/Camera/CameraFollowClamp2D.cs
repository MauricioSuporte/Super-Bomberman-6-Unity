using UnityEngine;
#if UNITY_2022_2_OR_NEWER
using UnityEngine.U2D;
#endif

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(PixelPerfectCamera))]
public sealed class CameraFollowClamp2D : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private bool followAllPlayers = true;

    [Header("Bounds")]
    [SerializeField] private Collider2D boundsCollider;

    [Header("Follow")]
    [SerializeField] private Vector2 followOffset;

    [Header("Speed (SB5 Internal)")]
    [SerializeField] private bool useTrackedPlayersSpeed = true;
    [SerializeField] private int speedInternal = PlayerPersistentStats.BaseSpeedNormal;
    public int SpeedInternal => speedInternal;

    [Header("Pixel Perfect Step (igual ao MovementController)")]
    [SerializeField, Min(1)] private int pixelsPerUnit = 16;
    [SerializeField] private bool useIntegerPixelSteps = true;

    [Header("Player Search")]
    [SerializeField] private float refreshPlayersEverySeconds = 0.1f;

    private Camera cam;

    private Transform followTarget;
    private PlayerIdentity[] cachedPlayers = new PlayerIdentity[0];
    private float refreshTimer;

    private Vector3 rawPos;

    private float accPixelsX;
    private float accPixelsY;
    private Vector2 lastMoveDirCardinal = Vector2.zero;

#if UNITY_2022_2_OR_NEWER
    private PixelPerfectCamera ppc;
#endif

    private float PixelWorldStep => (pixelsPerUnit > 0) ? (1f / pixelsPerUnit) : 0.0625f;

    void Awake()
    {
        cam = GetComponent<Camera>();

        followTarget = new GameObject("CameraTarget").transform;
        followTarget.hideFlags = HideFlags.HideInHierarchy;

        rawPos = transform.position;

#if UNITY_2022_2_OR_NEWER
        ppc = GetComponent<PixelPerfectCamera>();
#endif

        ApplySpeedInternal(speedInternal);
    }

    void OnDestroy()
    {
        if (followTarget != null)
            Destroy(followTarget.gameObject);
    }

    void FixedUpdate()
    {
        if (cam == null || !cam.orthographic || boundsCollider == null)
            return;

        refreshTimer -= Time.fixedDeltaTime;
        if (refreshTimer <= 0f)
        {
            RefreshPlayers();
            refreshTimer = Mathf.Max(0.05f, refreshPlayersEverySeconds);
        }

        if (!TryUpdateFollowTargetPosition(out var targetPos))
            return;

        Vector3 desired = new Vector3(
            targetPos.x + followOffset.x,
            targetPos.y + followOffset.y,
            transform.position.z
        );

        desired = ClampToBounds(desired, boundsCollider.bounds);

        Vector2 current = new Vector2(rawPos.x, rawPos.y);
        current = QuantizeToPixelGrid(current);

        Vector2 delta = new Vector2(desired.x - current.x, desired.y - current.y);
        Vector2 dir = NormalizeDominantCardinal(delta);

        float rawMoveWorld = GetRawMoveWorldPerFixedFrame();
        float moveWorld = GetQuantizedMoveWorldPerFixedFrame(dir, rawMoveWorld);

        if (moveWorld <= 0f || dir == Vector2.zero)
        {
            rawPos = ClampToBounds(new Vector3(current.x, current.y, rawPos.z), boundsCollider.bounds);
            transform.position = rawPos;
            return;
        }

        Vector2 next = current + dir * moveWorld;

        if (dir.x != 0f)
        {
            float remainingX = desired.x - current.x;
            if (Mathf.Abs(next.x - current.x) > Mathf.Abs(remainingX))
                next.x = desired.x;

            next.y = current.y;
        }
        else
        {
            float remainingY = desired.y - current.y;
            if (Mathf.Abs(next.y - current.y) > Mathf.Abs(remainingY))
                next.y = desired.y;

            next.x = current.x;
        }

        next = QuantizeToPixelGrid(next);

        rawPos = new Vector3(next.x, next.y, transform.position.z);
        rawPos = ClampToBounds(rawPos, boundsCollider.bounds);

        transform.position = rawPos;
    }

    void RefreshPlayers()
    {
        cachedPlayers = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }

    bool TryUpdateFollowTargetPosition(out Vector3 position)
    {
        position = default;

        if (cachedPlayers == null || cachedPlayers.Length == 0)
            return false;

        int activeCount = 1;
        if (GameSession.Instance != null)
            activeCount = Mathf.Clamp(GameSession.Instance.ActivePlayerCount, 1, 4);

        if (!followAllPlayers)
        {
            for (int i = 0; i < cachedPlayers.Length; i++)
            {
                var p = cachedPlayers[i];
                if (p != null && p.playerId == 1)
                {
                    position = p.transform.position;
                    followTarget.position = position;
                    return true;
                }
            }

            position = cachedPlayers[0].transform.position;
            followTarget.position = position;
            return true;
        }

        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < cachedPlayers.Length; i++)
        {
            var p = cachedPlayers[i];
            if (p == null)
                continue;

            if (p.playerId < 1 || p.playerId > activeCount)
                continue;

            sum += p.transform.position;
            count++;
        }

        if (count == 0)
            return false;

        position = sum / count;
        followTarget.position = position;
        return true;
    }

    Vector3 ClampToBounds(Vector3 desired, Bounds bounds)
    {
        float vertExtent = cam.orthographicSize;
        float horzExtent = vertExtent * cam.aspect;

        float minX = bounds.min.x + horzExtent;
        float maxX = bounds.max.x - horzExtent;
        float minY = bounds.min.y + vertExtent;
        float maxY = bounds.max.y - vertExtent;

        float x = desired.x;
        float y = desired.y;

        if (minX > maxX) x = bounds.center.x;
        else x = Mathf.Clamp(x, minX, maxX);

        if (minY > maxY) y = bounds.center.y;
        else y = Mathf.Clamp(y, minY, maxY);

        return new Vector3(x, y, desired.z);
    }

    private Vector2 QuantizeToPixelGrid(Vector2 world)
    {
        if (!useIntegerPixelSteps || pixelsPerUnit <= 0)
            return world;

        float ppu = pixelsPerUnit;
        return new Vector2(
            Mathf.Round(world.x * ppu) / ppu,
            Mathf.Round(world.y * ppu) / ppu
        );
    }

    private void ResetPixelAccumulators()
    {
        accPixelsX = 0f;
        accPixelsY = 0f;
    }

    private float GetRawMoveWorldPerFixedFrame()
    {
        float dt = Time.fixedDeltaTime;
        float speedWorldPerSecond = GetFollowTilesPerSecond();
        return speedWorldPerSecond * dt;
    }

    private float GetQuantizedMoveWorldPerFixedFrame(Vector2 moveDir, float rawWorldStep)
    {
        if (!useIntegerPixelSteps || pixelsPerUnit <= 0)
            return rawWorldStep;

        moveDir = NormalizeCardinal(moveDir);
        if (moveDir == Vector2.zero)
            return 0f;

        if (moveDir != lastMoveDirCardinal)
        {
            lastMoveDirCardinal = moveDir;
            ResetPixelAccumulators();
        }

        float rawPixels = rawWorldStep * pixelsPerUnit;

        if (Mathf.Abs(moveDir.x) > 0.01f)
        {
            accPixelsX += rawPixels * Mathf.Sign(moveDir.x);

            int whole = (int)accPixelsX;
            accPixelsX -= whole;

            return Mathf.Abs(whole) * PixelWorldStep;
        }
        else
        {
            accPixelsY += rawPixels * Mathf.Sign(moveDir.y);

            int whole = (int)accPixelsY;
            accPixelsY -= whole;

            return Mathf.Abs(whole) * PixelWorldStep;
        }
    }

    private float GetFollowTilesPerSecond()
    {
        if (!useTrackedPlayersSpeed || cachedPlayers == null || cachedPlayers.Length == 0)
            return PlayerPersistentStats.InternalSpeedToTilesPerSecond(speedInternal);

        int activeCount = 1;
        if (GameSession.Instance != null)
            activeCount = Mathf.Clamp(GameSession.Instance.ActivePlayerCount, 1, 4);

        int highestInternal = speedInternal;
        bool foundAny = false;

        for (int i = 0; i < cachedPlayers.Length; i++)
        {
            var p = cachedPlayers[i];
            if (p == null)
                continue;

            if (p.playerId < 1 || p.playerId > activeCount)
                continue;

            if (!p.TryGetComponent<MovementController>(out var movement) || movement == null)
                continue;

            highestInternal = Mathf.Max(highestInternal, movement.SpeedInternal);
            foundAny = true;
        }

        if (!foundAny)
            return PlayerPersistentStats.InternalSpeedToTilesPerSecond(speedInternal);

        return PlayerPersistentStats.InternalSpeedToTilesPerSecond(highestInternal);
    }

    private static Vector2 NormalizeCardinal(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return new Vector2(Mathf.Sign(dir.x), 0f);

        if (Mathf.Abs(dir.y) > 0f)
            return new Vector2(0f, Mathf.Sign(dir.y));

        return Vector2.zero;
    }

    private static Vector2 NormalizeDominantCardinal(Vector2 dir)
    {
        if (dir == Vector2.zero)
            return Vector2.zero;

        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            return new Vector2(Mathf.Sign(dir.x), 0f);

        return new Vector2(0f, Mathf.Sign(dir.y));
    }

    public void ApplySpeedInternal(int newInternal)
    {
        speedInternal = PlayerPersistentStats.ClampSpeedInternal(newInternal);
    }

    public void SetBounds(Collider2D newBounds)
    {
        boundsCollider = newBounds;
    }

    public void ForceSnapNow(bool refreshPlayersNow = true)
    {
        if (cam == null || !cam.orthographic || boundsCollider == null)
            return;

        if (refreshPlayersNow)
            RefreshPlayers();

        if (!TryUpdateFollowTargetPosition(out var targetPos))
            return;

        Vector3 desired = new Vector3(
            targetPos.x + followOffset.x,
            targetPos.y + followOffset.y,
            transform.position.z
        );

        desired = ClampToBounds(desired, boundsCollider.bounds);

        rawPos = desired;
        lastMoveDirCardinal = Vector2.zero;
        ResetPixelAccumulators();

        Vector2 quantized = QuantizeToPixelGrid(new Vector2(rawPos.x, rawPos.y));
        rawPos = new Vector3(quantized.x, quantized.y, rawPos.z);
        rawPos = ClampToBounds(rawPos, boundsCollider.bounds);

        transform.position = rawPos;
        refreshTimer = Mathf.Max(0.05f, refreshPlayersEverySeconds);
    }
}