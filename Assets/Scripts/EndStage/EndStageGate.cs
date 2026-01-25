using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(AudioSource))]
public sealed class EndStageGate : EndStage
{
    public AudioClip openGateSfx;

    [Header("Gate Tilemap")]
    public Tilemap gateTilemap;

    [Header("Closed Gate Tiles")]
    public TileBase closed00;
    public TileBase closed10;
    public TileBase closed20;
    public TileBase closed01;
    public TileBase closed11;
    public TileBase closed21;

    [Header("Mid Gate Tiles (Optional)")]
    public TileBase mid00;
    public TileBase mid10;
    public TileBase mid20;
    public TileBase mid01;
    public TileBase mid11;
    public TileBase mid21;

    [Header("Open Gate Tiles")]
    public TileBase open00;
    public TileBase open10;
    public TileBase open20;
    public TileBase open01;
    public TileBase open11;
    public TileBase open21;

    [Header("Open Transition")]
    [Min(0f)]
    public float midStepDelay = 0.18f;

    [Header("Open Delay")]
    [Min(0f)]
    public float openDelay = 0.5f;

    [Header("Entry (Open)")]
    public TileBase entryOpenTile;

    [Header("Trigger")]
    public BoxCollider2D gateTrigger;

    public Vector2 triggerSize = new(0.1f, 0.1f);

    AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (gateTrigger == null)
            gateTrigger = GetComponent<BoxCollider2D>();

        if (gateTrigger != null)
        {
            gateTrigger.isTrigger = true;
            gateTrigger.enabled = false;
        }
    }

    protected override void OnUnlocked()
    {
        StartCoroutine(OpenGateRoutine());
    }

    IEnumerator OpenGateRoutine()
    {
        if (openDelay > 0f)
            yield return new WaitForSeconds(openDelay);

        if (openGateSfx != null && audioSource != null)
            audioSource.PlayOneShot(openGateSfx);

        if (HasMidTiles())
        {
            ReplaceTilesClosedToMid();
            if (gateTilemap != null) gateTilemap.RefreshAllTiles();

            if (midStepDelay > 0f)
                yield return new WaitForSeconds(midStepDelay);
        }

        ReplaceTilesToOpen();
        if (gateTilemap != null) gateTilemap.RefreshAllTiles();

        PositionTriggerOnEntryTileFound();

        if (gateTrigger != null)
            gateTrigger.enabled = true;
    }

    bool HasMidTiles()
    {
        return mid00 != null || mid10 != null || mid20 != null ||
               mid01 != null || mid11 != null || mid21 != null;
    }

    void ReplaceTilesClosedToMid()
    {
        if (gateTilemap == null)
            return;

        var bounds = gateTilemap.cellBounds;

        for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                var current = gateTilemap.GetTile(cell);
                if (current == null) continue;

                var replacement = GetClosedToMid(current);
                if (replacement != null)
                    gateTilemap.SetTile(cell, replacement);
            }
    }

    TileBase GetClosedToMid(TileBase current)
    {
        if (current == null)
            return null;

        if (closed00 != null && current == closed00) return mid00 ?? open00;
        if (closed10 != null && current == closed10) return mid10 ?? open10;
        if (closed20 != null && current == closed20) return mid20 ?? open20;

        if (closed01 != null && current == closed01) return mid01 ?? open01;
        if (closed11 != null && current == closed11) return mid11 ?? open11;
        if (closed21 != null && current == closed21) return mid21 ?? open21;

        return null;
    }

    void ReplaceTilesToOpen()
    {
        if (gateTilemap == null)
            return;

        var bounds = gateTilemap.cellBounds;

        for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                var current = gateTilemap.GetTile(cell);
                if (current == null) continue;

                var replacement = GetToOpen(current);
                if (replacement != null)
                    gateTilemap.SetTile(cell, replacement);
            }
    }

    TileBase GetToOpen(TileBase current)
    {
        if (current == null)
            return null;

        if (closed00 != null && current == closed00) return open00;
        if (closed10 != null && current == closed10) return open10;
        if (closed20 != null && current == closed20) return open20;

        if (closed01 != null && current == closed01) return open01;
        if (closed11 != null && current == closed11) return open11;
        if (closed21 != null && current == closed21) return open21;

        if (mid00 != null && current == mid00) return open00;
        if (mid10 != null && current == mid10) return open10;
        if (mid20 != null && current == mid20) return open20;

        if (mid01 != null && current == mid01) return open01;
        if (mid11 != null && current == mid11) return open11;
        if (mid21 != null && current == mid21) return open21;

        return null;
    }

    void PositionTriggerOnEntryTileFound()
    {
        if (gateTilemap == null || gateTrigger == null || entryOpenTile == null)
            return;

        var bounds = gateTilemap.cellBounds;

        for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                var current = gateTilemap.GetTile(cell);

                if (current != entryOpenTile)
                    continue;

                var worldCenter = gateTilemap.GetCellCenterWorld(cell);

                var dx = worldCenter.x - transform.position.x;
                var dy = worldCenter.y - transform.position.y;

                gateTrigger.offset = new Vector2(dx, dy);
                gateTrigger.size = triggerSize;

                return;
            }

        gateTrigger.enabled = false;
    }
}
