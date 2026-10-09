using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StageAssets
{
    [DisallowMultipleComponent]
    public sealed class World3HallStageSevenSequence : MonoBehaviour
    {
        private const float SequenceDuration = 3f;
        private const float FallingDuration = 0.75f;
        private const float BeforeDropDelay = 0.35f;
        private const float ImpactShakeDuration = 0.2f;

        [SerializeField] private Tilemap indestructibleTilemap;
        [SerializeField] private Tile indestructibleTile;
        [SerializeField] private Tilemap destructibleTilemap;
        [SerializeField] private Tile destructibleTile;
        [SerializeField] private GameObject hallExit;
        [SerializeField] private AudioClip stageMusic;
        [SerializeField, Range(0f, 1f)] private float stageMusicVolume = 1f;
        [SerializeField] private AudioClip tileDropSfx;
        [SerializeField] private GameObject prettyBomberPrefab = null;
        [SerializeField] private AudioClip bossEntranceSfx = null;

        private GameObject entranceVisual;
        private GameObject enteringBoss;

        private bool started;
        public bool DuelActive { get; private set; }

        public void FinishDuel() => DuelActive = false;
        private bool playersMovementFinished;
        private static readonly HashSet<Vector3Int> DestructibleCells = new()
        {
            new(-5, -5, 0), new(-3, -5, 0), new(1, -5, 0), new(3, -5, 0),
            new(-6, -4, 0), new(4, -4, 0), new(-3, -3, 0), new(3, -3, 0),
            new(-6, 0, 0), new(4, 0, 0), new(-5, 1, 0), new(-3, 1, 0),
            new(1, 1, 0), new(3, 1, 0)
        };
        private int remainingDrops;
        private readonly List<ImpactVoice> impactVoices = new();
        private readonly List<PlayerDanceState> dancingPlayers = new();

        private sealed class PlayerDanceState
        {
            public MovementController Movement;
            public Transform Root;
            public Vector3 Origin;
            public Vector3 FinalPosition;
            public Collider2D Collider;
            public bool ColliderEnabled;
            public BombController Bomb;
            public bool BombEnabled;
            public bool InputLocked;
            public bool ExternalOverride;
            public bool MovementEnabled;
        }

        private struct ImpactVoice
        {
            public AudioSource Source;
            public double EndTime;
        }

        public bool CanPlay => indestructibleTilemap != null && indestructibleTile != null &&
            indestructibleTile.sprite != null && destructibleTilemap != null && destructibleTile != null &&
            destructibleTile.sprite != null && hallExit != null && stageMusic != null && tileDropSfx != null &&
            prettyBomberPrefab != null && bossEntranceSfx != null;

        public void Play()
        {
            if (started || !CanPlay)
                return;
            started = true;
            hallExit.SetActive(false);
            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            if (GameMusicController.Instance != null)
                GameMusicController.Instance.PlayMusic(stageMusic, stageMusicVolume, loop: true);

            tileDropSfx.LoadAudioData();
            yield return new WaitForSeconds(BeforeDropDelay);
            StartCoroutine(MovePlayersRoutine());

            var cells = new List<Vector3Int>();
            for (int y = 3; y <= 4; y++)
                for (int x = -7; x <= 5; x++)
                    cells.Add(new Vector3Int(x, y, 0));
            for (int y = -5; y <= 1; y += 2)
                for (int x = -6; x <= 4; x += 2)
                    cells.Add(new Vector3Int(x, y, 0));

            cells.AddRange(DestructibleCells);
            // Equal x+y cells fall together: sweep from bottom-left to top-right.
            cells.Sort((a, b) =>
            {
                int diagonal = (a.x + a.y).CompareTo(b.x + b.y);
                return diagonal != 0 ? diagonal : a.x.CompareTo(b.x);
            });

            int firstDiagonal = cells[0].x + cells[0].y;
            remainingDrops = cells.Count;
            int lastDiagonal = cells[cells.Count - 1].x + cells[cells.Count - 1].y;
            int next = 0;
            float elapsed = 0f;
            while (elapsed < SequenceDuration)
            {
                while (next < cells.Count)
                {
                    Vector3Int cell = cells[next];
                    float dropAt = (cell.x + cell.y - firstDiagonal) /
                        (float)(lastDiagonal - firstDiagonal) * (SequenceDuration - FallingDuration);
                    if (dropAt > elapsed)
                        break;
                    StartCoroutine(DropTile(cell));
                    next++;
                }
                yield return null;
                elapsed += Time.deltaTime;
            }

            while (remainingDrops > 0 || !playersMovementFinished)
                yield return null;
            var manager = FindAnyObjectByType<GameManager>();
            if (manager != null)
                manager.RegisterSpawnedDestructibles(DestructibleCells);
            yield return PrettyBomberEntrance();
            RestoreDancingPlayers(completed: true, enableGameplay: true);

            // The entrance has finished; remaining impact sounds can finish.
            while (impactVoices.Count > 0)
                yield return null;
        }

        private void Update()
        {
            CleanupImpactVoices();
        }

        private IEnumerator MovePlayersRoutine()
        {
            foreach (var movement in FindObjectsByType<MovementController>())
            {
                if (!movement.CompareTag("Player") || movement.isDead)
                    continue;
                var identity = movement.GetComponentInParent<PlayerIdentity>();
                Transform root = identity != null ? identity.transform : movement.transform;
                var collider = movement.GetComponent<Collider2D>();
                var bomb = movement.GetComponent<BombController>();
                dancingPlayers.Add(new PlayerDanceState
                {
                    Movement = movement, Root = root, Origin = root.position,
                    FinalPosition = root.position + Vector3.up * movement.tileSize,
                    Collider = collider, ColliderEnabled = collider != null && collider.enabled,
                    Bomb = bomb, BombEnabled = bomb != null && bomb.enabled,
                    InputLocked = movement.InputLocked, ExternalOverride = movement.ExternalMovementOverride,
                    MovementEnabled = movement.enabled
                });
                movement.SetInputLocked(true, true);
                movement.SetExternalMovementOverride(true);
                movement.enabled = true;
                if (collider != null)
                    collider.enabled = false;
                if (bomb != null)
                    bomb.enabled = false;
            }

            const float pauseDuration = 0.2f;
            const float secondsPerTile = (SequenceDuration - 2f * pauseDuration) / 13f;
            Vector2[] steps =
            {
                Vector2.up, Vector2.zero, Vector2.left, Vector2.right * 2f,
                Vector2.left * 2f, Vector2.right * 2f, Vector2.left * 2f,
                Vector2.right * 2f, Vector2.left
            };
            float elapsed = 0f;
            while (elapsed < SequenceDuration - pauseDuration)
            {
                float segmentTime = elapsed;
                Vector2 offset = Vector2.zero;
                Vector2 direction = Vector2.zero;
                foreach (Vector2 step in steps)
                {
                    float duration = step == Vector2.zero ? pauseDuration : step.magnitude * secondsPerTile;
                    if (segmentTime < duration)
                    {
                        offset += step * Mathf.Clamp01(segmentTime / duration);
                        direction = step.normalized;
                        break;
                    }
                    segmentTime -= duration;
                    offset += step;
                }

                foreach (PlayerDanceState player in dancingPlayers)
                {
                    if (player.Movement == null || player.Root == null || player.Movement.isDead)
                        continue;
                    player.Movement.ApplyDirectionFromVector(direction);
                    Vector3 position = player.Origin + (Vector3)(offset * player.Movement.tileSize);
                    player.Root.position = position;
                    if (player.Movement.Rigidbody != null)
                    {
                        player.Movement.Rigidbody.position = position;
                        player.Movement.Rigidbody.linearVelocity = Vector2.zero;
                    }
                }
                yield return null;
                elapsed += Time.deltaTime;
            }
            foreach (PlayerDanceState player in dancingPlayers)
            {
                if (player.Movement == null || player.Root == null || player.Movement.isDead)
                    continue;
                player.Root.position = player.FinalPosition;
                if (player.Movement.Rigidbody != null)
                {
                    player.Movement.Rigidbody.position = player.FinalPosition;
                    player.Movement.Rigidbody.linearVelocity = Vector2.zero;
                }
                player.Movement.ForceIdleFacing(Vector2.down);
            }
            yield return new WaitForSeconds(pauseDuration);
            foreach (PlayerDanceState player in dancingPlayers)
                if (player.Movement != null && !player.Movement.isDead)
                    player.Movement.ForceIdleUpConsideringMount();
            playersMovementFinished = true;
        }

        private void RestoreDancingPlayers(bool completed = false, bool enableGameplay = false)
        {
            foreach (PlayerDanceState player in dancingPlayers)
            {
                if (player.Movement == null)
                    continue;
                if (player.Root != null)
                    player.Root.position = completed ? player.FinalPosition : player.Origin;
                if (player.Movement.Rigidbody != null)
                    player.Movement.Rigidbody.position = completed ? player.FinalPosition : player.Origin;
                player.Movement.SetExternalMovementOverride(!enableGameplay && player.ExternalOverride);
                player.Movement.SetInputLocked(!enableGameplay && player.InputLocked, false);
                player.Movement.ForceIdleUpConsideringMount();
                player.Movement.enabled = enableGameplay || player.MovementEnabled;
                if (player.Collider != null)
                    player.Collider.enabled = enableGameplay || player.ColliderEnabled;
                if (player.Bomb != null)
                    player.Bomb.enabled = enableGameplay || player.BombEnabled;
            }
            dancingPlayers.Clear();
        }

        private void OnDisable()
        {
            DuelActive = false;
            StopAllCoroutines();
            RestoreDancingPlayers();
            if (enteringBoss != null)
                Destroy(enteringBoss);
            CleanupEntranceVisual();
            foreach (ImpactVoice voice in impactVoices)
                if (voice.Source != null)
                    Destroy(voice.Source.gameObject);
            impactVoices.Clear();
        }

        private IEnumerator PrettyBomberEntrance()
        {
            Vector3 destination = new(-1f, 0f, 0f);
            enteringBoss = Instantiate(prettyBomberPrefab, destination, Quaternion.identity);
            var movement = enteringBoss.GetComponent<MovementControllerAI>();
            var brain = enteringBoss.GetComponent<BrainIA>();
            var bombs = enteringBoss.GetComponent<BombController>();
            var colliders = enteringBoss.GetComponentsInChildren<Collider2D>();
            var colliderStates = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                colliderStates[i] = colliders[i].enabled;
                colliders[i].enabled = false;
            }
            brain.enabled = false;
            bombs.enabled = false;
            bombs.destructibleTiles = destructibleTilemap;
            movement.SetInputLocked(true, true);
            movement.SetExplosionInvulnerable(true);
            movement.ForceFacingDirection(Vector2.down);
            movement.SetIntroIdle(true);
            enteringBoss.GetComponent<AbilitySystem>().Enable(BombPassAbility.AbilityId);

            var renderers = enteringBoss.GetComponentsInChildren<SpriteRenderer>(true);
            var effect = PrettyBomberMagicEffect.Create(destination + Vector3.down * 0.5f,
                movement.tileSize, renderers[0]);
            entranceVisual = effect.gameObject;
            entranceVisual.transform.SetParent(transform, true);

            var visibleStates = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                visibleStates[i] = renderers[i].enabled;
                renderers[i].enabled = false;
            }
            yield return effect.Grow(0.5f);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = visibleStates[i];

            var audioObject = new GameObject("PrettyBomber Entrance Audio");
            audioObject.transform.SetParent(transform, false);
            var audio = audioObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            GameAudioSettings.PlaySfx(audio, bossEntranceSfx);
            Destroy(audioObject, bossEntranceSfx.length + 0.1f);

            yield return effect.Reveal(enteringBoss, movement, true, 0.5f);
            yield return effect.Close(0.5f);

            float elapsed = 0f;
            while (elapsed < 1f)
            {
                yield return null;
                if (!GamePauseController.IsPaused)
                    elapsed += Time.deltaTime;
            }

            movement.SetIntroIdle(false);
            movement.SetInputLocked(false, true);
            movement.SetExplosionInvulnerable(false);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = colliderStates[i];
            bombs.enabled = true;
            brain.enabled = true;
            DuelActive = true;
            if (StageIntroTransition.Instance != null)
            {
                StageIntroTransition.Instance.world = 3;
                StageIntroTransition.Instance.stageNumber = 7;
            }
            enteringBoss = null;
            CleanupEntranceVisual();
        }

        private void CleanupEntranceVisual()
        {
            if (entranceVisual != null)
                Destroy(entranceVisual);
        }

        private void CleanupImpactVoices()
        {
            double now = AudioSettings.dspTime;
            for (int i = impactVoices.Count - 1; i >= 0; i--)
            {
                ImpactVoice voice = impactVoices[i];
                if (voice.Source != null && now < voice.EndTime)
                    continue;
                if (voice.Source != null)
                    Destroy(voice.Source.gameObject);
                impactVoices.RemoveAt(i);
            }
        }

        private void PlayImpact()
        {
            var audioObject = new GameObject("World3Hall Tile Impact Audio");
            audioObject.transform.SetParent(transform, false);
            var source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = false;
            source.clip = tileDropSfx;
            source.volume = GameAudioSettings.ApplySfxVolume(1f);
            double now = AudioSettings.dspTime;
            impactVoices.Add(new ImpactVoice
            {
                Source = source,
                EndTime = now + tileDropSfx.length
            });
            // Start at the landing, without accumulating delays across blocks.
            source.Play();
        }

        private IEnumerator DropTile(Vector3Int cell)
        {
            bool destructible = DestructibleCells.Contains(cell);
            Tilemap tilemap = destructible ? destructibleTilemap : indestructibleTilemap;
            Tile tile = destructible ? destructibleTile : indestructibleTile;
            var tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();
            Vector3 end = tilemap.GetCellCenterWorld(cell);
            float spawnY = end.y + 6f;
            var camera = Camera.main;
            if (camera != null)
            {
                Vector3 top = camera.ViewportToWorldPoint(new Vector3(0.5f, 1f,
                    Mathf.Abs(camera.transform.position.z - end.z)));
                spawnY = Mathf.Max(spawnY, top.y + 0.5f);
            }
            Vector3 start = new Vector3(end.x, spawnY, end.z);
            var visual = new GameObject("Stage3-7 Falling Block");
            visual.transform.SetParent(transform, false);
            var sprite = visual.AddComponent<SpriteRenderer>();
            sprite.sprite = tile.sprite;
            if (tilemapRenderer != null)
            {
                sprite.sortingLayerID = tilemapRenderer.sortingLayerID;
                sprite.sortingOrder = tilemapRenderer.sortingOrder + 1;
            }

            var shadow = new GameObject("Stage3-7 Block Shadow");
            shadow.transform.SetParent(transform, false);
            shadow.transform.position = end;
            var shadowSprite = shadow.AddComponent<SpriteRenderer>();
            shadowSprite.sprite = sprite.sprite;
            shadowSprite.sortingLayerID = sprite.sortingLayerID;
            shadowSprite.sortingOrder = sprite.sortingOrder - 1;

            float elapsed = 0f;
            while (elapsed < FallingDuration)
            {
                float t = Mathf.Clamp01(elapsed / FallingDuration);
                visual.transform.position = Vector3.Lerp(start, end, t);
                shadow.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, 1f, t);
                shadowSprite.color = new Color(0f, 0f, 0f, 0.65f * t);
                yield return null;
                elapsed += Time.deltaTime;
            }

            visual.transform.position = end;
            Destroy(shadow);
            PlayImpact();
            elapsed = 0f;
            while (elapsed < ImpactShakeDuration)
            {
                float strength = 1f - Mathf.Clamp01(elapsed / ImpactShakeDuration);
                // One world pixel at PPU 16, settling back onto the cell center.
                float offset = Mathf.Round(Mathf.Sin(elapsed * 100f) * strength) / 16f;
                visual.transform.position = end + Vector3.right * offset;
                yield return null;
                elapsed += Time.deltaTime;
            }
            tilemap.SetTile(cell, tile);
            remainingDrops--;
            Destroy(visual);
        }
    }
}
