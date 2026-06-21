public static class GameTextDatabase
{
    public static TitleScreenText Title => GetTitleScreenText(SaveSystem.GetLanguage());
    public static ControlsMenuText Controls => GetControlsMenuText(SaveSystem.GetLanguage());
    public static UnlockText Unlocks => GetUnlockText(SaveSystem.GetLanguage());
    public static CommonMenuText Common => GetCommonMenuText(SaveSystem.GetLanguage());
    public static PauseMenuText Pause => GetPauseMenuText(SaveSystem.GetLanguage());
    public static SaveFileMenuText SaveFile => GetSaveFileMenuText(SaveSystem.GetLanguage());
    public static AchievementMenuText AchievementsMenu => GetAchievementMenuText(SaveSystem.GetLanguage());
    public static CreditsText Credits => GetCreditsText(SaveSystem.GetLanguage());
    public static WorldMapText WorldMap => GetWorldMapText(SaveSystem.GetLanguage());
    public static BossRushMenuText BossRushMenu => GetBossRushMenuText(SaveSystem.GetLanguage());
    public static BattleModeMenuText BattleModeMenu => GetBattleModeMenuText(SaveSystem.GetLanguage());

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

    public static UnlockText GetUnlockText(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Japanese => JapaneseUnlocks,
            GameLanguage.Spanish => SpanishUnlocks,
            GameLanguage.PortugueseBr => PortugueseBrUnlocks,
            _ => EnglishUnlocks
        };
    }

    public static CommonMenuText GetCommonMenuText(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Japanese => JapaneseCommon,
            GameLanguage.Spanish => SpanishCommon,
            GameLanguage.PortugueseBr => PortugueseBrCommon,
            _ => EnglishCommon
        };
    }

    public static PauseMenuText GetPauseMenuText(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Japanese => JapanesePause,
            GameLanguage.Spanish => SpanishPause,
            GameLanguage.PortugueseBr => PortugueseBrPause,
            _ => EnglishPause
        };
    }

    public static SaveFileMenuText GetSaveFileMenuText(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Japanese => JapaneseSaveFile,
            GameLanguage.Spanish => SpanishSaveFile,
            GameLanguage.PortugueseBr => PortugueseBrSaveFile,
            _ => EnglishSaveFile
        };
    }

    public static AchievementMenuText GetAchievementMenuText(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Japanese => JapaneseAchievementMenu,
            GameLanguage.Spanish => SpanishAchievementMenu,
            GameLanguage.PortugueseBr => PortugueseBrAchievementMenu,
            _ => EnglishAchievementMenu
        };
    }

    public static CreditsText GetCreditsText(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Japanese => JapaneseCredits,
            GameLanguage.Spanish => SpanishCredits,
            GameLanguage.PortugueseBr => PortugueseBrCredits,
            _ => EnglishCredits
        };
    }

    public static WorldMapText GetWorldMapText(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Japanese => JapaneseWorldMap,
            GameLanguage.Spanish => SpanishWorldMap,
            GameLanguage.PortugueseBr => PortugueseBrWorldMap,
            _ => EnglishWorldMap
        };
    }

    public static BossRushMenuText GetBossRushMenuText(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Japanese => JapaneseBossRushMenu,
            GameLanguage.Spanish => SpanishBossRushMenu,
            GameLanguage.PortugueseBr => PortugueseBrBossRushMenu,
            _ => EnglishBossRushMenu
        };
    }

    public static BattleModeMenuText GetBattleModeMenuText(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Japanese => JapaneseBattleModeMenu,
            GameLanguage.Spanish => SpanishBattleModeMenu,
            GameLanguage.PortugueseBr => PortugueseBrBattleModeMenu,
            _ => EnglishBattleModeMenu
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

    private static readonly CommonMenuText EnglishCommon = new()
    {
        Yes = "YES",
        No = "NO",
        On = "ON",
        Off = "OFF",
        Normal = "NORMAL",
        Hard = "HARD",
        Hardcore = "HARDCORE",
        Easy = "EASY",
        Nightmare = "NIGHTMARE",
        Infinite = "INFINITE",
        Random = "RANDOM",
        Min = "MIN",
        Max = "MAX",
        Stage = "STAGE {0}",
        BattleStage = "Battle Stage {0}",
        Locked = "LOCKED",
        Obtained = "OBTAINED",
        Unlocked = "Unlocked",
        Unlocks = "Unlocks"
    };

    private static readonly CommonMenuText JapaneseCommon = new()
    {
        Yes = "はい",
        No = "いいえ",
        On = "オン",
        Off = "オフ",
        Normal = "ノーマル",
        Hard = "ハード",
        Hardcore = "ハードコア",
        Easy = "イージー",
        Nightmare = "ナイトメア",
        Infinite = "無限",
        Random = "ランダム",
        Min = "最小",
        Max = "最大",
        Stage = "ステージ {0}",
        BattleStage = "バトルステージ {0}",
        Locked = "未解放",
        Obtained = "獲得済み",
        Unlocked = "解放済み",
        Unlocks = "解放"
    };

    private static readonly CommonMenuText SpanishCommon = new()
    {
        Yes = "SÍ",
        No = "NO",
        On = "SÍ",
        Off = "NO",
        Normal = "NORMAL",
        Hard = "DIFÍCIL",
        Hardcore = "EXTREMO",
        Easy = "FÁCIL",
        Nightmare = "PESADILLA",
        Infinite = "INFINITO",
        Random = "ALEATORIO",
        Min = "MÍN",
        Max = "MÁX",
        Stage = "FASE {0}",
        BattleStage = "Fase Batalla {0}",
        Locked = "BLOQUEADO",
        Obtained = "OBTENIDO",
        Unlocked = "Desbloqueado",
        Unlocks = "Desbloquea"
    };

    private static readonly CommonMenuText PortugueseBrCommon = new()
    {
        Yes = "SIM",
        No = "NÃO",
        On = "SIM",
        Off = "NÃO",
        Normal = "NORMAL",
        Hard = "DIFÍCIL",
        Hardcore = "EXTREMO",
        Easy = "FÁCIL",
        Nightmare = "PESADELO",
        Infinite = "INFINITO",
        Random = "ALEATÓRIO",
        Min = "MÍN",
        Max = "MÁX",
        Stage = "FASE {0}",
        BattleStage = "Estágio Batalha {0}",
        Locked = "BLOQUEADO",
        Obtained = "OBTIDO",
        Unlocked = "Liberado",
        Unlocks = "Libera"
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
        TouchButtons = "TOUCH BUTTONS",
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
        BossRushLocked = "UNLOCKED BY CLEARING NORMAL GAME",
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
        TouchButtons = "TOUCH BUTTONS",
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
        BossRushLocked = "ノーマルゲームクリアで解放",
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
        TouchButtons = "BOTONES TOUCH",
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
        BossRushLocked = "SE DESBLOQUEA AL COMPLETAR JUEGO NORMAL",
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
        TouchButtons = "BOTÕES TOUCH",
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
        BossRushLocked = "LIBERADO AO COMPLETAR O JOGO NORMAL",
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

    private static readonly PauseMenuText EnglishPause = new()
    {
        Stage = "STAGE",
        Pause = "PAUSE!",
        Resume = "RESUME",
        RestartRound = "RESTART ROUND",
        ReturnToBossRush = "RETURN TO BOSS RUSH",
        ReturnToWorldMap = "RETURN TO WORLD MAP",
        ReturnToTitle = "RETURN TO TITLE",
        ReturnToStageSelect = "RETURN TO STAGE SELECT",
        ReturnToWorldMapQuestion = "Return to World Map?",
        ReturnToBossRushQuestion = "Return to Boss Rush?",
        ReturnToTitleQuestion = "Return to Title Screen?",
        RestartRoundQuestion = "Restart Round?",
        ReturnToStageSelectQuestion = "Return to Stage Select?"
    };

    private static readonly PauseMenuText JapanesePause = new()
    {
        Stage = "ステージ",
        Pause = "ポーズ!",
        Resume = "再開",
        RestartRound = "ラウンドやり直し",
        ReturnToBossRush = "ボスラッシュへ戻る",
        ReturnToWorldMap = "ワールドマップへ戻る",
        ReturnToTitle = "タイトルへ戻る",
        ReturnToStageSelect = "ステージ選択へ戻る",
        ReturnToWorldMapQuestion = "ワールドマップへ戻りますか?",
        ReturnToBossRushQuestion = "ボスラッシュへ戻りますか?",
        ReturnToTitleQuestion = "タイトル画面へ戻りますか?",
        RestartRoundQuestion = "ラウンドをやり直しますか?",
        ReturnToStageSelectQuestion = "ステージ選択へ戻りますか?"
    };

    private static readonly PauseMenuText SpanishPause = new()
    {
        Stage = "FASE",
        Pause = "PAUSA!",
        Resume = "CONTINUAR",
        RestartRound = "REINICIAR RONDA",
        ReturnToBossRush = "VOLVER A JEFE RUSH",
        ReturnToWorldMap = "VOLVER AL MAPA",
        ReturnToTitle = "VOLVER AL TÍTULO",
        ReturnToStageSelect = "VOLVER A FASES",
        ReturnToWorldMapQuestion = "¿Volver al mapa?",
        ReturnToBossRushQuestion = "¿Volver a Jefe Rush?",
        ReturnToTitleQuestion = "¿Volver a la pantalla de título?",
        RestartRoundQuestion = "¿Reiniciar ronda?",
        ReturnToStageSelectQuestion = "¿Volver a seleccionar fase?"
    };

    private static readonly PauseMenuText PortugueseBrPause = new()
    {
        Stage = "FASE",
        Pause = "PAUSA!",
        Resume = "CONTINUAR",
        RestartRound = "REINICIAR RODADA",
        ReturnToBossRush = "VOLTAR AOS CHEFES",
        ReturnToWorldMap = "VOLTAR AO MAPA",
        ReturnToTitle = "VOLTAR AO TÍTULO",
        ReturnToStageSelect = "VOLTAR ÀS FASES",
        ReturnToWorldMapQuestion = "Voltar para o Mapa?",
        ReturnToBossRushQuestion = "Voltar para a Corrida de Chefes?",
        ReturnToTitleQuestion = "Voltar para a Tela de Título?",
        RestartRoundQuestion = "Reiniciar rodada?",
        ReturnToStageSelectQuestion = "Voltar para a seleção de fase?"
    };

    private static readonly SaveFileMenuText EnglishSaveFile = new()
    {
        MainPrompt = "NORMAL GAME",
        NewGamePrompt = "START WHICH FILE?",
        DifficultyPrompt = "SELECT DIFFICULTY",
        ContinuePrompt = "CONTINUE WHICH FILE?",
        DeletePrompt = "DELETE WHICH FILE?",
        SlotLabelPrefix = "File ",
        EmptySlot = "---",
        NewGame = "New Game",
        Continue = "Continue",
        DeleteFile = "Delete File",
        NoEmptySlot = "NO EMPTY SLOT",
        NoSaveData = "NO SAVE DATA",
        HardcoreLocked = "CLEAR NORMAL GAME ON HARD"
    };

    private static readonly SaveFileMenuText JapaneseSaveFile = new()
    {
        MainPrompt = "ノーマルゲーム",
        NewGamePrompt = "どのファイルではじめますか?",
        DifficultyPrompt = "難易度を選択",
        ContinuePrompt = "どのファイルを続けますか?",
        DeletePrompt = "どのファイルを消しますか?",
        SlotLabelPrefix = "ファイル ",
        EmptySlot = "---",
        NewGame = "はじめから",
        Continue = "つづきから",
        DeleteFile = "ファイル消去",
        NoEmptySlot = "空きファイルがありません",
        NoSaveData = "セーブデータがありません",
        HardcoreLocked = "ハードでノーマルゲームをクリア"
    };

    private static readonly SaveFileMenuText SpanishSaveFile = new()
    {
        MainPrompt = "JUEGO NORMAL",
        NewGamePrompt = "¿QUÉ ARCHIVO INICIAR?",
        DifficultyPrompt = "SELECCIONA DIFICULTAD",
        ContinuePrompt = "¿QUÉ ARCHIVO CONTINUAR?",
        DeletePrompt = "¿QUÉ ARCHIVO BORRAR?",
        SlotLabelPrefix = "Archivo ",
        EmptySlot = "---",
        NewGame = "Nuevo Juego",
        Continue = "Continuar",
        DeleteFile = "Borrar Archivo",
        NoEmptySlot = "NO HAY ARCHIVO VACÍO",
        NoSaveData = "NO HAY DATOS",
        HardcoreLocked = "COMPLETA JUEGO NORMAL EN DIFÍCIL"
    };

    private static readonly SaveFileMenuText PortugueseBrSaveFile = new()
    {
        MainPrompt = "JOGO NORMAL",
        NewGamePrompt = "COMEÇAR EM QUAL ARQUIVO?",
        DifficultyPrompt = "SELECIONE A DIFICULDADE",
        ContinuePrompt = "CONTINUAR QUAL ARQUIVO?",
        DeletePrompt = "APAGAR QUAL ARQUIVO?",
        SlotLabelPrefix = "Arquivo ",
        EmptySlot = "---",
        NewGame = "Novo Jogo",
        Continue = "Continuar",
        DeleteFile = "Apagar Arquivo",
        NoEmptySlot = "NENHUM ARQUIVO VAZIO",
        NoSaveData = "NENHUM SAVE",
        HardcoreLocked = "TERMINE O JOGO NORMAL NO DIFÍCIL"
    };

    private static readonly AchievementMenuText EnglishAchievementMenu = new()
    {
        Progress = "Unlocked: {0} of {1}",
        DetailState = "{0} - {1}",
        RewardLine = "{0}: {1}"
    };

    private static readonly AchievementMenuText JapaneseAchievementMenu = new()
    {
        Progress = "解放済み: {0} / {1}",
        DetailState = "{0} - {1}",
        RewardLine = "{0}: {1}"
    };

    private static readonly AchievementMenuText SpanishAchievementMenu = new()
    {
        Progress = "Desbloqueados: {0} de {1}",
        DetailState = "{0} - {1}",
        RewardLine = "{0}: {1}"
    };

    private static readonly AchievementMenuText PortugueseBrAchievementMenu = new()
    {
        Progress = "Liberadas: {0} de {1}",
        DetailState = "{0} - {1}",
        RewardLine = "{0}: {1}"
    };

    private static readonly CreditsText EnglishCredits = new()
    {
        DemoComplete = "DEMO 4 COMPLETE!",
        OpenSourceProject = "OPEN SOURCE PROJECT",
        PressStart = "PRESS START",
        ReturnToTitle = "TO RETURN TO TITLE SCREEN",
        StageCompletion = "STAGE COMPLETION",
        AchievementsUnlocked = "ACHIEVEMENTS UNLOCKED",
        Of = "OF",
        Tribute = "Tribute to Bomberman",
        Coding = "Coding",
        SpriteContribution = "Sprite Contribution",
        PlaytestingFeedback = "Playtesting/Feedback",
        SoundsMusics = "Sounds/Musics",
        BaseOfTheGame = "Base of the Game"
    };

    private static readonly CreditsText JapaneseCredits = new()
    {
        DemoComplete = "デモ4 クリア!",
        OpenSourceProject = "オープンソースプロジェクト",
        PressStart = "STARTを押す",
        ReturnToTitle = "タイトル画面へ戻る",
        StageCompletion = "ステージ達成率",
        AchievementsUnlocked = "実績解放",
        Of = "/",
        Tribute = "Bombermanへのトリビュート",
        Coding = "プログラム",
        SpriteContribution = "スプライト協力",
        PlaytestingFeedback = "テストプレイ/フィードバック",
        SoundsMusics = "サウンド/音楽",
        BaseOfTheGame = "ゲームのベース"
    };

    private static readonly CreditsText SpanishCredits = new()
    {
        DemoComplete = "¡DEMO 4 COMPLETA!",
        OpenSourceProject = "PROYECTO DE CÓDIGO ABIERTO",
        PressStart = "PULSA START",
        ReturnToTitle = "PARA VOLVER AL TÍTULO",
        StageCompletion = "PROGRESO DE FASES",
        AchievementsUnlocked = "LOGROS DESBLOQUEADOS",
        Of = "DE",
        Tribute = "Tributo a Bomberman",
        Coding = "Programación",
        SpriteContribution = "Contribución de Sprites",
        PlaytestingFeedback = "Pruebas/Comentarios",
        SoundsMusics = "Sonidos/Músicas",
        BaseOfTheGame = "Base del Juego"
    };

    private static readonly CreditsText PortugueseBrCredits = new()
    {
        DemoComplete = "DEMO 4 COMPLETA!",
        OpenSourceProject = "PROJETO OPEN SOURCE",
        PressStart = "APERTE START",
        ReturnToTitle = "PARA VOLTAR À TELA DE TÍTULO",
        StageCompletion = "PROGRESSO DAS FASES",
        AchievementsUnlocked = "CONQUISTAS LIBERADAS",
        Of = "DE",
        Tribute = "Tributo a Bomberman",
        Coding = "Programação",
        SpriteContribution = "Contribuição de Sprites",
        PlaytestingFeedback = "Testes/Feedback",
        SoundsMusics = "Sons/Músicas",
        BaseOfTheGame = "Base do Jogo"
    };

    private static readonly WorldMapText EnglishWorldMap = new() { WorldPrefix = "WORLD " };
    private static readonly WorldMapText JapaneseWorldMap = new() { WorldPrefix = "ワールド " };
    private static readonly WorldMapText SpanishWorldMap = new() { WorldPrefix = "MUNDO " };
    private static readonly WorldMapText PortugueseBrWorldMap = new() { WorldPrefix = "MUNDO " };

    private static readonly BossRushMenuText EnglishBossRushMenu = new()
    {
        Target = "TARGET {0}",
        NewRecord = "NEW RECORD!",
        NightmareLocked = "UNLOCKED BY CLEARING HARD"
    };

    private static readonly BossRushMenuText JapaneseBossRushMenu = new()
    {
        Target = "目標 {0}",
        NewRecord = "新記録!",
        NightmareLocked = "ハードクリアで解放"
    };

    private static readonly BossRushMenuText SpanishBossRushMenu = new()
    {
        Target = "META {0}",
        NewRecord = "¡NUEVO RÉCORD!",
        NightmareLocked = "SE DESBLOQUEA AL COMPLETAR DIFÍCIL"
    };

    private static readonly BossRushMenuText PortugueseBrBossRushMenu = new()
    {
        Target = "META {0}",
        NewRecord = "NOVO RECORDE!",
        NightmareLocked = "LIBERADO AO COMPLETAR NO DIFÍCIL"
    };

    private static readonly BattleModeMenuText EnglishBattleModeMenu = new()
    {
        BattleMode = "BATTLE MODE",
        SingleMatch = "Single Match",
        TagMatch = "Tag Match",
        PlayerSelect = "PLAYER SELECT",
        CharacterSelect = "CHARACTER SELECT",
        TeamMembers = "TEAM MEMBERS",
        RuleConfig = "RULE CONFIG",
        StageSelect = "STAGE SELECT",
        SpecificSettings = "SPECIFIC SETTINGS",
        SelectMusic = "SELECT MUSIC",
        SelectItems = "SELECT ITEMS",
        SelectHandicap = "SELECT HANDICAP",
        SelectLouies = "SELECT LOUIES",
        Items = "Items",
        Handicap = "Handicap",
        Louies = "Louies",
        Music = "Music",
        Start = "Start",
        ComputerLevel = "Computer Level",
        BattlesToWin = "Battles To Win",
        TimeLimit = "Time Limit",
        SuddenDeath = "Sudden Death",
        RevengeBomber = "Revenge Bomber",
        Easy = "Easy",
        Normal = "Normal",
        Hard = "Hard",
        Man = "MAN",
        Com = "COM",
        TeamRed = "Red",
        TeamBlue = "Blue",
        TeamGreen = "Green",
        ChooseChangeBack = "←→↑↓: Choose\nA/C: Change Number\nB: Back"
    };

    private static readonly BattleModeMenuText JapaneseBattleModeMenu = new()
    {
        BattleMode = "バトルモード",
        SingleMatch = "シングルマッチ",
        TagMatch = "タッグマッチ",
        PlayerSelect = "プレイヤー選択",
        CharacterSelect = "キャラ選択",
        TeamMembers = "チームメンバー",
        RuleConfig = "ルール設定",
        StageSelect = "ステージ選択",
        SpecificSettings = "詳細設定",
        SelectMusic = "音楽選択",
        SelectItems = "アイテム選択",
        SelectHandicap = "ハンデ選択",
        SelectLouies = "ルーイ選択",
        Items = "アイテム",
        Handicap = "ハンデ",
        Louies = "ルーイ",
        Music = "音楽",
        Start = "スタート",
        ComputerLevel = "CPUレベル",
        BattlesToWin = "勝利数",
        TimeLimit = "制限時間",
        SuddenDeath = "サドンデス",
        RevengeBomber = "リベンジボンバー",
        Easy = "イージー",
        Normal = "ノーマル",
        Hard = "ハード",
        Man = "人間",
        Com = "CPU",
        TeamRed = "赤",
        TeamBlue = "青",
        TeamGreen = "緑",
        ChooseChangeBack = "←→↑↓: 選択\nA/C: 数を変更\nB: 戻る"
    };

    private static readonly BattleModeMenuText SpanishBattleModeMenu = new()
    {
        BattleMode = "MODO BATALLA",
        SingleMatch = "Combate Individual",
        TagMatch = "Combate por Equipos",
        PlayerSelect = "SELECCIÓN DE JUGADOR",
        CharacterSelect = "SELECCIÓN DE PERSONAJE",
        TeamMembers = "MIEMBROS DEL EQUIPO",
        RuleConfig = "CONFIGURAR REGLAS",
        StageSelect = "SELECCIÓN DE FASE",
        SpecificSettings = "AJUSTES ESPECÍFICOS",
        SelectMusic = "SELECCIONAR MÚSICA",
        SelectItems = "SELECCIONAR OBJETOS",
        SelectHandicap = "SELECCIONAR VENTAJA",
        SelectLouies = "SELECCIONAR LOUIES",
        Items = "Objetos",
        Handicap = "Ventaja",
        Louies = "Louies",
        Music = "Música",
        Start = "Iniciar",
        ComputerLevel = "Nivel CPU",
        BattlesToWin = "Victorias Necesarias",
        TimeLimit = "Límite de Tiempo",
        SuddenDeath = "Muerte Súbita",
        RevengeBomber = "Bomber Vengador",
        Easy = "Fácil",
        Normal = "Normal",
        Hard = "Difícil",
        Man = "JUG",
        Com = "CPU",
        TeamRed = "Rojo",
        TeamBlue = "Azul",
        TeamGreen = "Verde",
        ChooseChangeBack = "←→↑↓: Elegir\nA/C: Cambiar número\nB: Volver"
    };

    private static readonly BattleModeMenuText PortugueseBrBattleModeMenu = new()
    {
        BattleMode = "MODO BATALHA",
        SingleMatch = "Partida Solo",
        TagMatch = "Partida em Times",
        PlayerSelect = "SELEÇÃO DE JOGADOR",
        CharacterSelect = "SELEÇÃO DE PERSONAGEM",
        TeamMembers = "MEMBROS DO TIME",
        RuleConfig = "CONFIGURAR REGRAS",
        StageSelect = "SELEÇÃO DE FASE",
        SpecificSettings = "CONFIGURAÇÕES ESPECÍFICAS",
        SelectMusic = "SELECIONAR MÚSICA",
        SelectItems = "SELECIONAR ITENS",
        SelectHandicap = "SELECIONAR DESVANTAGENS",
        SelectLouies = "SELECIONAR LOUIES",
        Items = "Itens",
        Handicap = "Vantagens",
        Louies = "Louies",
        Music = "Música",
        Start = "Iniciar",
        ComputerLevel = "Nível do CPU",
        BattlesToWin = "Vitórias",
        TimeLimit = "Tempo Limite",
        SuddenDeath = "Morte Súbita",
        RevengeBomber = "Bomber Vingança",
        Easy = "Fácil",
        Normal = "Normal",
        Hard = "Difícil",
        Man = "JOG",
        Com = "CPU",
        TeamRed = "Vermelho",
        TeamBlue = "Azul",
        TeamGreen = "Verde",
        ChooseChangeBack = "←→↑↓: Escolher\nA/C: Mudar número\nB: Voltar"
    };

    private static readonly UnlockText EnglishUnlocks = new()
    {
        AchievementBossRush = "BOSS RUSH",
        AchievementBossRushNightmare = "BOSS RUSH NIGHTMARE",
        AchievementHardcore = "HARDCORE",
        AchievementBattleStage = "BATTLE STAGE {0}",
        AchievementGolden = "GOLDEN",
        HintCheatKode = "UNLOCKED BY A CHEAT \"KODE\"",
        HintClearNormalAny = "CLEAR NORMAL GAME ON ANY DIFFICULTY",
        HintClearNormalNormal = "CLEAR NORMAL GAME ON NORMAL DIFFICULTY",
        HintClearNormalHard = "CLEAR NORMAL GAME ON HARD DIFFICULTY",
        HintClearNormalHardcore = "CLEAR NORMAL GAME ON HARDCORE DIFFICULTY",
        HintClearBossRushEasy = "CLEAR BOSS RUSH ON EASY",
        HintClearBossRushNormal = "CLEAR BOSS RUSH ON NORMAL",
        HintClearBossRushHard = "CLEAR BOSS RUSH ON HARD",
        HintClearBossRushEasyUnder4 = "CLEAR BOSS RUSH ON EASY UNDER 4:00",
        HintClearBossRushNormalUnder4 = "CLEAR BOSS RUSH ON NORMAL UNDER 4:00",
        HintClearBossRushHardUnder5 = "CLEAR BOSS RUSH ON HARD UNDER 5:00",
        HintClearBossRushNightmare = "CLEAR BOSS RUSH ON NIGHTMARE",
        HintWinBattleStage10 = "WIN STAGE 10 IN BATTLE MODE",
        HintWinBattleStages7And9 = "WIN STAGES 7 AND 9 IN BATTLE MODE",
        HintWinAnyBattleStage = "WIN ANY STAGE IN BATTLE MODE",
        HintWin7BattleStages = "WIN 7 DIFFERENT STAGES IN BATTLE MODE",
        HintWinAllOtherBattleStages = "WIN ALL OTHER STAGES IN BATTLE MODE",
        HintUnlockAllOtherAchievements = "UNLOCK ALL OTHER ACHIEVEMENTS",
        RewardBossRush = "Boss Rush Mode",
        RewardBossRushNightmare = "Boss Rush Nightmare Difficulty",
        RewardHardcore = "Hardcore Difficulty",
        RewardBattleStage = "Battle Mode Stage {0}",
        RewardSkin = "{0} Bomber",
        ToastNewCharacter = "New Character",
        ToastSkinUnlocked = "{0} Bomber Unlocked",
        ToastBossRushTitle = "It’s Time To Duel!",
        ToastBossRushSubtitle = "Boss Rush Unlocked",
        ToastNightmareTitle = "Are You Good Enough?",
        ToastNightmareSubtitle = "Nightmare Difficulty Unlocked",
        ToastHardcoreTitle = "No More Second Chances",
        ToastHardcoreSubtitle = "Hardcore Difficulty Unlocked",
        ToastBattleStageTitle = "New Stage",
        ToastBattleStageSubtitle = "Battle Mode Stage {0} Unlocked"
    };

    private static readonly UnlockText JapaneseUnlocks = new()
    {
        AchievementBossRush = "ボスラッシュ",
        AchievementBossRushNightmare = "ボスラッシュ ナイトメア",
        AchievementHardcore = "ハードコア",
        AchievementBattleStage = "バトルステージ {0}",
        AchievementGolden = "ゴールデン",
        HintCheatKode = "チート「KODE」で解放",
        HintClearNormalAny = "いずれかの難易度でノーマルゲームをクリア",
        HintClearNormalNormal = "ノーマル難易度でノーマルゲームをクリア",
        HintClearNormalHard = "ハード難易度でノーマルゲームをクリア",
        HintClearNormalHardcore = "ハードコア難易度でノーマルゲームをクリア",
        HintClearBossRushEasy = "イージーでボスラッシュをクリア",
        HintClearBossRushNormal = "ノーマルでボスラッシュをクリア",
        HintClearBossRushHard = "ハードでボスラッシュをクリア",
        HintClearBossRushEasyUnder4 = "イージーのボスラッシュを4:00以内にクリア",
        HintClearBossRushNormalUnder4 = "ノーマルのボスラッシュを4:00以内にクリア",
        HintClearBossRushHardUnder5 = "ハードのボスラッシュを5:00以内にクリア",
        HintClearBossRushNightmare = "ナイトメアでボスラッシュをクリア",
        HintWinBattleStage10 = "バトルモードのステージ10で勝利",
        HintWinBattleStages7And9 = "バトルモードのステージ7と9で勝利",
        HintWinAnyBattleStage = "バトルモードの任意のステージで勝利",
        HintWin7BattleStages = "バトルモードで7種類のステージに勝利",
        HintWinAllOtherBattleStages = "他の全バトルステージに勝利",
        HintUnlockAllOtherAchievements = "他の全実績を解放",
        RewardBossRush = "ボスラッシュモード",
        RewardBossRushNightmare = "ボスラッシュ ナイトメア難易度",
        RewardHardcore = "ハードコア難易度",
        RewardBattleStage = "バトルモード ステージ {0}",
        RewardSkin = "{0} ボンバー",
        ToastNewCharacter = "新キャラクター",
        ToastSkinUnlocked = "{0} ボンバー解放",
        ToastBossRushTitle = "決闘の時間だ!",
        ToastBossRushSubtitle = "ボスラッシュ解放",
        ToastNightmareTitle = "君にできるか?",
        ToastNightmareSubtitle = "ナイトメア難易度解放",
        ToastHardcoreTitle = "もう後戻りはできない",
        ToastHardcoreSubtitle = "ハードコア難易度解放",
        ToastBattleStageTitle = "新ステージ",
        ToastBattleStageSubtitle = "バトルモード ステージ {0} 解放"
    };

    private static readonly UnlockText SpanishUnlocks = new()
    {
        AchievementBossRush = "JEFE RUSH",
        AchievementBossRushNightmare = "JEFE RUSH PESADILLA",
        AchievementHardcore = "EXTREMO",
        AchievementBattleStage = "ESCENARIO BATALLA {0}",
        AchievementGolden = "DORADO",
        HintCheatKode = "DESBLOQUEADO CON EL TRUCO \"KODE\"",
        HintClearNormalAny = "COMPLETA EL JUEGO NORMAL EN CUALQUIER DIFICULTAD",
        HintClearNormalNormal = "COMPLETA EL JUEGO NORMAL EN NORMAL",
        HintClearNormalHard = "COMPLETA EL JUEGO NORMAL EN DIFÍCIL",
        HintClearNormalHardcore = "COMPLETA EL JUEGO NORMAL EN EXTREMO",
        HintClearBossRushEasy = "COMPLETA JEFE RUSH EN FÁCIL",
        HintClearBossRushNormal = "COMPLETA JEFE RUSH EN NORMAL",
        HintClearBossRushHard = "COMPLETA JEFE RUSH EN DIFÍCIL",
        HintClearBossRushEasyUnder4 = "COMPLETA JEFE RUSH FÁCIL EN MENOS DE 4:00",
        HintClearBossRushNormalUnder4 = "COMPLETA JEFE RUSH NORMAL EN MENOS DE 4:00",
        HintClearBossRushHardUnder5 = "COMPLETA JEFE RUSH DIFÍCIL EN MENOS DE 5:00",
        HintClearBossRushNightmare = "COMPLETA JEFE RUSH EN PESADILLA",
        HintWinBattleStage10 = "GANA EL ESCENARIO 10 EN MODO BATALLA",
        HintWinBattleStages7And9 = "GANA LOS ESCENARIOS 7 Y 9 EN MODO BATALLA",
        HintWinAnyBattleStage = "GANA CUALQUIER ESCENARIO EN MODO BATALLA",
        HintWin7BattleStages = "GANA 7 ESCENARIOS DIFERENTES EN MODO BATALLA",
        HintWinAllOtherBattleStages = "GANA TODOS LOS DEMÁS ESCENARIOS EN MODO BATALLA",
        HintUnlockAllOtherAchievements = "DESBLOQUEA TODOS LOS DEMÁS LOGROS",
        RewardBossRush = "Modo Jefe Rush",
        RewardBossRushNightmare = "Dificultad Pesadilla de Jefe Rush",
        RewardHardcore = "Dificultad Extrema",
        RewardBattleStage = "Escenario {0} de Modo Batalla",
        RewardSkin = "Bomber {0}",
        ToastNewCharacter = "Nuevo personaje",
        ToastSkinUnlocked = "Bomber {0} desbloqueado",
        ToastBossRushTitle = "¡Hora del duelo!",
        ToastBossRushSubtitle = "Jefe Rush desbloqueado",
        ToastNightmareTitle = "¿Eres lo bastante bueno?",
        ToastNightmareSubtitle = "Dificultad Pesadilla desbloqueada",
        ToastHardcoreTitle = "No hay segundas oportunidades",
        ToastHardcoreSubtitle = "Dificultad Extrema desbloqueada",
        ToastBattleStageTitle = "Nuevo escenario",
        ToastBattleStageSubtitle = "Escenario {0} de Modo Batalla desbloqueado"
    };

    private static readonly UnlockText PortugueseBrUnlocks = new()
    {
        AchievementBossRush = "CORRIDA DE CHEFES",
        AchievementBossRushNightmare = "CORRIDA DE CHEFES PESADELO",
        AchievementHardcore = "EXTREMO",
        AchievementBattleStage = "ESTÁGIO BATALHA {0}",
        AchievementGolden = "DOURADO",
        HintCheatKode = "LIBERADO POR UM \"KODIGO\"",
        HintClearNormalAny = "COMPLETE O JOGO NORMAL EM QUALQUER DIFICULDADE",
        HintClearNormalNormal = "COMPLETE O JOGO NORMAL NO NORMAL",
        HintClearNormalHard = "COMPLETE O JOGO NORMAL NO DIFÍCIL",
        HintClearNormalHardcore = "COMPLETE O JOGO NORMAL NO EXTREMO",
        HintClearBossRushEasy = "COMPLETE A CORRIDA DE CHEFES NO FÁCIL",
        HintClearBossRushNormal = "COMPLETE A CORRIDA DE CHEFES NO NORMAL",
        HintClearBossRushHard = "COMPLETE A CORRIDA DE CHEFES NO DIFÍCIL",
        HintClearBossRushEasyUnder4 = "COMPLETE A CORRIDA DE CHEFES FÁCIL EM MENOS DE 4:00",
        HintClearBossRushNormalUnder4 = "COMPLETE A CORRIDA DE CHEFES NORMAL EM MENOS DE 4:00",
        HintClearBossRushHardUnder5 = "COMPLETE A CORRIDA DE CHEFES DIFÍCIL EM MENOS DE 5:00",
        HintClearBossRushNightmare = "COMPLETE A CORRIDA DE CHEFES NO PESADELO",
        HintWinBattleStage10 = "VENÇA O ESTÁGIO 10 NO MODO BATALHA",
        HintWinBattleStages7And9 = "VENÇA OS ESTÁGIOS 7 E 9 NO MODO BATALHA",
        HintWinAnyBattleStage = "VENÇA QUALQUER ESTÁGIO NO MODO BATALHA",
        HintWin7BattleStages = "VENÇA 7 ESTÁGIOS DIFERENTES NO MODO BATALHA",
        HintWinAllOtherBattleStages = "VENÇA TODOS OS OUTROS ESTÁGIOS NO MODO BATALHA",
        HintUnlockAllOtherAchievements = "LIBERE TODAS AS OUTRAS CONQUISTAS",
        RewardBossRush = "Modo Corrida de Chefes",
        RewardBossRushNightmare = "Dificuldade Pesadelo da Corrida de Chefes",
        RewardHardcore = "Dificuldade Extremo",
        RewardBattleStage = "Estágio {0} do Modo Batalha",
        RewardSkin = "Bomber {0}",
        ToastNewCharacter = "Novo personagem",
        ToastSkinUnlocked = "Bomber {0} liberado",
        ToastBossRushTitle = "Hora do duelo!",
        ToastBossRushSubtitle = "Corrida de Chefes liberada",
        ToastNightmareTitle = "Você é bom o bastante?",
        ToastNightmareSubtitle = "Dificuldade Pesadelo liberada",
        ToastHardcoreTitle = "Sem segundas chances",
        ToastHardcoreSubtitle = "Dificuldade Extremo liberada",
        ToastBattleStageTitle = "Novo estágio",
        ToastBattleStageSubtitle = "Estágio {0} do Modo Batalha liberado"
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
    public string TouchButtons;
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

public sealed class CommonMenuText
{
    public string Yes;
    public string No;
    public string On;
    public string Off;
    public string Normal;
    public string Hard;
    public string Hardcore;
    public string Easy;
    public string Nightmare;
    public string Infinite;
    public string Random;
    public string Min;
    public string Max;
    public string Stage;
    public string BattleStage;
    public string Locked;
    public string Obtained;
    public string Unlocked;
    public string Unlocks;
}

public sealed class PauseMenuText
{
    public string Stage;
    public string Pause;
    public string Resume;
    public string RestartRound;
    public string ReturnToBossRush;
    public string ReturnToWorldMap;
    public string ReturnToTitle;
    public string ReturnToStageSelect;
    public string ReturnToWorldMapQuestion;
    public string ReturnToBossRushQuestion;
    public string ReturnToTitleQuestion;
    public string RestartRoundQuestion;
    public string ReturnToStageSelectQuestion;
}

public sealed class SaveFileMenuText
{
    public string MainPrompt;
    public string NewGamePrompt;
    public string DifficultyPrompt;
    public string ContinuePrompt;
    public string DeletePrompt;
    public string SlotLabelPrefix;
    public string EmptySlot;
    public string NewGame;
    public string Continue;
    public string DeleteFile;
    public string NoEmptySlot;
    public string NoSaveData;
    public string HardcoreLocked;
}

public sealed class AchievementMenuText
{
    public string Progress;
    public string DetailState;
    public string RewardLine;
}

public sealed class CreditsText
{
    public string DemoComplete;
    public string OpenSourceProject;
    public string PressStart;
    public string ReturnToTitle;
    public string StageCompletion;
    public string AchievementsUnlocked;
    public string Of;
    public string Tribute;
    public string Coding;
    public string SpriteContribution;
    public string PlaytestingFeedback;
    public string SoundsMusics;
    public string BaseOfTheGame;
}

public sealed class WorldMapText
{
    public string WorldPrefix;
}

public sealed class BossRushMenuText
{
    public string Target;
    public string NewRecord;
    public string NightmareLocked;
}

public sealed class BattleModeMenuText
{
    public string BattleMode;
    public string SingleMatch;
    public string TagMatch;
    public string PlayerSelect;
    public string CharacterSelect;
    public string TeamMembers;
    public string RuleConfig;
    public string StageSelect;
    public string SpecificSettings;
    public string SelectMusic;
    public string SelectItems;
    public string SelectHandicap;
    public string SelectLouies;
    public string Items;
    public string Handicap;
    public string Louies;
    public string Music;
    public string Start;
    public string ComputerLevel;
    public string BattlesToWin;
    public string TimeLimit;
    public string SuddenDeath;
    public string RevengeBomber;
    public string Easy;
    public string Normal;
    public string Hard;
    public string Man;
    public string Com;
    public string TeamRed;
    public string TeamBlue;
    public string TeamGreen;
    public string ChooseChangeBack;
}

public sealed class UnlockText
{
    public string AchievementBossRush;
    public string AchievementBossRushNightmare;
    public string AchievementHardcore;
    public string AchievementBattleStage;
    public string AchievementGolden;
    public string HintCheatKode;
    public string HintClearNormalAny;
    public string HintClearNormalNormal;
    public string HintClearNormalHard;
    public string HintClearNormalHardcore;
    public string HintClearBossRushEasy;
    public string HintClearBossRushNormal;
    public string HintClearBossRushHard;
    public string HintClearBossRushEasyUnder4;
    public string HintClearBossRushNormalUnder4;
    public string HintClearBossRushHardUnder5;
    public string HintClearBossRushNightmare;
    public string HintWinBattleStage10;
    public string HintWinBattleStages7And9;
    public string HintWinAnyBattleStage;
    public string HintWin7BattleStages;
    public string HintWinAllOtherBattleStages;
    public string HintUnlockAllOtherAchievements;
    public string RewardBossRush;
    public string RewardBossRushNightmare;
    public string RewardHardcore;
    public string RewardBattleStage;
    public string RewardSkin;
    public string ToastNewCharacter;
    public string ToastSkinUnlocked;
    public string ToastBossRushTitle;
    public string ToastBossRushSubtitle;
    public string ToastNightmareTitle;
    public string ToastNightmareSubtitle;
    public string ToastHardcoreTitle;
    public string ToastHardcoreSubtitle;
    public string ToastBattleStageTitle;
    public string ToastBattleStageSubtitle;
}
