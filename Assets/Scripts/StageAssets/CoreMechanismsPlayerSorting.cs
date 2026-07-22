using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class CoreMechanismsPlayerSorting : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float horizontalRange = 0.7f;
    [SerializeField, Min(0.1f)] private float verticalRange = 1.05f;
    [FormerlySerializedAs("playerBehindOrderOffset")]
    [SerializeField, Min(1)] private int sortingOrderOffset = 1;

    private SpriteRenderer[] renderers;
    private int[] originalSortingLayerIds;
    private int[] originalSortingOrders;
    private MovementController[] cachedPlayers;
    private SpriteRenderer[] adjustedBombRenderers = new SpriteRenderer[0];
    private int[] adjustedBombSortingLayerIds = new int[0];
    private int[] adjustedBombSortingOrders = new int[0];
    private readonly HashSet<Bomb> loggedSortedBombs = new();
    private readonly HashSet<Bomb> loggedBombsWithoutRenderer = new();

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
        RestoreAdjustedBombs();
    }

    private void LateUpdate()
    {
        ApplyOriginalSorting();
        RestoreAdjustedBombs();
        ApplyPlayerSorting();
        ApplyBombSorting();
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

            int targetSortingLayerId = playerRenderer.sortingLayerID;
            int targetSortingOrder = playerRenderer.sortingOrder + sortingOrderOffset;
            ApplyCoreSortingInFrontOf(playerRenderer, targetSortingLayerId, targetSortingOrder);
        }
    }

    private void ApplyBombSorting()
    {
        Vector3 position = transform.position;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            Vector3 bombPosition = bomb.transform.position;
            float dx = Mathf.Abs(bombPosition.x - position.x);
            float dy = Mathf.Abs(bombPosition.y - position.y);
            if (dx > horizontalRange || dy > verticalRange || bombPosition.y <= position.y)
                continue;

            SpriteRenderer bombRenderer = GetBombSpriteRenderer(bomb);

            StoreAdjustedBomb(bombRenderer);
            bombRenderer.sortingLayerID = GetHighestOriginalSortingLayerId();
            bombRenderer.sortingOrder = GetLowestOriginalSortingOrder() - sortingOrderOffset;
        }
    }

    private static SpriteRenderer GetActiveSpriteRenderer(MovementController player)
    {
        AnimatedSpriteRenderer active = player != null ? player.ActiveSpriteRenderer : null;
        if (active != null && active.TryGetComponent(out SpriteRenderer spriteRenderer))
            return spriteRenderer;

        return player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
    }

    private static SpriteRenderer GetBombSpriteRenderer(Bomb bomb)
    {
        if (bomb != null && bomb.TryGetComponent(out SpriteRenderer spriteRenderer))
            return spriteRenderer;

        return bomb != null ? bomb.GetComponentInChildren<SpriteRenderer>() : null;
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

    private void ApplyCoreSortingInFrontOf(
        SpriteRenderer playerRenderer,
        int targetSortingLayerId,
        int targetSortingOrder)
    {
        if (playerRenderer == null || renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer coreRenderer = renderers[i];
            if (coreRenderer == null)
                continue;

            coreRenderer.sortingLayerID = targetSortingLayerId;
            coreRenderer.sortingOrder = Mathf.Max(coreRenderer.sortingOrder, targetSortingOrder);
        }
    }

    private void StoreAdjustedBomb(SpriteRenderer bombRenderer)
    {
        for (int i = 0; i < adjustedBombRenderers.Length; i++)
        {
            if (adjustedBombRenderers[i] == bombRenderer)
                return;
        }

        int index = adjustedBombRenderers.Length;
        System.Array.Resize(ref adjustedBombRenderers, index + 1);
        System.Array.Resize(ref adjustedBombSortingLayerIds, index + 1);
        System.Array.Resize(ref adjustedBombSortingOrders, index + 1);

        adjustedBombRenderers[index] = bombRenderer;
        adjustedBombSortingLayerIds[index] = bombRenderer.sortingLayerID;
        adjustedBombSortingOrders[index] = bombRenderer.sortingOrder;
    }

    private void RestoreAdjustedBombs()
    {
        for (int i = 0; i < adjustedBombRenderers.Length; i++)
        {
            SpriteRenderer bombRenderer = adjustedBombRenderers[i];
            if (bombRenderer == null)
                continue;

            bombRenderer.sortingLayerID = adjustedBombSortingLayerIds[i];
            bombRenderer.sortingOrder = adjustedBombSortingOrders[i];
        }

        if (adjustedBombRenderers.Length == 0)
            return;

        adjustedBombRenderers = new SpriteRenderer[0];
        adjustedBombSortingLayerIds = new int[0];
        adjustedBombSortingOrders = new int[0];
    }
}
