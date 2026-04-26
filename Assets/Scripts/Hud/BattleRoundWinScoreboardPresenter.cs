using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class BattleRoundWinScoreboardPresenter : MonoBehaviour
{
    const int MaxPlayers = GameSession.MaxPlayerId;
    const int MaxVisibleTrophies = 5;
    const float ScreenWidth = 256f;
    const float ScreenHeight = 224f;
    const float DividerSize = 7f;
    const float DividerStep = 8f;
    const float TableWidth = 192f;
    const float TableTop = 180f;
    const float RowHeight = 24f;
    const float TeamGap = 8f;
    const float PortraitSize = 16f;
    const float TrophyWidth = 16f;
    const float TrophyHeight = 14f;
    const float TrophyStep = 24f;
    const float RowSpriteYOffset = 3f;
    const float PortraitExtraYOffset = 1f;
    const float ScoreboardWidth = 158f;
    const float ScoreboardHeight = 16f;
    const float ScoreboardBottom = 198f;
    const string RuntimeRootName = "__RoundWinScoreboardRuntime";
    const string ScoreboardChildName = "Scoreboard";
    const string DividerResourcesPath = "HUD/RoundWin/Divisor";
    const string TrophyResourcesPath = "HUD/RoundWin/Trophy";
    const string PortraitsResourcesPath = "HUD/PortraitBombersLive";

    readonly List<int> activePlayerIds = new(MaxPlayers);
    readonly List<int> visiblePlayerIds = new(MaxPlayers);
    readonly Dictionary<int, Sprite> portraits = new();
    readonly Dictionary<int, RowUi> rowsByPlayerId = new();

    RectTransform rootRect;
    RectTransform runtimeRoot;
    RectTransform scoreboardRect;
    AnimatedSpriteRenderer scoreboardAnimator;
    Sprite dividerSprite;
    Sprite trophySprite;
    bool spritesLoaded;
    int winnerPlayerId;
    bool usesTeams;
    int targetVictories = 3;

    sealed class RowUi
    {
        public Image[] Trophies;
        public int PreviousWinCount;
        public int CurrentWinCount;
        public bool IsWinner;
    }

    public void Configure(IReadOnlyList<int> players, int winnerId, bool groupByTeam, int victoriesToWinMatch)
    {
        winnerPlayerId = winnerId;
        usesTeams = groupByTeam;
        targetVictories = Mathf.Clamp(victoriesToWinMatch, 1, MaxVisibleTrophies);

        activePlayerIds.Clear();
        if (players != null)
        {
            for (int i = 0; i < players.Count; i++)
            {
                int playerId = players[i];
                if (GameSession.IsValidPlayerId(playerId) && !activePlayerIds.Contains(playerId))
                    activePlayerIds.Add(playerId);
            }
        }

        if (activePlayerIds.Count <= 0)
            activePlayerIds.Add(GameSession.MinPlayerId);

        Build();
    }

    public void RevealRoundWinTrophies()
    {
        foreach (KeyValuePair<int, RowUi> pair in rowsByPlayerId)
        {
            RowUi row = pair.Value;
            if (row == null || !row.IsWinner || row.Trophies == null)
                continue;

            int revealCount = Mathf.Clamp(row.CurrentWinCount, 0, targetVictories);
            for (int i = 0; i < revealCount && i < row.Trophies.Length; i++)
            {
                if (row.Trophies[i] != null)
                    row.Trophies[i].enabled = true;
            }
        }
    }

    void Build()
    {
        EnsureSpritesLoaded();
        EnsureRoot();
        ClearRuntimeChildren();

        rowsByPlayerId.Clear();
        PopulateVisiblePlayerIds();

        BuildBackground();
        ConfigureScoreboardTitle();
        BuildTable();
    }

    void BuildBackground()
    {
        Image background = CreateImage("Background", null);
        background.color = Color.black;
        ApplyLogicalRect(background.rectTransform, 0f, 0f, ScreenWidth, ScreenHeight, ScreenWidth, ScreenHeight);
        background.rectTransform.SetAsFirstSibling();
        Debug.Log("[RoundWinScoreboard] t=" + Time.unscaledTime.ToString("0.###") + " BlackBackgroundReady logicalSize=" + ScreenWidth.ToString("0.#") + "x" + ScreenHeight.ToString("0.#"));
    }

    void EnsureRoot()
    {
        if (rootRect == null)
            rootRect = (RectTransform)transform;

        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(ScreenWidth, ScreenHeight);
        rootRect.localScale = Vector3.one;

        Transform existing = transform.Find(RuntimeRootName);
        runtimeRoot = existing as RectTransform;
        if (runtimeRoot == null)
        {
            GameObject go = new GameObject(RuntimeRootName, typeof(RectTransform));
            runtimeRoot = go.GetComponent<RectTransform>();
            runtimeRoot.SetParent(transform, false);
        }

        runtimeRoot.SetAsFirstSibling();
        ApplyLogicalRect(runtimeRoot, 0f, 0f, ScreenWidth, ScreenHeight, ScreenWidth, ScreenHeight);
    }

    void ClearRuntimeChildren()
    {
        if (runtimeRoot == null)
            return;

        for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = runtimeRoot.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    void EnsureSpritesLoaded()
    {
        if (spritesLoaded)
            return;

        spritesLoaded = true;
        portraits.Clear();

        dividerSprite = Resources.Load<Sprite>(DividerResourcesPath);
        if (dividerSprite == null)
        {
            Sprite[] dividerSprites = Resources.LoadAll<Sprite>(DividerResourcesPath);
            if (dividerSprites != null && dividerSprites.Length > 0)
                dividerSprite = dividerSprites[0];
        }

        trophySprite = Resources.Load<Sprite>(TrophyResourcesPath);
        if (trophySprite == null)
        {
            Sprite[] trophySprites = Resources.LoadAll<Sprite>(TrophyResourcesPath);
            if (trophySprites != null && trophySprites.Length > 0)
                trophySprite = trophySprites[0];
        }

        Sprite[] loadedPortraits = Resources.LoadAll<Sprite>(PortraitsResourcesPath);
        if (loadedPortraits == null)
            return;

        for (int i = 0; i < loadedPortraits.Length; i++)
        {
            Sprite sprite = loadedPortraits[i];
            if (sprite == null)
                continue;

            if (TryParseSpriteNumericSuffix(sprite.name, out int index) && !portraits.ContainsKey(index))
                portraits.Add(index, sprite);
        }
    }

    void PopulateVisiblePlayerIds()
    {
        visiblePlayerIds.Clear();
        visiblePlayerIds.AddRange(activePlayerIds);

        if (usesTeams)
            visiblePlayerIds.Sort(ComparePlayersForScoreboard);
        else
            visiblePlayerIds.Sort();
    }

    int ComparePlayersForScoreboard(int leftPlayerId, int rightPlayerId)
    {
        int leftTeam = GetTeamSortKey(leftPlayerId);
        int rightTeam = GetTeamSortKey(rightPlayerId);

        if (leftTeam != rightTeam)
            return leftTeam.CompareTo(rightTeam);

        return leftPlayerId.CompareTo(rightPlayerId);
    }

    int GetTeamSortKey(int playerId)
    {
        if (BattleModeRules.Instance == null)
            return (int)BattleModeRules.TeamId.Blue;

        return (int)BattleModeRules.Instance.GetTeamForPlayer(playerId);
    }

    void ConfigureScoreboardTitle()
    {
        if (scoreboardRect == null)
        {
            Transform child = transform.Find(ScoreboardChildName);
            scoreboardRect = child as RectTransform;
        }

        if (scoreboardRect == null)
            return;

        scoreboardAnimator = scoreboardRect.GetComponent<AnimatedSpriteRenderer>();
        Image scoreboardImage = scoreboardRect.GetComponent<Image>();
        if (scoreboardImage == null)
            scoreboardImage = scoreboardRect.gameObject.AddComponent<Image>();

        scoreboardImage.raycastTarget = false;
        scoreboardImage.preserveAspect = false;

        float width = ScoreboardWidth;
        float centerX = 0f;
        float centerY = Mathf.Round(ScoreboardBottom + (ScoreboardHeight * 0.5f) - (ScreenHeight * 0.5f));

        scoreboardRect.gameObject.SetActive(true);
        scoreboardRect.anchorMin = new Vector2(0.5f, 0.5f);
        scoreboardRect.anchorMax = new Vector2(0.5f, 0.5f);
        scoreboardRect.pivot = new Vector2(0.5f, 0.5f);
        scoreboardRect.sizeDelta = new Vector2(width, ScoreboardHeight);
        scoreboardRect.anchoredPosition = new Vector2(centerX, centerY);
        scoreboardRect.localScale = Vector3.one;
        scoreboardRect.SetAsLastSibling();

        if (scoreboardAnimator != null)
        {
            scoreboardAnimator.enabled = false;
            scoreboardAnimator.enabled = true;
            scoreboardAnimator.SetExternalBaseLocalPosition(new Vector3(centerX, centerY, 0f));
            scoreboardRect.anchoredPosition = new Vector2(centerX, centerY);
            scoreboardAnimator.RefreshFrame();
        }

        Debug.Log("[RoundWinScoreboard] t=" + Time.unscaledTime.ToString("0.###") + " AnimatedScoreboardReady size=" + width.ToString("0.#") + "x" + ScoreboardHeight.ToString("0.#") + " pos=" + scoreboardRect.anchoredPosition.ToString("F1"));
    }

    void BuildTable()
    {
        float tableLeft = Mathf.Round((ScreenWidth - TableWidth) * 0.5f);
        float tableBottom = GetTableBottom();
        BuildDividers(tableLeft, tableBottom);

        for (int i = 0; i < visiblePlayerIds.Count; i++)
            BuildPlayerRow(visiblePlayerIds[i], i, tableLeft);
    }

    float GetTableBottom()
    {
        float tableHeight = GetTableHeight();
        return Mathf.Max(8f, TableTop - tableHeight);
    }

    float GetTableHeight()
    {
        float height = visiblePlayerIds.Count * RowHeight;

        if (usesTeams)
        {
            for (int i = 1; i < visiblePlayerIds.Count; i++)
            {
                if (GetTeamSortKey(visiblePlayerIds[i - 1]) != GetTeamSortKey(visiblePlayerIds[i]))
                    height += TeamGap;
            }
        }

        return height;
    }

    float GetRowTop(int rowIndex)
    {
        float y = TableTop - (rowIndex * RowHeight);

        if (usesTeams)
        {
            for (int i = 1; i <= rowIndex && i < visiblePlayerIds.Count; i++)
            {
                if (GetTeamSortKey(visiblePlayerIds[i - 1]) != GetTeamSortKey(visiblePlayerIds[i]))
                    y -= TeamGap;
            }
        }

        return y;
    }

    void BuildDividers(float tableLeft, float tableBottom)
    {
        if (dividerSprite == null)
            return;

        float tableHeight = GetTableHeight();
        float tableRight = tableLeft + TableWidth;

        for (float x = tableLeft; x <= tableRight + 0.1f; x += DividerStep)
        {
            CreateDivider(x, TableTop);
            CreateDivider(x, tableBottom);
        }

        for (float y = tableBottom; y <= TableTop + 0.1f; y += DividerStep)
        {
            CreateDivider(tableLeft, y);
            CreateDivider(tableRight, y);
        }

        for (int i = 1; i < visiblePlayerIds.Count; i++)
        {
            float y = GetRowTop(i);
            DrawHorizontalDivider(tableLeft, tableRight, y);

            if (usesTeams && GetTeamSortKey(visiblePlayerIds[i - 1]) != GetTeamSortKey(visiblePlayerIds[i]))
                DrawHorizontalDivider(tableLeft, tableRight, y + TeamGap);
        }
    }

    void DrawHorizontalDivider(float left, float right, float y)
    {
        for (float x = left; x <= right + 0.1f; x += DividerStep)
            CreateDivider(x, y);
    }

    void CreateDivider(float x, float y)
    {
        Image image = CreateImage("Divider", dividerSprite);
        ApplyLogicalRect(image.rectTransform, x, y, DividerSize, DividerSize, ScreenWidth, ScreenHeight);
    }

    void BuildPlayerRow(int playerId, int rowIndex, float tableLeft)
    {
        float rowTop = GetRowTop(rowIndex);
        float rowBottom = rowTop - RowHeight;
        float centerY = rowBottom + ((RowHeight - PortraitSize) * 0.5f) + RowSpriteYOffset + PortraitExtraYOffset;

        Image portrait = CreateImage("Player" + playerId + "Portrait", GetPortraitSprite(playerId));
        ApplyLogicalRect(portrait.rectTransform, tableLeft + 8f, centerY, PortraitSize, PortraitSize, ScreenWidth, ScreenHeight);

        RowUi row = new RowUi
        {
            CurrentWinCount = GetCurrentWinCount(playerId),
            IsWinner = IsWinningPlayerRow(playerId),
            Trophies = new Image[MaxVisibleTrophies]
        };

        row.PreviousWinCount = Mathf.Clamp(row.CurrentWinCount - (row.IsWinner ? 1 : 0), 0, targetVictories);

        float trophiesLeft = tableLeft + 48f;
        float trophyY = rowBottom + ((RowHeight - TrophyHeight) * 0.5f) + RowSpriteYOffset;

        for (int i = 0; i < MaxVisibleTrophies; i++)
        {
            Image trophy = CreateImage("Player" + playerId + "Trophy" + (i + 1), trophySprite);
            ApplyLogicalRect(trophy.rectTransform, trophiesLeft + (i * TrophyStep), trophyY, TrophyWidth, TrophyHeight, ScreenWidth, ScreenHeight);
            trophy.enabled = i < row.PreviousWinCount && i < targetVictories;
            row.Trophies[i] = trophy;
        }

        rowsByPlayerId[playerId] = row;
    }

    bool IsWinningPlayerRow(int playerId)
    {
        if (!GameSession.IsValidPlayerId(winnerPlayerId))
            return false;

        if (!usesTeams || BattleModeRules.Instance == null)
            return playerId == winnerPlayerId;

        return BattleModeRules.Instance.GetTeamForPlayer(playerId) == BattleModeRules.Instance.GetTeamForPlayer(winnerPlayerId);
    }

    int GetCurrentWinCount(int playerId)
    {
        if (GameSession.Instance == null)
            return 0;

        return Mathf.Clamp(GameSession.Instance.GetBattleMatchWins(playerId), 0, targetVictories);
    }

    Sprite GetPortraitSprite(int playerId)
    {
        BomberSkin skin = PlayerPersistentStats.Get(playerId).Skin;
        int portraitIndex = GetPortraitIndex(skin);

        if (portraits.TryGetValue(portraitIndex, out Sprite sprite))
            return sprite;

        return null;
    }

    static int GetPortraitIndex(BomberSkin skin)
    {
        switch (skin)
        {
            case BomberSkin.White: return 0;
            case BomberSkin.Black: return 1;
            case BomberSkin.Red: return 2;
            case BomberSkin.Blue: return 3;
            case BomberSkin.Green: return 4;
            case BomberSkin.Yellow: return 5;
            case BomberSkin.Pink: return 6;
            case BomberSkin.Aqua: return 7;
            case BomberSkin.Orange: return 8;
            case BomberSkin.Purple: return 9;
            case BomberSkin.Gray: return 10;
            case BomberSkin.Olive: return 11;
            case BomberSkin.DarkGreen: return 12;
            case BomberSkin.Cyan: return 13;
            case BomberSkin.DarkBlue: return 14;
            case BomberSkin.Brown: return 15;
            case BomberSkin.Magenta: return 16;
            case BomberSkin.Nightmare: return 17;
            case BomberSkin.Gold: return 18;
            case BomberSkin.Golden: return 19;
            default: return 0;
        }
    }

    Image CreateImage(string childName, Sprite sprite)
    {
        GameObject go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(runtimeRoot, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = sprite;
        image.preserveAspect = false;
        image.color = Color.white;
        return image;
    }

    static void ApplyLogicalRect(
        RectTransform rect,
        float left,
        float bottom,
        float width,
        float height,
        float logicalParentWidth,
        float logicalParentHeight)
    {
        if (rect == null || logicalParentWidth <= 0f || logicalParentHeight <= 0f)
            return;

        rect.anchorMin = new Vector2(left / logicalParentWidth, bottom / logicalParentHeight);
        rect.anchorMax = new Vector2((left + width) / logicalParentWidth, (bottom + height) / logicalParentHeight);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static float GetLogicalSpriteWidth(Sprite sprite, float targetHeight)
    {
        if (sprite == null || sprite.rect.height <= 0f)
            return targetHeight;

        return Mathf.Round(sprite.rect.width * (targetHeight / sprite.rect.height));
    }

    static int CompareSpritesByNumericSuffix(Sprite left, Sprite right)
    {
        int leftIndex = TryParseSpriteNumericSuffix(left != null ? left.name : string.Empty, out int parsedLeft)
            ? parsedLeft
            : 0;
        int rightIndex = TryParseSpriteNumericSuffix(right != null ? right.name : string.Empty, out int parsedRight)
            ? parsedRight
            : 0;

        return leftIndex.CompareTo(rightIndex);
    }

    static bool TryParseSpriteNumericSuffix(string spriteName, out int index)
    {
        index = 0;

        if (string.IsNullOrEmpty(spriteName))
            return false;

        int underscoreIndex = spriteName.LastIndexOf('_');
        if (underscoreIndex < 0 || underscoreIndex >= spriteName.Length - 1)
            return false;

        return int.TryParse(spriteName.Substring(underscoreIndex + 1), out index);
    }
}
