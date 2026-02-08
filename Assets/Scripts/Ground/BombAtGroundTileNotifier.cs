using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(Bomb))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class BombAtGroundTileNotifier : MonoBehaviour
{
    private BombController owner;
    private Bomb bomb;
    private Rigidbody2D rb;

    private MovementController ownerMovement;
    private float tileSize = 1f;

    private Tilemap groundTiles;

    private Vector3Int lastCell = new(int.MinValue, int.MinValue, int.MinValue);
    private bool hasLastCell;

    public void Initialize(BombController ownerController)
    {
        owner = ownerController;

        bomb = GetComponent<Bomb>();
        rb = GetComponent<Rigidbody2D>();

        if (owner != null)
        {
            ownerMovement = owner.GetComponent<MovementController>();
            if (ownerMovement != null)
                tileSize = Mathf.Max(0.0001f, ownerMovement.tileSize);

            groundTiles = owner.groundTiles;
        }

        hasLastCell = false;
        lastCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    }

    private void FixedUpdate()
    {
        if (owner == null)
            return;

        if (bomb == null)
            bomb = GetComponent<Bomb>();

        if (bomb == null || bomb.HasExploded)
            return;

        if (bomb.IsBeingPunched || bomb.IsBeingKicked || bomb.IsBeingMagnetPulled)
            return;

        Vector2 basePos = bomb.GetLogicalPosition();

        Vector3Int cell;
        Vector2 tileCenter;

        if (groundTiles != null)
        {
            cell = groundTiles.WorldToCell(basePos);
            Vector3 c = groundTiles.GetCellCenterWorld(cell);
            tileCenter = (Vector2)c;
        }
        else
        {
            float x = Mathf.Round(basePos.x / tileSize) * tileSize;
            float y = Mathf.Round(basePos.y / tileSize) * tileSize;
            tileCenter = new Vector2(x, y);
            cell = new Vector3Int(
                Mathf.RoundToInt(x / tileSize),
                Mathf.RoundToInt(y / tileSize),
                0
            );
        }

        if (hasLastCell && cell == lastCell)
            return;

        lastCell = cell;
        hasLastCell = true;

        owner.NotifyBombAt(tileCenter, gameObject);
    }
}
