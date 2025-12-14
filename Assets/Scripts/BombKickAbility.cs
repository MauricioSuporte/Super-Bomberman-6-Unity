using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BombController))]
public class BombKickAbility : MonoBehaviour
{
    [Header("SFX")]
    public AudioClip kickBombSfx;

    [Header("State")]
    [SerializeField] private bool canKickBombs;

    private AudioSource audioSource;
    private BombController bombController;

    public bool CanKickBombs => canKickBombs;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        bombController = GetComponent<BombController>();
    }

    public void EnableBombKick()
    {
        canKickBombs = true;

        if (CompareTag("Player"))
            PlayerPersistentStats.CanKickBombs = true;
    }

    public void DisableBombKick()
    {
        canKickBombs = false;

        if (CompareTag("Player"))
            PlayerPersistentStats.CanKickBombs = false;
    }

    public bool TryKickBomb(Bomb bomb, Vector2 direction, float tileSize, LayerMask baseObstacleMask)
    {
        if (!canKickBombs)
            return false;

        if (bomb == null || bomb.IsBeingKicked)
            return false;

        var bombCollider = bomb.GetComponent<Collider2D>();
        if (bombCollider == null || bombCollider.isTrigger)
            return false;

        LayerMask bombObstacles = baseObstacleMask | LayerMask.GetMask("Enemy");

        bool kicked = bomb.StartKick(
            direction,
            tileSize,
            bombObstacles,
            bombController != null ? bombController.destructibleTiles : null
        );

        if (!kicked)
            return false;

        if (audioSource != null && kickBombSfx != null)
            audioSource.PlayOneShot(kickBombSfx);

        return true;
    }
}
