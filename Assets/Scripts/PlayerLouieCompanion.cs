using UnityEngine;

public class PlayerLouieCompanion : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject louiePrefab;

    [Header("Follow Offset")]
    public Vector2 offset = new Vector2(-0.35f, -0.15f);

    private MovementController playerMovement;
    private GameObject currentLouie;

    private void Awake()
    {
        playerMovement = GetComponent<MovementController>();
    }

    public void SpawnOrRefreshLouie()
    {
        if (louiePrefab == null || playerMovement == null)
            return;

        // se já tem, destrói e recria (ou você pode só manter)
        if (currentLouie != null)
            Destroy(currentLouie);

        Vector2 pos = playerMovement.Rigidbody != null
            ? playerMovement.Rigidbody.position + offset
            : (Vector2)transform.position + offset;

        currentLouie = Instantiate(louiePrefab, pos, Quaternion.identity);

        var louieMove = currentLouie.GetComponent<LouieMovementController>();
        if (louieMove != null)
            louieMove.BindOwner(playerMovement, offset);

        // se o player morrer, some com o Louie
        playerMovement.Died += HandlePlayerDied;
    }

    private void HandlePlayerDied(MovementController _)
    {
        if (currentLouie != null)
            Destroy(currentLouie);
    }

    private void OnDestroy()
    {
        if (playerMovement != null)
            playerMovement.Died -= HandlePlayerDied;
    }
}
