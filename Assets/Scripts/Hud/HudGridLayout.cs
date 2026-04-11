using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class HudGridLayout : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private RectTransform[] grids;

    [Header("Dimensões lógicas da HUD (pixels SNES)")]
    [SerializeField] private float hudWidth = 256f;
    [SerializeField] private float hudHeight = 23f;

    [Header("Dimensões lógicas dos grids")]
    [SerializeField] private float defaultGridWidth = 46f;
    [SerializeField] private float[] gridWidths = new float[] { 46f, 46f, 46f, 20f };
    [SerializeField] private float gridHeight = 19f;

    [Header("Posicionamento Horizontal")]
    [SerializeField] private bool centralizarAutomaticamente = true;
    [SerializeField] private float primeiroGridLeft = 31f;
    [SerializeField] private float espacamentoEntreGrids = 2f;
    [SerializeField] private float ajusteHorizontal = 0f;

    [Header("Posicionamento Vertical")]
    [SerializeField] private float espacamentoSuperior = 2f;
    [SerializeField] private float espacamentoInferior = 2f;

    RectTransform _rt;

    void LateUpdate()
    {
        if (_rt == null)
            _rt = (RectTransform)transform;

        if (grids == null || grids.Length == 0)
            return;

        int count = grids.Length;

        float larguraTotal = CalcularLarguraTotal(count);

        float leftInicial = centralizarAutomaticamente
            ? (hudWidth - larguraTotal) * 0.5f
            : primeiroGridLeft;

        leftInicial += ajusteHorizontal;

        float bottom = espacamentoInferior;

        if (espacamentoInferior <= 0f)
            bottom = hudHeight - espacamentoSuperior - gridHeight;

        float leftAtual = leftInicial;

        for (int i = 0; i < count; i++)
        {
            RectTransform g = grids[i];
            if (g == null)
                continue;

            float larguraGrid = ObterLarguraGrid(i);

            float left = leftAtual;
            float right = left + larguraGrid;

            float minX = left / hudWidth;
            float maxX = right / hudWidth;
            float minY = bottom / hudHeight;
            float maxY = (bottom + gridHeight) / hudHeight;

            g.anchorMin = new Vector2(minX, minY);
            g.anchorMax = new Vector2(maxX, maxY);
            g.offsetMin = Vector2.zero;
            g.offsetMax = Vector2.zero;
            g.localScale = Vector3.one;

            leftAtual += larguraGrid + espacamentoEntreGrids;
        }
    }

    float CalcularLarguraTotal(int count)
    {
        float total = 0f;

        for (int i = 0; i < count; i++)
            total += ObterLarguraGrid(i);

        if (count > 1)
            total += espacamentoEntreGrids * (count - 1);

        return total;
    }

    float ObterLarguraGrid(int index)
    {
        if (gridWidths != null && index < gridWidths.Length && gridWidths[index] > 0f)
            return gridWidths[index];

        return defaultGridWidth;
    }
}