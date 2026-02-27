using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PixelPerfectLinearScroller : MonoBehaviour
{
    [Header("Pixel Perfect")]
    [SerializeField, Min(1)] private int pixelsPerUnit = 16;

    [Header("Space")]
    [SerializeField] private bool useLocalSpace = false;

    [Header("Path (Inspector)")]
    [SerializeField] private Vector2 startPosition = new Vector2(0f, 6f);
    [SerializeField] private Vector2 endPosition = new Vector2(0f, -6f);

    [Header("Random Start")]
    [SerializeField] private bool randomizeStartEnd = true;

    [Header("Speed")]
    [SerializeField, Min(0.0001f)] private float speedUnitsPerSecond = 1f;
    [SerializeField] private bool useRandomSpeedRange = false;
    [SerializeField, Min(0.0001f)] private float randomSpeedMinUnitsPerSecond = 1f;
    [SerializeField, Min(0.0001f)] private float randomSpeedMaxUnitsPerSecond = 3f;

    [Header("Loop")]
    [SerializeField, Min(0f)] private float respawnDelaySeconds = 0.25f;

    private Vector2 currentStart;
    private Vector2 currentEnd;

    private Vector2 dir;
    private float pathLengthUnits;
    private int pathLengthPixels;

    private float traveledPixelsFloat;
    private int traveledPixelsInt;
    private bool isWaitingRespawn;

    private float currentSpeedUnitsPerSecond;

    private float SpeedPixelsPerSecond => currentSpeedUnitsPerSecond * Mathf.Max(1, pixelsPerUnit);

    private void OnEnable()
    {
        ResetToStart();
    }

    private void OnValidate()
    {
        if (pixelsPerUnit < 1) pixelsPerUnit = 1;
        if (speedUnitsPerSecond < 0.0001f) speedUnitsPerSecond = 0.0001f;
        if (randomSpeedMinUnitsPerSecond < 0.0001f) randomSpeedMinUnitsPerSecond = 0.0001f;
        if (randomSpeedMaxUnitsPerSecond < 0.0001f) randomSpeedMaxUnitsPerSecond = 0.0001f;
        if (respawnDelaySeconds < 0f) respawnDelaySeconds = 0f;

        if (randomSpeedMaxUnitsPerSecond < randomSpeedMinUnitsPerSecond)
            randomSpeedMaxUnitsPerSecond = randomSpeedMinUnitsPerSecond;
    }

    private void Update()
    {
        if (isWaitingRespawn)
            return;

        if (pathLengthPixels <= 0)
            RebuildPath();

        traveledPixelsFloat += SpeedPixelsPerSecond * Time.deltaTime;

        int targetPixels = Mathf.Min(pathLengthPixels, Mathf.FloorToInt(traveledPixelsFloat));
        if (targetPixels != traveledPixelsInt)
        {
            traveledPixelsInt = targetPixels;
            ApplyPixelPosition(traveledPixelsInt);
        }

        if (traveledPixelsInt >= pathLengthPixels)
            StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        isWaitingRespawn = true;

        if (respawnDelaySeconds > 0f)
            yield return new WaitForSeconds(respawnDelaySeconds);

        ResetToStart();
        isWaitingRespawn = false;
    }

    private void ResetToStart()
    {
        PickDirectionAndEndpoints();
        PickSpeed();

        RebuildPath();

        traveledPixelsFloat = 0f;
        traveledPixelsInt = 0;

        SetPosition2D(currentStart);
    }

    private void PickDirectionAndEndpoints()
    {
        if (randomizeStartEnd && Random.value < 0.5f)
        {
            currentStart = startPosition;
            currentEnd = endPosition;
        }
        else
        {
            currentStart = endPosition;
            currentEnd = startPosition;
        }
    }

    private void PickSpeed()
    {
        if (useRandomSpeedRange)
        {
            float min = Mathf.Min(randomSpeedMinUnitsPerSecond, randomSpeedMaxUnitsPerSecond);
            float max = Mathf.Max(randomSpeedMinUnitsPerSecond, randomSpeedMaxUnitsPerSecond);
            currentSpeedUnitsPerSecond = Random.Range(min, max);
        }
        else
        {
            currentSpeedUnitsPerSecond = speedUnitsPerSecond;
        }
    }

    private void RebuildPath()
    {
        Vector2 delta = currentEnd - currentStart;
        pathLengthUnits = delta.magnitude;

        if (pathLengthUnits <= 0.000001f)
        {
            dir = Vector2.down;
            pathLengthPixels = 0;
            return;
        }

        dir = delta / pathLengthUnits;
        pathLengthPixels = Mathf.Max(1, Mathf.RoundToInt(pathLengthUnits * Mathf.Max(1, pixelsPerUnit)));
    }

    private void ApplyPixelPosition(int pixelsFromStart)
    {
        float unitsFromStart = pixelsFromStart / (float)Mathf.Max(1, pixelsPerUnit);
        Vector2 pos = currentStart + dir * unitsFromStart;
        SetPosition2D(pos);
    }

    private void SetPosition2D(Vector2 pos)
    {
        if (useLocalSpace)
        {
            Vector3 p = transform.localPosition;
            transform.localPosition = new Vector3(pos.x, pos.y, p.z);
        }
        else
        {
            Vector3 p = transform.position;
            transform.position = new Vector3(pos.x, pos.y, p.z);
        }
    }
}