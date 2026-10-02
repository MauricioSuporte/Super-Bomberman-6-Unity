using UnityEngine;

public sealed class CrasherWreckage : MonoBehaviour
{
    private Vector2 velocity;
    private float elapsed;

    public void Initialize(Vector2 flightVelocity)
    {
        velocity = flightVelocity;
    }

    private void Update()
    {
        if (GamePauseController.IsPaused) return;
        float delta = Time.deltaTime;
        transform.position += (Vector3)(velocity * delta);
        elapsed += delta;
        if (elapsed >= 0.5f) Destroy(gameObject);
    }
}
