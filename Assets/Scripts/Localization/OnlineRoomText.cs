public static partial class GameTextDatabase
{
    public static OnlineRoomText Online => SaveSystem.GetLanguage() switch
    {
        GameLanguage.Japanese => JapaneseOnlineRoom,
        GameLanguage.Spanish => SpanishOnlineRoom,
        GameLanguage.PortugueseBr => PortugueseOnlineRoom,
        _ => EnglishOnlineRoom
    };

    static readonly OnlineRoomText EnglishOnlineRoom = new("ONLINE", "OFFLINE", "ONLINE ROOM", "BATTLE MODE", "CREATE ROOM", "JOIN", "HOST IP", "LEAVE ROOM", "CONFIGURE MATCH", "CONNECTING...", "WAITING FOR PLAYERS", "YOU: P{0}", "EMPTY", "THE HOST IS CONFIGURING THE MATCH", "P{0} IS CHOOSING", "CONFIRMED", "AT LEAST TWO ONLINE PLAYERS ARE REQUIRED", "ROOM FULL OR MATCH ALREADY STARTED", "RULES", "ITEMS", "LOUIES", "HANDICAP");
    static readonly OnlineRoomText JapaneseOnlineRoom = new("オンライン", "オフライン", "オンラインルーム", "バトルモード", "ルーム作成", "参加", "ホストIP", "ルームを退出", "対戦設定", "接続中...", "プレイヤーを待っています", "あなた: P{0}", "空き", "ホストが対戦を設定中です", "P{0}が選択中", "決定済み", "オンラインプレイヤーが2人以上必要です", "満員または対戦開始済み", "ルール", "アイテム", "ルーイ", "ハンデ");
    static readonly OnlineRoomText SpanishOnlineRoom = new("ONLINE", "OFFLINE", "SALA ONLINE", "MODO BATALLA", "CREAR SALA", "ENTRAR", "IP DEL HOST", "SALIR DE LA SALA", "CONFIGURAR PARTIDA", "CONECTANDO...", "ESPERANDO JUGADORES", "TÚ: P{0}", "VACÍO", "EL HOST ESTÁ CONFIGURANDO LA PARTIDA", "P{0} ESTÁ ELIGIENDO", "CONFIRMADO", "SE NECESITAN DOS JUGADORES ONLINE", "SALA LLENA O PARTIDA INICIADA", "REGLAS", "OBJETOS", "LOUIES", "HÁNDICAP");
    static readonly OnlineRoomText PortugueseOnlineRoom = new("ONLINE", "OFFLINE", "SALA ONLINE", "MODO BATALHA", "CRIAR SALA", "ENTRAR", "IP DO HOST", "SAIR DA SALA", "CONFIGURAR PARTIDA", "CONECTANDO...", "AGUARDANDO JOGADORES", "VOCÊ: P{0}", "VAZIO", "O HOST ESTÁ CONFIGURANDO A PARTIDA", "P{0} ESTÁ ESCOLHENDO", "CONFIRMADO", "SÃO NECESSÁRIOS DOIS JOGADORES ONLINE", "SALA CHEIA OU PARTIDA INICIADA", "REGRAS", "ITENS", "LOUIES", "HANDICAP");
}

public sealed class OnlineRoomText
{
    public readonly string Online, Offline, Room, BattleMode, Host, Join, Address, Leave, Configure,
        Connecting, Waiting, You, Empty, HostConfiguring, Choosing, Confirmed, NeedPlayers, Unavailable,
        Rules, Items, Louies, Handicap;
    public string PrivateRoom => LanguageText("PRIVATE", "非公開", "PRIVADA", "PRIVADA");
    public string Password => LanguageText("PASSWORD (6 DIGITS)", "パスワード（数字6桁）", "CLAVE (6 DÍGITOS)", "SENHA (6 DÍGITOS)");
    public string PasswordHint => LanguageText("LEFT/RIGHT: DIGIT · UP/DOWN: CHANGE\nA: CONFIRM · B: BACK", "左右: 桁 · 上下: 数字\nA: 決定 · B: 戻る", "IZQ/DER: DÍGITO · ARR/ABA: CAMBIAR\nA: CONFIRMAR · B: VOLVER", "ESQ/DIR: DÍGITO · CIMA/BAIXO: ALTERAR\nA: CONFIRMAR · B: VOLTAR");
    public string WrongPassword => LanguageText("INCORRECT PASSWORD", "パスワードが違います", "CLAVE INCORRECTA", "SENHA INCORRETA");
    public string Back => LanguageText("BACK", "戻る", "VOLVER", "VOLTAR");
    public string AvailableRooms => LanguageText("AVAILABLE ROOMS", "参加できるルーム", "SALAS DISPONIBLES", "SALAS DISPONÍVEIS");
    public string NoRooms => LanguageText("SEARCHING · NO ROOMS FOUND", "検索中 · ルームが見つかりません", "BUSCANDO · NO HAY SALAS", "BUSCANDO · NENHUMA SALA ENCONTRADA");
    public string SelectRoom => LanguageText("SELECT A ROOM TO JOIN", "ルームを選んで参加", "ELIGE UNA SALA PARA ENTRAR", "SELECIONE UMA SALA PARA ENTRAR");
    public string LocalRooms => LanguageText("LAN", "LAN", "LAN", "LAN");
    public string PublicRooms => LanguageText("PUBLIC", "公開", "PÚBLICA", "PÚBLICA");
    public string RefreshRooms => LanguageText("REFRESH ROOMS", "ルームを更新", "ACTUALIZAR SALAS", "ATUALIZAR SALAS");
    public string CreatePublic => LanguageText("VISIBILITY: PUBLIC", "公開範囲: 公開", "VISIBILIDAD: PÚBLICA", "VISIBILIDADE: PÚBLICA");
    public string CreateLocal => LanguageText("VISIBILITY: LAN", "公開範囲: LAN", "VISIBILIDAD: LAN", "VISIBILIDADE: LAN");
    public string PublicNotConfigured => LanguageText("PUBLIC DIRECTORY NOT CONFIGURED", "公開リスト未設定", "LISTA PÚBLICA SIN CONFIGURAR", "LISTA PÚBLICA NÃO CONFIGURADA");
    public string PublicUnavailable => LanguageText("PUBLIC DIRECTORY UNAVAILABLE", "公開リストに接続できません", "LISTA PÚBLICA NO DISPONIBLE", "LISTA PÚBLICA INDISPONÍVEL");
    public string LanUnavailable => LanguageText("LAN DISCOVERY UNAVAILABLE", "LAN検索を利用できません", "BÚSQUEDA LAN NO DISPONIBLE", "BUSCA LAN INDISPONÍVEL");
    public string DirectJoin => LanguageText("OR JOIN BY HOST IP", "またはホストIPで参加", "O ENTRA POR IP DEL HOST", "OU ENTRE PELO IP DO HOST");
    public string VersionMismatch => LanguageText("INCOMPATIBLE VERSION: UPDATE THE GAME", "バージョンが異なります。ゲームを更新してください", "VERSIÓN INCOMPATIBLE: ACTUALIZA EL JUEGO", "VERSÃO INCOMPATÍVEL: ATUALIZE O JOGO");
    public string RoomAdmin => LanguageText("ROOM ADMIN: P{0}", "ルーム管理者: P{0}", "ADMINISTRADOR: P{0}", "ADMINISTRADOR: P{0}");

    public string RunCompleted => LanguageText("RUN COMPLETE!", "クリア！", "¡PARTIDA COMPLETADA!", "PARTIDA CONCLUÍDA!");
    public string StartGame => LanguageText("START ONLINE GAME", "オンラインゲーム開始", "INICIAR PARTIDA ONLINE", "INICIAR PARTIDA ONLINE");
    public string LobbyTimeout => LanguageText("START WITHIN {0} OR THE ROOM CLOSES", "{0}以内に開始しないと終了します", "INICIA EN {0} O SE CIERRA LA SALA", "INICIE EM {0} OU A SALA SERÁ ENCERRADA");
    public string IdleTimeout => LanguageText("NO INPUT: ROOM CLOSES IN {0}", "操作なし: あと{0}で終了", "SIN ACTIVIDAD: CIERRE EN {0}", "SEM INPUTS: SALA ENCERRA EM {0}");
    public string ModeName(Assets.Scripts.Netcode.OnlineRoomMode mode) => mode switch
    {
        Assets.Scripts.Netcode.OnlineRoomMode.NormalGame => LanguageText("NORMAL GAME", "ノーマルゲーム", "JUEGO NORMAL", "JOGO NORMAL"),
        Assets.Scripts.Netcode.OnlineRoomMode.BossRush => LanguageText("BOSS RUSH", "ボスラッシュ", "BOSS RUSH", "BOSS RUSH"),
        _ => BattleMode
    };

    static string LanguageText(string en, string ja, string es, string pt) => SaveSystem.GetLanguage() switch
    {
        GameLanguage.Japanese => ja,
        GameLanguage.Spanish => es,
        GameLanguage.PortugueseBr => pt,
        _ => en
    };
    public OnlineRoomText(string online, string offline, string room, string battleMode, string host,
        string join, string address, string leave, string configure, string connecting, string waiting,
        string you, string empty, string hostConfiguring, string choosing, string confirmed,
        string needPlayers, string unavailable, string rules, string items, string louies, string handicap)
    {
        Online = online; Offline = offline; Room = room; BattleMode = battleMode; Host = host;
        Join = join; Address = address; Leave = leave; Configure = configure; Connecting = connecting;
        Waiting = waiting; You = you; Empty = empty; HostConfiguring = hostConfiguring;
        Choosing = choosing; Confirmed = confirmed; NeedPlayers = needPlayers; Unavailable = unavailable;
        Rules = rules; Items = items; Louies = louies; Handicap = handicap;
    }
}
