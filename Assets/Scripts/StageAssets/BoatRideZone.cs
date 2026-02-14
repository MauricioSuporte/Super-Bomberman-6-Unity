using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class BoatRideZone : MonoBehaviour
{
    [Header("Boat Visuals (children)")]
    [SerializeField] private AnimatedSpriteRenderer down;
    [SerializeField] private AnimatedSpriteRenderer up;
    [SerializeField] private AnimatedSpriteRenderer left;
    [SerializeField] private AnimatedSpriteRenderer right;

    [Header("Mount Settings")]
    [SerializeField] private bool hidePlayerWhileRiding = true;

    [Header("Water Pass (by Tag)")]
    [SerializeField] private bool allowPassOnWaterWhileRiding = true;
    [SerializeField] private string waterTag = "Water";

    [Header("Follow")]
    [SerializeField] private Vector2 followOffset = Vector2.zero;

    [Header("Mount/Unmount Safety")]
    [SerializeField, Min(0f)] private float remountBlockSeconds = 0.15f;

    [Header("Anchor (Mount Zone Restriction)")]
    [SerializeField, Min(0.001f)] private float anchorCheckDistance = 0.25f;
    private BoxCollider2D boatCollider;

    [Header("Auto Anchor Snap (when idle)")]
    [SerializeField] private bool autoAnchorSnapWhenIdle = true;
    [SerializeField, Min(0.01f)] private float autoAnchorRefreshSeconds = 0.35f;
    [SerializeField, Min(0.01f)] private float autoAnchorSnapDistance = 1.25f;
    private float nextAutoAnchorTime;
    private readonly List<BoatMountZone> cachedAnchorZones = new();

    [Header("HeadOnly (child names)")]
    [SerializeField] private string headOnlyUpName = "HeadOnlyUp";
    [SerializeField] private string headOnlyDownName = "HeadOnlyDown";
    [SerializeField] private string headOnlyLeftName = "HeadOnlyLeft";
    [SerializeField] private string headOnlyRightName = "HeadOnlyRight";

    [Header("HeadOnly Visibility")]
    [SerializeField] private bool hideHead = false;

    [Header("HeadOnly Offsets (local)")]
    [SerializeField] private bool applyHeadOnlyOffsets = true;
    [SerializeField] private Vector2 headOnlyUpOffset = Vector2.zero;
    [SerializeField] private Vector2 headOnlyDownOffset = Vector2.zero;
    [SerializeField] private Vector2 headOnlyLeftOffset = Vector2.zero;
    [SerializeField] private Vector2 headOnlyRightOffset = Vector2.zero;

    private MovementController rider;
    private Rigidbody2D riderRb;
    private float boatZ;

    private readonly List<Collider2D> riderColliders = new();
    private readonly List<Collider2D> waterColliders = new();
    private bool waterIgnoredApplied;

    private AnimatedSpriteRenderer currentVisual;
    private bool currentIdle = true;

    private float nextAllowedMountTime;
    private int lastUnmountFrame = -999;

    private MovementController remountBlockedRider;
    private bool blockRemountUntilExit;

    private AnimatedSpriteRenderer headUp;
    private AnimatedSpriteRenderer headDown;
    private AnimatedSpriteRenderer headLeft;
    private AnimatedSpriteRenderer headRight;
    private AnimatedSpriteRenderer currentHead;

    private readonly List<AnimatedSpriteRenderer> riderAllAnimRenderers = new();
    private readonly Dictionary<AnimatedSpriteRenderer, Vector3> headVisualBaseLocal = new();

    private static readonly HashSet<MovementController> ridersOnBoats = new();
    private static readonly Dictionary<MovementController, BoatRideZone> riderToBoat = new();

    public static bool TryGetBoatForRider(MovementController mc, out BoatRideZone boat)
    {
        boat = null;
        if (mc == null) return false;
        return riderToBoat.TryGetValue(mc, out boat) && boat != null;
    }

    public static bool IsRidingBoat(MovementController mc)
    {
        if (mc == null) return false;
        return ridersOnBoats.Contains(mc);
    }

    private void Awake()
    {
        boatCollider = GetComponent<BoxCollider2D>();
        boatCollider.isTrigger = true;

        boatZ = transform.position.z;

        currentVisual = down;
        ForceOnly(currentVisual);
        ApplyIdleToAll(true);

        RebuildAnchorZonesCache();

        if (autoAnchorSnapWhenIdle)
            TryAutoSnapToNearestAnchorZone();
    }

    private void LateUpdate()
    {
        if (rider == null || riderRb == null)
        {
            if (autoAnchorSnapWhenIdle && Time.time >= nextAutoAnchorTime)
            {
                nextAutoAnchorTime = Time.time + Mathf.Max(0.05f, autoAnchorRefreshSeconds);
                TryAutoSnapToNearestAnchorZone();
            }

            return;
        }

        Vector2 p = riderRb.position + followOffset;
        transform.position = new Vector3(p.x, p.y, boatZ);

        UpdateBoatVisualFromRider();
        UpdateHeadOnlyFromRider();
        ApplyHeadOnlyOffsetsIfEnabled();
    }

    private void OnDisable()
    {
        if (rider != null)
        {
            ridersOnBoats.Remove(rider);
            riderToBoat.Remove(rider);
            ForceUnmount();
        }
    }

    public bool HasRider => rider != null;
    public bool IsRider(MovementController mc) => rider != null && rider == mc;

    private Vector2 GetBoatCenter()
    {
        if (boatCollider != null)
            return (Vector2)boatCollider.bounds.center;

        return (Vector2)transform.position;
    }

    public bool IsAnchoredAt(Vector2 worldPoint, out string reason)
    {
        if (HasRider)
        {
            reason = "HasRider=true";
            return false;
        }

        Vector2 boatCenter = GetBoatCenter();
        float dist = Vector2.Distance(boatCenter, worldPoint);
        bool ok = dist <= anchorCheckDistance;

        reason = ok ? "ok" : "too far";
        return ok;
    }

    public bool IsRemountBlockedFor(MovementController mc)
    {
        if (mc == null) return false;
        return blockRemountUntilExit && remountBlockedRider == mc;
    }

    public void ClearRemountBlock(MovementController mc)
    {
        if (mc == null) return;
        if (!blockRemountUntilExit) return;
        if (remountBlockedRider != mc) return;

        blockRemountUntilExit = false;
        remountBlockedRider = null;
    }

    public bool CanMount(MovementController mc, out string reason)
    {
        if (mc == null) { reason = "mc NULL"; return false; }
        if (HasRider) { reason = "boat already has rider"; return false; }
        if (mc.isDead) { reason = "mc.isDead=true"; return false; }
        if (!mc.CompareTag("Player")) { reason = "mc tag != Player"; return false; }

        if (IsRidingBoat(mc)) { reason = "mc already riding another boat"; return false; }
        if (IsRemountBlockedFor(mc)) { reason = "remount blocked until exit"; return false; }
        if (Time.frameCount == lastUnmountFrame) { reason = "blocked same frame as unmount"; return false; }
        if (Time.time < nextAllowedMountTime) { reason = "blocked by cooldown"; return false; }

        reason = "ok";
        return true;
    }

    public bool TryMount(MovementController mc, out string reason)
    {
        if (!CanMount(mc, out var canReason))
        {
            reason = $"CanMount=false ({canReason})";
            return false;
        }

        rider = mc;

        if (rider != null && rider.TryGetComponent<CharacterHealth>(out var health) && health != null)
            health.SetExternalInvulnerability(true);

        ridersOnBoats.Add(rider);
        riderToBoat[rider] = this;

        rider.SetSuppressInactivityAnimation(true);
        riderRb = mc.Rigidbody;

        boatZ = transform.position.z;

        Vector2 p = riderRb != null ? riderRb.position : (Vector2)rider.transform.position;
        transform.position = new Vector3(p.x + followOffset.x, p.y + followOffset.y, boatZ);

        CacheHeadOnlyFromRider();
        DisableAllPlayerAnimSprites();

        if (hidePlayerWhileRiding)
        {
            rider.SetExternalVisualSuppressed(true);
            rider.SetAllSpritesVisible(false);

            if (!hideHead)
            {
                currentHead = PickHeadRenderer(rider.FacingDirection);
                if (currentHead == null) currentHead = headDown;

                ForceOnlyHead(currentHead);
                SetIdle(currentHead, true);
            }
            else
            {
                ForceOnlyHead(null);
                currentHead = null;
            }
        }

        if (allowPassOnWaterWhileRiding)
        {
            rider.SetPassTaggedObstacles(true, waterTag);
            ApplyIgnoreWater(true);
        }

        currentVisual = down;
        currentIdle = true;
        ForceOnly(currentVisual);
        ApplyIdleToAll(true);

        reason = "ok";
        return true;
    }

    public bool TryUnmount(MovementController mc)
    {
        if (mc == null) return false;
        if (rider == null) return false;
        if (mc != rider) return false;

        ForceUnmount();

        remountBlockedRider = mc;
        blockRemountUntilExit = true;

        lastUnmountFrame = Time.frameCount;
        nextAllowedMountTime = Time.time + remountBlockSeconds;

        return true;
    }

    private void ForceUnmount()
    {
        if (rider == null) return;

        var prevRider = rider;

        if (allowPassOnWaterWhileRiding)
        {
            ApplyIgnoreWater(false);
            prevRider.SetPassTaggedObstacles(false, waterTag);
        }

        ResetHeadOnlyExternalBases();
        DisableAllPlayerAnimSprites();
        currentHead = null;

        if (hidePlayerWhileRiding)
            prevRider.SetExternalVisualSuppressed(false);

        prevRider.SetSuppressInactivityAnimation(false);

        ridersOnBoats.Remove(prevRider);
        riderToBoat.Remove(prevRider);

        if (prevRider != null && prevRider.TryGetComponent<CharacterHealth>(out var health) && health != null)
            health.SetExternalInvulnerability(false);

        riderAllAnimRenderers.Clear();
        riderColliders.Clear();
        waterColliders.Clear();
        headVisualBaseLocal.Clear();

        rider = null;
        riderRb = null;

        currentVisual = down;
        currentIdle = true;
        ForceOnly(currentVisual);
        ApplyIdleToAll(true);
    }

    private void RebuildAnchorZonesCache()
    {
        cachedAnchorZones.Clear();

        var zones = FindObjectsByType<BoatMountZone>(FindObjectsSortMode.None);
        if (zones == null || zones.Length == 0) return;

        for (int i = 0; i < zones.Length; i++)
        {
            var z = zones[i];
            if (z == null) continue;

            if (z.ReferencesBoat(this))
                cachedAnchorZones.Add(z);
        }
    }

    private bool TryAutoSnapToNearestAnchorZone()
    {
        if (HasRider) return false;

        if (cachedAnchorZones.Count == 0)
            RebuildAnchorZonesCache();

        if (cachedAnchorZones.Count == 0)
            return false;

        Vector2 boatCenter = GetBoatCenter();

        BoatMountZone best = null;
        float bestDist = float.MaxValue;
        Vector2 bestCenter = Vector2.zero;

        for (int i = 0; i < cachedAnchorZones.Count; i++)
        {
            var z = cachedAnchorZones[i];
            if (z == null) continue;

            Vector2 c = z.GetZoneCenterWorld();
            float d = Vector2.Distance(boatCenter, c);
            if (d < bestDist)
            {
                bestDist = d;
                best = z;
                bestCenter = c;
            }
        }

        if (best == null)
            return false;

        bool shouldSnap = bestDist <= autoAnchorSnapDistance;
        bool alreadyOk = bestDist <= anchorCheckDistance;

        if (!shouldSnap || alreadyOk)
            return alreadyOk;

        transform.position = new Vector3(bestCenter.x, bestCenter.y, boatZ);
        return true;
    }

    private void UpdateBoatVisualFromRider()
    {
        Vector2 moveDir = rider.Direction;
        Vector2 faceDir = rider.FacingDirection;

        bool isMoving = moveDir != Vector2.zero;
        Vector2 dirToUse = isMoving ? moveDir : faceDir;

        var target = PickBoatRenderer(dirToUse);
        if (target == null) target = down;

        bool wantIdle = !isMoving;

        if (target != currentVisual)
        {
            currentVisual = target;
            ForceOnly(currentVisual);
        }

        if (wantIdle != currentIdle)
        {
            currentIdle = wantIdle;
            ApplyIdleToAll(true);
            SetIdle(currentVisual, currentIdle);
        }
        else
        {
            SetIdle(currentVisual, currentIdle);
        }
    }

    private void CacheHeadOnlyFromRider()
    {
        riderAllAnimRenderers.Clear();
        rider.GetComponentsInChildren(true, riderAllAnimRenderers);

        headUp = FindChildAnimByName(rider.transform, headOnlyUpName);
        headDown = FindChildAnimByName(rider.transform, headOnlyDownName);
        headLeft = FindChildAnimByName(rider.transform, headOnlyLeftName);
        headRight = FindChildAnimByName(rider.transform, headOnlyRightName);

        CacheHeadVisualBase(headUp);
        CacheHeadVisualBase(headDown);
        CacheHeadVisualBase(headLeft);
        CacheHeadVisualBase(headRight);

        ApplyHeadOnlyOffsetsIfEnabled();
    }

    private void CacheHeadVisualBase(AnimatedSpriteRenderer r)
    {
        if (r == null) return;
        if (headVisualBaseLocal.ContainsKey(r)) return;

        var vt = FindVisualTransform(r);
        if (vt == null) return;

        headVisualBaseLocal[r] = vt.localPosition;
    }

    private void ApplyHeadOnlyOffsetsIfEnabled()
    {
        if (!applyHeadOnlyOffsets) return;
        if (rider == null) return;

        ApplyHeadExternalBase(headUp, headOnlyUpOffset);
        ApplyHeadExternalBase(headDown, headOnlyDownOffset);
        ApplyHeadExternalBase(headLeft, headOnlyLeftOffset);
        ApplyHeadExternalBase(headRight, headOnlyRightOffset);
    }

    private void ApplyHeadExternalBase(AnimatedSpriteRenderer r, Vector2 offset)
    {
        if (r == null) return;
        if (!headVisualBaseLocal.TryGetValue(r, out var baseLocal)) return;

        r.SetExternalBaseLocalPosition(baseLocal + (Vector3)offset);
    }

    private void ResetHeadOnlyExternalBases()
    {
        ResetHeadExternalBase(headUp);
        ResetHeadExternalBase(headDown);
        ResetHeadExternalBase(headLeft);
        ResetHeadExternalBase(headRight);
    }

    private void ResetHeadExternalBase(AnimatedSpriteRenderer r)
    {
        if (r == null) return;
        if (!headVisualBaseLocal.TryGetValue(r, out var baseLocal)) return;

        r.SetExternalBaseLocalPosition(baseLocal);
    }

    private static Transform FindVisualTransform(AnimatedSpriteRenderer r)
    {
        if (r == null) return null;

        var srHere = r.GetComponent<SpriteRenderer>();
        if (srHere != null) return srHere.transform;

        var imgHere = r.GetComponent<Image>();
        if (imgHere != null) return imgHere.transform;

        var imgChild = r.GetComponentInChildren<Image>(true);
        if (imgChild != null) return imgChild.transform;

        var srChild = r.GetComponentInChildren<SpriteRenderer>(true);
        if (srChild != null) return srChild.transform;

        return r.transform;
    }

    private static AnimatedSpriteRenderer FindChildAnimByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        var trs = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            if (trs[i] != null && trs[i].name == childName)
                return trs[i].GetComponent<AnimatedSpriteRenderer>();
        }

        return null;
    }

    private void UpdateHeadOnlyFromRider()
    {
        if (hideHead) return;
        if (!hidePlayerWhileRiding) return;
        if (rider == null) return;

        if (headDown == null && headUp == null && headLeft == null && headRight == null)
            return;

        Vector2 moveDir = rider.Direction;
        Vector2 faceDir = rider.FacingDirection;

        bool isMoving = moveDir != Vector2.zero;
        Vector2 dirToUse = isMoving ? moveDir : faceDir;

        var target = PickHeadRenderer(dirToUse);
        if (target == null) target = headDown;

        bool wantIdle = !isMoving;

        if (target != currentHead)
            ForceOnlyHead(target);

        SetIdle(currentHead, wantIdle);
    }

    private AnimatedSpriteRenderer PickHeadRenderer(Vector2 dir)
    {
        if (dir == Vector2.up) return headUp != null ? headUp : headDown;
        if (dir == Vector2.down) return headDown;
        if (dir == Vector2.left) return headLeft != null ? headLeft : headDown;
        if (dir == Vector2.right) return headRight != null ? headRight : headDown;
        return headDown;
    }

    private void DisableAllPlayerAnimSprites()
    {
        for (int i = 0; i < riderAllAnimRenderers.Count; i++)
        {
            var r = riderAllAnimRenderers[i];
            if (r == null) continue;

            r.enabled = false;

            if (r.TryGetComponent(out SpriteRenderer sr) && sr != null)
                sr.enabled = false;
        }
    }

    private void ForceOnlyHead(AnimatedSpriteRenderer target)
    {
        for (int i = 0; i < riderAllAnimRenderers.Count; i++)
        {
            var r = riderAllAnimRenderers[i];
            if (r == null) continue;

            bool enable = (target != null && r == target);
            r.enabled = enable;

            if (r.TryGetComponent(out SpriteRenderer sr) && sr != null)
                sr.enabled = enable;
        }

        currentHead = target;
    }

    private AnimatedSpriteRenderer PickBoatRenderer(Vector2 dir)
    {
        if (dir == Vector2.up) return up != null ? up : down;
        if (dir == Vector2.down) return down;
        if (dir == Vector2.left) return left != null ? left : down;
        if (dir == Vector2.right) return right != null ? right : down;
        return down;
    }

    private void ApplyIgnoreWater(bool ignore)
    {
        if (rider == null) return;

        if (ignore)
        {
            if (waterIgnoredApplied) return;

            riderColliders.Clear();
            waterColliders.Clear();

            rider.GetComponentsInChildren(true, riderColliders);

            var waterGos = GameObject.FindGameObjectsWithTag(waterTag);
            for (int i = 0; i < waterGos.Length; i++)
            {
                if (waterGos[i] == null) continue;

                if (waterGos[i].TryGetComponent<Collider2D>(out var wc)) waterColliders.Add(wc);

                var childColliders = waterGos[i].GetComponentsInChildren<Collider2D>(true);
                for (int c = 0; c < childColliders.Length; c++)
                {
                    var cc = childColliders[c];
                    if (cc != null && !waterColliders.Contains(cc))
                        waterColliders.Add(cc);
                }
            }

            for (int i = 0; i < riderColliders.Count; i++)
            {
                var rc = riderColliders[i];
                if (rc == null) continue;

                for (int j = 0; j < waterColliders.Count; j++)
                {
                    var wc = waterColliders[j];
                    if (wc == null) continue;

                    Physics2D.IgnoreCollision(rc, wc, true);
                }
            }

            waterIgnoredApplied = true;
        }
        else
        {
            if (!waterIgnoredApplied) return;

            for (int i = 0; i < riderColliders.Count; i++)
            {
                var rc = riderColliders[i];
                if (rc == null) continue;

                for (int j = 0; j < waterColliders.Count; j++)
                {
                    var wc = waterColliders[j];
                    if (wc == null) continue;

                    Physics2D.IgnoreCollision(rc, wc, false);
                }
            }

            riderColliders.Clear();
            waterColliders.Clear();
            waterIgnoredApplied = false;
        }
    }

    private void ForceOnly(AnimatedSpriteRenderer target)
    {
        SetEnabled(down, target == down);
        SetEnabled(up, target == up);
        SetEnabled(left, target == left);
        SetEnabled(right, target == right);
    }

    private void ApplyIdleToAll(bool idle)
    {
        SetIdle(down, idle);
        SetIdle(up, idle);
        SetIdle(left, idle);
        SetIdle(right, idle);
    }

    private static void SetEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null) return;
        r.enabled = on;

        if (r.TryGetComponent(out SpriteRenderer sr) && sr != null)
            sr.enabled = on;
    }

    private static void SetIdle(AnimatedSpriteRenderer r, bool idle)
    {
        if (r == null) return;
        r.idle = idle;
        r.RefreshFrame();
    }
}
