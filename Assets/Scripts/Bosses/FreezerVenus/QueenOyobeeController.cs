using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FreezerVenusBoss))]
public sealed class QueenOyobeeController : MonoBehaviour
{
    public Sprite transformationSprite;
    public Sprite[] idleFrames;
    public Sprite[] hurtFrames;
    public Sprite[] crownlessFrames;
    public Sprite[] crownFrames;
    public Sprite[] snowFrames;
    public Sprite[] snowDestructionFrames;
    public Sprite[] scepterFrames;
    public Sprite shadowSprite;
    public AudioClip iceCreateSfx;
    [Min(0f)] public float iceCreateSfxGain = 2f;
    public AudioClip crownThrowSfx;
    [Min(0f)] public float crownThrowSfxGain = 2f;
    public AudioClip iceThrowSfx;
    [Min(0f)] public float iceThrowSfxGain = 2f;
    public AudioClip scepterShakeSfx;
    [Min(0f)] public float scepterShakeSfxGain = 2f;
    public AudioClip music;
    [Range(0f, 1f)] public float musicVolume = 0.4f;
    private FreezerVenusBoss boss;
    private SpriteRenderer scepter;
    private SpriteRenderer wornCrown;
    private GameObject telegraph;
    private QueenOyobeeIceWave iceWave;
    private readonly List<QueenOyobeeProjectile> projectiles = new();
    private float floatTime;
    private bool iceThrowSoundPlayed;
    public const float IceScepterSeconds = 0.52f;
    private static readonly int[] IceScepterSequence = { 0, 1, 0, 3, 3, 4, 4, 4, 4, 3 };
    public float FloatOffset => Mathf.Round(Mathf.Sin(floatTime * Mathf.PI * 2f / 1.6f) * 2f) / 16f;
    public void AdvanceFloat(float seconds) => floatTime += seconds;
    public bool Active { get; private set; }
    public Sprite[] CurrentIdleFrames => wornCrown != null && !wornCrown.enabled ? crownlessFrames : idleFrames;
    public FreezerVenusBoss Boss => boss;
    public Vector3 CrownOrigin => boss.transform.position + Vector3.up * 3.125f;

    private void Awake() => boss = GetComponent<FreezerVenusBoss>();

    public IEnumerator TransformRoutine()
    {
        boss.body.color = Color.white;
        boss.body.sprite = transformationSprite;
        if (GameMusicController.Instance != null && music != null)
            GameMusicController.Instance.PlayMusic(music, musicVolume, loop: true);
        // Preserve native pixels while the compressed Venus pose flashes into the queen.
        float elapsed = 0f;
        telegraph = QueenOyobeeProjectile.CreateRitual(this);
        while (elapsed < 2.2f)
        {
            yield return null;
            if (GamePauseController.IsPaused) continue;
            elapsed += Time.deltaTime;
            boss.body.color = Color.Lerp(Color.white, new Color(0.5f, 0.85f, 1f), Mathf.PingPong(elapsed * 2f, 1f));
            boss.body.enabled = Mathf.FloorToInt(elapsed / Mathf.Lerp(0.18f, 0.045f, elapsed / 2.2f)) % 2 == 0;
        }
        if (telegraph != null) Destroy(telegraph);
        boss.body.enabled = true;
        boss.body.color = Color.white;
        Active = true;
        if (boss.shadow != null && shadowSprite != null)
        {
            boss.shadow.sprite = shadowSprite;
            boss.shadow.color = Color.white;
            boss.shadow.transform.localScale = Vector3.one;
        }
        boss.SetFrame(idleFrames, 0);
        var go = new GameObject("Queen Oyobee scepter");
        go.transform.SetParent(boss.body.transform, false);
        go.transform.localPosition = new Vector3(1.375f, 0.25f, 0f);
        scepter = go.AddComponent<SpriteRenderer>();
        scepter.sortingLayerID = boss.body.sortingLayerID;
        scepter.sortingOrder = boss.body.sortingOrder + 1;
        scepter.sprite = scepterFrames[0];
        var crownObject = new GameObject("Queen Oyobee worn crown");
        crownObject.transform.SetParent(boss.body.transform, false);
        crownObject.transform.localPosition = Vector3.up * 3.125f;
        wornCrown = crownObject.AddComponent<SpriteRenderer>();
        wornCrown.sprite = crownFrames[0];
        wornCrown.sortingLayerID = boss.body.sortingLayerID;
        wornCrown.sortingOrder = boss.body.sortingOrder + 1;
        iceWave = QueenOyobeeIceWave.Create(this);
        while (iceWave != null) yield return null;
        var snowfall = FindAnyObjectByType<StageAssets.Stage35SnowfallEffect>();
        if (snowfall == null)
            snowfall = new GameObject("Queen Oyobee stage snowfall").AddComponent<StageAssets.Stage35SnowfallEffect>();
        snowfall.Configure(240, 1);
    }

    public IEnumerator AttackRoutine(int index)
    {
        bool ice = index % 3 == 0;
        Vector2[] path = null;
        float crownFlightSeconds = 0f;
        if (!ice)
        {
            var target = boss.FindTarget(CrownOrigin);
            path = QueenOyobeeProjectile.BuildCrownPath(this, target != null ? (Vector2)target.transform.position : (Vector2)boss.GroundPosition, index);
            crownFlightSeconds = QueenOyobeeProjectile.CrownFlightSeconds(path);
            path = QueenOyobeeProjectile.ExpandCrownPath(boss.Arena, path);
            telegraph = QueenOyobeeProjectile.CreateTelegraph(this, path);
        }
        if (ice)
        {
            yield return PlayIceScepterShake();
            // Complete the casting frame before any orbiting snow becomes visible.
            yield return null;
            while (GamePauseController.IsPaused) yield return null;
        }
        else
        {
            // Preserve the crown preparation: two vertical lifts.
            for (int repeat = 0; repeat < 2; repeat++)
            {
                // Each lift starts at the beginning or midpoint of the preparation.
                PlayScepterShakeSound();
                for (int frame = 0; frame < 4; frame++)
                {
                    if (scepter != null)
                    {
                        scepter.sprite = scepterFrames[frame == 1 ? 1 : 0];
                        scepter.transform.localRotation = Quaternion.identity;
                        scepter.transform.localPosition = new Vector3(1.125f,
                            0.25f + (frame == 1 ? 0.5f : 0f), 0f);
                    }
                    yield return FreezerVenusBoss.WaitUnpaused(0.09f);
                }
            }
        }
        if (scepter != null)
        {
            scepter.sprite = scepterFrames[0];
            scepter.flipX = false;
            scepter.flipY = false;
            scepter.transform.localRotation = Quaternion.identity;
            scepter.transform.localPosition = new Vector3(1.375f, 0.25f, 0f);
        }
        projectiles.RemoveAll(p => p == null);
        if (ice)
        {
            iceThrowSoundPlayed = false;
            var destinations = QueenOyobeeProjectile.GroundSnowDestinations(boss.Arena, boss.GroundPosition);
            for (int i = 0; i < 6; i++)
            {
                if (i < destinations.Count)
                    projectiles.Add(QueenOyobeeProjectile.CreateGroundSnow(this, i, destinations[i]));
                projectiles.Add(QueenOyobeeProjectile.Create(this, false, i));
            }
            boss.PlayAttackSfx(iceCreateSfx, iceCreateSfxGain);
            yield return FreezerVenusBoss.WaitUnpaused(0.3f);
        }
        else
        {
            if (wornCrown != null) wornCrown.enabled = false;
            boss.SetFrame(crownlessFrames, 0);
            var crown = QueenOyobeeProjectile.Create(this, true, 0, path, crownFlightSeconds);
            projectiles.Add(crown);
            boss.PlayAttackSfx(crownThrowSfx, crownThrowSfxGain);
            while (crown != null) yield return null;
            if (telegraph != null) Destroy(telegraph);
            if (wornCrown != null) wornCrown.enabled = true;
            boss.SetFrame(idleFrames, 0);
        }
        yield return FreezerVenusBoss.WaitUnpaused(0.18f);
    }

    public void PlayIceThrowOnce()
    {
        if (iceThrowSoundPlayed) return;
        iceThrowSoundPlayed = true;
        boss.PlayAttackSfx(iceThrowSfx, iceThrowSfxGain);
    }

    public static void IceScepterPose(int step, out int frame, out bool flipX, out bool flipY)
    {
        step = Mathf.Clamp(step, 0, IceScepterSequence.Length - 1);
        frame = IceScepterSequence[step];
        flipX = step == 4 || step == 6 || step == 7 || step == 9;
        flipY = step == 4 || step == 9;
    }

    private void PlayScepterShakeSound() => boss.PlayAttackSfx(scepterShakeSfx, scepterShakeSfxGain);

    private IEnumerator PlayIceScepterShake()
    {
        float elapsed = 0f;
        bool secondSoundPlayed = false;
        PlayScepterShakeSound();
        // One clock keeps ten poses inside the original eight-frame preparation duration.
        while (elapsed < IceScepterSeconds)
        {
            int step = Mathf.Min(9, Mathf.FloorToInt(elapsed * 10f / IceScepterSeconds));
            IceScepterPose(step, out int frame, out bool flipX, out bool flipY);
            if (scepter != null)
            {
                scepter.sprite = scepterFrames[frame];
                scepter.flipX = flipX;
                scepter.flipY = flipY;
                scepter.transform.localRotation = Quaternion.identity;
                scepter.transform.localPosition = new Vector3(1.125f, 0.25f, 0f);
            }
            // Sound timing is shared by both attacks: start and halfway through.
            if (elapsed >= IceScepterSeconds * 0.5f && !secondSoundPlayed)
            {
                PlayScepterShakeSound();
                secondSoundPlayed = true;
            }
            yield return null;
            if (!GamePauseController.IsPaused) elapsed += Time.deltaTime;
        }
    }

    public void ClearProjectiles()
    {
        if (telegraph != null) Destroy(telegraph);
        if (iceWave != null) Destroy(iceWave.gameObject);
        foreach (var projectile in projectiles)
            if (projectile != null) Destroy(projectile.gameObject);
        projectiles.Clear();
        if (scepter != null) scepter.enabled = false;
        if (wornCrown != null) wornCrown.enabled = true;
    }

    public void SyncVisibility()
    {
        if (boss.shadow != null && shadowSprite != null)
        {
            boss.shadow.transform.position = boss.GroundPosition;
            boss.shadow.enabled = boss.body.enabled;
        }
        if (scepter != null) scepter.enabled = boss.CombatActive && boss.body.enabled;
        if (wornCrown != null) wornCrown.gameObject.SetActive(boss.body.enabled);
    }

    private void LateUpdate()
    {
        if (Active) SyncVisibility();
    }

    private void OnDisable() => ClearProjectiles();
}
