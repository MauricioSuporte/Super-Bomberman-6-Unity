using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Scripts.Explosions
{
    public sealed class BoilerPowderTrailIgniterPrefab : MonoBehaviour
    {
        public enum PowderTileKind
        {
            Horizontal,
            Vertical,
            CurveDownLeft,
            CurveDownRight,
            CurveUpLeft,
            CurveUpRight
        }

        [Serializable]
        public struct PowderTileEntry
        {
            public int x;
            public int y;
            public PowderTileKind kind;
            public readonly Vector3Int Cell => new(x, y, 0);
        }

        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private PowderIgniterFlame powderIgniterPrefab;
        [SerializeField] private List<PowderTileEntry> orderedTiles = new();

        [SerializeField, Min(0.01f)] private float flameDurationSeconds = 0.5f;
        [SerializeField, Min(0f)] private float stepDelaySeconds = 0.06f;

        [SerializeField, Min(0.1f)] private float restartEverySeconds = 5f;
        [SerializeField] private bool validateTileExistsOnGround = true;

        [Header("Pre Ignite SFX")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip preIgniteClip;
        [SerializeField, Min(0f)] private float preIgniteLeadSeconds = 1f;
        [SerializeField, Range(0f, 1f)] private float preIgniteVolume = 1f;

        [Header("Boiler Steam (child / prefab)")]
        [SerializeField] private bool enableSteam = true;
        [SerializeField] private int steamTileX;
        [SerializeField] private int steamTileY;
        [SerializeField, Min(0.01f)] private float steamDurationSeconds = 2.5f;
        [SerializeField] private bool steamUseGroundTilemapZ = false;
        [SerializeField] private bool steamUsePrefabLocalOffset = true;
        [SerializeField] private bool steamEnforcePositionWhileEnabled = true;

        private AnimatedSpriteRenderer steamRenderer;
        private GameObject steamInstance;
        private Transform steamRoot;
        private Vector3 steamPrefabLocalOffset;
        private bool hasSteamPrefabLocalOffset;

        private Coroutine routine;
        private Coroutine steamRoutine;

        private bool hasExpectedSteamPos;
        private Vector3 expectedSteamWorldPos;

        private void Awake()
        {
            ResolveGroundTilemapIfNeeded();
            ResolveSfxSourceIfNeeded();
            ResolveSteamRendererIfNeeded();
            SetSteamEnabled(false);
        }

        private void LateUpdate()
        {
            if (!steamEnforcePositionWhileEnabled)
                return;

            if (!hasExpectedSteamPos)
                return;

            if (steamRoot == null || steamRenderer == null)
                return;

            if (!steamRenderer.enabled)
                return;

            ApplySteamWorldPos(expectedSteamWorldPos);
        }

        private void OnDisable()
        {
            StopAll();
        }

        public void IgniteSequence()
        {
            ResolveGroundTilemapIfNeeded();
            ResolveSfxSourceIfNeeded();
            ResolveSteamRendererIfNeeded();

            if (!ValidateSetup())
                return;

            StopAll();
            routine = StartCoroutine(LoopRoutine());
        }

        public void StopIgniteSequence()
        {
            StopAll();
        }

        private IEnumerator LoopRoutine()
        {
            float waitBetween = Mathf.Max(0.1f, restartEverySeconds);

            while (true)
            {
                yield return PreIgniteRoutine();
                yield return IgniteOnceRoutine();
                yield return new WaitForSeconds(waitBetween);
            }
        }

        private IEnumerator PreIgniteRoutine()
        {
            float lead = Mathf.Max(0f, preIgniteLeadSeconds);

            if (preIgniteClip != null)
            {
                ResolveSfxSourceIfNeeded();
                if (sfxSource != null)
                    sfxSource.PlayOneShot(preIgniteClip, Mathf.Clamp01(preIgniteVolume));
            }

            StartSteamForBoiler();

            if (lead > 0f)
                yield return new WaitForSeconds(lead);
        }

        private void StartSteamForBoiler()
        {
            hasExpectedSteamPos = false;

            if (!enableSteam)
                return;

            if (groundTilemap == null)
                return;

            ResolveSteamRendererIfNeeded();
            if (steamRenderer == null || steamRoot == null)
                return;

            expectedSteamWorldPos = GetExpectedSteamWorldPos();
            hasExpectedSteamPos = true;

            ApplySteamWorldPos(expectedSteamWorldPos);

            if (steamRoutine != null)
            {
                StopCoroutine(steamRoutine);
                steamRoutine = null;
            }

            steamRoutine = StartCoroutine(SteamRoutine(expectedSteamWorldPos));
        }

        private IEnumerator SteamRoutine(Vector3 expected)
        {
            SetSteamEnabled(true);
            ApplySteamWorldPos(expected);

            yield return null;

            ApplySteamWorldPos(expected);

            float d = Mathf.Max(0.01f, steamDurationSeconds);
            yield return new WaitForSeconds(d);

            SetSteamEnabled(false);
            steamRoutine = null;
        }

        private void SetSteamEnabled(bool enabled)
        {
            if (steamRenderer == null)
                return;

            steamRenderer.enabled = enabled;

            if (enabled)
                steamRenderer.RefreshFrame();
        }

        private void ApplySteamWorldPos(Vector3 worldPos)
        {
            if (steamRoot == null)
                return;

            steamRoot.position = worldPos;
        }

        private IEnumerator IgniteOnceRoutine(int startIndex = 0)
        {
            int count = orderedTiles != null ? orderedTiles.Count : 0;
            if (count <= 0)
                yield break;

            float stepDelay = Mathf.Max(0f, stepDelaySeconds);
            float flameDur = Mathf.Max(0.01f, flameDurationSeconds);

            for (int i = startIndex; i < count; i++)
            {
                if (groundTilemap == null || powderIgniterPrefab == null)
                    yield break;

                var e = orderedTiles[i];
                Vector3Int cell = e.Cell;

                if (validateTileExistsOnGround)
                {
                    var t = groundTilemap.GetTile(cell);
                    if (t == null)
                    {
                        if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);
                        else yield return null;
                        continue;
                    }
                }

                Vector3 pos = groundTilemap.GetCellCenterWorld(cell);
                pos.z = 0f;

                var inst = Instantiate(powderIgniterPrefab, pos, Quaternion.identity);
                if (inst != null)
                    inst.Play(ToFlameKind(e.kind), flameDur);

                if (stepDelay > 0f)
                    yield return new WaitForSeconds(stepDelay);
                else
                    yield return null;
            }
        }

        private PowderIgniterFlame.FlameKind ToFlameKind(PowderTileKind kind)
        {
            return kind switch
            {
                PowderTileKind.Horizontal => PowderIgniterFlame.FlameKind.Horizontal,
                PowderTileKind.Vertical => PowderIgniterFlame.FlameKind.Vertical,
                PowderTileKind.CurveDownLeft => PowderIgniterFlame.FlameKind.CurveDownLeft,
                PowderTileKind.CurveDownRight => PowderIgniterFlame.FlameKind.CurveDownRight,
                PowderTileKind.CurveUpLeft => PowderIgniterFlame.FlameKind.CurveUpLeft,
                PowderTileKind.CurveUpRight => PowderIgniterFlame.FlameKind.CurveUpRight,
                _ => PowderIgniterFlame.FlameKind.Horizontal,
            };
        }

        private void ResolveGroundTilemapIfNeeded()
        {
            if (groundTilemap != null)
                return;

            var tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            if (tilemaps == null || tilemaps.Length == 0)
                return;

            for (int i = 0; i < tilemaps.Length; i++)
            {
                var tm = tilemaps[i];
                if (tm == null)
                    continue;

                string n = tm.name.ToLowerInvariant();
                if (n.Contains("ground"))
                {
                    groundTilemap = tm;
                    return;
                }
            }

            groundTilemap = tilemaps[0];
        }

        private void ResolveSfxSourceIfNeeded()
        {
            if (sfxSource != null)
                return;

            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }
        }

        private void ResolveSteamRendererIfNeeded()
        {
            if (steamRenderer != null && steamRoot != null)
                return;

            var t = transform.Find("Steam");
            if (t != null)
            {
                steamRenderer = t.GetComponent<AnimatedSpriteRenderer>();
                steamRoot = t;
                steamPrefabLocalOffset = t.localPosition;
                hasSteamPrefabLocalOffset = true;
                return;
            }

            if (powderIgniterPrefab == null)
                return;

            Transform prefabSteam = powderIgniterPrefab.transform.Find("Steam");
            if (prefabSteam == null)
                return;

            var prefabSteamRenderer = prefabSteam.GetComponent<AnimatedSpriteRenderer>();
            if (prefabSteamRenderer == null)
                return;

            steamPrefabLocalOffset = prefabSteam.localPosition;
            hasSteamPrefabLocalOffset = true;

            if (steamInstance == null)
            {
                steamInstance = Instantiate(prefabSteam.gameObject);
                steamInstance.name = "Steam(Runtime)";
                steamInstance.transform.SetParent(transform, worldPositionStays: true);
            }

            steamRenderer = steamInstance.GetComponent<AnimatedSpriteRenderer>();
            steamRoot = steamRenderer != null ? steamRenderer.transform : steamInstance.transform;

            if (steamRenderer != null)
                steamRenderer.enabled = false;
        }

        private bool ValidateSetup()
        {
            if (groundTilemap == null)
                return false;

            if (powderIgniterPrefab == null)
                return false;

            if (orderedTiles == null || orderedTiles.Count == 0)
                return false;

            return true;
        }

        private void StopAll()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            if (steamRoutine != null)
            {
                StopCoroutine(steamRoutine);
                steamRoutine = null;
            }

            SetSteamEnabled(false);
            hasExpectedSteamPos = false;
        }

        private Vector3 GetExpectedSteamWorldPos()
        {
            if (groundTilemap == null)
                return Vector3.zero;

            Vector3Int cell = new(steamTileX, steamTileY, 0);
            Vector3 cellCenter = groundTilemap.GetCellCenterWorld(cell);

            float z = steamUseGroundTilemapZ ? cellCenter.z : 0f;
            cellCenter.z = z;

            Vector3 expected = cellCenter;

            if (steamUsePrefabLocalOffset && hasSteamPrefabLocalOffset)
                expected += steamPrefabLocalOffset;

            return expected;
        }
    }
}
