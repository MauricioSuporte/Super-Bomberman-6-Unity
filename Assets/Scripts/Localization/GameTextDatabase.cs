public static class GameTextDatabase
{
    public static TitleScreenText Title => GetTitleScreenText(SaveSystem.GetLanguage());

    public static TitleScreenText GetTitleScreenText(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Japanese => JapaneseTitle,
            GameLanguage.Spanish => SpanishTitle,
            GameLanguage.PortugueseBr => PortugueseBrTitle,
            _ => EnglishTitle
        };
    }

    public static string GetLanguageNativeName(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Japanese => "日本語",
            GameLanguage.Spanish => "Español",
            GameLanguage.PortugueseBr => "Português (BR)",
            _ => "English"
        };
    }

    public static readonly GameLanguage[] SupportedLanguages =
    {
        GameLanguage.English,
        GameLanguage.Japanese,
        GameLanguage.Spanish,
        GameLanguage.PortugueseBr
    };

    private static readonly TitleScreenText EnglishTitle = new()
    {
        GameModes = "GAME MODES",
        Achievements = "ACHIEVEMENTS",
        Options = "OPTIONS",
        Exit = "EXIT",
        NormalGame = "NORMAL GAME",
        BossRush = "BOSS RUSH",
        BattleMode = "BATTLE MODE",
        Controls = "CONTROLS",
        Language = "LANGUAGE",
        Video = "VIDEO",
        ResetSave = "RESET SAVE",
        Player1 = "1 PLAYER",
        Player2 = "2 PLAYERS",
        Player3 = "3 PLAYERS",
        Player4 = "4 PLAYERS",
        Fullscreen = "FULLSCREEN",
        WindowSize = "WINDOW SIZE",
        On = "ON",
        Off = "OFF",
        ResetWarning = "THIS WILL ERASE:",
        ResetNormalSaves = "ALL NORMAL GAME SAVES",
        ResetUnlocks = "UNLOCKED SKINS / MODES",
        ResetBossRushRecords = "BOSS RUSH RECORDS",
        ResetControls = "CONTROLS",
        Cancel = "CANCEL",
        PushStart = "PUSH START BUTTON",
        BossRushLocked = "UNLOCKED BY CLEARING ALL STAGES",
        SaveDataErased = "SAVE DATA ERASED"
    };

    private static readonly TitleScreenText JapaneseTitle = new()
    {
        GameModes = "ゲームモード",
        Achievements = "実績",
        Options = "オプション",
        Exit = "終了",
        NormalGame = "ノーマルゲーム",
        BossRush = "ボスラッシュ",
        BattleMode = "バトルモード",
        Controls = "操作設定",
        Language = "言語",
        Video = "ビデオ",
        ResetSave = "セーブ消去",
        Player1 = "1人プレイ",
        Player2 = "2人プレイ",
        Player3 = "3人プレイ",
        Player4 = "4人プレイ",
        Fullscreen = "フルスクリーン",
        WindowSize = "ウィンドウサイズ",
        On = "オン",
        Off = "オフ",
        ResetWarning = "消去されます:",
        ResetNormalSaves = "通常ゲームのセーブ",
        ResetUnlocks = "解放スキン / モード",
        ResetBossRushRecords = "ボスラッシュ記録",
        ResetControls = "操作設定",
        Cancel = "キャンセル",
        PushStart = "スタートボタンをおしてください",
        BossRushLocked = "全ステージクリアで解放",
        SaveDataErased = "セーブデータを消去しました"
    };

    private static readonly TitleScreenText SpanishTitle = new()
    {
        GameModes = "MODOS DE JUEGO",
        Achievements = "LOGROS",
        Options = "OPCIONES",
        Exit = "SALIR",
        NormalGame = "JUEGO NORMAL",
        BossRush = "JEFE RUSH",
        BattleMode = "MODO BATALLA",
        Controls = "CONTROLES",
        Language = "IDIOMA",
        Video = "VIDEO",
        ResetSave = "BORRAR PARTIDA",
        Player1 = "1 JUGADOR",
        Player2 = "2 JUGADORES",
        Player3 = "3 JUGADORES",
        Player4 = "4 JUGADORES",
        Fullscreen = "PANTALLA COMPLETA",
        WindowSize = "TAMAÑO VENTANA",
        On = "SÍ",
        Off = "NO",
        ResetWarning = "ESTO BORRARÁ:",
        ResetNormalSaves = "PARTIDAS DEL MODO NORMAL",
        ResetUnlocks = "SKINS / MODOS DESBLOQUEADOS",
        ResetBossRushRecords = "RÉCORDS DE JEFE RUSH",
        ResetControls = "CONTROLES",
        Cancel = "CANCELAR",
        PushStart = "PULSA START",
        BossRushLocked = "SE DESBLOQUEA AL COMPLETAR TODO",
        SaveDataErased = "DATOS BORRADOS"
    };

    private static readonly TitleScreenText PortugueseBrTitle = new()
    {
        GameModes = "MODOS DE JOGO",
        Achievements = "CONQUISTAS",
        Options = "OPÇÕES",
        Exit = "SAIR",
        NormalGame = "JOGO NORMAL",
        BossRush = "CORRIDA DE CHEFES",
        BattleMode = "MODO BATALHA",
        Controls = "CONTROLES",
        Language = "IDIOMA",
        Video = "VIDEO",
        ResetSave = "APAGAR SAVE",
        Player1 = "1 JOGADOR",
        Player2 = "2 JOGADORES",
        Player3 = "3 JOGADORES",
        Player4 = "4 JOGADORES",
        Fullscreen = "TELA CHEIA",
        WindowSize = "TAMANHO DA JANELA",
        On = "SIM",
        Off = "NÃO",
        ResetWarning = "ISTO VAI APAGAR:",
        ResetNormalSaves = "SAVES DO JOGO NORMAL",
        ResetUnlocks = "SKINS / MODOS LIBERADOS",
        ResetBossRushRecords = "RECORDES DE CHEFES",
        ResetControls = "CONTROLES",
        Cancel = "CANCELAR",
        PushStart = "APERTE START",
        BossRushLocked = "LIBERADO AO COMPLETAR TODAS AS FASES",
        SaveDataErased = "SAVE APAGADO"
    };
}

public sealed class TitleScreenText
{
    public string GameModes;
    public string Achievements;
    public string Options;
    public string Exit;
    public string NormalGame;
    public string BossRush;
    public string BattleMode;
    public string Controls;
    public string Language;
    public string Video;
    public string ResetSave;
    public string Player1;
    public string Player2;
    public string Player3;
    public string Player4;
    public string Fullscreen;
    public string WindowSize;
    public string On;
    public string Off;
    public string ResetWarning;
    public string ResetNormalSaves;
    public string ResetUnlocks;
    public string ResetBossRushRecords;
    public string ResetControls;
    public string Cancel;
    public string PushStart;
    public string BossRushLocked;
    public string SaveDataErased;
}
