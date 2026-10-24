using Mirror;
using UnityEngine;
using TMPro;

namespace Assets.Scripts.Netcode
{
    /// <summary>
    /// Replica o playerId (1..6) do Player e o aplica aos componentes de
    /// gameplay já existentes em TODAS as instâncias (host e clients), para
    /// que o input sintético/hardware acerte o slot certo.
    ///
    /// Colocar no Player.prefab (junto com NetworkIdentity, NetworkTransform
    /// e NetworkPlayerInput).
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class NetworkPlayerSetup : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnPlayerIdChanged))]
        int netPlayerId = 1;

        public int PlayerId => netPlayerId;
        TextMeshProUGUI nameLabel;

        void LateUpdate()
        {
            if (!isClient || !NetSync.IsOnline || !NetworkClient.active) return;
            string name = BombermanNetworkManager.RoomPlayerName(netPlayerId);
            if (string.IsNullOrEmpty(name) && !System.Array.Exists(BombermanNetworkManager.RoomPlayers, id => id == netPlayerId)) name = $"COM {netPlayerId}";
            if (string.IsNullOrEmpty(name)) { if (nameLabel != null) nameLabel.gameObject.SetActive(false); return; }
            if (nameLabel == null)
            {
                var canvasObject = new GameObject("OnlinePlayerName", typeof(RectTransform), typeof(Canvas));
                canvasObject.transform.SetParent(transform, false);
                canvasObject.transform.localScale = Vector3.one / 16f;
                canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 12);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace; canvas.overrideSorting = true; canvas.sortingOrder = 300;
                var textObject = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(canvasObject.transform, false);
                nameLabel = textObject.GetComponent<TextMeshProUGUI>();
                nameLabel.rectTransform.sizeDelta = new Vector2(100, 12);
                nameLabel.font = Resources.Load<TMP_FontAsset>("Fonts/Retro Gaming SDF");
                nameLabel.fontSize = 6; nameLabel.alignment = TextAlignmentOptions.Center;
                nameLabel.color = new Color(1, 1, 1, .6f); nameLabel.outlineColor = new Color32(95, 95, 95, 255);
                nameLabel.outlineWidth = .25f; nameLabel.richText = false; nameLabel.raycastTarget = false;
                LocalizedTmpFontFallback.Apply(nameLabel);
            }
            nameLabel.gameObject.SetActive(true); nameLabel.text = name;
            nameLabel.transform.parent.localPosition = new Vector3(0, 1.05f, 0);
        }

        [Server]
        public void ServerAssignPlayerId(int playerId)
        {
            netPlayerId = Mathf.Clamp(playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);
            ApplyPlayerId(netPlayerId);
        }

        public override void OnStartClient()
        {
            ApplyPlayerId(netPlayerId);
            if (TryGetComponent<NetworkTransformReliable>(out var transformSync))
                transformSync.syncDirection = SyncDirection.ServerToClient;

            // Clients use a kinematic body: observers interpolate, while the owner
            // predicts collision queries and replays authoritative movement snapshots.
            if (!isServer && TryGetComponent<Rigidbody2D>(out var rb))
                rb.bodyType = RigidbodyType2D.Kinematic;
        }

        void OnPlayerIdChanged(int _, int newValue)
        {
            ApplyPlayerId(newValue);
        }

        void ApplyPlayerId(int playerId)
        {
            if (TryGetComponent<PlayerIdentity>(out var identity))
                identity.playerId = playerId;

            if (TryGetComponent<MovementController>(out var move))
                move.SetPlayerId(playerId);

            if (TryGetComponent<BombController>(out var bomb))
                bomb.SetPlayerId(playerId);
        }
    }
}
