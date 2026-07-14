using UnityEngine;

public sealed class CoreMechanismsPlayerSorting : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float horizontalRange = 0.7f;
    [SerializeField, Min(0.1f)] private float verticalRange = 1.05f;
    [SerializeField, Min(1)] private int playerBehindOrderOffset = 1;

    private SpriteRenderer[] renderers;
    private int[] originalSortingLayerIds;
    private int[] originalSortingOrders;
    private MovementController[] cachedPlayers;
    private SpriteRenderer[] adjustedPlayerRenderers = new SpriteRenderer[0];
    private int[] adjustedPlayerSortingLayerIds = new int[0];
    private int[] adjustedPlayerSortingOrders = new int[0];

    private void Awake()
    {
        CacheRenderers();
        RefreshPlayers();
    }

    private void OnEnable()
    {
        RefreshPlayers();
        ApplyOriginalSorting();
    }

    private void OnDisable()
    {
        RestoreAdjustedPlayers();
    }

    private void LateUpdate()
    {
        ApplyOriginalSorting();
        RestoreAdjustedPlayers();
        ApplyPlayerSorting();
    }

    private void CacheRenderers()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalSortingLayerIds = new int[renderers.Length];
        originalSortingOrders = new int[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalSortingLayerIds[i] = renderers[i] != null ? renderers[i].sortingLayerID : 0;
            originalSortingOrders[i] = renderers[i] != null ? renderers[i].sortingOrder : 0;
        }
    }

    private void RefreshPlayers()
    {
        cachedPlayers = FindObjectsByType<MovementController>(
            FindObjectsInactive.Exclude);
    }

    private void ApplyPlayerSorting()
    {
        if (cachedPlayers == null || cachedPlayers.Length == 0)
            RefreshPlayers();

        Vector3 position = transform.position;

        for (int i = 0; i < cachedPlayers.Length; i++)
        {
            MovementController player = cachedPlayers[i];
            if (player == null || !player.CompareTag("Player") || player.isDead)
                continue;

            Vector3 playerPosition = player.transform.position;
            float dx = Mathf.Abs(playerPosition.x - position.x);
            float dy = Mathf.Abs(playerPosition.y - position.y);
            if (dx > horizontalRange || dy > verticalRange)
                continue;

            if (playerPosition.y <= position.y)
                continue;

            SpriteRenderer playerRenderer = GetActiveSpriteRenderer(player);
            if (playerRenderer == null)
                continue;

            StoreAdjustedPlayer(playerRenderer);
            playerRenderer.sortingLayerID = GetHighestOriginalSortingLayerId();
            playerRenderer.sortingOrder = GetLowestOriginalSortingOrder() - playerBehindOrderOffset;
        }
    }

    private static SpriteRenderer GetActiveSpriteRenderer(MovementController player)
    {
        AnimatedSpriteRenderer active = player != null ? player.ActiveSpriteRenderer : null;
        if (active != null && active.TryGetComponent(out SpriteRenderer spriteRenderer))
            return spriteRenderer;

        return player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
    }

    private int GetHighestOriginalSortingLayerId()
    {
        return originalSortingLayerIds != null && originalSortingLayerIds.Length > 0
            ? originalSortingLayerIds[0]
            : 0;
    }

    private int GetLowestOriginalSortingOrder()
    {
        if (originalSortingOrders == null || originalSortingOrders.Length == 0)
            return 0;

        int order = originalSortingOrders[0];
        for (int i = 1; i < originalSortingOrders.Length; i++)
            order = Mathf.Min(order, originalSortingOrders[i]);

        return order;
    }

    private void ApplyOriginalSorting()
    {
        if (renderers == null || originalSortingLayerIds == null || originalSortingOrders == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || i >= originalSortingOrders.Length || i >= originalSortingLayerIds.Length)
                continue;

            renderers[i].sortingLayerID = originalSortingLayerIds[i];
            renderers[i].sortingOrder = originalSortingOrders[i];
        }
    }

    private void StoreAdjustedPlayer(SpriteRenderer playerRenderer)
    {
        for (int i = 0; i < adjustedPlayerRenderers.Length; i++)
        {
            if (adjustedPlayerRenderers[i] == playerRenderer)
                return;
        }

        int index = adjustedPlayerRenderers.Length;
        System.Array.Resize(ref adjustedPlayerRenderers, index + 1);
        System.Array.Resize(ref adjustedPlayerSortingLayerIds, index + 1);
        System.Array.Resize(ref adjustedPlayerSortingOrders, index + 1);

        adjustedPlayerRenderers[index] = playerRenderer;
        adjustedPlayerSortingLayerIds[index] = playerRenderer.sortingLayerID;
        adjustedPlayerSortingOrders[index] = playerRenderer.sortingOrder;
    }

    private void RestoreAdjustedPlayers()
    {
        for (int i = 0; i < adjustedPlayerRenderers.Length; i++)
        {
            SpriteRenderer playerRenderer = adjustedPlayerRenderers[i];
            if (playerRenderer == null)
                continue;

            playerRenderer.sortingLayerID = adjustedPlayerSortingLayerIds[i];
            playerRenderer.sortingOrder = adjustedPlayerSortingOrders[i];
        }

        if (adjustedPlayerRenderers.Length == 0)
            return;

        adjustedPlayerRenderers = new SpriteRenderer[0];
        adjustedPlayerSortingLayerIds = new int[0];
        adjustedPlayerSortingOrders = new int[0];
    }
}
