using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class HudGridLayout : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private RectTransform[] grids;
    [SerializeField] private RectTransform[] gridsAlternativos;

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

        int count = ObterMaiorQuantidadeDeSlots();
        if (count <= 0)
            return;

        float larguraTotal = CalcularLarguraTotal(count);

        float leftInicial = centralizarAutomaticamente
            ? (hudWidth - larguraTotal) * 0.5f
            : primeiroGridLeft;

        leftInicial += ajusteHorizontal;

        float bottom = espacamentoInferior;

        if (espacamentoInferior <= 0f)
            bottom = hudHeight - espacamentoSuperior - gridHeight;

        int activePlayerCount = ObterQuantidadePlayersAtivos();

        float leftAtual = leftInicial;

        for (int i = 0; i < count; i++)
        {
            float larguraGrid = ObterLarguraGrid(i);

            float left = leftAtual;
            float right = left + larguraGrid;

            float minX = left / hudWidth;
            float maxX = right / hudWidth;
            float minY = bottom / hudHeight;
            float maxY = (bottom + gridHeight) / hudHeight;

            bool playerAtivo = i < activePlayerCount;

            RectTransform gridNormal = ObterGrid(grids, i);
            RectTransform gridAlternativo = ObterGrid(gridsAlternativos, i);

            AplicarLayout(gridNormal, minX, minY, maxX, maxY);
            AplicarLayout(gridAlternativo, minX, minY, maxX, maxY);

            DefinirVisibilidade(gridNormal, playerAtivo);
            DefinirVisibilidade(gridAlternativo, !playerAtivo);

            leftAtual += larguraGrid + espacamentoEntreGrids;
        }
    }

    int ObterMaiorQuantidadeDeSlots()
    {
        int countGridsNormais = grids != null ? grids.Length : 0;
        int countGridsAlternativos = gridsAlternativos != null ? gridsAlternativos.Length : 0;
        return Mathf.Max(countGridsNormais, countGridsAlternativos);
    }

    int ObterQuantidadePlayersAtivos()
    {
        if (Application.isPlaying && GameSession.Instance != null)
            return Mathf.Clamp(GameSession.Instance.ActivePlayerCount, 1, 4);

#if UNITY_EDITOR
        if (!Application.isPlaying)
            return 4;
#endif

        return 1;
    }

    RectTransform ObterGrid(RectTransform[] array, int index)
    {
        if (array == null || index < 0 || index >= array.Length)
            return null;

        return array[index];
    }

    void AplicarLayout(RectTransform g, float minX, float minY, float maxX, float maxY)
    {
        if (g == null)
            return;

        g.anchorMin = new Vector2(minX, minY);
        g.anchorMax = new Vector2(maxX, maxY);
        g.offsetMin = Vector2.zero;
        g.offsetMax = Vector2.zero;
        g.localScale = Vector3.one;
    }

    void DefinirVisibilidade(RectTransform g, bool visible)
    {
        if (g == null)
            return;

        if (g.gameObject.activeSelf != visible)
            g.gameObject.SetActive(visible);
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