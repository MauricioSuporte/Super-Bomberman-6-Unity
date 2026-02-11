using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class CameraFollowClamp2D : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private bool followAllPlayers = true;

    [Header("Bounds")]
    [SerializeField] private Collider2D boundsCollider;

    [Header("Follow")]
    [SerializeField] private Vector2 followOffset;
    [SerializeField] private float smoothTime = 0.12f;

    [Header("Player Search")]
    [SerializeField] private float refreshPlayersEverySeconds = 0.5f;

    Camera cam;
    Vector3 velocity;

    Transform followTarget;
    PlayerIdentity[] cachedPlayers = new PlayerIdentity[0];
    float refreshTimer;

    void Awake()
    {
        cam = GetComponent<Camera>();
        followTarget = new GameObject("CameraTarget").transform;
        followTarget.hideFlags = HideFlags.HideInHierarchy;
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

        var clamped = ClampToBounds(desired, boundsCollider.bounds);
        transform.position = Vector3.SmoothDamp(transform.position, clamped, ref velocity, smoothTime);
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
        float horzExtent = vertExtent * (4f / 3f);

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

        transform.position = clamped;
        velocity = Vector3.zero;

        refreshTimer = Mathf.Max(0.05f, refreshPlayersEverySeconds);
    }
}
