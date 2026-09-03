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
        private const string AttackSfxPath = "Sounds/Worm Attack";
        private const float TelegraphDuration = 1f;
        private const float AttackDuration = 0.5f;
        private const float AttackPeriod = 10f;
        private const float DamageRadius = 0.3f;

        private static readonly AttackPoint[] AttackPoints =
        {
            new(new Vector2(0.5f, 0.5f), new Vector2(-1f, 0f), Direction.Left),
            new(new Vector2(1.5f, 1.5f), new Vector2(1f, 2f), Direction.Up),
            new(new Vector2(2.5f, 0.5f), new Vector2(3f, 0f), Direction.Right),
            new(new Vector2(1.5f, -0.5f), new Vector2(1f, -2f), Direction.Down)
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
        private CircleCollider2D damageAreaCollider;
        private Tilemap destructibleTilemap;
        private Collider2D roomBounds;
        private Sprite[] rightSprites;
        private Sprite[] downSprites;
        private Sprite[] upSprites;
        private AudioSource audioSource;
        private AudioClip attackSfx;
        private Coroutine attackRoutine;
        private bool loggedMissingRoom;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!sceneHooked)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
                sceneHooked = true;
            }

            TryCreateForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode _)
        {
            TryCreateForScene(scene);
        }

        private static void TryCreateForScene(Scene scene)
        {
            if (!scene.isLoaded || scene.name != StageSceneName)
                return;

            if (FindAnyObjectByType<Stage34BigWormHazard>() != null)
                return;

            GameObject root = new("BigWorm Hazard");
            root.AddComponent<Stage34BigWormHazard>();
        }

        private void Awake()
        {
            ConfigureAsEnemy();

            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingLayerName = "Default";
            spriteRenderer.sortingOrder = 5;
            spriteRenderer.enabled = false;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            attackSfx = Resources.Load<AudioClip>(AttackSfxPath);

            CreateDamageAreaCollider();

            CreateSprites();
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

                if (!IsRoomOccupied())
                {
                    Hide();
                    nextAttackAt = Time.time + AttackPeriod;
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
                    nextAttackAt += AttackPeriod;
                    continue;
                }

                yield return AttackRoutine(point);
                nextAttackAt += AttackPeriod;
                if (nextAttackAt < Time.time)
                    nextAttackAt = Time.time + AttackPeriod;
            }
        }

        private IEnumerator AttackRoutine(AttackPoint point)
        {
            transform.position = GetVisualPosition(point);
            spriteRenderer.flipX = point.Direction == Direction.Left;

            Sprite[] sprites = GetSprites(point.Direction);
            if (sprites == null || sprites.Length < 4)
            {
                Debug.LogError($"[BigWorm] Sequência de sprites inválida para {point.Direction}.");
                Hide();
                yield break;
            }

            PositionDamageArea(point);

            float telegraphEndsAt = Time.time + TelegraphDuration;
            int telegraphFrame = 0;
            while (Time.time < telegraphEndsAt)
            {
                if (!IsRoomOccupied())
                {
                    Hide();
                    yield break;
                }

                Show(sprites[telegraphFrame % 2]);
                telegraphFrame++;
                yield return new WaitForSeconds(0.1f);
            }

            if (!IsRoomOccupied())
            {
                Hide();
                yield break;
            }

            EnableDamageAreaCollider();
            DamagePlayersOnTargetTile(point);
            // The supplied effect is longer than the strike animation. Use the
            // source's clip playback so it can end exactly with the 0.5 s hit.
            GameAudioSettings.PlaySfxClip(audioSource, attackSfx);

            int[] attackSequence = sprites.Length == 5 ? LateralAttackSequence : VerticalAttackSequence;
            float attackFrameDuration = AttackDuration / attackSequence.Length;
            for (int i = 0; i < attackSequence.Length; i++)
            {
                if (!IsRoomOccupied())
                {
                    audioSource.Stop();
                    Hide();
                    yield break;
                }

                Show(sprites[attackSequence[i]]);
                yield return new WaitForSeconds(attackFrameDuration);
            }

            audioSource.Stop();
            Hide();
        }

        private void DamagePlayersOnTargetTile(AttackPoint point)
        {
            Vector2 target = point.TargetTilePosition;
            MovementController[] players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                MovementController player = players[i];
                if (player == null)
                    continue;

                if (player.isDead)
                    continue;

                if (!IsPlayer(player))
                    continue;

                Vector2 playerPosition = player.Rigidbody != null ? player.Rigidbody.position : player.transform.position;
                Vector2 offsetFromTarget = playerPosition - target;
                const float positionTolerance = 0.01f;
                float distanceToTarget = offsetFromTarget.magnitude;
                if (distanceToTarget > DamageRadius + positionTolerance)
                    continue;

                CharacterHealth playerHealth = player.GetComponent<CharacterHealth>() ?? player.GetComponentInParent<CharacterHealth>();
                if (playerHealth == null)
                    continue;

                playerHealth.TakeDamage(1);
            }
        }

        private bool IsRoomOccupied()
        {
            MovementController[] players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                MovementController player = players[i];
                if (player == null || player.isDead || !IsPlayer(player))
                    continue;

                Vector2 position = player.Rigidbody != null ? player.Rigidbody.position : player.transform.position;
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
            ResolveDestructibleTilemap();

            int availableCount = 0;
            for (int i = 0; i < AttackPoints.Length; i++)
            {
                if (!HasDestructibleTileInDamageArea(AttackPoints[i]))
                    availableCount++;
            }

            if (availableCount == 0)
            {
                selectedPoint = default;
                return false;
            }

            int selectedAvailableIndex = UnityEngine.Random.Range(0, availableCount);
            for (int i = 0; i < AttackPoints.Length; i++)
            {
                AttackPoint candidate = AttackPoints[i];
                if (HasDestructibleTileInDamageArea(candidate))
                    continue;

                if (selectedAvailableIndex-- == 0)
                {
                    selectedPoint = candidate;
                    return true;
                }
            }

            selectedPoint = default;
            return false;
        }

        private bool HasDestructibleTileInDamageArea(AttackPoint point)
        {
            if (destructibleTilemap == null)
                return false;

            Vector3Int cell = destructibleTilemap.WorldToCell(point.TargetTilePosition);
            return destructibleTilemap.HasTile(cell);
        }

        private void ResolveDestructibleTilemap()
        {
            if (destructibleTilemap != null)
                return;

            GameManager manager = GameManager.Instance != null ? GameManager.Instance : FindAnyObjectByType<GameManager>();
            destructibleTilemap = manager != null ? manager.destructibleTilemap : null;
        }

        private void ConfigureAsEnemy()
        {
            gameObject.tag = "Enemy";

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
                gameObject.layer = enemyLayer;
        }

        private static Vector2 GetVisualPosition(AttackPoint point) => point.Direction switch
        {
            Direction.Up => new Vector2(1f, 1.75f),
            Direction.Down => new Vector2(1f, -1.5f),
            Direction.Right => new Vector2(2.72f, 0.1f),
            Direction.Left => new Vector2(-0.75f, 0.1f),
            _ => point.WorldPosition
        };

        private Sprite[] GetSprites(Direction direction) => direction switch
        {
            Direction.Down => downSprites,
            Direction.Up => upSprites,
            _ => rightSprites
        };

        private void CreateDamageAreaCollider()
        {
            GameObject areaObject = new("BigWorm Damage Area");
            areaObject.transform.SetParent(transform, false);
            areaObject.tag = "Enemy";
            areaObject.layer = gameObject.layer;
            damageAreaCollider = areaObject.AddComponent<CircleCollider2D>();
            damageAreaCollider.radius = DamageRadius;
            damageAreaCollider.isTrigger = true;
            damageAreaCollider.enabled = false;
        }

        private void PositionDamageArea(AttackPoint point)
        {
            if (damageAreaCollider == null)
                return;

            Vector2 target = point.TargetTilePosition;
            damageAreaCollider.transform.position = new Vector3(target.x, target.y, 0f);
        }

        private void EnableDamageAreaCollider()
        {
            if (damageAreaCollider != null)
                damageAreaCollider.enabled = true;
        }

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

            if (damageAreaCollider != null)
                damageAreaCollider.enabled = false;
        }
    }
}
