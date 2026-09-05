using UnityEngine;
using UnityEngine.U2D;

/// <summary>Sortable, room-bounded output for StageBlackout. Light coordinates are world units.</summary>
[DisallowMultipleComponent]
public sealed class WorldBlackoutRenderer : MonoBehaviour
{
    [SerializeField] private Material sourceMaterial;
    [SerializeField] private BoxCollider2D roomBounds;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 100;
    [Header("Player vision")]
    [Tooltip("Clear radius in tiles, projected by the gameplay camera along with the stage.")]
    [SerializeField, Min(0.01f)] private float playerRadius = 3f;
    [Tooltip("Circle center offset in tiles along the player's facing direction.")]
    [SerializeField, Min(0f)] private float playerForwardOffset = 1.5f;
    [Tooltip("Maximum time for the light to turn to the opposite direction. Movement still follows the player immediately.")]
    [SerializeField, Min(0.01f)] private float playerLightTurnSeconds = 0.1f;
    [Tooltip("Additional soft edge in world units; clipped at the room boundary.")]
    [SerializeField, Min(0.001f)] private float playerSoftness = 0.35f;

    private static readonly int PlayerCountId = Shader.PropertyToID("_PlayerCount");
    private static readonly int PlayerCirclesId = Shader.PropertyToID("_PlayerCircles");
    private static readonly int PlayerSoftnessId = Shader.PropertyToID("_PlayerSoftness");
    private readonly Vector4[] playerCircles = new Vector4[6];
    private readonly PlayerIdentity[] lightOwners = new PlayerIdentity[6];
    private readonly Vector2[] lightOffsets = new Vector2[6];
    private Material runtimeMaterial;
    private Mesh mesh;
    private MeshRenderer overlayRenderer;
    private bool visible;

    public Material SourceMaterial => sourceMaterial;
    public string SortingLayerName => sortingLayerName;
    public int SortingOrder => sortingOrder;
    public Bounds RoomWorldBounds => roomBounds != null ? roomBounds.bounds : default;
    public bool IsVisible => visible && isActiveAndEnabled && roomBounds != null;

    public void Initialize(Material material)
    {
        runtimeMaterial = material;
        if (overlayRenderer != null || roomBounds == null || material == null) return;

        var overlay = new GameObject("WorldBlackoutOverlay");
        overlay.layer = gameObject.layer;
        overlay.transform.SetParent(transform, false);
        mesh = new Mesh { name = "Room blackout quad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f), new Vector3(-0.5f, 0.5f),
            new Vector3(0.5f, 0.5f), new Vector3(0.5f, -0.5f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        overlay.AddComponent<MeshFilter>().sharedMesh = mesh;
        overlayRenderer = overlay.AddComponent<MeshRenderer>();
        overlayRenderer.sharedMaterial = material;
        overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.enabled = false;
        UpdateGeometry();
    }

    public void SetVisible(bool value)
    {
        visible = value;
        if (!value) System.Array.Clear(lightOwners, 0, lightOwners.Length);
        if (overlayRenderer != null) overlayRenderer.enabled = value && isActiveAndEnabled;
    }

    private void LateUpdate()
    {
        if (overlayRenderer == null || runtimeMaterial == null) return;
        overlayRenderer.enabled = visible && roomBounds != null;
        if (!overlayRenderer.enabled) return;
        UpdateGeometry();

        int count = 0;
        int activeSlots = 0;
        float pixelsPerUnit = 16f;
        Camera camera = Camera.main;
        if (camera != null && camera.TryGetComponent<PixelPerfectCamera>(out var pixelCamera))
            pixelsPerUnit = Mathf.Max(1, pixelCamera.assetsPPU);
        foreach (PlayerIdentity player in PlayerIdentity.ActivePlayers)
        {
            if (player == null || !player.CompareTag("Player")) continue;
            if (!player.TryGetComponent<MovementController>(out var movement) || movement.isDead) continue;
            Vector2 position = BattleMode7PortalController.GetRoomPresencePosition(movement);
            if (!roomBounds.OverlapPoint(position)) continue;
            float tileSize = Mathf.Max(0.01f, movement.tileSize);
            Vector2 facing = movement.FacingDirection;
            if (facing.sqrMagnitude < 0.0001f) facing = Vector2.down;
            int slot = player.playerId - 1;
            if (slot < 0 || slot >= lightOwners.Length) continue;
            activeSlots |= 1 << slot;
            Vector2 targetOffset = facing.normalized * (playerForwardOffset * tileSize);
            if (lightOwners[slot] != player)
            {
                // Spawn, respawn and room entry start at the correct direction.
                lightOwners[slot] = player;
                lightOffsets[slot] = targetOffset;
            }
            else
            {
                float speed = 2f * playerForwardOffset * tileSize / Mathf.Max(0.01f, playerLightTurnSeconds);
                lightOffsets[slot] = Vector2.MoveTowards(lightOffsets[slot], targetOffset, speed * Time.deltaTime);
            }
            // Keep the accumulator continuous so rounding cannot stall a turn.
            // Only the submitted center moves in whole source-pixel increments.
            Vector2 center = position + lightOffsets[slot];
            center.x = Mathf.Round(center.x * pixelsPerUnit) / pixelsPerUnit;
            center.y = Mathf.Round(center.y * pixelsPerUnit) / pixelsPerUnit;
            center = ClampToRoom(center);
            // World-space distances naturally scale with camera projection and
            // the pixel-perfect safe frame, independently for every active player.
            playerCircles[count++] = new Vector4(center.x, center.y, playerRadius * tileSize, 0f);
            if (count == playerCircles.Length) break;
        }
        for (int slot = 0; slot < lightOwners.Length; slot++)
            if ((activeSlots & (1 << slot)) == 0) lightOwners[slot] = null;
        runtimeMaterial.SetInt(PlayerCountId, count);
        runtimeMaterial.SetVectorArray(PlayerCirclesId, playerCircles);
        runtimeMaterial.SetFloat(PlayerSoftnessId, playerSoftness);
    }

    private void UpdateGeometry()
    {
        Transform output = overlayRenderer.transform;
        // Follow the authored collider, including its offset, without relying on the active camera.
        output.SetParent(roomBounds.transform, false);
        output.localPosition = new Vector3(roomBounds.offset.x, roomBounds.offset.y, 0f);
        output.localRotation = Quaternion.identity;
        output.localScale = new Vector3(roomBounds.size.x, roomBounds.size.y, 1f);
        overlayRenderer.sortingLayerName = sortingLayerName;
        overlayRenderer.sortingOrder = sortingOrder;
    }

    private Vector2 ClampToRoom(Vector2 worldPosition)
    {
        // Clamp in collider-local space so offset, scale and rotation are respected.
        // The room mesh clips the circle and its soft edge at the same boundary.
        Vector3 local = roomBounds.transform.InverseTransformPoint(
            new Vector3(worldPosition.x, worldPosition.y, roomBounds.transform.position.z));
        Vector2 halfSize = roomBounds.size * 0.5f;
        local.x = Mathf.Clamp(local.x, roomBounds.offset.x - halfSize.x, roomBounds.offset.x + halfSize.x);
        local.y = Mathf.Clamp(local.y, roomBounds.offset.y - halfSize.y, roomBounds.offset.y + halfSize.y);
        local.z = 0f;
        return roomBounds.transform.TransformPoint(local);
    }

    private void OnDisable()
    {
        System.Array.Clear(lightOwners, 0, lightOwners.Length);
        if (overlayRenderer != null) overlayRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (overlayRenderer != null) Destroy(overlayRenderer.gameObject);
        if (mesh != null) Destroy(mesh);
    }
}
