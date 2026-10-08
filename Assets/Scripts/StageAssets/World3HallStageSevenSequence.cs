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
        [SerializeField] private GameObject hallExit;
        [SerializeField] private AudioClip stageMusic;
        [SerializeField, Range(0f, 1f)] private float stageMusicVolume = 1f;
        [SerializeField] private AudioClip tileDropSfx;

        private bool started;
        private int remainingDrops;
        private double nextImpactTime;
        private readonly List<ImpactVoice> impactVoices = new();

        private struct ImpactVoice
        {
            public AudioSource Source;
            public double EndTime;
        }

        public bool CanPlay => indestructibleTilemap != null && indestructibleTile != null &&
            indestructibleTile.sprite != null && hallExit != null && stageMusic != null && tileDropSfx != null;

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

            var cells = new List<Vector3Int>();
            for (int y = 3; y <= 4; y++)
                for (int x = -7; x <= 5; x++)
                    cells.Add(new Vector3Int(x, y, 0));
            for (int y = -5; y <= 1; y += 2)
                for (int x = -6; x <= 4; x += 2)
                    cells.Add(new Vector3Int(x, y, 0));

            // Equal x+y cells fall together: sweep from bottom-left to top-right.
            cells.Sort((a, b) =>
            {
                int diagonal = (a.x + a.y).CompareTo(b.x + b.y);
                return diagonal != 0 ? diagonal : a.x.CompareTo(b.x);
            });

            int firstDiagonal = cells[0].x + cells[0].y;
            remainingDrops = cells.Count;
            int lastDiagonal = cells[cells.Count - 1].x + cells[cells.Count - 1].y;
            var renderer = indestructibleTilemap.GetComponent<TilemapRenderer>();
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
                    StartCoroutine(DropTile(cell, renderer));
                    next++;
                }
                yield return null;
                elapsed += Time.deltaTime;
            }

            // Let every overlapping impact finish, including the last tile's tail.
            while (remainingDrops > 0 || impactVoices.Count > 0)
                yield return null;
        }

        private void Update()
        {
            CleanupImpactVoices();
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

        private IEnumerator DropTile(Vector3Int cell, TilemapRenderer tilemapRenderer)
        {
            Vector3 end = indestructibleTilemap.GetCellCenterWorld(cell);
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
            sprite.sprite = indestructibleTile.sprite;
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

            indestructibleTilemap.SetTile(cell, indestructibleTile);
            PlayImpact();
            remainingDrops--;
            Destroy(visual);
            Destroy(shadow);
        }
    }
}
