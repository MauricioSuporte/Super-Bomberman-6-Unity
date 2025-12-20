using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MovementController))]
public class PlayerLouieCompanion : MonoBehaviour
{
    [Header("Prefab (Louie Root)")]
    public GameObject louiePrefab;

    [Header("Local Offset")]
    public Vector2 localOffset = new(0f, -0.15f);

    [Header("Louie Death")]
    public float louieDeathSeconds = 0.5f;

    [Header("Player Invulnerability After Losing Louie")]
    public float playerInvulnerabilityAfterLoseLouieSeconds = 0.8f;

    private MovementController movement;
    private GameObject currentLouie;

    private void Awake()
    {
        movement = GetComponent<MovementController>();
    }

    public void MountLouie()
    {
        if (louiePrefab == null || movement == null)
            return;

        if (currentLouie != null)
            return;

        currentLouie = Instantiate(louiePrefab, transform);
        currentLouie.transform.SetLocalPositionAndRotation(localOffset, Quaternion.identity);
        currentLouie.transform.localScale = Vector3.one;

        LouieMovementController louieMove = null;

        if (currentLouie.TryGetComponent<LouieMovementController>(out var lm))
            louieMove = lm;

        if (louieMove != null)
            louieMove.BindOwner(movement, localOffset);

        if (louieMove != null)
            louieMove.enabled = false;

        if (currentLouie.TryGetComponent<Rigidbody2D>(out var rb))
            rb.simulated = false;

        if (currentLouie.TryGetComponent<Collider2D>(out var col))
            col.enabled = false;

        if (currentLouie.TryGetComponent<BombController>(out var bc))
            bc.enabled = false;

        movement.SetMountedOnLouie(true);

        if (currentLouie.TryGetComponent<LouieRiderVisual>(out var visual))
        {
            visual.localOffset = localOffset;
            visual.Bind(movement);
        }

        movement.Died += OnPlayerDied;
    }

    public void LoseLouie()
    {
        if (currentLouie == null)
            return;

        var louie = currentLouie;
        currentLouie = null;

        louie.transform.GetPositionAndRotation(out Vector3 worldPos, out Quaternion worldRot);
        if (movement != null)
        {
            movement.SetMountedOnLouie(false);

            if (movement.TryGetComponent<CharacterHealth>(out var health))
                health.StartTemporaryInvulnerability(playerInvulnerabilityAfterLoseLouieSeconds);
        }

        louie.transform.SetParent(null, true);
        louie.transform.SetPositionAndRotation(worldPos, worldRot);

        if (louie.TryGetComponent<LouieRiderVisual>(out var riderVisual))
            Destroy(riderVisual);

        if (louie.TryGetComponent<LouieMovementController>(out var louieMovement))
        {
            louieMovement.enabled = true;
            louieMovement.Kill();
        }
        else if (louie.TryGetComponent<MovementController>(out var mc))
        {
            mc.enabled = true;
            mc.Kill();
        }
        else
        {
            Destroy(louie);
        }
    }

    public void UnmountLouie()
    {
        if (currentLouie != null)
            Destroy(currentLouie);

        currentLouie = null;

        if (movement != null)
            movement.SetMountedOnLouie(false);
    }

    private void OnPlayerDied(MovementController _)
    {
        UnmountLouie();
    }

    private void OnDestroy()
    {
        if (movement != null)
            movement.Died -= OnPlayerDied;
    }
}
