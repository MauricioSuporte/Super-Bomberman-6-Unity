using UnityEngine;

public class DpadProbe : MonoBehaviour
{
    void Update()
    {
        for (int j = 1; j <= 11; j++)
        {
            float x = Input.GetAxisRaw($"joy{j}_6");
            float y = Input.GetAxisRaw($"joy{j}_7");

            if (Mathf.Abs(x) > 0.01f || Mathf.Abs(y) > 0.01f)
                Debug.Log($"[DPAD] joy{j} x={x:0.00} y={y:0.00}");
        }
    }
}
