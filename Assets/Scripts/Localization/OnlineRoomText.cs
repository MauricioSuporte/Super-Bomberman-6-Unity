public static partial class GameTextDatabase
{
    public static OnlineRoomText Online => SaveSystem.GetLanguage() switch
    {
        GameLanguage.Japanese => JapaneseOnlineRoom,
        GameLanguage.Spanish => SpanishOnlineRoom,
        GameLanguage.PortugueseBr => PortugueseOnlineRoom,
        _ => EnglishOnlineRoom
    };

    static readonly OnlineRoomText EnglishOnlineRoom = new("ONLINE", "OFFLINE", "ONLINE ROOM", "BATTLE MODE", "HOST", "JOIN", "HOST IP", "LEAVE ROOM", "CONFIGURE MATCH", "CONNECTING...", "WAITING FOR PLAYERS", "YOU: P{0}", "EMPTY", "THE HOST IS CONFIGURING THE MATCH", "P{0} IS CHOOSING", "CONFIRMED", "AT LEAST TWO ONLINE PLAYERS ARE REQUIRED", "ROOM FULL OR MATCH ALREADY STARTED", "RULES", "ITEMS", "LOUIES", "HANDICAP");
    static readonly OnlineRoomText JapaneseOnlineRoom = new("オンライン", "オフライン", "オンラインルーム", "バトルモード", "ルーム作成", "参加", "ホストIP", "ルームを退出", "対戦設定", "接続中...", "プレイヤーを待っています", "あなた: P{0}", "空き", "ホストが対戦を設定中です", "P{0}が選択中", "決定済み", "オンラインプレイヤーが2人以上必要です", "満員または対戦開始済み", "ルール", "アイテム", "ルーイ", "ハンデ");
    static readonly OnlineRoomText SpanishOnlineRoom = new("ONLINE", "OFFLINE", "SALA ONLINE", "MODO BATALLA", "CREAR SALA", "ENTRAR", "IP DEL HOST", "SALIR DE LA SALA", "CONFIGURAR PARTIDA", "CONECTANDO...", "ESPERANDO JUGADORES", "TÚ: P{0}", "VACÍO", "EL HOST ESTÁ CONFIGURANDO LA PARTIDA", "P{0} ESTÁ ELIGIENDO", "CONFIRMADO", "SE NECESITAN DOS JUGADORES ONLINE", "SALA LLENA O PARTIDA INICIADA", "REGLAS", "OBJETOS", "LOUIES", "HÁNDICAP");
    static readonly OnlineRoomText PortugueseOnlineRoom = new("ONLINE", "OFFLINE", "SALA ONLINE", "MODO BATALHA", "CRIAR SALA", "ENTRAR", "IP DO HOST", "SAIR DA SALA", "CONFIGURAR PARTIDA", "CONECTANDO...", "AGUARDANDO JOGADORES", "VOCÊ: P{0}", "VAZIO", "O HOST ESTÁ CONFIGURANDO A PARTIDA", "P{0} ESTÁ ESCOLHENDO", "CONFIRMADO", "SÃO NECESSÁRIOS DOIS JOGADORES ONLINE", "SALA CHEIA OU PARTIDA INICIADA", "REGRAS", "ITENS", "LOUIES", "HANDICAP");
}

public sealed class OnlineRoomText
{
    public readonly string Online, Offline, Room, BattleMode, Host, Join, Address, Leave, Configure,
        Connecting, Waiting, You, Empty, HostConfiguring, Choosing, Confirmed, NeedPlayers, Unavailable,
        Rules, Items, Louies, Handicap;
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
