using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class BattleMode11ImpulseRopeController : MonoBehaviour, IIndestructibleKickedBombHandler
{
    const string BattleMode11SceneName = "BattleMode_11";

    [Header("SFX")]
    [SerializeField] private AudioClip ropeBounceSfx;
    [SerializeField, Range(0f, 1f)] private float ropeBounceSfxVolume = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootstrapOnInitialScene()
    {
        EnsureForActiveScene();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForActiveScene();
    }

    static void EnsureForActiveScene()
    {
        if (!Application.isPlaying)
            return;

        if (!IsBattleMode11Active())
            return;

        if (FindAnyObjectByType<BattleMode11ImpulseRopeController>() != null)
            return;

        var host = new GameObject(nameof(BattleMode11ImpulseRopeController));
        host.AddComponent<BattleMode11ImpulseRopeController>();
    }

    void Awake()
    {
        if (!IsBattleMode11Active())
            Destroy(gameObject);
    }

    public bool TryHandleKickedBombBlocked(
        Bomb bomb,
        Vector2 currentWorldPos,
        Vector2 blockedWorldPos,
        Vector2 kickDirection,
        Tilemap indestructibleTilemap,
        Vector3Int blockedCell,
        TileBase blockedTile,
        out AudioClip bounceSfx,
        out float bounceSfxVolume)
    {
        bounceSfx = null;
        bounceSfxVolume = 1f;

        if (!IsBattleMode11Active() || bomb == null || blockedTile == null)
            return false;

        Vector2 dir = ToCardinal(kickDirection);
        if (!IsImpulseRopeImpact(blockedCell, dir))
            return false;

        bounceSfx = ropeBounceSfx;
        bounceSfxVolume = ropeBounceSfxVolume;
        return true;
    }

    static bool IsImpulseRopeImpact(Vector3Int cell, Vector2 direction)
    {
        if (direction == Vector2.up)
            return cell.y == 5 && cell.x >= -7 && cell.x <= 5;

        if (direction == Vector2.right)
            return cell.x == 6 && cell.y >= -6 && cell.y <= 4;

        if (direction == Vector2.down)
            return cell.y == -7 && cell.x >= -7 && cell.x <= 5;

        if (direction == Vector2.left)
            return cell.x == -8 && cell.y >= -6 && cell.y <= 4;

        return false;
    }

    static Vector2 ToCardinal(Vector2 direction)
    {
        if (direction == Vector2.zero)
            return Vector2.zero;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            return direction.x > 0f ? Vector2.right : Vector2.left;

        return direction.y > 0f ? Vector2.up : Vector2.down;
    }

    static bool IsBattleMode11Active()
        => string.Equals(SceneManager.GetActiveScene().name, BattleMode11SceneName, System.StringComparison.Ordinal);
}
