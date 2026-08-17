/// <summary>
/// Crazy Cup-style random junction movement that is blocked by destructible tiles.
/// </summary>
public sealed class TreeMovementController : CrazyCupMovementController
{
    protected override bool PassesThroughDestructibles => false;
}
