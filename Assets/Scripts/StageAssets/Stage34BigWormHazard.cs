using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace StageAssets
{
    /// <summary>
    /// Room 1's invulnerable Big Worm hazard. It telegraphs from one of the four
    /// surrounding holes, then strikes the shared centre tile.
    /// </summary>
    public sealed class Stage34BigWormHazard : MonoBehaviour
    {
        private const string StageSceneName = "Stage_3-4";
        private const string RoomName = "Room 1";
        private const string SpriteSheetPath = "StageAssets/BigWorm";
        private const float TelegraphDuration = 1f;
        private const float AttackDuration = 0.5f;
        private const float AttackPeriod = 2f;

        private static readonly AttackPoint[] AttackPoints =
        {
            new(new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f), Direction.Left),
            new(new Vector2(1.5f, 1.5f), new Vector2(1.5f, 2.5f), Direction.Up),
            new(new Vector2(2.5f, 0.5f), new Vector2(3.5f, 0.5f), Direction.Right),
            new(new Vector2(1.5f, -0.5f), new Vector2(1.5f, -1.5f), Direction.Down)
        };
        private static readonly int[] LateralAttackSequence = { 2, 3, 4, 3, 2 };
        private static readonly int[] VerticalAttackSequence = { 2, 3, 2 };

        private static bool sceneHooked;

        private enum Direction
        {
            Right,
            Down,
            Left,
            Up
        }

        private readonly struct AttackPoint
        {
            public readonly Vector2 WorldPosition;
            public readonly Vector2 TargetTilePosition;
            public readonly Direction Direction;

            public AttackPoint(Vector2 worldPosition, Vector2 targetTilePosition, Direction direction)
            {
                WorldPosition = worldPosition;
                TargetTilePosition = targetTilePosition;
                Direction = direction;
            }
        }

        private SpriteRenderer spriteRenderer;
        private Collider2D roomBounds;
        private Sprite[] rightSprites;
        private Sprite[] downSprites;
        private Sprite[] upSprites;
        private Coroutine attackRoutine;
        private bool roomOccupied;
        private bool activatedForRoomOne;
        private bool hasLoggedRoomOccupancy;
        private bool loggedMissingRoom;
        private int playerCandidatesInLastRoomCheck;
        private Vector2 lastPlayerCandidatePosition;
        private Tilemap destructibleTilemap;
        private Tilemap indestructibleTilemap;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            Debug.Log($"[BigWorm] Bootstrap iniciado. Cena ativa: '{SceneManager.GetActiveScene().name}'.");
            if (!sceneHooked)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
                sceneHooked = true;
            }

            TryCreateForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode _)
        {
            Debug.Log($"[BigWorm] Cena carregada: '{scene.name}'.");
            TryCreateForScene(scene);
        }

        private static void TryCreateForScene(Scene scene)
        {
            if (!scene.isLoaded || scene.name != StageSceneName)
                return;

            if (FindAnyObjectByType<Stage34BigWormHazard>() != null)
            {
                Debug.Log("[BigWorm] Controlador já existe nesta cena.");
                return;
            }

            GameObject root = new("BigWorm Hazard");
            root.AddComponent<Stage34BigWormHazard>();
            Debug.Log("[BigWorm] Controlador criado; aguardando os limites da Room 1.");
        }

        private void Awake()
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingLayerName = "Default";
            spriteRenderer.sortingOrder = 5;
            spriteRenderer.enabled = false;

            CreateSprites();
            Debug.Log($"[BigWorm] Awake. Sprites: direita={rightSprites?.Length ?? 0}, baixo={downSprites?.Length ?? 0}, cima={upSprites?.Length ?? 0}.");
        }

        private void OnEnable() => attackRoutine = StartCoroutine(AttackLoop());

        private void OnDisable()
        {
            if (attackRoutine != null)
                StopCoroutine(attackRoutine);

            attackRoutine = null;
            Hide();
        }

        private IEnumerator AttackLoop()
        {
            float nextAttackAt = Time.time + AttackPeriod;
            while (true)
            {
                if (roomBounds == null && !TryResolveRoomBounds())
                {
                    Hide();
                    nextAttackAt = Time.time + AttackPeriod;
                    yield return null;
                    continue;
                }

                bool isOccupied = IsRoomOccupied();
                if (!hasLoggedRoomOccupancy || isOccupied != roomOccupied)
                {
                    roomOccupied = isOccupied;
                    hasLoggedRoomOccupancy = true;
                    Debug.Log($"[BigWorm] Room 1 {(roomOccupied ? "ocupada" : "vazia pelo detector")}. " +
                        $"Jogadores válidos: {playerCandidatesInLastRoomCheck}; última posição: {lastPlayerCandidatePosition}.");
                }

                if (isOccupied && !activatedForRoomOne)
                {
                    activatedForRoomOne = true;
                    nextAttackAt = Time.time + AttackPeriod;
                    Debug.Log("[BigWorm] Primeira entrada na Room 1 confirmada; ataques permanecerão ativos nesta visita à fase.");
                }

                if (!activatedForRoomOne)
                {
                    Hide();
                    yield return null;
                    continue;
                }

                if (Time.time < nextAttackAt)
                {
                    yield return null;
                    continue;
                }

                if (!TryPickAttackPoint(out AttackPoint point))
                {
                    Debug.Log($"[BigWorm] Nenhuma direção livre: {DescribeBlockedTargets()}");
                    nextAttackAt += AttackPeriod;
                    continue;
                }

                Debug.Log($"[BigWorm] Ataque sorteado: {point.Direction} em {point.WorldPosition}.");
                yield return AttackRoutine(point);
                nextAttackAt += AttackPeriod;
                if (nextAttackAt < Time.time)
                    nextAttackAt = Time.time + AttackPeriod;
            }
        }

        private IEnumerator AttackRoutine(AttackPoint point)
        {
            transform.position = point.WorldPosition;
            spriteRenderer.flipX = point.Direction == Direction.Left;

            Sprite[] sprites = GetSprites(point.Direction);
            if (sprites == null || sprites.Length < 4)
            {
                Debug.LogError($"[BigWorm] Sequência de sprites inválida para {point.Direction}.");
                Hide();
                yield break;
            }

            float telegraphEndsAt = Time.time + TelegraphDuration;
            int telegraphFrame = 0;
            while (Time.time < telegraphEndsAt)
            {
                Show(sprites[telegraphFrame % 2]);
                telegraphFrame++;
                yield return new WaitForSeconds(0.1f);
            }

            DamagePlayersOnTargetTile(point);

            int[] attackSequence = sprites.Length == 5 ? LateralAttackSequence : VerticalAttackSequence;
            float attackFrameDuration = AttackDuration / attackSequence.Length;
            for (int i = 0; i < attackSequence.Length; i++)
            {
                Show(sprites[attackSequence[i]]);
                yield return new WaitForSeconds(attackFrameDuration);
            }

            Hide();
        }

        private void DamagePlayersOnTargetTile(AttackPoint point)
        {
            Vector2 target = point.TargetTilePosition;
            MovementController[] players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
            int damagedPlayers = 0;
            for (int i = 0; i < players.Length; i++)
            {
                MovementController player = players[i];
                if (player == null || player.isDead || !IsPlayer(player))
                    continue;

                Vector2 playerPosition = player.Rigidbody != null ? player.Rigidbody.position : player.transform.position;
                if (Vector2.Distance(playerPosition, target) > 0.45f)
                    continue;

                CharacterHealth playerHealth = player.GetComponent<CharacterHealth>();
                if (playerHealth == null)
                    continue;

                playerHealth.TakeDamage(1);
                damagedPlayers++;
            }

            Debug.Log($"[BigWorm] Golpe em {target}; jogadores atingidos: {damagedPlayers}.");
        }

        private bool IsRoomOccupied()
        {
            playerCandidatesInLastRoomCheck = 0;
            MovementController[] players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                MovementController player = players[i];
                if (player == null || player.isDead || !IsPlayer(player))
                    continue;

                playerCandidatesInLastRoomCheck++;
                Vector2 position = player.Rigidbody != null ? player.Rigidbody.position : player.transform.position;
                lastPlayerCandidatePosition = position;
                if (roomBounds.OverlapPoint(position))
                    return true;
            }

            return false;
        }

        private static bool IsPlayer(MovementController controller) =>
            controller.CompareTag("Player") ||
            controller.GetComponent<PlayerIdentity>() != null ||
            controller.GetComponentInParent<PlayerIdentity>() != null;

        private bool TryPickAttackPoint(out AttackPoint selectedPoint)
        {
            int validAttackCount = 0;
            for (int i = 0; i < AttackPoints.Length; i++)
                if (!HasBlockingTile(AttackPoints[i]))
                    validAttackCount++;

            if (validAttackCount == 0)
            {
                selectedPoint = default;
                return false;
            }

            int chosenValidAttack = UnityEngine.Random.Range(0, validAttackCount);
            for (int i = 0; i < AttackPoints.Length; i++)
            {
                AttackPoint candidate = AttackPoints[i];
                if (HasBlockingTile(candidate))
                    continue;

                if (chosenValidAttack-- == 0)
                {
                    selectedPoint = candidate;
                    return true;
                }
            }

            selectedPoint = default;
            return false;
        }

        private bool HasBlockingTile(AttackPoint point)
        {
            ResolveBlockingTilemaps();
            Vector2 checkPosition = GetRoundedBlockCheckPosition(point.TargetTilePosition);
            return HasTileAt(destructibleTilemap, checkPosition) ||
                   HasTileAt(indestructibleTilemap, checkPosition);
        }

        private void ResolveBlockingTilemaps()
        {
            if (destructibleTilemap != null && indestructibleTilemap != null)
                return;

            GameManager manager = GameManager.Instance != null ? GameManager.Instance : FindAnyObjectByType<GameManager>();
            if (manager == null)
                return;

            destructibleTilemap ??= manager.destructibleTilemap;
            indestructibleTilemap ??= manager.indestructibleTilemap;
        }

        private static bool HasTileAt(Tilemap tilemap, Vector2 worldPosition) =>
            tilemap != null && tilemap.HasTile(tilemap.WorldToCell(worldPosition));

        private static Vector2 GetRoundedBlockCheckPosition(Vector2 targetPosition) => new(
            Mathf.RoundToInt(targetPosition.x),
            Mathf.RoundToInt(targetPosition.y));

        private string DescribeBlockedTargets()
        {
            ResolveBlockingTilemaps();
            string description = string.Empty;
            for (int i = 0; i < AttackPoints.Length; i++)
            {
                AttackPoint point = AttackPoints[i];
                Vector2 checkPosition = GetRoundedBlockCheckPosition(point.TargetTilePosition);
                string destructible = GetTileNameAt(destructibleTilemap, checkPosition);
                string indestructible = GetTileNameAt(indestructibleTilemap, checkPosition);
                description += $"{point.Direction} alvo {point.TargetTilePosition}, checagem {checkPosition} [D:{destructible}, I:{indestructible}]";
                if (i < AttackPoints.Length - 1)
                    description += "; ";
            }

            return description;
        }

        private static string GetTileNameAt(Tilemap tilemap, Vector2 worldPosition)
        {
            if (tilemap == null)
                return "tilemap nulo";

            Vector3Int cell = tilemap.WorldToCell(worldPosition);
            TileBase tile = tilemap.GetTile(cell);
            return tile == null ? $"livre célula {cell}" : $"{tile.name} célula {cell}";
        }

        private Sprite[] GetSprites(Direction direction) => direction switch
        {
            Direction.Down => downSprites,
            Direction.Up => upSprites,
            _ => rightSprites
        };

        private void CreateSprites()
        {
            Sprite[] importedSprites = Resources.LoadAll<Sprite>(SpriteSheetPath);
            if (importedSprites == null || importedSprites.Length == 0)
            {
                Debug.LogError($"[BigWorm] Não foi possível carregar sprites em Resources/{SpriteSheetPath}. Verifique o import do sheet.");
                return;
            }

            Sprite[] indexedSprites = new Sprite[13];
            for (int i = 0; i < importedSprites.Length; i++)
            {
                Sprite sprite = importedSprites[i];
                if (sprite == null || !TryGetSpriteIndex(sprite.name, out int index))
                    continue;

                indexedSprites[index] = sprite;
            }

            if (Array.Exists(indexedSprites, sprite => sprite == null))
            {
                Debug.LogError($"[BigWorm] O sheet precisa expor BigWorm_0 a BigWorm_12. Foram carregados {importedSprites.Length} sprites.");
                return;
            }

            rightSprites = new[] { indexedSprites[0], indexedSprites[1], indexedSprites[2], indexedSprites[3], indexedSprites[4] };
            downSprites = new[] { indexedSprites[5], indexedSprites[6], indexedSprites[7], indexedSprites[8] };
            upSprites = new[] { indexedSprites[9], indexedSprites[10], indexedSprites[11], indexedSprites[12] };
        }

        private bool TryResolveRoomBounds()
        {
            roomBounds = World3RoomProgressionController.FindRoomBounds(RoomName);
            if (roomBounds == null)
            {
                if (!loggedMissingRoom)
                {
                    Debug.LogError("[BigWorm] Room 1 não encontrada no World3RoomProgressionController.");
                    loggedMissingRoom = true;
                }

                return false;
            }

            loggedMissingRoom = false;
            Debug.Log($"[BigWorm] Room 1 encontrada em {roomBounds.transform.position}; controlador usa posições globais.");
            return true;
        }

        private static bool TryGetSpriteIndex(string spriteName, out int index)
        {
            const string prefix = "BigWorm_";
            index = -1;
            if (string.IsNullOrEmpty(spriteName) || !spriteName.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            return int.TryParse(spriteName.Substring(prefix.Length), out index) && index >= 0 && index <= 12;
        }

        private void Show(Sprite sprite)
        {
            spriteRenderer.sprite = sprite;
            spriteRenderer.enabled = sprite != null;
        }

        private void Hide()
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = false;
        }
    }
}
