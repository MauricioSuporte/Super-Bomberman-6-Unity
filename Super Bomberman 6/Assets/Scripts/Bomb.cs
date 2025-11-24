using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Bomb : MonoBehaviour
{
    private BombController owner;
    public bool HasExploded { get; private set; }
    public AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void MarkAsExploded()
    {
        HasExploded = true;
    }

    public void Initialize(BombController owner)
    {
        this.owner = owner;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (HasExploded)
            return;

        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            if (owner != null)
            {
                owner.ExplodeBomb(gameObject);
            }
        }
    }
}
