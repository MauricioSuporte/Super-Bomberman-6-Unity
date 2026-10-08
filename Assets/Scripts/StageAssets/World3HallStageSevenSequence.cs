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
        private const float FallingDuration = 0.5f;

        [SerializeField] private Tilemap indestructibleTilemap;
        [SerializeField] private Tile indestructibleTile;
        [SerializeField] private Tilemap destructibleTilemap;
        [SerializeField] private Tile destructibleTile;
        [SerializeField] private GameObject hallExit;
        [SerializeField] private AudioClip stageMusic;
        [SerializeField, Range(0f, 1f)] private float stageMusicVolume = 1f;
        [SerializeField] private AudioClip tileDropSfx;

        private bool started;
        private bool playersMovementFinished;
        private static readonly HashSet<Vector3Int> DestructibleCells = new()
        {
            new(-5, -5, 0), new(-3, -5, 0), new(1, -5, 0), new(3, -5, 0),
            new(-6, -4, 0), new(4, -4, 0), new(-3, -3, 0), new(3, -3, 0),
            new(-6, 0, 0), new(4, 0, 0), new(-5, 1, 0), new(-3, 1, 0),
            new(1, 1, 0), new(3, 1, 0)
        };
        private int remainingDrops;
        private double nextImpactTime;
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
            destructibleTile.sprite != null && hallExit != null && stageMusic != null && tileDropSfx != null;

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
            RestoreDancingPlayers(completed: true, enableGameplay: true);

            // Gameplay starts immediately; the final impact sounds can finish.
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
                Vector2.right * 2f, Vector2.left, Vector2.zero
            };
            float elapsed = 0f;
            while (elapsed < SequenceDuration)
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
                    if (elapsed >= SequenceDuration - pauseDuration)
                        player.Movement.ForceIdleFacing(Vector2.down);
                    else
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
            RestoreDancingPlayers();
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
            double startTime = System.Math.Max(now, nextImpactTime);
            // Keep the first landing immediate; stagger simultaneous landings
            // by 15-30 ms without changing their individual playback volume.
            nextImpactTime = startTime + Random.Range(0.015f, 0.03f);
            impactVoices.Add(new ImpactVoice
            {
                Source = source,
                EndTime = startTime + tileDropSfx.length
            });
            if (startTime <= now)
                source.Play();
            else
                source.PlayScheduled(startTime);
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

            tilemap.SetTile(cell, tile);
            PlayImpact();
            remainingDrops--;
            Destroy(visual);
            Destroy(shadow);
        }
    }
}
