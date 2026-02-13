using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class RedBoatRideZone : MonoBehaviour
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

    [Header("HeadOnly (child names)")]
    [SerializeField] private string headOnlyUpName = "HeadOnlyUp";
    [SerializeField] private string headOnlyDownName = "HeadOnlyDown";
    [SerializeField] private string headOnlyLeftName = "HeadOnlyLeft";
    [SerializeField] private string headOnlyRightName = "HeadOnlyRight";

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

    private void Awake()
    {
        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;

        boatZ = transform.position.z;

        currentVisual = down;
        ForceOnly(currentVisual);
        ApplyIdleToAll(true);
    }

    private void LateUpdate()
    {
        if (rider == null || riderRb == null)
            return;

        Vector2 p = riderRb.position + followOffset;
        transform.position = new Vector3(p.x, p.y, boatZ);

        UpdateBoatVisualFromRider();
        UpdateHeadOnlyFromRider();
    }

    private void OnDisable()
    {
        if (rider != null)
            ForceUnmount();
    }

    public bool HasRider => rider != null;
    public bool IsRider(MovementController mc) => rider != null && rider == mc;

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

    public bool CanMount(MovementController mc)
    {
        if (mc == null) return false;
        if (HasRider) return false;
        if (mc.isDead) return false;
        if (!mc.CompareTag("Player")) return false;

        if (IsRemountBlockedFor(mc)) return false;

        if (Time.frameCount == lastUnmountFrame) return false;
        if (Time.time < nextAllowedMountTime) return false;

        return true;
    }

    public bool TryMount(MovementController mc)
    {
        if (!CanMount(mc))
            return false;

        rider = mc;
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

            CacheHeadOnlyFromRider();
            currentHead = PickHeadRenderer(rider.FacingDirection);
            if (currentHead == null) currentHead = headDown;

            ForceOnlyHead(currentHead);
            SetIdle(currentHead, true);
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

        if (allowPassOnWaterWhileRiding)
        {
            ApplyIgnoreWater(false);
            rider.SetPassTaggedObstacles(false, waterTag);
        }

        DisableAllPlayerAnimSprites();
        currentHead = null;

        if (hidePlayerWhileRiding)
        {
            rider.SetExternalVisualSuppressed(false);
        }

        riderAllAnimRenderers.Clear();
        riderColliders.Clear();
        waterColliders.Clear();

        rider = null;
        riderRb = null;

        currentVisual = down;
        currentIdle = true;
        ForceOnly(currentVisual);
        ApplyIdleToAll(true);
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
        {
            ForceOnlyHead(target);
        }

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
        if (target == null)
            return;

        for (int i = 0; i < riderAllAnimRenderers.Count; i++)
        {
            var r = riderAllAnimRenderers[i];
            if (r == null) continue;

            bool enable = (r == target);
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
