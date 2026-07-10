using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class BattleRoundWinScoreboardPresenter : MonoBehaviour
{
    const int MaxPlayers = GameSession.MaxPlayerId;
    const int MaxTableRows = GameSession.MaxPlayerId;
    const int MaxVisibleTrophies = 5;
    const float ScreenWidth = 256f;
    const float ScreenHeight = 224f;
    const float DividerSize = 7f;
    const float DividerStep = 8f;
    const float TableWidth = 192f;
    const float TableTop = 180f;
    const float RowHeight = 24f;
    const float PortraitSize = 28f;
    const float TrophyWidth = 26f;
    const float TrophyHeight = 23f;
    const float TrophyStep = 26f;
    const float RowSpriteYOffset = 3f;
    const float PortraitExtraYOffset = 8f;
    const float TrophyExtraYOffset = 2f;
    const float ScoreboardWidth = 158f;
    const float ScoreboardHeight = 16f;
    const float ScoreboardBottom = 198f;
    const string RuntimeRootName = "__RoundWinScoreboardRuntime";
    const string ScoreboardChildName = "Scoreboard";
    const string DividerChildName = "Divisor";
    const string TrophyChildName = "Trophy";
    const string DividerResourcesPath = "HUD/RoundWin/Divisor";
    const string PortraitsResourcesPath = "HUD/PortraitBombersLive";

    readonly List<int> activePlayerIds = new(MaxPlayers);
    readonly List<int> visiblePlayerIds = new(MaxPlayers);
    readonly Dictionary<int, Sprite> portraits = new();
    readonly Dictionary<int, RowUi> rowsByPlayerId = new();

    RectTransform rootRect;
    RectTransform runtimeRoot;
    RectTransform scoreboardRect;
    RectTransform dividerTemplateRect;
    RectTransform trophyTemplateRect;
    AnimatedSpriteRenderer scoreboardAnimator;
    AnimatedSpriteRenderer dividerTemplateAnimator;
    AnimatedSpriteRenderer trophyTemplateAnimator;
    Sprite dividerSprite;
    bool spritesLoaded;
    int winnerPlayerId;
    bool usesTeams;
    int targetVictories = 3;

    sealed class RowUi
    {
        public TrophyUi[] Trophies;
        public int PreviousWinCount;
        public int CurrentWinCount;
        public bool IsWinner;
    }

    sealed class TrophyUi
    {
        public GameObject Root;
        public AnimatedSpriteRenderer Animator;
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
                ShowTrophy(row.Trophies[i], animate: i >= row.PreviousWinCount);
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
        ConfigureDividerTemplate();
        ConfigureTrophyTemplate();
        BuildTable();
    }

    void BuildBackground()
    {
        Image background = CreateImage("Background", null);
        background.color = Color.black;
        ApplyLogicalRect(background.rectTransform, 0f, 0f, ScreenWidth, ScreenHeight, ScreenWidth, ScreenHeight);
        background.rectTransform.SetAsFirstSibling();
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
    }

    void ConfigureDividerTemplate()
    {
        if (dividerTemplateRect == null)
        {
            Transform child = transform.Find(DividerChildName);
            dividerTemplateRect = child as RectTransform;
        }

        if (dividerTemplateRect == null)
            return;

        dividerTemplateAnimator = dividerTemplateRect.GetComponent<AnimatedSpriteRenderer>();
        dividerTemplateRect.gameObject.SetActive(false);
    }

    void ConfigureTrophyTemplate()
    {
        if (trophyTemplateRect == null)
        {
            Transform child = transform.Find(TrophyChildName);
            trophyTemplateRect = child as RectTransform;
        }

        if (trophyTemplateRect == null)
            return;

        trophyTemplateAnimator = trophyTemplateRect.GetComponent<AnimatedSpriteRenderer>();
        trophyTemplateRect.gameObject.SetActive(false);
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
        return TableTop - GetTableHeight();
    }

    float GetTableHeight()
    {
        return MaxTableRows * RowHeight;
    }

    float GetRowSpacing()
    {
        int playerCount = Mathf.Max(1, visiblePlayerIds.Count);
        return GetTableHeight() / playerCount;
    }

    float GetRowTop(int rowIndex)
    {
        return TableTop - (rowIndex * GetRowSpacing());
    }

    void BuildDividers(float tableLeft, float tableBottom)
    {
        if (dividerSprite == null && dividerTemplateRect == null)
            return;

        float tableRight = tableLeft + TableWidth;
        int sequenceIndex = 0;

        for (float x = tableLeft; x <= tableRight + 0.1f; x += DividerStep)
        {
            CreateDivider(x, TableTop, sequenceIndex++);
            CreateDivider(x, tableBottom, sequenceIndex++);
        }

        for (float y = tableBottom; y <= TableTop + 0.1f; y += DividerStep)
        {
            CreateDivider(tableLeft, y, sequenceIndex++);
            CreateDivider(tableRight, y, sequenceIndex++);
        }

        for (int i = 1; i < visiblePlayerIds.Count; i++)
        {
            bool separatesTeams = usesTeams && GetTeamSortKey(visiblePlayerIds[i - 1]) != GetTeamSortKey(visiblePlayerIds[i]);

            if (!usesTeams || separatesTeams)
                DrawHorizontalDivider(tableLeft, tableRight, ref sequenceIndex, GetRowTop(i));
        }
    }

    void DrawHorizontalDivider(float left, float right, ref int sequenceIndex, float y)
    {
        for (float x = left; x <= right + 0.1f; x += DividerStep)
            CreateDivider(x, y, sequenceIndex++);
    }

    void CreateDivider(float x, float y, int sequenceIndex)
    {
        if (dividerTemplateRect != null && dividerTemplateAnimator != null)
        {
            CreateAnimatedDivider(x, y, sequenceIndex);
            return;
        }

        Image image = CreateImage("Divider", dividerSprite);
        ApplyLogicalRect(image.rectTransform, x, y, DividerSize, DividerSize, ScreenWidth, ScreenHeight);
    }

    void CreateAnimatedDivider(float x, float y, int sequenceIndex)
    {
        GameObject go = Instantiate(dividerTemplateRect.gameObject, runtimeRoot, false);
        go.name = "Divider";
        go.SetActive(false);

        RectTransform rect = go.GetComponent<RectTransform>();
        Image image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();

        image.raycastTarget = false;
        image.preserveAspect = false;

        float centerX = x + (DividerSize * 0.5f) - (ScreenWidth * 0.5f);
        float centerY = y + (DividerSize * 0.5f) - (ScreenHeight * 0.5f);

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(DividerSize, DividerSize);
        rect.anchoredPosition = new Vector2(centerX, centerY);
        rect.localScale = Vector3.one;

        AnimatedSpriteRenderer animator = go.GetComponent<AnimatedSpriteRenderer>();
        if (animator != null)
        {
            animator.enabled = false;
            int frameCount = animator.animationSprite != null && animator.animationSprite.Length > 0
                ? animator.animationSprite.Length
                : 1;
            animator.CurrentFrame = Mathf.Abs(sequenceIndex) % frameCount;
            animator.SetExternalBaseLocalPosition(new Vector3(centerX, centerY, 0f));
        }

        go.SetActive(true);

        if (animator != null)
        {
            animator.enabled = true;
            animator.CurrentFrame = Mathf.Abs(sequenceIndex) % Mathf.Max(1, animator.animationSprite != null ? animator.animationSprite.Length : 1);
            animator.SetExternalBaseLocalPosition(new Vector3(centerX, centerY, 0f));
            animator.RefreshFrame();
        }
    }

    void BuildPlayerRow(int playerId, int rowIndex, float tableLeft)
    {
        float rowTop = GetRowTop(rowIndex);
        float rowBottom = GetRowTop(rowIndex + 1);
        float rowHeight = rowTop - rowBottom;

        float portraitY = rowBottom + ((rowHeight - PortraitSize) * 0.5f) + RowSpriteYOffset + PortraitExtraYOffset;

        Image portrait = CreateImage("Player" + playerId + "Portrait", GetPortraitSprite(playerId));
        ApplyLogicalRect(portrait.rectTransform, tableLeft + 8f, portraitY, PortraitSize, PortraitSize, ScreenWidth, ScreenHeight);

        if (usesTeams && !IsFirstPlayerInTeam(rowIndex))
            return;

        RowUi row = new RowUi
        {
            CurrentWinCount = GetCurrentWinCount(playerId),
            IsWinner = IsWinningPlayerRow(playerId),
            Trophies = new TrophyUi[MaxVisibleTrophies]
        };

        row.PreviousWinCount = Mathf.Clamp(row.CurrentWinCount - (row.IsWinner ? 1 : 0), 0, targetVictories);

        float trophiesLeft = tableLeft + 48f;
        float trophyY = GetTrophyBottomForRowGroup(rowIndex);

        for (int i = 0; i < MaxVisibleTrophies; i++)
        {
            TrophyUi trophy = CreateTrophy("Player" + playerId + "Trophy" + (i + 1), trophiesLeft + (i * TrophyStep), trophyY);

            if (i < row.PreviousWinCount && i < targetVictories)
                ShowTrophy(trophy, animate: false);

            row.Trophies[i] = trophy;
        }

        rowsByPlayerId[playerId] = row;
    }

    bool IsFirstPlayerInTeam(int rowIndex)
    {
        if (!usesTeams || rowIndex <= 0 || rowIndex >= visiblePlayerIds.Count)
            return true;

        return GetTeamSortKey(visiblePlayerIds[rowIndex - 1]) != GetTeamSortKey(visiblePlayerIds[rowIndex]);
    }

    float GetTrophyBottomForRowGroup(int rowIndex)
    {
        if (!usesTeams)
        {
            float rowTop = GetRowTop(rowIndex);
            float rowBottom = GetRowTop(rowIndex + 1);
            float rowHeight = rowTop - rowBottom;

            return rowBottom + ((rowHeight - TrophyHeight) * 0.5f) + RowSpriteYOffset + TrophyExtraYOffset;
        }

        int team = GetTeamSortKey(visiblePlayerIds[rowIndex]);
        int lastRowIndex = rowIndex;

        for (int i = rowIndex + 1; i < visiblePlayerIds.Count; i++)
        {
            if (GetTeamSortKey(visiblePlayerIds[i]) != team)
                break;

            lastRowIndex = i;
        }

        float groupTop = GetRowTop(rowIndex);
        float groupBottom = GetRowTop(lastRowIndex + 1);
        float groupCenterY = groupBottom + ((groupTop - groupBottom) * 0.5f);

        return groupCenterY - (TrophyHeight * 0.5f) + RowSpriteYOffset + TrophyExtraYOffset;
    }

    TrophyUi CreateTrophy(string childName, float left, float bottom)
    {
        if (trophyTemplateRect != null && trophyTemplateAnimator != null)
        {
            GameObject go = Instantiate(trophyTemplateRect.gameObject, runtimeRoot, false);
            go.name = childName;
            go.SetActive(false);

            RectTransform rect = go.GetComponent<RectTransform>();
            Image image = go.GetComponent<Image>();
            if (image == null)
                image = go.AddComponent<Image>();

            image.raycastTarget = false;
            image.preserveAspect = false;

            float centerX = left + (TrophyWidth * 0.5f) - (ScreenWidth * 0.5f);
            float centerY = bottom + (TrophyHeight * 0.5f) - (ScreenHeight * 0.5f);

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(TrophyWidth, TrophyHeight);
            rect.anchoredPosition = new Vector2(centerX, centerY);
            rect.localScale = Vector3.one;

            AnimatedSpriteRenderer animator = go.GetComponent<AnimatedSpriteRenderer>();
            if (animator != null)
            {
                animator.loop = false;
                animator.idle = false;
                animator.SetExternalBaseLocalPosition(new Vector3(centerX, centerY, 0f));
            }

            return new TrophyUi
            {
                Root = go,
                Animator = animator
            };
        }

        Image fallback = CreateImage(childName, null);
        ApplyLogicalRect(fallback.rectTransform, left, bottom, TrophyWidth, TrophyHeight, ScreenWidth, ScreenHeight);
        fallback.gameObject.SetActive(false);

        return new TrophyUi { Root = fallback.gameObject };
    }

    void ShowTrophy(TrophyUi trophy, bool animate)
    {
        if (trophy == null || trophy.Root == null)
            return;

        trophy.Root.SetActive(true);

        if (trophy.Animator == null)
            return;

        int frameCount = trophy.Animator.animationSprite != null && trophy.Animator.animationSprite.Length > 0
            ? trophy.Animator.animationSprite.Length
            : 1;

        trophy.Animator.loop = false;
        trophy.Animator.idle = false;
        trophy.Animator.SetFrozen(false);

        if (animate)
        {
            trophy.Animator.CurrentFrame = 0;
            trophy.Animator.RefreshFrame();
            return;
        }

        trophy.Animator.CurrentFrame = frameCount - 1;
        trophy.Animator.RefreshFrame();
        trophy.Animator.SetFrozen(true);
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
        PlayerPersistentStats.PlayerState stats = PlayerPersistentStats.Get(playerId);
        int expressionIndex = IsWinningPlayerRow(playerId)
            ? HudCharacterPortraitCatalog.VictoryExpression
            : HudCharacterPortraitCatalog.TimeUpExpression;
        Sprite generatedPortrait = HudCharacterPortraitCatalog.Load(
            stats.Character,
            stats.Skin,
            expressionIndex);
        if (generatedPortrait != null)
            return generatedPortrait;

        BomberSkin skin = stats.Skin;
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
