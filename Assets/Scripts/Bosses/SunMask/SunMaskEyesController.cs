using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SunMaskEyesController : MonoBehaviour
{
    private enum EyeDir
    {
        Noroeste,
        Nordeste,
        Sudeste,
        Sudoeste,
        Oeste,
        Leste,
        Norte,
        Sul
    }

    private enum EyeSet
    {
        Normal,
        Damaged
    }

    private enum EyeOverride
    {
        None,
        Damaged,
        Front,
        Track
    }

    [Header("Eye Roots (children of SunMask)")]
    [SerializeField] private Transform rightEyeRoot; // "RigthEye"
    [SerializeField] private Transform leftEyeRoot;  // "LeftEye"

    [Header("Targeting")]
    [SerializeField] private bool autoFindPlayers = true;
    [SerializeField, Min(0f)] private float retargetInterval = 0.15f;

    [Header("Boss State / Events")]
    [SerializeField] private SunMaskBoss boss;

    [Tooltip("Se true, troca automaticamente pro conjunto Damaged enquanto o boss está no hurtRenderer.")]
    [SerializeField] private bool useDamagedEyesWhenBossHurt = true;

    [Header("Direction")]
    [Tooltip("Se true, prioriza eixo dominante (8-direções estilo grid). Se false, usa ângulos (setores).")]
    [SerializeField] private bool useDominantAxisFor8Dir = true;

    [Tooltip("Deadzone do eixo (evita flicker quando quase diagonal). Recomendado 0.15~0.30.")]
    [SerializeField, Range(0f, 0.49f)] private float diagonalBias = 0.22f;

    private readonly List<MovementController> _players = new(8);
    private MovementController _currentTarget;
    private float _nextRetargetTime;

    // direções "normais"
    private readonly Dictionary<EyeDir, AnimatedSpriteRenderer> _rightNormal = new(8);
    private readonly Dictionary<EyeDir, AnimatedSpriteRenderer> _leftNormal = new(8);

    // sprites especiais por olho (single)
    private AnimatedSpriteRenderer _rightDamagedSingle;
    private AnimatedSpriteRenderer _leftDamagedSingle;
    private AnimatedSpriteRenderer _rightFrontSingle;
    private AnimatedSpriteRenderer _leftFrontSingle;

    private EyeDir _currentDir = EyeDir.Noroeste;
    private bool _hasDir;

    private EyeSet _currentSet = EyeSet.Normal;
    private bool _hasSet;

    private EyeOverride _override = EyeOverride.None;
    private Coroutine _deathEyesRoutine;

    private void Awake()
    {
        if (boss == null)
            boss = GetComponentInParent<SunMaskBoss>();

        AutoResolveEyeRootsIfNeeded();

        CacheEyeChildren(rightEyeRoot, _rightNormal, out _rightDamagedSingle, out _rightFrontSingle);
        CacheEyeChildren(leftEyeRoot, _leftNormal, out _leftDamagedSingle, out _leftFrontSingle);

        _currentSet = EyeSet.Normal;
        _hasSet = true;

        ApplyEyeSet(_currentSet, force: true);
        ApplyDirection(_currentDir, force: true);

        if (autoFindPlayers)
            RefreshPlayers();
    }

    private void OnEnable()
    {
        _nextRetargetTime = 0f;
        _currentTarget = null;

        _hasDir = false;
        _currentSet = EyeSet.Normal;
        _hasSet = false;

        _override = EyeOverride.None;

        if (autoFindPlayers)
            RefreshPlayers();
    }

    private void OnDisable()
    {
        if (_deathEyesRoutine != null)
        {
            StopCoroutine(_deathEyesRoutine);
            _deathEyesRoutine = null;
        }

        _currentTarget = null;
        _players.Clear();
    }

    private void LateUpdate()
    {
        if (boss != null && !boss.isActiveAndEnabled)
            return;

        // 0) Override (fluxo de morte / comandos externos)
        if (_override != EyeOverride.None)
        {
            if (_override == EyeOverride.Track)
            {
                TickTracking();
            }

            return;
        }

        // 1) Damaged automático durante hurt (modo normal)
        if (useDamagedEyesWhenBossHurt)
        {
            bool bossHurtActive = boss != null && boss.hurtRenderer != null && boss.hurtRenderer.enabled;
            ApplyEyeSet(bossHurtActive ? EyeSet.Damaged : EyeSet.Normal, force: false);
        }

        // Se estiver em Damaged (auto), não mira (é sprite fixo)
        if (_currentSet == EyeSet.Damaged)
            return;

        // tracking normal
        TickTracking();
    }

    private void TickTracking()
    {
        if (autoFindPlayers && (retargetInterval <= 0f || Time.time >= _nextRetargetTime))
        {
            _nextRetargetTime = Time.time + Mathf.Max(0f, retargetInterval);
            EnsurePlayersRefsIfNeeded();
            _currentTarget = FindClosestAlivePlayer();
        }

        if (_currentTarget == null)
            return;

        Vector2 myPos = transform.position;
        Vector2 targetPos = _currentTarget.transform.position;
        Vector2 toTarget = targetPos - myPos;

        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        EyeDir dir = Get8Dir(toTarget);
        ApplyDirection(dir, force: false);
    }

    // ======= PUBLIC API: fluxo de morte dos olhos =======

    public void BeginDeathEyesFlow(float damagedDuration = 1f, float frontDuration = 0.75f)
    {
        if (_deathEyesRoutine != null)
        {
            StopCoroutine(_deathEyesRoutine);
            _deathEyesRoutine = null;
        }

        _deathEyesRoutine = StartCoroutine(DeathEyesRoutine(damagedDuration, frontDuration));
    }

    private IEnumerator DeathEyesRoutine(float damagedDuration, float frontDuration)
    {
        // 1) Damaged por 1s (ou configurável)
        SetOverride(EyeOverride.Damaged);
        float t1 = Mathf.Max(0f, damagedDuration);
        if (t1 > 0f) yield return new WaitForSeconds(t1);

        // 2) Front por 0.75s (ou configurável)
        SetOverride(EyeOverride.Front);
        float t2 = Mathf.Max(0f, frontDuration);
        if (t2 > 0f) yield return new WaitForSeconds(t2);

        // 3) Volta a seguir player, mesmo com boss mantendo sprite Damaged
        SetOverride(EyeOverride.Track);

        _deathEyesRoutine = null;
    }

    private void SetOverride(EyeOverride ov)
    {
        _override = ov;

        switch (ov)
        {
            case EyeOverride.Damaged:
                ApplyFront(false);
                ApplyDamaged(true);
                // garante que normais fiquem off
                DisableAll(_rightNormal);
                DisableAll(_leftNormal);
                break;

            case EyeOverride.Front:
                ApplyDamaged(false);
                ApplyFront(true);
                DisableAll(_rightNormal);
                DisableAll(_leftNormal);
                break;

            case EyeOverride.Track:
                ApplyDamaged(false);
                ApplyFront(false);
                // força conjunto normal e reaplica direção
                _currentSet = EyeSet.Normal;
                _hasSet = true;
                ApplyDirection(_currentDir, force: true);
                break;

            default:
                ApplyDamaged(false);
                ApplyFront(false);
                break;
        }
    }

    private void ApplyDamaged(bool on)
    {
        SetRendererEnabled(_rightDamagedSingle, on, refresh: on);
        SetRendererEnabled(_leftDamagedSingle, on, refresh: on);
    }

    private void ApplyFront(bool on)
    {
        SetRendererEnabled(_rightFrontSingle, on, refresh: on);
        SetRendererEnabled(_leftFrontSingle, on, refresh: on);
    }

    // ======= Core switching =======

    private void ApplyEyeSet(EyeSet set, bool force)
    {
        if (!force && _hasSet && set == _currentSet)
            return;

        _currentSet = set;
        _hasSet = true;

        if (_currentSet == EyeSet.Damaged)
        {
            ApplyFront(false);

            ApplyDamaged(true);
            DisableAll(_rightNormal);
            DisableAll(_leftNormal);
            return;
        }

        ApplyDamaged(false);
        ApplyFront(false);

        ApplyDirection(_currentDir, force: true);
    }

    private void ApplyDirection(EyeDir dir, bool force)
    {
        if (!force && _hasDir && dir == _currentDir)
            return;

        _currentDir = dir;
        _hasDir = true;

        if (_currentSet == EyeSet.Damaged)
            return;

        EnableOnly(_rightNormal, dir);
        EnableOnly(_leftNormal, dir);
    }

    private static void SetRendererEnabled(AnimatedSpriteRenderer r, bool on, bool refresh)
    {
        if (r == null) return;

        if (!r.gameObject.activeSelf)
            r.gameObject.SetActive(true);

        r.enabled = on;

        if (r.TryGetComponent<SpriteRenderer>(out var sr))
            sr.enabled = on;

        if (on && refresh)
            r.RefreshFrame();
    }

    private static void DisableAll(Dictionary<EyeDir, AnimatedSpriteRenderer> map)
    {
        if (map == null || map.Count == 0)
            return;

        foreach (var kv in map)
        {
            var r = kv.Value;
            if (r == null) continue;

            r.enabled = false;
            if (r.TryGetComponent<SpriteRenderer>(out var sr))
                sr.enabled = false;
        }
    }

    private static void EnableOnly(Dictionary<EyeDir, AnimatedSpriteRenderer> map, EyeDir dir)
    {
        if (map == null || map.Count == 0)
            return;

        AnimatedSpriteRenderer keep = null;

        if (!map.TryGetValue(dir, out keep) || keep == null)
        {
            if (dir == EyeDir.Noroeste || dir == EyeDir.Nordeste)
                map.TryGetValue(EyeDir.Norte, out keep);
            else if (dir == EyeDir.Sudoeste || dir == EyeDir.Sudeste)
                map.TryGetValue(EyeDir.Sul, out keep);
            else if (dir == EyeDir.Oeste)
                map.TryGetValue(EyeDir.Oeste, out keep);
            else if (dir == EyeDir.Leste)
                map.TryGetValue(EyeDir.Leste, out keep);

            if (keep == null)
            {
                foreach (var kv in map)
                {
                    if (kv.Value != null) { keep = kv.Value; break; }
                }
            }
        }

        foreach (var kv in map)
        {
            var r = kv.Value;
            if (r == null) continue;

            bool on = (r == keep);

            if (!r.gameObject.activeSelf)
                r.gameObject.SetActive(true);

            r.enabled = on;

            if (r.TryGetComponent<SpriteRenderer>(out var sr))
                sr.enabled = on;

            if (on)
                r.RefreshFrame();
        }
    }

    // ======= Direction math =======

    private EyeDir Get8Dir(Vector2 v)
    {
        v.Normalize();

        if (useDominantAxisFor8Dir)
        {
            float ax = Mathf.Abs(v.x);
            float ay = Mathf.Abs(v.y);

            bool diagonal = Mathf.Abs(ax - ay) <= diagonalBias;

            if (diagonal)
            {
                if (v.x >= 0f && v.y >= 0f) return EyeDir.Nordeste;
                if (v.x < 0f && v.y >= 0f) return EyeDir.Noroeste;
                if (v.x >= 0f && v.y < 0f) return EyeDir.Sudeste;
                return EyeDir.Sudoeste;
            }

            if (ax > ay)
                return v.x >= 0f ? EyeDir.Leste : EyeDir.Oeste;

            return v.y >= 0f ? EyeDir.Norte : EyeDir.Sul;
        }

        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        if (InSector(angle, 337.5f, 22.5f)) return EyeDir.Leste;
        if (InSector(angle, 22.5f, 67.5f)) return EyeDir.Nordeste;
        if (InSector(angle, 67.5f, 112.5f)) return EyeDir.Norte;
        if (InSector(angle, 112.5f, 157.5f)) return EyeDir.Noroeste;
        if (InSector(angle, 157.5f, 202.5f)) return EyeDir.Oeste;
        if (InSector(angle, 202.5f, 247.5f)) return EyeDir.Sudoeste;
        if (InSector(angle, 247.5f, 292.5f)) return EyeDir.Sul;
        return EyeDir.Sudeste;
    }

    private static bool InSector(float angle, float minInclusive, float maxExclusive)
    {
        if (minInclusive <= maxExclusive)
            return angle >= minInclusive && angle < maxExclusive;

        return angle >= minInclusive || angle < maxExclusive;
    }

    // ======= Resolve / Cache =======

    private void AutoResolveEyeRootsIfNeeded()
    {
        if (rightEyeRoot != null && leftEyeRoot != null)
            return;

        var trs = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            var t = trs[i];
            if (t == null) continue;

            string n = t.name.ToLowerInvariant();

            if (rightEyeRoot == null && (n == "rigtheye" || n == "righteye" || n.Contains("rigtheye")))
                rightEyeRoot = t;

            if (leftEyeRoot == null && (n == "lefteye" || n.Contains("lefteye")))
                leftEyeRoot = t;

            if (rightEyeRoot != null && leftEyeRoot != null)
                break;
        }
    }

    private static void CacheEyeChildren(
        Transform root,
        Dictionary<EyeDir, AnimatedSpriteRenderer> normalMap,
        out AnimatedSpriteRenderer damagedSingle,
        out AnimatedSpriteRenderer frontSingle)
    {
        normalMap.Clear();
        damagedSingle = null;
        frontSingle = null;

        if (root == null)
            return;

        var trs = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < trs.Length; i++)
        {
            var t = trs[i];
            if (t == null) continue;
            if (t == root) continue;

            var r = t.GetComponent<AnimatedSpriteRenderer>();
            if (r == null) continue;

            if (t.name.Equals("Damaged", System.StringComparison.OrdinalIgnoreCase))
            {
                damagedSingle = r;
                SetRendererEnabled(damagedSingle, false, refresh: false);
                continue;
            }

            if (t.name.Equals("Front", System.StringComparison.OrdinalIgnoreCase) ||
                t.name.Equals("Frente", System.StringComparison.OrdinalIgnoreCase))
            {
                frontSingle = r;
                SetRendererEnabled(frontSingle, false, refresh: false);
                continue;
            }

            if (TryParseDir(t.name, out var d))
            {
                normalMap[d] = r;
                SetRendererEnabled(r, false, refresh: false);
            }
        }
    }

    private static bool TryParseDir(string name, out EyeDir dir)
    {
        dir = EyeDir.Noroeste;

        if (string.IsNullOrEmpty(name))
            return false;

        string n = name.Trim().ToLowerInvariant();

        if (n == "noroeste" || n == "nw") { dir = EyeDir.Noroeste; return true; }
        if (n == "nordeste" || n == "ne") { dir = EyeDir.Nordeste; return true; }
        if (n == "sudeste" || n == "se") { dir = EyeDir.Sudeste; return true; }
        if (n == "sudoeste" || n == "sw") { dir = EyeDir.Sudoeste; return true; }
        if (n == "oeste" || n == "w" || n == "west") { dir = EyeDir.Oeste; return true; }
        if (n == "leste" || n == "e" || n == "east") { dir = EyeDir.Leste; return true; }
        if (n == "norte" || n == "n" || n == "north") { dir = EyeDir.Norte; return true; }
        if (n == "sul" || n == "s" || n == "south") { dir = EyeDir.Sul; return true; }

        return false;
    }

    // ======= Players =======

    private void EnsurePlayersRefsIfNeeded()
    {
        for (int i = _players.Count - 1; i >= 0; i--)
        {
            if (_players[i] == null)
                _players.RemoveAt(i);
        }

        if (_players.Count == 0)
            RefreshPlayers();
    }

    public void RefreshPlayers()
    {
        _players.Clear();

        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (ids != null && ids.Length > 0)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                if (id == null) continue;

                MovementController m = null;
                if (!id.TryGetComponent(out m))
                    m = id.GetComponentInChildren<MovementController>(true);

                if (m == null) continue;
                if (!m.CompareTag("Player")) continue;

                _players.Add(m);
            }

            return;
        }

        var go = GameObject.FindGameObjectsWithTag("Player");
        if (go == null || go.Length == 0)
            return;

        for (int i = 0; i < go.Length; i++)
        {
            if (go[i] == null) continue;

            var m = go[i].GetComponent<MovementController>();
            if (m == null) continue;

            _players.Add(m);
        }
    }

    private MovementController FindClosestAlivePlayer()
    {
        if (_players.Count == 0)
            return null;

        Vector2 myPos = transform.position;

        MovementController best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < _players.Count; i++)
        {
            var p = _players[i];
            if (p == null) continue;
            if (p.isDead) continue;
            if (p.IsEndingStage) continue;

            Vector2 pp = p.transform.position;
            float d = (pp - myPos).sqrMagnitude;

            if (d < bestSqr)
            {
                bestSqr = d;
                best = p;
            }
        }

        return best;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (diagonalBias < 0f) diagonalBias = 0f;
        if (diagonalBias > 0.49f) diagonalBias = 0.49f;
    }
#endif
}