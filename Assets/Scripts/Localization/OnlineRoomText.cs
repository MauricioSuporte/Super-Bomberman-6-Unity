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
    public string ChatButton => LanguageText("CHAT", "チャット", "CHAT", "CHAT");
    public string ChatPublic => LanguageText("PUBLIC CHAT", "全体チャット", "CHAT GENERAL", "CHAT GERAL");
    public string ChatRoom => LanguageText("ROOM CHAT", "ルームチャット", "CHAT DE SALA", "CHAT DA SALA");
    public string ChatPresence(string name, bool joined, bool you) => you
        ? joined ? LanguageText("YOU JOINED THE ROOM.", "ルームに参加しました。", "HAS ENTRADO EN LA SALA.", "VOCÊ ENTROU NA SALA.")
            : LanguageText("YOU LEFT THE ROOM.", "ルームを退出しました。", "HAS SALIDO DE LA SALA.", "VOCÊ SAIU DA SALA.")
        : string.Format(joined ? LanguageText("{0} JOINED THE ROOM.", "{0}が参加しました。", "{0} HA ENTRADO EN LA SALA.", "{0} ENTROU NA SALA.")
            : LanguageText("{0} LEFT THE ROOM.", "{0}が退出しました。", "{0} HA SALIDO DE LA SALA.", "{0} SAIU DA SALA."), name);
    public string ChatUnavailable => LanguageText("MESSAGE NOT SENT. CHECK CONNECTION.", "送信できません。接続を確認してください", "MENSAJE NO ENVIADO. REVISA LA CONEXIÓN.", "MENSAGEM NÃO ENVIADA. VERIFIQUE A CONEXÃO.");
    public string NewRoom => LanguageText("NEW ROOM", "新しいルーム", "NUEVA SALA", "NOVA SALA");
    public string RoomName => LanguageText("ROOM NAME", "ルーム名", "NOMBRE DE SALA", "NOME DA SALA");
    public string RoomType => LanguageText("TYPE", "種類", "TIPO", "TIPO");
    public string RoomPassword => LanguageText("ROOM PASSWORD", "ルームのパスワード", "CLAVE DE SALA", "SENHA DA SALA");
    public string MaxPlayers => LanguageText("MAXIMUM PLAYERS", "最大プレイヤー数", "MÁXIMO DE JUGADORES", "MÁXIMO DE JOGADORES");
    public string NoPassword => LanguageText("NO PASSWORD", "パスワードなし", "SIN CLAVE", "SEM SENHA");
    public string Creator => LanguageText("CREATOR", "作成者", "CREADOR", "CRIADOR");
    public string NameColumn => LanguageText("NAME", "名前", "NOMBRE", "NOME");
    public string PlayersColumn => LanguageText("PLAYERS", "人数", "JUGADORES", "JOGADORES");
    public string PasswordColumn => LanguageText("PASS", "パス", "CLAVE", "SENHA");
    public string Yes => LanguageText("YES", "あり", "SÍ", "SIM");
    public string No => LanguageText("NO", "なし", "NO", "NÃO");
    public string EditName => LanguageText("EDIT NAME", "名前を変更", "CAMBIAR NOMBRE", "ALTERAR NOME");
    public string CurrentName => LanguageText("NAME", "名前", "NOMBRE", "NOME");
    public string ChangeNameRequired => LanguageText("SELECT EDIT NAME: NAME MISSING, INVALID OR IN USE", "名前が空欄・無効・使用中です。名前を変更してください", "ELIGE CAMBIAR NOMBRE: VACÍO, INVÁLIDO O EN USO", "SELECIONE ALTERAR NOME: VAZIO, INVÁLIDO OU EM USO");
    public string UpdateGame => LanguageText("UPDATE GAME", "ゲームを更新", "ACTUALIZAR JUEGO", "ATUALIZAR JOGO");
    public string MatchInProgress => LanguageText("IN MATCH", "対戦中", "EN PARTIDA", "EM PARTIDA");
    public string RoomFull => LanguageText("FULL", "満員", "LLENA", "CHEIA");
    public string RoomLimitReached => LanguageText("ROOM LIMIT REACHED", "ルーム数が上限です", "LÍMITE DE SALAS ALCANZADO", "LIMITE DE SALAS ATINGIDO");
    public string UpdateChecking => LanguageText("CHECKING UPDATE...", "更新を確認中...", "BUSCANDO ACTUALIZACIÓN...", "VERIFICANDO ATUALIZAÇÃO...");
    public string UpdateDownloading => LanguageText("DOWNLOADING", "ダウンロード中", "DESCARGANDO", "BAIXANDO");
    public string UpdateInstall => LanguageText("CONFIRM INSTALLATION ON ANDROID", "Androidでインストールを確認してください", "CONFIRMA LA INSTALACIÓN EN ANDROID", "CONFIRME A INSTALAÇÃO NO ANDROID");
    public string UpdatePermission => LanguageText("ALLOW INSTALL UNKNOWN APPS, RETURN AND UPDATE AGAIN", "不明なアプリのインストールを許可し、戻って再度更新してください", "PERMITE INSTALAR APPS DESCONOCIDAS, VUELVE E INTÉNTALO DE NUEVO", "PERMITA INSTALAR APPS DESCONHECIDOS, VOLTE E ATUALIZE NOVAMENTE");
    public string UpdateBuiltPlayer => LanguageText("RUN THE UPDATER IN THE BUILT GAME", "ビルドしたゲームから更新してください", "ACTUALIZA DESDE EL JUEGO COMPILADO", "EXECUTE O ATUALIZADOR NO JOGO COMPILADO");
    public string UpdateMissing => LanguageText("UPDATE PACKAGE OR HELPER UNAVAILABLE", "更新パッケージがありません", "PAQUETE O ACTUALIZADOR NO DISPONIBLE", "PACOTE OU ATUALIZADOR INDISPONÍVEL");
    public string UpdateFailed => LanguageText("UPDATE FAILED. TRY AGAIN.", "更新に失敗しました。再試行してください。", "ERROR AL ACTUALIZAR. INTÉNTALO DE NUEVO.", "FALHA AO ATUALIZAR. TENTE NOVAMENTE.");
    public string UpdateDismiss => LanguageText("TAP TO CLOSE / START / B", "タップで閉じる / START / B", "TOCA PARA CERRAR / START / B", "TOQUE PARA FECHAR / START / B");
    public string RunUpdater => LanguageText("RUN GAME UPDATER", "ゲーム更新を起動", "EJECUTAR ACTUALIZADOR", "EXECUTAR ATUALIZADOR");
    public string EnterName => LanguageText("YOUR NAME (1-16)", "名前（1～16文字）", "TU NOMBRE (1-16)", "SEU NOME (1-16)");
    public string NameInUse => LanguageText("NAME ALREADY IN USE IN THIS ROOM\nCHOOSE ANOTHER NAME", "この部屋では名前が使用中です\n別の名前を選んでください", "NOMBRE YA EN USO EN ESTA SALA\nELIGE OTRO NOMBRE", "NOME JÁ EM USO NESTA SALA\nESCOLHA OUTRO NOME");
    public string NameUnavailable => LanguageText("NAME EMPTY, INVALID OR ALREADY IN USE", "名前が空欄・無効・使用中です", "NOMBRE VACÍO, INVÁLIDO O EN USO", "NOME VAZIO, INVÁLIDO OU JÁ EM USO");
    public string NameHint => LanguageText("TYPE TEXT\nENTER: CONFIRM / ESC: BACK\nPAD: START / B", "文字を入力\nENTER: 決定 / ESC: 戻る\nパッド: START / B", "ESCRIBE\nENTER: CONFIRMAR / ESC: VOLVER\nMANDO: START / B", "DIGITE O TEXTO\nENTER: CONFIRMAR / ESC: VOLTAR\nCONTROLE: START / B");
    public string PrivateRoom => LanguageText("PRIVATE", "非公開", "PRIVADA", "PRIVADA");
    public string Password => LanguageText("PASSWORD (6 DIGITS)", "パスワード（数字6桁）", "CLAVE (6 DÍGITOS)", "SENHA (6 DÍGITOS)");
    public string PasswordHint => LanguageText("TYPE TEXT\nENTER: CONFIRM / ESC: BACK\nPAD: START / B", "文字を入力\nENTER: 決定 / ESC: 戻る\nパッド: START / B", "ESCRIBE\nENTER: CONFIRMAR / ESC: VOLVER\nMANDO: START / B", "DIGITE O TEXTO\nENTER: CONFIRMAR / ESC: VOLTAR\nCONTROLE: START / B");
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
    public string VersionMismatch => UnityEngine.Application.isEditor ? LanguageText(
        "EDITOR/SERVER PROTOCOL MISMATCH: WAIT FOR DEPLOY", "Editorとサーバーのプロトコルが異なります。デプロイを待ってください", "PROTOCOLO EDITOR/SERVIDOR DISTINTO: ESPERA EL DESPLIEGUE", "PROTOCOLO EDITOR/SERVIDOR DIFERENTE: AGUARDE O DEPLOY") : LanguageText("INCOMPATIBLE VERSION: UPDATE GAME", "バージョンが異なります。ゲームを更新してください", "VERSIÓN INCOMPATIBLE: ACTUALIZA EL JUEGO", "VERSÃO INCOMPATÍVEL: ATUALIZE O JOGO");
    public string YouJoined => LanguageText("YOU JOINED THE ROOM!", "ルームに参加しました！", "¡HAS ENTRADO EN LA SALA!", "VOCÊ ENTROU NA SALA!");
    public string StartingMatch => LanguageText("Starting the match!", "対戦を開始します！", "¡Comenzando la partida!", "Iniciando a partida!");
    public string PleaseWait => LanguageText("Please wait...", "お待ちください…", "Espera, por favor...", "Por favor, aguarde...");
    public string PlayerJoined => LanguageText("{0} JOINED!", "{0}が参加！", "¡{0} HA ENTRADO!", "{0} ENTROU!");
    public string PlayersCount => LanguageText("PLAYERS {0}/{1}", "プレイヤー {0}/{1}", "JUGADORES {0}/{1}", "JOGADORES {0}/{1}");
    public string NewPlayer => LanguageText("NEW", "新規", "NUEVO", "NOVO");
    public string RoomAdmin => LanguageText("ROOM ADMIN: P{0}", "ルーム管理者: P{0}", "ADMINISTRADOR: P{0}", "ADMINISTRADOR: P{0}");

    public string RunCompleted => LanguageText("RUN COMPLETE!", "クリア！", "¡PARTIDA COMPLETADA!", "PARTIDA CONCLUÍDA!");
    public string StartGame => LanguageText("START ONLINE GAME", "オンラインゲーム開始", "INICIAR PARTIDA ONLINE", "INICIAR PARTIDA ONLINE");
    public string LobbyTimeout => LanguageText("START WITHIN {0} OR THE ROOM CLOSES", "{0}以内に開始しないと終了します", "INICIA EN {0} O SE CIERRA LA SALA", "INICIE EM {0} OU A SALA SERÁ ENCERRADA");
    public string IdleTimeout => LanguageText("NO INPUT: ROOM CLOSES IN {0}", "操作なし: あと{0}で終了", "SIN ACTIVIDAD: CIERRE EN {0}", "SEM INPUTS: SALA ENCERRA EM {0}");
    public string TableModeName(Assets.Scripts.Netcode.OnlineRoomMode mode) => mode switch
    {
        Assets.Scripts.Netcode.OnlineRoomMode.NormalGame => LanguageText("NORMAL", "ノーマル", "NORMAL", "NORMAL"),
        Assets.Scripts.Netcode.OnlineRoomMode.BossRush => LanguageText("BOSS RUSH", "ボスラッシュ", "BOSS RUSH", "BOSS RUSH"),
        _ => LanguageText("BATTLE", "バトル", "BATALLA", "BATALHA")
    };
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
