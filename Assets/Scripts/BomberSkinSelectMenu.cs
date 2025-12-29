using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BomberSkinSelectMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] GameObject root;
    [SerializeField] Image previewImage;
    [SerializeField] Text nameText;
    [SerializeField] Text hintText;

    [Header("Input")]
    [SerializeField] KeyCode leftKey = KeyCode.LeftArrow;
    [SerializeField] KeyCode rightKey = KeyCode.RightArrow;
    [SerializeField] KeyCode confirmKey = KeyCode.Return;
    [SerializeField] KeyCode cancelKey = KeyCode.Escape;

    [Header("Skins (ordem do menu)")]
    [SerializeField]
    List<BomberSkin> selectableSkins = new()
    {
        BomberSkin.White,
        BomberSkin.Black,
        BomberSkin.Blue,
        BomberSkin.Red,
        BomberSkin.Green,
        BomberSkin.Yellow,
        BomberSkin.Pink,
        BomberSkin.Aqua,
        BomberSkin.Golden
    };

    [Header("Preview Sprites (opcional)")]
    [SerializeField] List<SkinPreview> previews = new();

    [System.Serializable]
    public class SkinPreview
    {
        public BomberSkin skin;
        public Sprite sprite;
    }

    bool isOpen;
    int index;
    BomberSkin selected;

    public void Hide()
    {
        isOpen = false;
        if (root != null) root.SetActive(false);
        else gameObject.SetActive(false);
    }

    public IEnumerator SelectSkinRoutine()
    {
        if (selectableSkins == null || selectableSkins.Count == 0)
            yield break;

        isOpen = true;

        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);

        index = Mathf.Max(0, selectableSkins.IndexOf(PlayerPersistentStats.Skin));
        if (index < 0) index = 0;

        Refresh();

        bool done = false;
        while (!done)
        {
            if (Input.GetKeyDown(leftKey))
            {
                index = Wrap(index - 1, selectableSkins.Count);
                Refresh();
            }
            else if (Input.GetKeyDown(rightKey))
            {
                index = Wrap(index + 1, selectableSkins.Count);
                Refresh();
            }
            else if (Input.GetKeyDown(confirmKey))
            {
                var skin = selectableSkins[index];
                if (PlayerPersistentStats.IsSkinUnlocked(skin))
                {
                    selected = skin;
                    done = true;
                }
                else
                {
                    if (hintText != null) hintText.text = "Bloqueada";
                }
            }
            else if (Input.GetKeyDown(cancelKey))
            {
                selected = PlayerPersistentStats.Skin;
                done = true;
            }

            yield return null;
        }

        Hide();
    }

    public BomberSkin GetSelectedSkin() => selected;

    void Refresh()
    {
        var skin = selectableSkins[index];

        if (nameText != null)
            nameText.text = skin.ToString();

        bool unlocked = PlayerPersistentStats.IsSkinUnlocked(skin);

        if (hintText != null)
            hintText.text = unlocked ? "← → escolher  |  Enter confirmar" : "Bloqueada (Golden)";

        if (previewImage != null)
        {
            previewImage.sprite = GetPreviewSprite(skin);
            previewImage.enabled = (previewImage.sprite != null);
            previewImage.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }
    }

    Sprite GetPreviewSprite(BomberSkin skin)
    {
        for (int i = 0; i < previews.Count; i++)
            if (previews[i] != null && previews[i].skin == skin)
                return previews[i].sprite;

        return null;
    }

    int Wrap(int v, int count)
    {
        if (count <= 0) return 0;
        v %= count;
        if (v < 0) v += count;
        return v;
    }
}
