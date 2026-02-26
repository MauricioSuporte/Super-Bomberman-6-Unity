using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class ClockStageStunEffect : MonoBehaviour
{
    [SerializeField] private string enemyLayerName = "Enemy";
    [SerializeField, Min(0.01f)] private float durationSeconds = 5f;

    [Header("End Blink")]
    [SerializeField, Min(0f)] private float endBlinkSeconds = 1.2f;
    [SerializeField, Min(0.01f)] private float endBlinkInterval = 0.12f;
    [SerializeField, Range(0.05f, 1f)] private float blinkAlpha = 0.25f;

    private sealed class Target
    {
        public EnemyMovementController move;
        public Rigidbody2D rb;
        public StunReceiver stun;

        public bool stunHadUseAnimated;
        public bool stunHadShake;
        public bool stunHadFreeze;

        public SpriteRenderer[] srs;
        public Color[] colors;

        public bool moveWasEnabled;
    }

    public static void Trigger(float seconds)
    {
        var go = new GameObject("ClockStageStunEffect");
        var fx = go.AddComponent<ClockStageStunEffect>();
        fx.durationSeconds = Mathf.Max(0.01f, seconds);
        fx.Begin();
    }

    private void Begin()
    {
        StartCoroutine(Routine());
    }

    private IEnumerator Routine()
    {
        int enemyLayer = LayerMask.NameToLayer(enemyLayerName);

        var enemies = FindObjectsByType<EnemyMovementController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        var targets = new List<Target>(enemies != null ? enemies.Length : 0);

        if (enemies != null)
        {
            for (int i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                if (e == null) continue;

                if (enemyLayer >= 0 && e.gameObject.layer != enemyLayer)
                    continue;

                var t = new Target();
                t.move = e;
                t.moveWasEnabled = e.enabled;
                t.rb = e.GetComponent<Rigidbody2D>();
                t.stun = e.GetComponent<StunReceiver>();

                t.srs = e.GetComponentsInChildren<SpriteRenderer>(true);
                if (t.srs == null) t.srs = new SpriteRenderer[0];

                t.colors = new Color[t.srs.Length];
                for (int s = 0; s < t.srs.Length; s++)
                    t.colors[s] = t.srs[s] != null ? t.srs[s].color : Color.white;

                if (t.stun != null)
                {
                    t.stunHadUseAnimated = GetPrivateBool(t.stun, "useAnimatedStunRenderer");
                    t.stunHadShake = t.stun.shakeWhileStunned;
                    t.stunHadFreeze = t.stun.freezeAnimatedSprites;

                    SetPrivateBool(t.stun, "useAnimatedStunRenderer", false);
                    t.stun.shakeWhileStunned = false;
                    t.stun.freezeAnimatedSprites = true;

                    t.stun.Stun(durationSeconds);
                }
                else
                {
                    var anims = e.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
                    if (anims != null)
                    {
                        for (int a = 0; a < anims.Length; a++)
                        {
                            var ar = anims[a];
                            if (ar == null) continue;
                            ar.idle = true;
                            ar.RefreshFrame();
                            ar.SetFrozen(true);
                        }
                    }

                    if (t.rb != null)
                        t.rb.linearVelocity = Vector2.zero;

                    if (e.enabled)
                        e.enabled = false;
                }

                targets.Add(t);
            }
        }

        float nonBlink = Mathf.Max(0f, durationSeconds - Mathf.Max(0f, endBlinkSeconds));
        if (nonBlink > 0f)
            yield return new WaitForSeconds(nonBlink);

        float blinkTime = Mathf.Clamp(endBlinkSeconds, 0f, durationSeconds);
        if (blinkTime > 0f)
        {
            float elapsed = 0f;
            bool faded = false;

            while (elapsed < blinkTime)
            {
                faded = !faded;

                for (int i = 0; i < targets.Count; i++)
                {
                    var t = targets[i];
                    if (t == null || t.move == null) continue;

                    for (int s = 0; s < t.srs.Length; s++)
                    {
                        var sr = t.srs[s];
                        if (sr == null) continue;

                        Color c = t.colors[s];
                        sr.color = faded ? new Color(c.r, c.g, c.b, blinkAlpha) : c;
                    }
                }

                float w = Mathf.Max(0.01f, endBlinkInterval);
                yield return new WaitForSeconds(w);
                elapsed += w;
            }
        }

        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t == null) continue;

            for (int s = 0; s < t.srs.Length; s++)
            {
                var sr = t.srs[s];
                if (sr == null) continue;
                sr.color = t.colors[s];
            }

            if (t.stun != null)
            {
                SetPrivateBool(t.stun, "useAnimatedStunRenderer", t.stunHadUseAnimated);
                t.stun.shakeWhileStunned = t.stunHadShake;
                t.stun.freezeAnimatedSprites = t.stunHadFreeze;
            }
            else
            {
                if (t.move != null && t.moveWasEnabled && t.move.gameObject.activeInHierarchy)
                    t.move.enabled = true;

                var anims = t.move != null ? t.move.GetComponentsInChildren<AnimatedSpriteRenderer>(true) : null;
                if (anims != null)
                {
                    for (int a = 0; a < anims.Length; a++)
                    {
                        var ar = anims[a];
                        if (ar == null) continue;
                        ar.SetFrozen(false);
                    }
                }
            }
        }

        Destroy(gameObject);
    }

    private static void SetPrivateBool(object obj, string fieldName, bool value)
    {
        if (obj == null) return;
        var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (f == null || f.FieldType != typeof(bool)) return;
        f.SetValue(obj, value);
    }

    private static bool GetPrivateBool(object obj, string fieldName)
    {
        if (obj == null) return false;
        var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (f == null || f.FieldType != typeof(bool)) return false;
        return (bool)f.GetValue(obj);
    }
}