using UnityEngine;

[DisallowMultipleComponent]
public sealed class BombAtGroundTileNotifier : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float tickSeconds = 0.02f;

    private float nextTickTime;

    private BombController fallbackSource;
    private Bomb bomb;

    private bool hasLast;
    private Vector3Int lastCell;

    public void Initialize(BombController source)
    {
        fallbackSource = source;
        bomb = GetComponent<Bomb>();
        hasLast = false;
        nextTickTime = 0f;
    }

    private void OnEnable()
    {
        nextTickTime = 0f;
    }

    private void FixedUpdate()
    {
        if (Time.time < nextTickTime)
            return;

        nextTickTime = Time.time + tickSeconds;

        var source = bomb != null && bomb.Owner != null ? bomb.Owner : fallbackSource;
        if (source == null)
            return;

        Vector2 logicalPos = bomb != null ? bomb.GetLogicalPosition() : (Vector2)transform.position;

        logicalPos.x = Mathf.Round(logicalPos.x);
        logicalPos.y = Mathf.Round(logicalPos.y);

        if (source.groundTiles == null)
            return;

        Vector3Int cell = source.groundTiles.WorldToCell(logicalPos);

        if (hasLast && cell == lastCell)
            return;

        lastCell = cell;
        hasLast = true;

        source.NotifyBombAt(logicalPos, gameObject);
    }
}
