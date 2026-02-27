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
    [SerializeField] private float smoothTime = 0.12f;

    [Header("Pixel Perfect Snap")]
    [SerializeField] private bool pixelSnap = true;
    [SerializeField, Min(1)] private int pixelsPerUnit = 16;

    [Header("Player Search")]
    [SerializeField] private float refreshPlayersEverySeconds = 0.5f;

    private Camera cam;
    private Vector3 velocity;

    private Transform followTarget;
    private PlayerIdentity[] cachedPlayers = new PlayerIdentity[0];
    private float refreshTimer;

    private Vector3 rawPos;

    private float accPixelsX;
    private float accPixelsY;

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
        if (ppc != null)
        {
            pixelSnap = false;
        }
#endif
    }

    void OnDestroy()
    {
        if (followTarget != null)
            Destroy(followTarget.gameObject);
    }

    void LateUpdate()
    {
        if (cam == null || !cam.orthographic || boundsCollider == null)
            return;

        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            RefreshPlayers();
            refreshTimer = Mathf.Max(0.05f, refreshPlayersEverySeconds);
        }

        if (!TryUpdateFollowTargetPosition(out var targetPos))
            return;

        var desired = new Vector3(
            targetPos.x + followOffset.x,
            targetPos.y + followOffset.y,
            transform.position.z
        );

        var clampedTarget = ClampToBounds(desired, boundsCollider.bounds);

        rawPos = Vector3.SmoothDamp(rawPos, clampedTarget, ref velocity, smoothTime);

        rawPos = ClampToBounds(rawPos, boundsCollider.bounds);

        var finalPos = rawPos;

        if (pixelSnap)
            finalPos = SnapWithPixelAccumulator(finalPos, pixelsPerUnit);

        finalPos = ClampToBounds(finalPos, boundsCollider.bounds);

        transform.position = finalPos;
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

    Vector3 SnapWithPixelAccumulator(Vector3 targetWorldPos, int ppu)
    {
        if (ppu <= 0)
            return targetWorldPos;

        Vector3 current = transform.position;

        float dxPixels = (targetWorldPos.x - current.x) * ppu;
        float dyPixels = (targetWorldPos.y - current.y) * ppu;

        accPixelsX += dxPixels;
        accPixelsY += dyPixels;

        int stepX = (int)accPixelsX;
        int stepY = (int)accPixelsY;

        accPixelsX -= stepX;
        accPixelsY -= stepY;

        float newX = current.x + (stepX * PixelWorldStep);
        float newY = current.y + (stepY * PixelWorldStep);

        return new Vector3(newX, newY, targetWorldPos.z);
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

        var desired = new Vector3(
            targetPos.x + followOffset.x,
            targetPos.y + followOffset.y,
            transform.position.z
        );

        var clamped = ClampToBounds(desired, boundsCollider.bounds);

        rawPos = clamped;
        velocity = Vector3.zero;

        accPixelsX = 0f;
        accPixelsY = 0f;

        if (pixelSnap)
            clamped = SnapToPixelGrid(clamped, pixelsPerUnit);

        clamped = ClampToBounds(clamped, boundsCollider.bounds);

        transform.position = clamped;

        refreshTimer = Mathf.Max(0.05f, refreshPlayersEverySeconds);
    }

    static Vector3 SnapToPixelGrid(Vector3 worldPos, int ppu)
    {
        if (ppu <= 0) return worldPos;

        float x = Mathf.Round(worldPos.x * ppu) / ppu;
        float y = Mathf.Round(worldPos.y * ppu) / ppu;
        return new Vector3(x, y, worldPos.z);
    }
}