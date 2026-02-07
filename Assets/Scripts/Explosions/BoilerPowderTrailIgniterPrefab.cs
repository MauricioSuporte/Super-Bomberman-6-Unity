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
        [SerializeField] private List<PowderTileEntry> orderedTiles = new List<PowderTileEntry>();

        [SerializeField, Min(0.01f)] private float flameDurationSeconds = 0.5f;
        [SerializeField, Min(0f)] private float stepDelaySeconds = 0.06f;

        [SerializeField, Min(0.1f)] private float restartEverySeconds = 5f;
        [SerializeField] private bool validateTileExistsOnGround = true;

        private Coroutine routine;

        private void Awake()
        {
            ResolveGroundTilemapIfNeeded();
        }

        private void OnDisable()
        {
            StopAll();
        }

        public void IgniteSequence()
        {
            ResolveGroundTilemapIfNeeded();

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
                yield return IgniteOnceRoutine();
                yield return new WaitForSeconds(waitBetween);
            }
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
        }
    }
}
