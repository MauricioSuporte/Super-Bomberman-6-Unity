using System.Collections.Generic;
using UnityEngine;

namespace StageAssets
{
    /// <summary>
    /// Gives the Room 2 bridge in Stage 3-1 a small, reversible bend while a
    /// player stands on one of its three walkable tiles.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class World3BridgeWeightController : MonoBehaviour
    {
        private const int FirstBridgeCellX = 20;
        private const int LastBridgeCellX = 22;
        private const int BridgeCellY = -1;
        private const int CenterBridgeCellX = 21;

        [Header("Bridge Bend")]
        [SerializeField, Min(0f)] private float boardDropY = 0.0625f;
        [SerializeField, Min(0.01f)] private float transitionSpeed = 1.5f;

        [Header("Center Tile Player Offset")]
        [SerializeField, Min(0f)] private float centerPlayerSpriteDropY = 0.125f;

        private readonly Dictionary<Transform, Vector3> boardBasePositions = new();
        private readonly Dictionary<AnimatedSpriteRenderer, float> playerSpriteOffsets = new();
        private readonly HashSet<int> occupiedBridgeColumns = new();

        private void Awake()
        {
            CacheBoardSprites();
        }

        private void LateUpdate()
        {
            UpdateOccupiedBridgeColumns();
            AnimateBridgeBoards();
            AnimateCenterTilePlayers();
        }

        private void OnDisable()
        {
            RestorePositions(boardBasePositions);
            ClearPlayerSpriteOffsets();
        }

        private void CacheBoardSprites()
        {
            SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                SpriteRenderer sprite = sprites[i];
                if (sprite == null ||
                    sprite.transform == transform ||
                    sprite.gameObject.name.StartsWith("Bridge Pillar"))
                    continue;

                if (!boardBasePositions.ContainsKey(sprite.transform))
                    boardBasePositions.Add(sprite.transform, sprite.transform.localPosition);
            }
        }

        private void UpdateOccupiedBridgeColumns()
        {
            occupiedBridgeColumns.Clear();

            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            for (int i = 0; i < players.Length; i++)
            {
                GameObject player = players[i];
                if (player == null || !player.activeInHierarchy)
                    continue;

                Vector3Int cell = GetWorldCell(player.transform.position);
                if (cell.y == BridgeCellY &&
                    cell.x >= FirstBridgeCellX &&
                    cell.x <= LastBridgeCellX)
                {
                    occupiedBridgeColumns.Add(cell.x);
                }
            }
        }

        private void AnimateBridgeBoards()
        {
            foreach (KeyValuePair<Transform, Vector3> entry in boardBasePositions)
            {
                Transform board = entry.Key;
                if (board == null)
                    continue;

                int boardColumn = Mathf.RoundToInt(board.position.x);
                bool isOccupied = occupiedBridgeColumns.Contains(boardColumn);
                Vector3 target = entry.Value + (isOccupied ? Vector3.down * boardDropY : Vector3.zero);

                board.localPosition = Vector3.MoveTowards(
                    board.localPosition,
                    target,
                    transitionSpeed * Time.deltaTime);
            }
        }

        private void AnimateCenterTilePlayers()
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            for (int i = 0; i < players.Length; i++)
            {
                GameObject player = players[i];
                if (player == null || !player.activeInHierarchy)
                    continue;

                Vector3Int playerCell = GetWorldCell(player.transform.position);
                bool isOnCenterTile = playerCell == new Vector3Int(CenterBridgeCellX, BridgeCellY, 0);
                AnimatePlayerBodySprites(player.transform, isOnCenterTile);
            }
        }

        private void AnimatePlayerBodySprites(Transform player, bool isOnCenterTile)
        {
            GameObject mountedLouie = null;
            if (player.TryGetComponent(out PlayerMountCompanion mountCompanion))
                mountedLouie = mountCompanion.GetMountedLouieObject();

            AnimatedSpriteRenderer[] animatedSprites = player.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
            for (int i = 0; i < animatedSprites.Length; i++)
            {
                AnimatedSpriteRenderer animatedSprite = animatedSprites[i];
                if (animatedSprite == null ||
                    animatedSprite.transform.parent != player ||
                    (mountedLouie != null &&
                     (animatedSprite.transform == mountedLouie.transform ||
                      animatedSprite.transform.IsChildOf(mountedLouie.transform))))
                    continue;

                AnimateSpriteOffset(animatedSprite, isOnCenterTile);
            }

            if (mountedLouie == null)
                return;

            AnimatedSpriteRenderer[] mountSprites = mountedLouie.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
            for (int i = 0; i < mountSprites.Length; i++)
            {
                if (mountSprites[i] != null)
                    AnimateSpriteOffset(mountSprites[i], isOnCenterTile);
            }
        }

        private void AnimateSpriteOffset(AnimatedSpriteRenderer animatedSprite, bool isOnCenterTile)
        {
            bool hasCachedOffset = playerSpriteOffsets.TryGetValue(animatedSprite, out float cachedOffset);
            if (!isOnCenterTile && !hasCachedOffset)
                return;

            float currentOffset = hasCachedOffset ? cachedOffset : 0f;
            float targetOffset = isOnCenterTile ? -centerPlayerSpriteDropY : 0f;
            float nextOffset = Mathf.MoveTowards(
                currentOffset,
                targetOffset,
                transitionSpeed * Time.deltaTime);

            if (Mathf.Abs(nextOffset) <= 0.0001f && !isOnCenterTile)
            {
                if (Mathf.Abs(currentOffset) > 0.0001f)
                    animatedSprite.ClearExternalBase();
                playerSpriteOffsets[animatedSprite] = 0f;
            }
            else
            {
                animatedSprite.SetExternalBaseOffsetFromInitial(Vector3.up * nextOffset);
                playerSpriteOffsets[animatedSprite] = nextOffset;
            }
        }

        private static Vector3Int GetWorldCell(Vector3 position)
        {
            return new Vector3Int(
                Mathf.RoundToInt(position.x),
                Mathf.RoundToInt(position.y),
                0);
        }

        private static void RestorePositions(Dictionary<Transform, Vector3> positions)
        {
            foreach (KeyValuePair<Transform, Vector3> entry in positions)
            {
                if (entry.Key != null)
                    entry.Key.localPosition = entry.Value;
            }
        }

        private void ClearPlayerSpriteOffsets()
        {
            foreach (KeyValuePair<AnimatedSpriteRenderer, float> entry in playerSpriteOffsets)
            {
                if (entry.Key != null)
                    entry.Key.ClearExternalBase();
            }

            playerSpriteOffsets.Clear();
        }
    }
}
