public static class GameTextDatabase
{
    public static TitleScreenText Title => GetTitleScreenText(SaveSystem.GetLanguage());
    public static ControlsMenuText Controls => GetControlsMenuText(SaveSystem.GetLanguage());

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

    public static ControlsMenuText GetControlsMenuText(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Japanese => JapaneseControls,
            GameLanguage.Spanish => SpanishControls,
            GameLanguage.PortugueseBr => PortugueseBrControls,
            _ => EnglishControls
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
        PushStart = "PRECIONE  START",
        BossRushLocked = "LIBERADO AO COMPLETAR TODAS AS FASES",
        SaveDataErased = "SAVE APAGADO"
    };

    private static readonly ControlsMenuText EnglishControls = new()
    {
        Title = "CONTROLS",
        ChoosePlayer = "CHOOSE A PLAYER",
        Player = "PLAYER",
        On = "ON",
        Off = "OFF",
        Yes = "YES",
        No = "NO",
        MapControls = "MAP CONTROLS",
        TogglePlayer = "TOGGLE PLAYER",
        RestoreDefaultKeys = "RESTORE DEFAULT KEYS",
        Return = "RETURN",
        Confirm = "CONFIRM",
        Cancel = "CANCEL",
        RestoreDefaultKeysQuestion = "RESTORE DEFAULT KEYS?",
        ConfiguringPlayer = "CONFIGURING PLAYER",
        PressControlsYouWant = "PRESS THE CONTROLS YOU WANT",
        ToUseForThisPlayer = "TO USE FOR THIS PLAYER.",
        PressAnyKeyOrButton = "PRESS ANY KEY OR BUTTON",
        ToStartMapping = "TO START MAPPING",
        EscBToCancel = "ESC / B TO CANCEL",
        EscToCancel = "ESC TO CANCEL",
        ChooseButtonFor = "CHOOSE A BUTTON FOR:",
        ConfirmPlaceBomb = "CONFIRM / PLACE BOMB",
        ReturnExplodeControlBomb = "RETURN / EXPLODE CONTROL BOMB",
        RestoreDefaultKeysAbilities = "RESTORE DEFAULT KEYS / ABILITIES",
        Dismount = "DISMOUNT",
        StopKickedBombs = "STOP KICKED BOMBS",
        Riding = "RIDING",
        PlayerActivationUnavailable = "PLAYER ACTIVATION IS UNAVAILABLE.",
        KeyAlreadyInUse = "KEY {0} IS ALREADY IN USE!",
        PleasePressAnotherKey = "PLEASE PRESS ANOTHER KEY.",
        MoveUp = "UP",
        MoveDown = "DOWN",
        MoveLeft = "LEFT",
        MoveRight = "RIGHT",
        Start = "START",
        ActionA = "A",
        ActionB = "B",
        ActionC = "C",
        ActionL = "L",
        ActionR = "R"
    };

    private static readonly ControlsMenuText JapaneseControls = new()
    {
        Title = "操作設定",
        ChoosePlayer = "プレイヤーを選択",
        Player = "プレイヤー",
        On = "オン",
        Off = "オフ",
        Yes = "はい",
        No = "いいえ",
        MapControls = "操作を設定",
        TogglePlayer = "プレイヤー切替",
        RestoreDefaultKeys = "初期設定に戻す",
        Return = "戻る",
        Confirm = "決定",
        Cancel = "キャンセル",
        RestoreDefaultKeysQuestion = "初期設定に戻しますか?",
        ConfiguringPlayer = "設定中 プレイヤー",
        PressControlsYouWant = "使いたい操作を押してください",
        ToUseForThisPlayer = "このプレイヤーに使用します。",
        PressAnyKeyOrButton = "キーかボタンを押す",
        ToStartMapping = "割り当てを開始",
        EscBToCancel = "ESC / B でキャンセル",
        EscToCancel = "ESC でキャンセル",
        ChooseButtonFor = "ボタンを選択:",
        ConfirmPlaceBomb = "決定 / 爆弾を置く",
        ReturnExplodeControlBomb = "戻る / リモコン爆弾を爆破",
        RestoreDefaultKeysAbilities = "初期設定に戻す / 特殊能力",
        Dismount = "降りる",
        StopKickedBombs = "蹴った爆弾を止める",
        Riding = "乗車中",
        PlayerActivationUnavailable = "プレイヤー切替は使用できません。",
        KeyAlreadyInUse = "キー {0} は使用中です!",
        PleasePressAnotherKey = "別のキーを押してください。",
        MoveUp = "上",
        MoveDown = "下",
        MoveLeft = "左",
        MoveRight = "右",
        Start = "スタート",
        ActionA = "A",
        ActionB = "B",
        ActionC = "C",
        ActionL = "L",
        ActionR = "R"
    };

    private static readonly ControlsMenuText SpanishControls = new()
    {
        Title = "CONTROLES",
        ChoosePlayer = "ELIGE UN JUGADOR",
        Player = "JUGADOR",
        On = "SÍ",
        Off = "NO",
        Yes = "SÍ",
        No = "NO",
        MapControls = "ASIGNAR CONTROLES",
        TogglePlayer = "ACTIVAR JUGADOR",
        RestoreDefaultKeys = "RESTAURAR TECLAS",
        Return = "VOLVER",
        Confirm = "CONFIRMAR",
        Cancel = "CANCELAR",
        RestoreDefaultKeysQuestion = "¿RESTAURAR TECLAS?",
        ConfiguringPlayer = "CONFIGURANDO JUGADOR",
        PressControlsYouWant = "PULSA LOS CONTROLES QUE QUIERAS",
        ToUseForThisPlayer = "USAR CON ESTE JUGADOR.",
        PressAnyKeyOrButton = "PULSA UNA TECLA O BOTÓN",
        ToStartMapping = "PARA EMPEZAR",
        EscBToCancel = "ESC / B PARA CANCELAR",
        EscToCancel = "ESC PARA CANCELAR",
        ChooseButtonFor = "ELIGE UN BOTÓN PARA:",
        ConfirmPlaceBomb = "CONFIRMAR / PONER BOMBA",
        ReturnExplodeControlBomb = "VOLVER / EXPLOTAR BOMBA CONTROL",
        RestoreDefaultKeysAbilities = "RESTAURAR TECLAS / HABILIDADES",
        Dismount = "BAJAR",
        StopKickedBombs = "DETENER BOMBAS PATEADAS",
        Riding = "MONTADO",
        PlayerActivationUnavailable = "NO SE PUEDE ACTIVAR JUGADORES.",
        KeyAlreadyInUse = "¡LA TECLA {0} YA ESTÁ EN USO!",
        PleasePressAnotherKey = "PULSA OTRA TECLA.",
        MoveUp = "ARRIBA",
        MoveDown = "ABAJO",
        MoveLeft = "IZQUIERDA",
        MoveRight = "DERECHA",
        Start = "START",
        ActionA = "A",
        ActionB = "B",
        ActionC = "C",
        ActionL = "L",
        ActionR = "R"
    };

    private static readonly ControlsMenuText PortugueseBrControls = new()
    {
        Title = "CONTROLES",
        ChoosePlayer = "ESCOLHA UM JOGADOR",
        Player = "JOGADOR",
        On = "SIM",
        Off = "NÃO",
        Yes = "SIM",
        No = "NÃO",
        MapControls = "MAPEAR CONTROLES",
        TogglePlayer = "ATIVAR JOGADOR",
        RestoreDefaultKeys = "RESTAURAR TECLAS",
        Return = "VOLTAR",
        Confirm = "CONFIRMAR",
        Cancel = "CANCELAR",
        RestoreDefaultKeysQuestion = "RESTAURAR TECLAS?",
        ConfiguringPlayer = "CONFIGURANDO JOGADOR",
        PressControlsYouWant = "APERTE OS CONTROLES QUE DESEJA",
        ToUseForThisPlayer = "USAR COM ESTE JOGADOR.",
        PressAnyKeyOrButton = "APERTE UMA TECLA OU BOTÃO",
        ToStartMapping = "PARA COMEÇAR",
        EscBToCancel = "ESC / B PARA CANCELAR",
        EscToCancel = "ESC PARA CANCELAR",
        ChooseButtonFor = "ESCOLHA UM BOTÃO PARA:",
        ConfirmPlaceBomb = "CONFIRMAR / COLOCAR BOMBA",
        ReturnExplodeControlBomb = "VOLTAR / EXPLODIR BOMBA CONTROLE",
        RestoreDefaultKeysAbilities = "RESTAURAR TECLAS / HABILIDADES",
        Dismount = "DESCER",
        StopKickedBombs = "PARAR BOMBAS CHUTADAS",
        Riding = "MONTADO",
        PlayerActivationUnavailable = "ATIVAÇÃO DE JOGADOR INDISPONÍVEL.",
        KeyAlreadyInUse = "TECLA {0} JÁ ESTÁ EM USO!",
        PleasePressAnotherKey = "APERTE OUTRA TECLA.",
        MoveUp = "CIMA",
        MoveDown = "BAIXO",
        MoveLeft = "ESQUERDA",
        MoveRight = "DIREITA",
        Start = "START",
        ActionA = "A",
        ActionB = "B",
        ActionC = "C",
        ActionL = "L",
        ActionR = "R"
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

public sealed class ControlsMenuText
{
    public string Title;
    public string ChoosePlayer;
    public string Player;
    public string On;
    public string Off;
    public string Yes;
    public string No;
    public string MapControls;
    public string TogglePlayer;
    public string RestoreDefaultKeys;
    public string Return;
    public string Confirm;
    public string Cancel;
    public string RestoreDefaultKeysQuestion;
    public string ConfiguringPlayer;
    public string PressControlsYouWant;
    public string ToUseForThisPlayer;
    public string PressAnyKeyOrButton;
    public string ToStartMapping;
    public string EscBToCancel;
    public string EscToCancel;
    public string ChooseButtonFor;
    public string ConfirmPlaceBomb;
    public string ReturnExplodeControlBomb;
    public string RestoreDefaultKeysAbilities;
    public string Dismount;
    public string StopKickedBombs;
    public string Riding;
    public string PlayerActivationUnavailable;
    public string KeyAlreadyInUse;
    public string PleasePressAnotherKey;
    public string MoveUp;
    public string MoveDown;
    public string MoveLeft;
    public string MoveRight;
    public string Start;
    public string ActionA;
    public string ActionB;
    public string ActionC;
    public string ActionL;
    public string ActionR;
}
