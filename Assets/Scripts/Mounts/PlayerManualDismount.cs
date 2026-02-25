using UnityEngine;

[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(PlayerMountCompanion))]
public sealed class PlayerManualDismount : MonoBehaviour
{
    [Header("Input")]
    private readonly PlayerAction dismountAction = PlayerAction.ActionL;

    [Header("Freeze")]
    [SerializeField] private bool freezeEggQueueOnDismount = true;

    MovementController movement;
    PlayerMountCompanion companion;
    PlayerRidingController rider;
    MountEggQueue eggQueue;

    bool wasHeld;

    void Awake()
    {
        movement = GetComponent<MovementController>();
        companion = GetComponent<PlayerMountCompanion>();
        rider = GetComponent<PlayerRidingController>();
        eggQueue = GetComponent<MountEggQueue>();
    }

    void Update()
    {
        if (movement == null || companion == null)
            return;

        if (!movement.CompareTag("Player"))
            return;

        if (movement.isDead || GamePauseController.IsPaused)
            return;

        if (rider != null && rider.IsPlaying)
            return;

        if (!movement.IsMountedOnLouie || !companion.HasMountedLouie())
            return;

        var input = PlayerInputManager.Instance;
        if (input == null)
            return;

        bool held = input.Get(movement.PlayerId, dismountAction);

        if (held && !wasHeld)
            TryManualDismount();

        wasHeld = held;
    }

    void TryManualDismount()
    {
        if (movement == null || companion == null || rider == null)
            return;

        if (!movement.IsMountedOnLouie || !companion.HasMountedLouie())
            return;

        Vector2 facingAtPress = movement.FacingDirection;
        if (facingAtPress == Vector2.zero)
            facingAtPress = Vector2.down;

        if (!companion.TryDetachMountedLouieToWorldStationary(out var detachedLouie, out var detachedType))
            return;

        if (detachedLouie != null)
        {
            if (!detachedLouie.TryGetComponent<MountWorldPickup>(out var pickup))
                pickup = detachedLouie.AddComponent<MountWorldPickup>();

            pickup.Init(detachedType);

            ClearDetachedLouieInvulnerability(detachedLouie);
        }

        bool started = rider.TryPlayRiding(
            facingAtPress,
            onComplete: () =>
            {
                StartDetachedLouieInactivityLoop(detachedLouie);
            },
            onStart: () =>
            {
                if (eggQueue != null)
                    eggQueue.SetIgnoreOwnerInvulnerability(true);

                if (!freezeEggQueueOnDismount)
                {
                    if (eggQueue != null)
                        eggQueue.SetIgnoreOwnerInvulnerability(false);
                    return;
                }

                if (detachedLouie == null)
                {
                    if (eggQueue != null)
                        eggQueue.SetIgnoreOwnerInvulnerability(false);
                    return;
                }

                if (eggQueue == null || eggQueue.Count <= 0)
                {
                    if (eggQueue != null)
                        eggQueue.SetIgnoreOwnerInvulnerability(false);
                    return;
                }

                Vector3 freezePos = detachedLouie.transform.position;
                freezePos.z = 0f;

                eggQueue.TransferToDetachedLouieAndFreeze(detachedLouie, freezePos);

                if (eggQueue != null)
                    eggQueue.SetIgnoreOwnerInvulnerability(false);
            }
        );

        if (!started)
            return;
    }

    static void StartDetachedLouieInactivityLoop(GameObject detachedLouie)
    {
        if (detachedLouie == null)
            return;

        if (detachedLouie.TryGetComponent<MovementController>(out var mc) && mc != null && mc.isDead)
            return;

        var visual = detachedLouie.GetComponentInChildren<MountVisualController>(true);
        if (visual == null)
            return;

        visual.localOffset = (Vector2)visual.transform.localPosition;

        var selfMovement = detachedLouie.GetComponentInChildren<MovementController>(true);
        if (selfMovement == null || selfMovement.isDead)
            return;

        visual.Bind(selfMovement);
        visual.enabled = true;

        visual.SetInactivityEmote(true);
    }

    static void ClearDetachedLouieInvulnerability(GameObject detachedLouie)
    {
        if (detachedLouie == null)
            return;

        if (detachedLouie.TryGetComponent<CharacterHealth>(out var louieHealth))
            louieHealth.StopInvulnerability();

        var move = detachedLouie.GetComponent<MovementController>();
        if (move != null)
            move.SetExplosionInvulnerable(false);
    }
}
