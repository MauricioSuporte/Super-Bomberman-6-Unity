using TMPro;
using UnityEngine;

public class StageLabel : MonoBehaviour
{
    public TMP_Text stageText;

    public void SetStage(int world, int stage)
    {
        string stageNumber = $"{world}-{stage}";

        stageText.text =
            $"<size=44><color=#40FF00>STAGE</color></size>  " +
            $"<size=40><color=#E8E8E8>{stageNumber}</color></size>";
    }
}
