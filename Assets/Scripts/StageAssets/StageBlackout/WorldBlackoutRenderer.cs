using UnityEngine;

/// <summary>Sortable, room-bounded output for StageBlackout. Light coordinates are world units.</summary>
[DisallowMultipleComponent]
public sealed class WorldBlackoutRenderer : MonoBehaviour
{
    [SerializeField] private Material sourceMaterial;
    [SerializeField] private BoxCollider2D roomBounds;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 100;
    [Header("Player vision (world units / tiles)")]
    [SerializeField, Min(0.01f)] private float playerRadius = 1.5f;
    [SerializeField, Min(0.001f)] private float playerSoftness = 0.35f;

    private static readonly int PlayerCountId = Shader.PropertyToID("_PlayerCount");
    private static readonly int PlayerCirclesId = Shader.PropertyToID("_PlayerCircles");
    private static readonly int PlayerSoftnessId = Shader.PropertyToID("_PlayerSoftness");
    private readonly Vector4[] playerCircles = new Vector4[6];
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
        if (overlayRenderer != null) overlayRenderer.enabled = value && isActiveAndEnabled;
    }

    private void LateUpdate()
    {
        if (overlayRenderer == null || runtimeMaterial == null) return;
        overlayRenderer.enabled = visible && roomBounds != null;
        if (!overlayRenderer.enabled) return;
        UpdateGeometry();

        int count = 0;
        foreach (PlayerIdentity player in PlayerIdentity.ActivePlayers)
        {
            if (player == null || !player.CompareTag("Player")) continue;
            if (!player.TryGetComponent<MovementController>(out var movement) || movement.isDead) continue;
            Vector2 position = BattleMode7PortalController.GetRoomPresencePosition(movement);
            if (!roomBounds.OverlapPoint(position)) continue;
            playerCircles[count++] = new Vector4(position.x, position.y, playerRadius, 0f);
            if (count == playerCircles.Length) break;
        }
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

    private void OnDisable()
    {
        if (overlayRenderer != null) overlayRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (overlayRenderer != null) Destroy(overlayRenderer.gameObject);
        if (mesh != null) Destroy(mesh);
    }
}
