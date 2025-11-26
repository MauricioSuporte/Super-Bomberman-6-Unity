using UnityEngine;

public class Destructible : MonoBehaviour
{
    public float destructionTime = 0.5f;

    private void Start()
    {
        Destroy(gameObject, destructionTime);
    }
}
