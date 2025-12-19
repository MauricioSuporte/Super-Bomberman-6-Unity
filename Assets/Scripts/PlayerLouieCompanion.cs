using UnityEngine;

[RequireComponent(typeof(MovementController))]
public class PlayerLouieCompanion : MonoBehaviour
{
    [Header("Prefab (Louie Root)")]
    public GameObject louiePrefab;

    [Header("Local Offset")]
    public Vector2 localOffset = new(0f, -0.15f);

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

        if (currentLouie.TryGetComponent<LouieMovementController>(out var louieMove))
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
