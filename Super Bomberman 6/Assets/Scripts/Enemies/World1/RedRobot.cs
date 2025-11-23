using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class RedRobot : MonoBehaviour
{
    public float speed = 2f;
    public LayerMask obstacleMask;
    public float tileSize = 1f;

    public AnimatedSpriteRenderer spriteUp;
    public AnimatedSpriteRenderer spriteDown;
    public AnimatedSpriteRenderer spriteLeft;
    public AnimatedSpriteRenderer spriteRight; // pode ficar sem usar

    private AnimatedSpriteRenderer activeSprite;
    private Rigidbody2D rb;
    private Vector2 direction;
    private Vector2 targetTile;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        activeSprite = spriteDown;
    }

    private void Start()
    {
        SnapToGrid();
        ChooseInitialDirection();
        UpdateSpriteDirection(direction);
        DecideNextTile();
    }

    private void FixedUpdate()
    {
        MoveTowardsTile();

        if (ReachedTile())
        {
            SnapToGrid();
            DecideNextTile();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
            Destroy(gameObject);
    }

    void SnapToGrid()
    {
        Vector2 pos = rb.position;
        pos.x = Mathf.Round(pos.x / tileSize) * tileSize;
        pos.y = Mathf.Round(pos.y / tileSize) * tileSize;
        rb.position = pos;
    }

    void MoveTowardsTile()
    {
        rb.MovePosition(Vector2.MoveTowards(rb.position, targetTile, speed * Time.fixedDeltaTime));
    }

    bool ReachedTile()
    {
        return Vector2.Distance(rb.position, targetTile) < 0.01f;
    }

    void ChooseInitialDirection()
    {
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        direction = dirs[Random.Range(0, dirs.Length)];
    }

    // --- TROCA DE SPRITES COM FLIP PARA A DIREITA ---
    void UpdateSpriteDirection(Vector2 dir)
    {
        spriteUp.enabled = false;
        spriteDown.enabled = false;
        spriteLeft.enabled = false;
        spriteRight.enabled = false; // não vamos usar, mas desliga por garantia

        if (dir == Vector2.up)
        {
            activeSprite = spriteUp;
        }
        else if (dir == Vector2.down)
        {
            activeSprite = spriteDown;
        }
        else if (dir == Vector2.left || dir == Vector2.right)
        {
            // usa SEMPRE o sprite da esquerda
            activeSprite = spriteLeft;
        }

        activeSprite.enabled = true;

        // aplica flip horizontal só se estiver indo para a direita
        var sr = activeSprite.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.flipX = (dir == Vector2.right);
    }

    // --- IA DE MOVIMENTO EM GRID ---
    void DecideNextTile()
    {
        Vector2 forwardTile = rb.position + direction * tileSize;

        bool blockedForward = Physics2D.OverlapBox(
            forwardTile,
            Vector2.one * (tileSize * 0.8f),
            0f,
            obstacleMask
        );

        if (!blockedForward)
        {
            targetTile = forwardTile;
            return;
        }

        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        var freeDirs = new List<Vector2>();

        foreach (var dir in dirs)
        {
            if (dir == direction)
                continue;

            Vector2 checkTile = rb.position + dir * tileSize;
            Collider2D hit = Physics2D.OverlapBox(
                checkTile,
                Vector2.one * (tileSize * 0.8f),
                0f,
                obstacleMask
            );

            if (hit == null)
                freeDirs.Add(dir);
        }

        if (freeDirs.Count == 0)
            direction = -direction;
        else
            direction = freeDirs[Random.Range(0, freeDirs.Count)];

        UpdateSpriteDirection(direction);
        targetTile = rb.position + direction * tileSize;
    }
}
