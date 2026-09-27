using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController), typeof(BombController))]
public sealed class LineBombAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "LineBomb";
    [SerializeField] private bool enabledAbility;
    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;
    public int SuccessfulCastVersion { get; private set; }

    public void Enable()
    {
        enabledAbility = true;
    }

    public void Disable()
    {
        enabledAbility = false;
    }

    // Called by BombController before normal ActionA placement, never in a second Update.
    public bool TryUseOnExistingBomb()
    {
        if (!enabledAbility)
            return false;

        var movement = GetComponent<MovementController>();
        var bomb = GetComponent<BombController>();
        if (movement.isDead || movement.InputLocked || movement.IsEndingStage ||
            movement.IsRidingPlaying() || GamePauseController.IsPaused)
            return false;

        Vector2 origin = movement.Rigidbody != null
            ? movement.Rigidbody.position : (Vector2)transform.position;
        origin.x = Mathf.Round(origin.x / movement.tileSize) * movement.tileSize;
        origin.y = Mathf.Round(origin.y / movement.tileSize) * movement.tileSize;
        var hit = Physics2D.OverlapBox(origin, Vector2.one * (movement.tileSize * 0.6f),
            0f, LayerMask.GetMask("Bomb"));
        if (hit == null || !hit.TryGetComponent<Bomb>(out _) ||
            hit.GetComponent<BoilerCapturedBomb>() != null)
        {
            return false;
        }

        Vector2 dir = movement.FacingDirection;
        if (dir == Vector2.zero) dir = Vector2.down;
        dir = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y)
            ? (dir.x >= 0f ? Vector2.right : Vector2.left)
            : (dir.y >= 0f ? Vector2.up : Vector2.down);
        int placed = PlaceLine(movement, bomb, dir, trackOwnerBombTraversal: false);
        if (placed > 0) SuccessfulCastVersion++;
        if (placed > 0 && bomb.playerAudioSource != null && bomb.placeBombSfx != null)
            GameAudioSettings.PlaySfx(bomb.playerAudioSource, bomb.placeBombSfx);
        return true;
    }

    // Shared with PurpleLouie: placement retains the bomb controller's tile/variant rules.
    public static int PlaceLine(
        MovementController movement,
        BombController bomb,
        Vector2 dir,
        bool trackOwnerBombTraversal = true)
    {
        if (movement == null || bomb == null) return 0;
        int count = bomb.BombsRemaining;
        Vector2 origin = movement.Rigidbody != null
            ? movement.Rigidbody.position : (Vector2)movement.transform.position;
        origin.x = Mathf.Round(origin.x / movement.tileSize) * movement.tileSize;
        origin.y = Mathf.Round(origin.y / movement.tileSize) * movement.tileSize;
        Vector2 pos = origin + dir * movement.tileSize;
        int placed = 0;
        int blockers = LayerMask.GetMask("Stage", "Enemy");
        for (int i = 0; i < count; i++)
        {
            if (Physics2D.OverlapBox(pos, Vector2.one * 0.4f, 0f, blockers) != null ||
                !bomb.TryPlaceBombAtIgnoringInputLock(
                    pos,
                    out _,
                    consumeBomb: true,
                    playSfx: false,
                    trackOwnerBombTraversal: trackOwnerBombTraversal))
            {
                break;
            }
            placed++;
            pos += dir * movement.tileSize;
            if (bomb.BombsRemaining <= 0) break;
        }
        return placed;
    }
}
