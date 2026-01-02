using UnityEngine;

public class JoystickNamesProbe : MonoBehaviour
{
    void Start()
    {
        var names = Input.GetJoystickNames();
        Debug.Log($"Joysticks: {names?.Length ?? 0}");
        if (names == null) return;

        for (int i = 0; i < names.Length; i++)
            Debug.Log($"[{i}] '{names[i]}'");
    }
}
