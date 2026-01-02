using UnityEngine;

public class JoystickAxisScanner : MonoBehaviour
{
    [Range(1, 28)] public int axisCount = 16;
    [Range(0.05f, 0.95f)] public float threshold = 0.5f;

    float[] prev;

    void Awake() => prev = new float[axisCount + 1];

    void Update()
    {
        for (int i = 1; i <= axisCount; i++)
        {
            float v = Input.GetAxisRaw("joy_" + i);
            if (Mathf.Abs(v - prev[i]) > 0.01f && Mathf.Abs(v) >= threshold)
                Debug.Log($"[JOY AXIS] i={i} v={v:0.00}");

            prev[i] = v;
        }
    }
}
