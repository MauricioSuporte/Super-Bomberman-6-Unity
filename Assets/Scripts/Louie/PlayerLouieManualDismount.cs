using UnityEngine;

[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(PlayerLouieCompanion))]
public sealed class PlayerLouieManualDismount : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private PlayerAction dismountAction = PlayerAction.ActionR;

    [Header("Freeze")]
    [SerializeField] private bool freezeEggQueueOnDismount = true;

    MovementController movement;
    PlayerLouieCompanion companion;
    PlayerRidingController rider;
    LouieEggQueue eggQueue;

    bool wasHeld;

    void Awake()
    {
        movement = GetComponent<MovementController>();
        companion = GetComponent<PlayerLouieCompanion>();
        rider = GetComponent<PlayerRidingController>();
        eggQueue = GetComponent<LouieEggQueue>();
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
            var pickup = detachedLouie.GetComponent<LouieWorldPickup>();
            if (pickup == null)
                pickup = detachedLouie.AddComponent<LouieWorldPickup>();

            pickup.Init(detachedType);
        }

        bool started = rider.TryPlayRiding(
            facingAtPress,
            onComplete: null,
            onStart: () =>
            {
                // IMPORTANT: no início do desmontar, os ovos param de seguir o player
                // e ficam "presos" no Louie destacado (para serem adotados por quem montar depois).
                if (!freezeEggQueueOnDismount)
                    return;

                if (detachedLouie == null)
                    return;

                if (eggQueue == null || eggQueue.Count <= 0)
                    return;

                Vector3 freezePos = detachedLouie.transform.position;
                freezePos.z = 0f;

                eggQueue.TransferToDetachedLouieAndFreeze(detachedLouie, freezePos);
            }
        );

        if (!started)
            return;
    }
}
