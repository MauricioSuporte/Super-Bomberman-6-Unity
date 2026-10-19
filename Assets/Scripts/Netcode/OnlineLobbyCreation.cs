using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Netcode
{
    public partial class OnlineLobbyMenu
    {
        bool creationOpen, editingRoomName;
        string createdRoomName = "ROOM 1";
        int selectedMaxPlayers = 6;
        GameObject creationForm;
        Button roomNameButton, browseCreateButton;
        TextMeshProUGUI creationTitle, passwordLeft, passwordRight;

        static string ConnectingText(OnlineRoomText text) => text.Connecting.TrimEnd('.') + new string('.', (int)(Time.unscaledTime * 3f) % 4);
        readonly TextMeshProUGUI[] creationCaptions = new TextMeshProUGUI[4];
        readonly TextMeshProUGUI[] tableHeaders = new TextMeshProUGUI[5];
        static readonly float[] ColumnX = { -85, -41, 7, 51, 88 };
        static readonly float[] ColumnWidth = { 38, 46, 46, 38, 30 };

        void CreateCreationForm(Transform parent)
        {
            creationForm = new GameObject("NewRoom", typeof(RectTransform));
            creationForm.transform.SetParent(parent, false);
            Position(creationForm.GetComponent<RectTransform>(), 0, 0, 256, 224);
            creationTitle = Label(creationForm.transform, "Title", 0, 74, 212, 12, 6);
            creationTitle.color = Gold;
            passwordLeft = Label(passwordButton.transform, "PreviousPassword", -108, 0, 10, 16, 8); passwordLeft.text = "<";
            passwordRight = Label(passwordButton.transform, "NextPassword", 108, 0, 10, 16, 8); passwordRight.text = ">";
            roomNameButton = MakeButton(creationForm.transform, "RoomName", 0, 46, OpenRoomName);
            Button[] fields = { roomNameButton, modeButton, passwordButton, capacityButton };
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i].transform.SetParent(creationForm.transform, false);
                Position(fields[i].GetComponent<RectTransform>(), 0, 46 - i * 26, 196, 16);
                creationCaptions[i] = Label(creationForm.transform, "FieldCaption" + i, 0, 58 - i * 26, 212, 8, 4);
                creationCaptions[i].color = Gold;
            }
            browseCreateButton = MakeButton(browserRoot.transform, "NewRoom", 0, -61, () =>
            {
                creationOpen = true;
                cursorButton = roomNameButton;
                PlayLobbySound(confirmSfx);
            });
        }

        void UpdateCreationForm(bool idle)
        {
            creationForm.SetActive(idle && creationOpen);
            var text = GameTextDatabase.Online;
            creationTitle.text = browser != null && browser.Creating ? ConnectingText(text) : text.NewRoom;
            browseCreateButton.gameObject.SetActive(idle);
            foreach (var heading in tableHeaders) heading.gameObject.SetActive(idle);
            browseCreateButton.GetComponentInChildren<TextMeshProUGUI>(true).text = text.Host;
            browseCreateButton.interactable = browser != null && browser.CanUseOnline && !browser.Creating;
            roomNameButton.GetComponentInChildren<TextMeshProUGUI>(true).text = createdRoomName;
            creationCaptions[0].text = text.RoomName;
            creationCaptions[1].text = text.RoomType;
            creationCaptions[2].text = text.RoomPassword;
            creationCaptions[3].text = text.MaxPlayers;
            roomNameButton.interactable = passwordButton.interactable = browser != null && !browser.Creating;
        }

        void OpenRoomName()
        {
            editingRoomName = true;
            BeginNameEdit(createdRoomName);
        }

        void ChangeCapacity(int delta)
        {
            int maximum = OnlineRoomRules.Capacity(selectedMode);
            selectedMaxPlayers = 2 + (selectedMaxPlayers - 2 + delta + maximum - 1) % (maximum - 1);
            PlayLobbySound(moveCursorSfx);
        }

        void CreateRoomTableHeader(Transform parent)
        {
            for (int i = 0; i < tableHeaders.Length; i++)
            {
                tableHeaders[i] = Label(parent, "Column" + i, ColumnX[i], 53, ColumnWidth[i], 10, 4);
                tableHeaders[i].color = Gold;
            }
        }

        void RefreshRoomTableHeader(OnlineRoomText text)
        {
            string[] headings = { text.Creator, text.NameColumn, text.RoomType, text.PlayersColumn, text.PasswordColumn };
            for (int i = 0; i < tableHeaders.Length; i++) tableHeaders[i].text = headings[i];
        }

        void UpdateRoomTableRow(Button button, OnlineListedRoom room, OnlineRoomText text)
        {
            var cells = button.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (cells.Length == 1)
            {
                for (int i = 1; i < 5; i++) Label(button.transform, "Cell" + i, ColumnX[i], 0, ColumnWidth[i], 18, 4);
                cells = button.GetComponentsInChildren<TextMeshProUGUI>(true);
            }
            string[] values = { string.IsNullOrEmpty(room.creator) ? "-" : room.creator, room.name,
                text.TableModeName(room.Mode), room.players + "/" + room.maxPlayers, room.requiresPassword ? text.Yes : text.No };
            for (int i = 0; i < cells.Length; i++)
            {
                Position(cells[i].rectTransform, ColumnX[i], 0, ColumnWidth[i], 18);
                cells[i].fontSize = 4;
                cells[i].richText = false;
                cells[i].textWrappingMode = TextWrappingModes.NoWrap;
                cells[i].overflowMode = TextOverflowModes.Ellipsis;
                cells[i].text = values[i];
            }
        }
    }
}
