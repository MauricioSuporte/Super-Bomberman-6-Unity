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

        Vector3 freezeWorldPos = movement.Rigidbody != null
            ? (Vector3)movement.Rigidbody.position
            : movement.transform.position;

        freezeWorldPos.z = 0f;

        if (freezeEggQueueOnDismount && eggQueue != null)
            eggQueue.BeginHardFreeze();

        if (!companion.TryDetachMountedLouieToWorldStationary(out var detachedLouie))
        {
            if (freezeEggQueueOnDismount && eggQueue != null)
                eggQueue.EndHardFreezeAndRebind(movement);

            return;
        }

        if (detachedLouie != null)
        {
            detachedLouie.transform.position = freezeWorldPos;
            detachedLouie.transform.rotation = Quaternion.identity;

            detachedLouie.SendMessage("ForceIdleFacing", facingAtPress, SendMessageOptions.DontRequireReceiver);
            detachedLouie.SendMessage("ApplyMoveDelta", Vector3.zero, SendMessageOptions.DontRequireReceiver);

            ForceAllAnimatedRenderersIdle(detachedLouie);
        }

        bool started = rider.TryPlayRiding(
            facingAtPress,
            onComplete: () =>
            {
                if (freezeEggQueueOnDismount && eggQueue != null)
                    eggQueue.EndHardFreezeAndKeepWorld(freezeWorldPos);
            },
            onStart: null
        );

        if (!started)
        {
            if (freezeEggQueueOnDismount && eggQueue != null)
                eggQueue.EndHardFreezeAndKeepWorld(freezeWorldPos);
        }
    }

    static void ForceAllAnimatedRenderersIdle(GameObject root)
    {
        if (root == null)
            return;

        var anims = root.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            if (a == null)
                continue;

            a.idle = true;
            a.CurrentFrame = 0;
            a.RefreshFrame();
        }
    }
}
