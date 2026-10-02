using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class BirdRobotRocket : MonoBehaviour
{
    private Rigidbody2D body;
    private Vector2 direction;
    private GameObject owner;
    private GameObject impactTemplate;
    private float tileSize;
    private float elapsed;
    private bool impacted;

    public void Init(Vector2 travelDirection, GameObject shotOwner, AnimatedSpriteRenderer explosion, float gridSize)
    {
        direction = travelDirection;
        owner = shotOwner;
        tileSize = gridSize;
        // Keep a detached template so rockets still explode after their owner dies.
        impactTemplate = Instantiate(explosion.gameObject);
        impactTemplate.SetActive(false);
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        GetComponent<CircleCollider2D>().isTrigger = true;
        GetComponent<CircleCollider2D>().radius = 0.15f;
    }

    private void FixedUpdate()
    {
        if (impacted || GamePauseController.IsPaused)
            return;
        elapsed += Time.fixedDeltaTime;
        if (elapsed >= 5f)
        {
            Destroy(gameObject);
            return;
        }
        body.MovePosition(body.position + direction * 5f * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (impacted || other == null || (owner != null && other.transform.IsChildOf(owner.transform)))
            return;
        Bomb bomb = other.GetComponentInParent<Bomb>();
        PlayerIdentity player = other.GetComponentInParent<PlayerIdentity>();
        bool stage = other.gameObject.layer == LayerMask.NameToLayer("Stage");
        if (bomb == null && player == null && !stage)
            return;
        impacted = true;
        Vector2 contact = other.ClosestPoint(body.position) + direction * 0.2f;
        Tilemap map = other.GetComponentInParent<Tilemap>();
        Vector2 center = map != null ? (Vector2)map.GetCellCenterWorld(map.WorldToCell(contact))
            : new Vector2(Mathf.Round(contact.x / tileSize) * tileSize, Mathf.Round(contact.y / tileSize) * tileSize);
        if (bomb != null)
        {
            center = bomb.transform.position;
            if (bomb.Owner != null)
                bomb.Owner.ExplodeBomb(bomb.gameObject);
            else
                Destroy(bomb.gameObject);
        }
        else if (player != null)
        {
            CharacterHealth health = player.GetComponent<CharacterHealth>();
            if (health != null) health.TakeDamage(1);
        }
        else if (stage)
        {
            foreach (PlayerIdentity activePlayer in PlayerIdentity.ActivePlayers)
            {
                if (activePlayer != null && activePlayer.TryGetComponent<BombController>(out var controller))
                {
                    controller.TriggerDestructibleTileEffectWithoutExplosion(center);
                    break;
                }
            }
        }
        SpawnImpact(center);
        Destroy(gameObject);
    }

    private void SpawnImpact(Vector2 center)
    {
        if (impactTemplate == null)
            return;
        GameObject effect = new("RocketImpact");
        effect.transform.position = center;
        effect.AddComponent<BirdRobotRocketImpact>().Init(impactTemplate, tileSize);
        impactTemplate = null;
    }

    private void OnDestroy()
    {
        if (impactTemplate != null) Destroy(impactTemplate);
    }
}