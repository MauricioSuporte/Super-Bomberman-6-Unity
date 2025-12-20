using UnityEngine;

public class BlackLouieMovementController : LouieMovementController
{
    protected override void Awake()
    {
        base.Awake();

        if (TryGetComponent<CharacterHealth>(out var health))
            health.life = 2;
    }
}
