using UnityEngine;

public class Destructible : MonoBehaviour
{
    public float destructionTime = 0.5f;

    private void Start()
    {
        Destroy(gameObject, destructionTime);
    }

    private void OnDestroy()
    {
        var gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null)
            return;

        var prefab = gameManager.GetSpawnForDestroyedBlock();
        if (prefab != null)
        {
            Instantiate(prefab, transform.position, Quaternion.identity);
        }
    }
}
