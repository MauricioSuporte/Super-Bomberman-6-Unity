using UnityEngine;

public class PerJoystickAxisScanner : MonoBehaviour
{
    [Range(1, 11)] public int maxJoysticks = 11;
    [Range(1, 28)] public int axisCount = 28;
    [Range(0.05f, 0.95f)] public float threshold = 0.5f;

    float[,] prev;

    void Awake()
    {
        prev = new float[maxJoysticks + 1, axisCount + 1];
    }

    void Update()
    {
        for (int j = 1; j <= maxJoysticks; j++)
        {
            for (int a = 1; a <= axisCount; a++)
            {
                float v = Input.GetAxisRaw($"joy{j}_{a}");
                float p = prev[j, a];

                if (Mathf.Abs(v - p) > 0.01f && Mathf.Abs(v) >= threshold)
                    Debug.Log($"[AXIS] joy{j}_{a} v={v:0.00}");

                prev[j, a] = v;
            }
        }
    }
}
