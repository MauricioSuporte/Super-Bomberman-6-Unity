using TMPro;
using UnityEngine;

public class StageLabel : MonoBehaviour
{
    public TMP_Text stageText;

    static readonly string NBSP = "\u00A0";
    const int OptionsIndentPx = -18;

    void EnsureNoWrap()
    {
        if (stageText == null)
            return;

        stageText.textWrappingMode = TextWrappingModes.NoWrap;
        stageText.overflowMode = TextOverflowModes.Overflow;
    }

    public void SetStage(int world, int stage)
    {
        EnsureNoWrap();

        string stageNumber = $"{world}-{stage}";

        stageText.text =
            "<align=center>" +
            $"<size=44><color=#1ABC00>STAGE</color></size>  " +
            $"<size=40><color=#E8E8E8>{stageNumber}</color></size>" +
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
            $"<size=44><color=#1ABC00>STAGE</color></size>  " +
            $"<size=40><color=#E8E8E8>{stageNumber}</color></size>\n" +
            $"<size=38><color=#3392FF>PAUSE!</color></size>\n\n" +
            $"<size=34>{resume}</size>\n" +
            $"<size=34>{ret}</size>" +
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

        stageText.text =
            "<align=center>" +
            $"<size=44><color=#1ABC00>STAGE</color></size>  " +
            $"<size=40><color=#E8E8E8>{stageNumber}</color></size>\n" +
            $"<size=38><color=#3392FF>PAUSE!</color></size>\n\n" +
            $"<size=30><color=#E8E8E8>Return to Title Screen?</color></size>\n" +
            $"<size=26><color=#E8E8E8>All progress will be lost.</color></size>\n\n" +
            $"<size=34><indent={OptionsIndentPx}>{noOpt}</indent></size>\n" +
            $"<size=34><indent={OptionsIndentPx}>{yesOpt}</indent></size>" +
            "</align>";
    }
}
