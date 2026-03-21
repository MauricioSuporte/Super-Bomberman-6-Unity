#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class DpadLayoutConfigurator : MonoBehaviour
{
    [Header("Configuração dos Botões")]
    public float buttonSize = 160f;
    public float buttonSpacing = 80f;

    [Header("Referências")]
    public RectTransform upButton;
    public RectTransform downButton;
    public RectTransform leftButton;
    public RectTransform rightButton;

    [ContextMenu("Aplicar Layout do Dpad")]
    public void ApplyLayout()
    {
        float offset = (buttonSize / 2f) + (buttonSpacing / 2f);

        Apply(upButton, 0, offset);
        Apply(downButton, 0, -offset);
        Apply(leftButton, -offset, 0);
        Apply(rightButton, offset, 0);

        Debug.Log($"Dpad configurado: tamanho={buttonSize}, offset={offset}");
    }

    void Apply(RectTransform rt, float x, float y)
    {
        if (rt == null) return;

        rt.sizeDelta = new Vector2(buttonSize, buttonSize);
        rt.anchoredPosition = new Vector2(x, y);

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
    }
}

[CustomEditor(typeof(DpadLayoutConfigurator))]
public class DpadLayoutConfiguratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);
        if (GUILayout.Button("▶ Aplicar Layout", GUILayout.Height(35)))
        {
            ((DpadLayoutConfigurator)target).ApplyLayout();
        }
    }
}
#endif