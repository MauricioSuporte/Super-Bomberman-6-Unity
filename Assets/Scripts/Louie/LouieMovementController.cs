using UnityEngine;

public class LouieMovementController : MovementController
{
    [Header("Louie Follow")]
    [SerializeField] private MovementController owner;
    [SerializeField] private Vector2 followOffset = new Vector2(-0.35f, -0.15f);
    [SerializeField] private float followLerp = 25f;

    // guarda a última direção não-zero pra manter "pra onde ele tá virado"
    private Vector2 lastNonZeroDir = Vector2.down;

    public void BindOwner(MovementController ownerMovement, Vector2 offset)
    {
        owner = ownerMovement;
        followOffset = offset;
    }

    protected override void Awake()
    {
        base.Awake();

        // Louie não deve colocar bombas
        if (bombController != null)
            bombController.enabled = false;

        // Louie não deve colidir/bloquear movimento (segue livre)
        obstacleMask = 0;

        // opcional: Louie nunca morre por explosão (fica só visual/companheiro)
        SetExplosionInvulnerable(true);
    }

    protected override void Update()
    {
        // não lê input do teclado
        if (GamePauseController.IsPaused)
            return;

        if (owner == null || owner.isDead)
        {
            Destroy(gameObject);
            return;
        }

        // copia direção do dono
        Vector2 dir = owner.Direction;

        if (dir != Vector2.zero)
            lastNonZeroDir = dir;

        // aplica animação do Louie (mesma lógica do player, mas sem teclado)
        ApplyDirectionFromVector(dir);

        // idle do Louie quando player tá idle
        if (activeSpriteRenderer != null)
            activeSpriteRenderer.idle = (dir == Vector2.zero);
    }

    protected override void FixedUpdate()
    {
        if (GamePauseController.IsPaused)
            return;

        if (owner == null)
            return;

        // segue grudado no player com offset, suavizado
        Vector2 desired = owner.Rigidbody != null
            ? owner.Rigidbody.position + followOffset
            : (Vector2)owner.transform.position + followOffset;

        Vector2 current = Rigidbody.position;
        float t = 1f - Mathf.Exp(-followLerp * Time.fixedDeltaTime);
        Vector2 next = Vector2.Lerp(current, desired, t);

        Rigidbody.MovePosition(next);
    }

    // Louie não bloqueia em nada (ignora obstáculos)
    protected new bool IsBlocked(Vector2 targetPosition) => false;
    protected new bool IsSolidAt(Vector2 worldPosition) => false;

    // Louie não morre por trigger
    protected override void OnTriggerEnter2D(Collider2D other) { }
}
