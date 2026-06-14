public interface IBattleModeComDangerProvider
{
    bool TryGetDangerSeconds(
        UnityEngine.Vector2Int tile,
        out float dangerSeconds);
}
