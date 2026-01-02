using UnityEngine;

public class PerJoystickButtonScanner : MonoBehaviour
{
    [Range(1, 11)] public int maxJoysticks = 11;
    [Range(0, 39)] public int maxButtons = 39;

    void Update()
    {
        for (int j = 1; j <= maxJoysticks; j++)
        {
            for (int b = 0; b <= maxButtons; b++)
            {
                string name = $"Joystick{j}Button{b}";
                if (System.Enum.TryParse(name, out KeyCode kc) && Input.GetKeyDown(kc))
                    Debug.Log($"[BTN DOWN] joy{j} btn{b} ({kc})");
            }
        }
    }
}
