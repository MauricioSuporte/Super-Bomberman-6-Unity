using System.Collections.Generic;
using UnityEngine;

public sealed class BirdRobotRocketImpact : MonoBehaviour
{
    private const float GenerationDuration = 0.5f;
    private const float ExplosionDuration = 0.1f;
    private readonly List<AnimatedSpriteRenderer> visuals = new();
    private readonly List<float> ages = new();
    private GameObject template;
    private float tileSize;
    private float elapsed;
    private float nextSpawnTime;

    public void Init(GameObject explosionTemplate, float gridSize)
    {
        template = explosionTemplate;
        template.transform.SetParent(transform);
        tileSize = gridSize;
        SpawnExplosion();
        nextSpawnTime = Random.Range(0.025f, 0.075f);
    }

    private void Update()
    {
        if (GamePauseController.IsPaused)
            return;
        elapsed += Time.deltaTime;
        for (int i = visuals.Count - 1; i >= 0; i--)
        {
            ages[i] += Time.deltaTime;
            if (ages[i] >= ExplosionDuration)
            {
                Destroy(visuals[i].gameObject);
                visuals.RemoveAt(i);
                ages.RemoveAt(i);
                continue;
            }
            visuals[i].CurrentFrame = ages[i] < 0.05f ? 0 : 1;
            visuals[i].RefreshFrame();
        }
        if (elapsed < GenerationDuration && elapsed >= nextSpawnTime)
        {
            SpawnExplosion();
            nextSpawnTime = elapsed + Random.Range(0.025f, 0.075f);
        }
        if (elapsed >= GenerationDuration && visuals.Count == 0)
            Destroy(gameObject);
    }

    private void SpawnExplosion()
    {
        GameObject visual = Instantiate(template, transform);
        visual.transform.localPosition = new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.35f, 0.35f), 0f) * tileSize;
        visual.SetActive(true);
        AnimatedSpriteRenderer animation = visual.GetComponent<AnimatedSpriteRenderer>();
        animation.SetManualAnimationUpdate(true);
        animation.animationTime = 0.05f;
        animation.useSequenceDuration = false;
        animation.frameDurations = null;
        animation.loop = false;
        animation.idle = false;
        animation.enabled = true;
        animation.RestartAnimation();
        visuals.Add(animation);
        ages.Add(0f);
    }
}