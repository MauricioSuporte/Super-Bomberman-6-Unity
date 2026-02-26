using TMPro;
using UnityEngine;

public class StageLabel : MonoBehaviour
{
    public TMP_Text stageText;

    static readonly string NBSP = "\u00A0";

    const int SizeStageLabel = 44;
    const int SizeStageNumber = 40;
    const int SizePauseTitle = 38;
    const int SizeMenuItem = 34;
    const int SizeConfirmTitle = 30;
    const int SizeConfirmSubtitle = 26;

    const int OptionsIndentPxBase = -18;

    float UiScale
    {
        get
        {
            if (stageText == null) return 1f;
            var c = stageText.canvas;
            if (c == null) return 1f;
            return Mathf.Max(0.01f, c.scaleFactor);
        }
    }

    int S(int baseSize) => Mathf.Clamp(Mathf.RoundToInt(baseSize * UiScale), 10, 200);
    int Px(int basePx) => Mathf.RoundToInt(basePx * UiScale);

    void EnsureNoWrap()
    {
        if (stageText == null) return;
        stageText.textWrappingMode = TextWrappingModes.NoWrap;
        stageText.overflowMode = TextOverflowModes.Overflow;
        stageText.richText = true;
    }

    public void SetStage(int world, int stage)
    {
        EnsureNoWrap();

        string stageNumber = $"{world}-{stage}";

        stageText.text =
            "<align=center>" +
            $"<size={S(SizeStageLabel)}><color=#1ABC00>STAGE</color></size>  " +
            $"<size={S(SizeStageNumber)}><color=#E8E8E8>{stageNumber}</color></size>" +
            "</align>";
    }

    public void SetPauseMenu(int world, int stage, int selectedIndex)
    {
        EnsureNoWrap();

        string stageNumber = $"{world}-{stage}";

        string resume = (selectedIndex == 0)
            ? "<color=#FF6F31>> RESUME</color>"
            : "<color=#E8E8E8>  RESUME</color>";

        string returnToTitleText = $"RETURN{NBSP}TO{NBSP}TITLE";

        string ret = (selectedIndex == 1)
            ? $"<color=#FF6F31>> {returnToTitleText}</color>"
            : $"<color=#E8E8E8>  {returnToTitleText}</color>";

        stageText.text =
            "<align=center>" +
            $"<size={S(SizeStageLabel)}><color=#1ABC00>STAGE</color></size>  " +
            $"<size={S(SizeStageNumber)}><color=#E8E8E8>{stageNumber}</color></size>\n" +
            $"<size={S(SizePauseTitle)}><color=#3392FF>PAUSE!</color></size>\n\n" +
            $"<size={S(SizeMenuItem)}>{resume}</size>\n" +
            $"<size={S(SizeMenuItem)}>{ret}</size>" +
            "</align>";
    }

    public void SetPauseConfirmReturnToTitle(int world, int stage, int selectedIndex)
    {
        EnsureNoWrap();

        string stageNumber = $"{world}-{stage}";

        string ArrowVisible(string text) => $"<color=#FF6F31>> </color>{text}";
        string ArrowHidden(string text) => $"<color=#00000000>> </color>{text}";

        string noOpt = (selectedIndex == 0)
            ? ArrowVisible("<color=#FF6F31>NO</color>")
            : ArrowHidden("<color=#E8E8E8>NO</color>");

        string yesOpt = (selectedIndex == 1)
            ? ArrowVisible("<color=#FF6F31>YES</color>")
            : ArrowHidden("<color=#E8E8E8>YES</color>");

        int indent = Px(OptionsIndentPxBase);

        stageText.text =
            "<align=center>" +
            $"<size={S(SizeStageLabel)}><color=#1ABC00>STAGE</color></size>  " +
            $"<size={S(SizeStageNumber)}><color=#E8E8E8>{stageNumber}</color></size>\n" +
            $"<size={S(SizePauseTitle)}><color=#3392FF>PAUSE!</color></size>\n\n" +
            $"<size={S(SizeConfirmTitle)}><color=#E8E8E8>Return to Title Screen?</color></size>\n" +
            $"<size={S(SizeConfirmSubtitle)}><color=#E8E8E8>All progress will be lost.</color></size>\n\n" +
            $"<size={S(SizeMenuItem)}><indent={indent}>{noOpt}</indent></size>\n" +
            $"<size={S(SizeMenuItem)}><indent={indent}>{yesOpt}</indent></size>" +
            "</align>";
    }
}