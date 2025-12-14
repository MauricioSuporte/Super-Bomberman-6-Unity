public interface IPlayerAbility
{
    string Id { get; }
    bool IsEnabled { get; }

    void Enable();
    void Disable();
}
