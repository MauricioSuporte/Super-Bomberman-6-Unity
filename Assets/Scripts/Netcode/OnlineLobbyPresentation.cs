using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Netcode
{
    public partial class OnlineLobbyMenu
    {
        readonly System.Collections.Generic.List<Button> styledButtons = new();
        readonly Image[] slotPanels = new Image[6];
        readonly TextMeshProUGUI[] slotNames = new TextMeshProUGUI[6];
        readonly TextMeshProUGUI[] slotBadges = new TextMeshProUGUI[6];
        readonly bool[] occupiedSlots = new bool[6];
        readonly float[] arrivalUntil = new float[6];
        GameObject rosterRoot;
        TextMeshProUGUI rosterStatus, arrivalNotice;
        bool rosterInitialized;
        float noticeUntil;
        string arrivalName;
        bool ownArrival;
        int presentedLocalId;

        static readonly Color32 Ink = new(19, 15, 43, 255);
        static readonly Color32 Gold = new(255, 216, 106, 255);
        static readonly Color32 Paper = new(248, 240, 208, 255);
        static readonly Color32 Muted = new(157, 150, 177, 255);

        Image PixelPanel(Transform parent, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
            go.transform.SetParent(parent, false);
            Position(go.GetComponent<RectTransform>(), x, y, width, height);
            var image = go.GetComponent<Image>();
            image.color = Ink;
            image.raycastTarget = false;
            var edge = go.GetComponent<Outline>();
            edge.effectColor = new Color32(105, 87, 151, 255);
            edge.effectDistance = new Vector2(1, -1);
            return image;
        }

        void CreatePixelPanels(Transform parent)
        {
            rosterRoot = new GameObject("PlayerRoster", typeof(RectTransform));
            rosterRoot.transform.SetParent(parent, false);
            Position(rosterRoot.GetComponent<RectTransform>(), 0, 0, 256, 224);
            for (int i = 0; i < slotPanels.Length; i++)
            {
                float x = i % 2 == 0 ? -54 : 54;
                float y = 34 - i / 2 * 26;
                slotPanels[i] = PixelPanel(rosterRoot.transform, "SlotP" + (i + 1), x, y, 102, 22);
                var number = Label(slotPanels[i].transform, "PlayerId", -39, 0, 20, 18, 7);
                number.text = "P" + (i + 1);
                number.color = Gold;
                slotNames[i] = Label(slotPanels[i].transform, "Name", 10, 3, 76, 10, 5);
                slotNames[i].richText = false;
                slotNames[i].overflowMode = TextOverflowModes.Ellipsis;
                slotNames[i].alignment = TextAlignmentOptions.Left;
                slotBadges[i] = Label(slotPanels[i].transform, "State", 10, -6, 76, 8, 4);
                slotBadges[i].alignment = TextAlignmentOptions.Left;
            }
            rosterStatus = Label(rosterRoot.transform, "PlayerCount", 0, -33, 212, 8, 5);
            var notice = PixelPanel(rosterRoot.transform, "Arrival", 0, -43, 212, 10);
            arrivalNotice = Label(notice.transform, "Message", 0, 0, 204, 10, 5);
            arrivalNotice.richText = false;
            arrivalNotice.overflowMode = TextOverflowModes.Ellipsis;
        }

        void UpdateRoomPresentation(bool connected, OnlineRoomText text)
        {
            rosterRoot.SetActive(connected);
            // A private room needs a password row; public creation uses the spare space.
            Position(hostButton.GetComponent<RectTransform>(), 0, -61, 196, 16);
            if (!connected)
            {
                rosterInitialized = false;
                presentedLocalId = 0;
                System.Array.Clear(occupiedSlots, 0, occupiedSlots.Length);
                System.Array.Clear(arrivalUntil, 0, arrivalUntil.Length);
                noticeUntil = 0;
                return;
            }
            if (!rosterInitialized) cursorButton = BombermanNetworkManager.CanConfigure ? configureButton : leaveButton;
            int localId = BombermanNetworkManager.LocalRoomPlayerId;
            if (localId <= 0) return; // Wait for the roster that identifies this connection.
            if (presentedLocalId != localId)
            {
                presentedLocalId = localId;
                ownArrival = true;
                noticeUntil = Time.unscaledTime + 5f;
            }
            creationOpen = false;
            bool joined = false;
            int count = 0;
            for (int i = 0; i < slotPanels.Length; i++)
            {
                int id = i + 1;
                bool available = id <= BombermanNetworkManager.RoomCapacity;
                slotPanels[i].gameObject.SetActive(available);
                bool occupied = available && BombermanNetworkManager.IsConnectedSlot(id);
                if (occupied) count++;
                if (rosterInitialized && occupied && !occupiedSlots[i] && id != BombermanNetworkManager.LocalRoomPlayerId)
                {
                    arrivalUntil[i] = Time.unscaledTime + 5f;
                    noticeUntil = Time.unscaledTime + 5f;
                    arrivalName = BombermanNetworkManager.RoomPlayerName(id);
                    ownArrival = false;
                    joined = true;
                }
                occupiedSlots[i] = occupied;
                bool recent = occupied && Time.unscaledTime < arrivalUntil[i];
                slotPanels[i].color = recent ? new Color32(64, 51, 103, 255) : Ink;
                var edge = slotPanels[i].GetComponent<Outline>();
                edge.effectColor = recent ? Gold : new Color32(105, 87, 151, 255);
                slotNames[i].text = occupied ? BombermanNetworkManager.RoomPlayerName(id) : text.Empty;
                slotNames[i].color = occupied ? Paper : Muted;
                slotBadges[i].color = recent ? Gold : Muted;
                slotBadges[i].text = recent ? "+ " + text.NewPlayer :
                    occupied && id == BombermanNetworkManager.RoomAdminPlayerId ? string.Format(text.RoomAdmin, id) :
                    occupied && id == BombermanNetworkManager.LocalRoomPlayerId ? string.Format(text.You, id) : "";
            }
            rosterInitialized = true;
            if (joined) PlayLobbySound(confirmSfx);
            rosterStatus.text = string.Format(text.PlayersCount, count, BombermanNetworkManager.RoomCapacity);
            arrivalNotice.text = Time.unscaledTime < noticeUntil ? (ownArrival ? text.YouJoined : string.Format(text.PlayerJoined, arrivalName)) :
                BombermanNetworkManager.OwnerModeLocked ? GameTextDatabase.Title.BossRushLocked :
                BombermanNetworkManager.RoomCompleted ? text.RunCompleted : text.Waiting;
            arrivalNotice.color = Time.unscaledTime < noticeUntil ? Gold : Muted;
        }

        void RefreshButtonPresentation()
        {
            foreach (var button in styledButtons)
            {
                bool focused = button == cursorButton && button.interactable;
                button.GetComponent<Image>().color = focused ? Gold : Ink;
                button.GetComponent<Outline>().effectColor = focused ? Paper : new Color32(105, 87, 151, 255);
                foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>(true))
                    label.color = !button.interactable ? Muted : focused ? Ink : Paper;
            }
        }
    }
}
