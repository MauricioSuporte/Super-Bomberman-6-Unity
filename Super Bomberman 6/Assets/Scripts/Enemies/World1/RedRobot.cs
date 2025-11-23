using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class RedRobot : MonoBehaviour
{
    public float speed = 2f;
    public LayerMask obstacleMask;   // Stage + Destructible + Bomb
    public float tileSize = 1f;

    private Rigidbody2D rb;
    private Vector2 direction;
    private Vector2 targetTile;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        SnapToGrid();
        ChooseInitialDirection();
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

    // decide qual será o próximo tile e, se precisar, muda de direção
    void DecideNextTile()
    {
        // 1) verifica o tile à frente, na direção atual
        Vector2 forwardTile = rb.position + direction * tileSize;
        bool blockedForward = Physics2D.OverlapBox(
            forwardTile,
            Vector2.one * (tileSize * 0.8f),
            0f,
            obstacleMask
        );

        if (!blockedForward)
        {
            // caminho livre, continua reto
            targetTile = forwardTile;
            return;
        }

        // 2) caminho na frente está bloqueado → escolher outra direção
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        var freeDirs = new List<Vector2>();

        foreach (var dir in dirs)
        {
            if (dir == direction) // já sabemos que na direção atual está bloqueado
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
        {
            // beco sem saída: volta para trás
            direction = -direction;
        }
        else
        {
            direction = freeDirs[Random.Range(0, freeDirs.Count)];
        }

        targetTile = rb.position + direction * tileSize;
    }
}
