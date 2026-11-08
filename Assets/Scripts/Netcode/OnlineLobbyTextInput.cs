using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Netcode
{
    public partial class OnlineLobbyMenu
    {
        TouchScreenKeyboard touchKeyboard;
        bool touchConfirm, touchCancel;
        bool keyboardConfirm, keyboardCancel;
        int previousTextKeys;
        string typedPassword = "";

        static bool ValidRoomName(string value) => !string.IsNullOrWhiteSpace(value) &&
            value.Length <= MaxRoomNameLength && value.All(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-' or '\'');
        bool TextInputDown(PlayerAction action) => PlayerInputManager.Instance != null && PlayerInputManager.Instance.GetTextDialogDown(action);
        bool TextConfirm => touchConfirm || keyboardConfirm || TextInputDown(PlayerAction.Start);
        bool TextCancel => touchCancel || keyboardCancel || TextInputDown(PlayerAction.ActionB);
        static int TextKeys => Keyboard.current == null ? 0 :
            (Keyboard.current.enterKey.isPressed || Keyboard.current.numpadEnterKey.isPressed ? 1 : 0) |
            (Keyboard.current.escapeKey.isPressed ? 2 : 0);

        void OpenTouchKeyboard(string value, bool password)
        {
            touchConfirm = touchCancel = false;
            keyboardConfirm = keyboardCancel = false;
            previousTextKeys = TextKeys;
            typedPassword = "";
            if (TouchScreenKeyboard.isSupported && Application.isMobilePlatform)
                touchKeyboard = TouchScreenKeyboard.Open(value, password ? TouchScreenKeyboardType.NumberPad : TouchScreenKeyboardType.Default,
                    false, false, password, false, "", password ? 6 : NameLength);
        }

        void CloseTouchKeyboard()
        {
            ReleasePasswordKeyboard();
            if (touchKeyboard != null) touchKeyboard.active = false;
            touchKeyboard = null;
            touchConfirm = touchCancel = false;
        }

        void PollTouchKeyboard(bool password)
        {
            int keys = TextKeys;
            int pressed = keys & ~previousTextKeys;
            previousTextKeys = keys;
            keyboardConfirm = (pressed & 1) != 0;
            keyboardCancel = (pressed & 2) != 0;
            touchConfirm = touchCancel = false;
            if (touchKeyboard != null)
            {
                string value = touchKeyboard.text;
                if (password)
                {
                    typedPassword = new string(value.Where(c => c is >= '0' and <= '9').Take(6).ToArray());
                    for (int i = 0; i < 6; i++) passwordDigits[i] = i < typedPassword.Length ? typedPassword[i] - '0' : 0;
                    passwordDigit = Mathf.Min(typedPassword.Length, 5);
                }
                else
                {
                    editedName = new string(value.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-' || (editingRoomName && c == '\'')).Take(NameLength).ToArray());
                    nameCursor = Mathf.Min(editedName.Length, NameLength - 1);
                    invalidName = false;
                }
                touchConfirm = touchKeyboard.status == TouchScreenKeyboard.Status.Done;
                touchCancel = touchKeyboard.status is TouchScreenKeyboard.Status.Canceled or TouchScreenKeyboard.Status.LostFocus;
            }
            if (password && Keyboard.current != null)
            {
                // Text events are independent of the user's gameplay key bindings.
                if (!passwordKeyboardHooked)
                {
                    Keyboard.current.onTextInput += TypePassword;
                    passwordKeyboardHooked = true;
                    passwordKeyboard = Keyboard.current;
                }
            }
        }

        Keyboard passwordKeyboard;
        bool passwordKeyboardHooked;
        void TypePassword(char c)
        {
            if (!passwordOpen || touchKeyboard != null) return;
            if (c == '\b' && typedPassword.Length > 0) typedPassword = typedPassword[..^1];
            else if (c is >= '0' and <= '9' && typedPassword.Length < 6) typedPassword += c;
            else return;
            for (int i = 0; i < 6; i++) passwordDigits[i] = i < typedPassword.Length ? typedPassword[i] - '0' : 0;
            passwordDigit = Mathf.Min(typedPassword.Length, 5);
        }

        void ReleasePasswordKeyboard()
        {
            if (passwordKeyboard != null) passwordKeyboard.onTextInput -= TypePassword;
            passwordKeyboard = null;
            passwordKeyboardHooked = false;
        }
    }
}
