/// <summary>
/// Receives a bomb explosion that hits an active stage collider.
/// This is intentionally separate from <see cref="Destructible"/>
/// CoreMechanismsDestructible so stage props do not participate in room-core
/// completion accounting.
/// </summary>
public interface IExplosionDestructible
{
    /// <returns>True when this object consumed the explosion hit.</returns>
    bool TryDestroyByExplosion(BombExplosion.ExplosionPart explosionPart);
}
