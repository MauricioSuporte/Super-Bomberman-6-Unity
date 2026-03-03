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
        HoldSul,
        IntroHold,
        IntroSpin
    }

    [Header("Eye Roots (children of SunMask)")]
    [SerializeField] private Transform rightEyeRoot;
    [SerializeField] private Transform leftEyeRoot;

    [Header("Targeting")]
    [SerializeField] private bool autoFindPlayers = true;
    [SerializeField, Min(0f)] private float retargetInterval = 0.15f;

    [Header("Boss State / Events")]
    [SerializeField] private SunMaskBoss boss;

    [Tooltip("Se true, troca automaticamente pro conjunto Damaged enquanto o boss está no hurtRenderer.")]
    [SerializeField] private bool useDamagedEyesWhenBossHurt = true;

    [Header("Direction")]
    [SerializeField] private bool useDominantAxisFor8Dir = true;
    [SerializeField, Range(0f, 0.49f)] private float diagonalBias = 0.22f;

    private readonly List<MovementController> _players = new(8);
    private MovementController _currentTarget;
    private float _nextRetargetTime;

    private readonly Dictionary<EyeDir, AnimatedSpriteRenderer> _rightNormal = new(8);
    private readonly Dictionary<EyeDir, AnimatedSpriteRenderer> _leftNormal = new(8);

    private AnimatedSpriteRenderer _rightDamagedSingle;
    private AnimatedSpriteRenderer _leftDamagedSingle;

    private EyeDir _currentDir = EyeDir.Noroeste;
    private bool _hasDir;

    private EyeSet _currentSet = EyeSet.Normal;
    private bool _hasSet;

    private EyeOverride _override = EyeOverride.None;
    private Coroutine _deathEyesRoutine;
    private Coroutine _introEyesRoutine;

    private Vector3 _rightEyeBaseLocalPos;
    private Vector3 _leftEyeBaseLocalPos;
    private bool _hasEyeBasePos;
    private bool _angryOffsetApplied;

    private void Awake()
    {
        if (boss == null)
            boss = GetComponentInParent<SunMaskBoss>();

        AutoResolveEyeRootsIfNeeded();

        if (rightEyeRoot != null) _rightEyeBaseLocalPos = rightEyeRoot.localPosition;
        if (leftEyeRoot != null) _leftEyeBaseLocalPos = leftEyeRoot.localPosition;
        _hasEyeBasePos = (rightEyeRoot != null || leftEyeRoot != null);
        _angryOffsetApplied = false;

        CacheEyeChildren(rightEyeRoot, _rightNormal, out _rightDamagedSingle);
        CacheEyeChildren(leftEyeRoot, _leftNormal, out _leftDamagedSingle);

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

        if (_introEyesRoutine != null)
        {
            StopCoroutine(_introEyesRoutine);
            _introEyesRoutine = null;
        }

        _angryOffsetApplied = false;
        RestoreEyeRootsBaseLocalPos();

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

        if (_introEyesRoutine != null)
        {
            StopCoroutine(_introEyesRoutine);
            _introEyesRoutine = null;
        }

        RestoreEyeRootsBaseLocalPos();

        _currentTarget = null;
        _players.Clear();
    }

    private void LateUpdate()
    {
        if (boss != null && !boss.isActiveAndEnabled)
            return;

        UpdateAngryEyeOffset();

        if (_override == EyeOverride.Damaged || _override == EyeOverride.HoldSul || _override == EyeOverride.IntroHold || _override == EyeOverride.IntroSpin)
            return;

        if (useDamagedEyesWhenBossHurt)
        {
            bool bossHurtActive = boss != null && boss.hurtRenderer != null && boss.hurtRenderer.enabled;
            ApplyEyeSet(bossHurtActive ? EyeSet.Damaged : EyeSet.Normal, force: false);
        }

        if (_currentSet == EyeSet.Damaged)
            return;

        TickTracking();
    }

    private void UpdateAngryEyeOffset()
    {
        if (boss == null || !_hasEyeBasePos)
        {
            _angryOffsetApplied = false;
            return;
        }

        bool shouldApply = boss.IsAngryRendererActive;

        if (shouldApply == _angryOffsetApplied)
            return;

        _angryOffsetApplied = shouldApply;

        if (shouldApply)
        {
            float y = boss.AngryEyesYOffset;
            if (rightEyeRoot != null) rightEyeRoot.localPosition = _rightEyeBaseLocalPos + new Vector3(0f, y, 0f);
            if (leftEyeRoot != null) leftEyeRoot.localPosition = _leftEyeBaseLocalPos + new Vector3(0f, y, 0f);
        }
        else
        {
            RestoreEyeRootsBaseLocalPos();
        }
    }

    private void RestoreEyeRootsBaseLocalPos()
    {
        if (!_hasEyeBasePos)
            return;

        if (rightEyeRoot != null) rightEyeRoot.localPosition = _rightEyeBaseLocalPos;
        if (leftEyeRoot != null) leftEyeRoot.localPosition = _leftEyeBaseLocalPos;
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

    // =========================
    // INTRO CONTROLS
    // =========================

    public void BeginIntroHoldDown()
    {
        if (_override == EyeOverride.IntroHold || _override == EyeOverride.IntroSpin)
            return;

        if (_introEyesRoutine != null)
        {
            StopCoroutine(_introEyesRoutine);
            _introEyesRoutine = null;
        }

        _override = EyeOverride.IntroHold;

        ApplyEyeSet(EyeSet.Normal, force: true);
        ApplyDirection(EyeDir.Sul, force: true);

        _currentDir = EyeDir.Sul;
        _hasDir = true;

        _currentTarget = null;
    }

    public void ClearOverrideIfIntro()
    {
        if (_override != EyeOverride.IntroHold && _override != EyeOverride.IntroSpin)
            return;

        if (_introEyesRoutine != null)
        {
            StopCoroutine(_introEyesRoutine);
            _introEyesRoutine = null;
        }

        _override = EyeOverride.None;
        _currentTarget = null;
        _nextRetargetTime = 0f;
    }

    public IEnumerator PlayIntroSpinCounterClockwise(float totalDuration)
    {
        if (totalDuration <= 0f)
        {
            ClearOverrideIfIntro();
            yield break;
        }

        if (_introEyesRoutine != null)
        {
            StopCoroutine(_introEyesRoutine);
            _introEyesRoutine = null;
        }

        _introEyesRoutine = StartCoroutine(IntroSpinCCWRoutine(totalDuration));
        yield return _introEyesRoutine;
        _introEyesRoutine = null;
    }

    private IEnumerator IntroSpinCCWRoutine(float totalDuration)
    {
        _override = EyeOverride.IntroSpin;

        ApplyEyeSet(EyeSet.Normal, force: true);
        ApplyDirection(EyeDir.Sul, force: true);

        EyeDir[] seq =
        {
            EyeDir.Sul,
            EyeDir.Sudeste,
            EyeDir.Leste,
            EyeDir.Nordeste,
            EyeDir.Norte,
            EyeDir.Noroeste,
            EyeDir.Oeste,
            EyeDir.Sudoeste,
            EyeDir.Sul
        };

        int steps = seq.Length;
        float stepDur = totalDuration / Mathf.Max(1, steps);
        stepDur = Mathf.Max(0.001f, stepDur);

        for (int i = 0; i < seq.Length; i++)
        {
            ApplyDirection(seq[i], force: true);
            _currentDir = seq[i];
            _hasDir = true;

            float t = 0f;
            while (t < stepDur)
            {
                if (GamePauseController.IsPaused)
                {
                    yield return null;
                    continue;
                }

                t += Time.deltaTime;
                yield return null;
            }
        }

        ClearOverrideIfIntro();
    }

    // =========================
    // DEATH FLOW (existing)
    // =========================

    public void BeginDeathEyesFlowDamagedThenSul(float damagedDuration = 1f)
    {
        if (_deathEyesRoutine != null)
        {
            StopCoroutine(_deathEyesRoutine);
            _deathEyesRoutine = null;
        }

        if (_introEyesRoutine != null)
        {
            StopCoroutine(_introEyesRoutine);
            _introEyesRoutine = null;
        }

        _deathEyesRoutine = StartCoroutine(DeathEyesRoutine_DamagedThenSul(damagedDuration));
    }

    private IEnumerator DeathEyesRoutine_DamagedThenSul(float damagedDuration)
    {
        SetOverrideDamaged();

        float t = Mathf.Max(0f, damagedDuration);
        if (t > 0f)
            yield return new WaitForSeconds(t);

        SetOverrideHoldSul();

        _deathEyesRoutine = null;
    }

    private void SetOverrideDamaged()
    {
        _override = EyeOverride.Damaged;
        ApplyEyeSet(EyeSet.Damaged, force: true);
    }

    private void SetOverrideHoldSul()
    {
        _override = EyeOverride.HoldSul;

        ApplyEyeSet(EyeSet.Normal, force: true);
        ApplyDirection(EyeDir.Sul, force: true);

        _currentDir = EyeDir.Sul;
        _hasDir = true;

        _currentTarget = null;
    }

    private void ApplyEyeSet(EyeSet set, bool force)
    {
        if (!force && _hasSet && set == _currentSet)
            return;

        _currentSet = set;
        _hasSet = true;

        if (_currentSet == EyeSet.Damaged)
        {
            SetRendererEnabled(_rightDamagedSingle, true, refresh: true);
            SetRendererEnabled(_leftDamagedSingle, true, refresh: true);

            DisableAll(_rightNormal);
            DisableAll(_leftNormal);
            return;
        }

        SetRendererEnabled(_rightDamagedSingle, false, refresh: false);
        SetRendererEnabled(_leftDamagedSingle, false, refresh: false);

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
        out AnimatedSpriteRenderer damagedSingle)
    {
        normalMap.Clear();
        damagedSingle = null;

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