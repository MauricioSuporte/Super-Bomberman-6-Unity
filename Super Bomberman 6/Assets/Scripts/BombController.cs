using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BombController : MonoBehaviour
{
    [Header("Bomb")]
    public KeyCode inputKey = KeyCode.Space;
    public GameObject bombPrefab;
    public float bombFuseTime = 3f;
    public int bombAmout = 1;
    private int bombsRemaining = 0;

    [Header("Explosion")]
    public Explosion explosionPrefab;
    public LayerMask explosionLayerMask;
    public float explosionDuration = 1f;
    public int explosionRadius = 2;

    [Header("Destructible")]
    public Tilemap destructibleTiles;
    public Destructible destructiblePrefab;

    private void OnEnable()
    {
        bombsRemaining = bombAmout;
    }

    private void Update()
    {
        if (bombsRemaining > 0 && Input.GetKeyDown(inputKey))
        {
            PlaceBomb();
        }
    }

    public void AddBomb()
    {
        bombAmout++;
        bombsRemaining++;
    }

    private Explosion GetExplosionAt(Vector2 position)
    {
        int explosionLayer = LayerMask.NameToLayer("Explosion");
        int mask = 1 << explosionLayer;

        var hit = Physics2D.OverlapBox(position, Vector2.one * 0.4f, 0f, mask);
        if (hit != null)
            return hit.GetComponent<Explosion>();

        return null;
    }

    public void ExplodeBomb(GameObject bomb)
    {
        if (bomb == null)
            return;

        var bombComponent = bomb.GetComponent<Bomb>();
        if (bombComponent != null)
        {
            if (bombComponent.HasExploded)
                return;

            bombComponent.MarkAsExploded();
        }

        Vector2 position = bomb.transform.position;
        position.x = Mathf.Round(position.x);
        position.y = Mathf.Round(position.y);

        Explosion explosion = GetExplosionAt(position);
        if (explosion == null)
        {
            explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
            explosion.DestroyAfter(explosionDuration);
        }

        explosion.SetStart();

        Explode(position, Vector2.up, explosionRadius);
        Explode(position, Vector2.down, explosionRadius);
        Explode(position, Vector2.left, explosionRadius);
        Explode(position, Vector2.right, explosionRadius);

        Destroy(bomb);
        bombsRemaining++;
    }

    private void PlaceBomb()
    {
        Vector2 position = transform.position;
        position.x = Mathf.Round(position.x);
        position.y = Mathf.Round(position.y);

        GameObject bomb = Instantiate(bombPrefab, position, Quaternion.identity);
        bombsRemaining--;

        var bombComponent = bomb.GetComponent<Bomb>();
        if (bombComponent == null)
            bombComponent = bomb.AddComponent<Bomb>();

        bombComponent.Initialize(this);

        StartCoroutine(BombFuse(bomb));
    }

    private IEnumerator BombFuse(GameObject bomb)
    {
        yield return new WaitForSeconds(bombFuseTime);
        ExplodeBomb(bomb);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Bomb"))
        {
            other.isTrigger = false;
        }
    }

    private void Explode(Vector2 position, Vector2 direction, int length)
    {
        if (length <= 0)
            return;

        position += direction;

        if (Physics2D.OverlapBox(position, Vector2.one * 0.5f, 0f, explosionLayerMask))
        {
            ClearDestructible(position);
            return;
        }

        var existing = GetExplosionAt(position);
        if (existing != null)
        {
            if (length > 1)
            {
                existing.UpgradeToMiddleIfNeeded();
            }

            Explode(position, direction, length - 1);
            return;
        }

        Explosion explosion = Instantiate(explosionPrefab, position, Quaternion.identity);

        if (length > 1)
            explosion.SetMiddle();
        else
            explosion.SetEnd();

        explosion.SetDirection(direction);
        explosion.DestroyAfter(explosionDuration);

        Explode(position, direction, length - 1);
    }

    private void ClearDestructible(Vector2 position)
    {
        Vector3Int cell = destructibleTiles.WorldToCell(position);
        TileBase tile = destructibleTiles.GetTile(cell);

        if (tile != null)
        {
            Instantiate(destructiblePrefab, position, Quaternion.identity);
            destructibleTiles.SetTile(cell, null);
        }
    }
}
