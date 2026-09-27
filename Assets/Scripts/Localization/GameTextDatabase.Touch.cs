public static partial class GameTextDatabase
{
    public static string Touch(int index)
    {
        string[] text = SaveSystem.GetLanguage() switch
        {
            GameLanguage.PortugueseBr => TouchPortuguese,
            GameLanguage.Spanish => TouchSpanish,
            GameLanguage.Japanese => TouchJapanese,
            _ => TouchEnglish
        };
        return text[index];
    }
    static readonly string[] TouchEnglish = {
        "TOUCH CONTROLS", "Direction control", "Dynamic analog", "Block diagonals", "Deadzone",
        "Show touch buttons", "Hide when idle", "Hide after", "Action icons", "Hide unused buttons",
        "Position / size", "Opacity", "Reset positions", "Reset sizes", "Reset all", "BACK",
        "ON", "OFF", "ANALOG", "D-PAD", "Drag a control; select it to resize. Rotate to edit each orientation.",
        "DONE", "Portrait", "Landscape", "Control", "Size", "Reset touch controls?", "YES", "NO"
    };
    static readonly string[] TouchPortuguese = {
        "CONTROLES DE TOQUE", "Direcional", "Analógico dinâmico", "Bloquear diagonais", "Zona morta",
        "Exibir botões", "Ocultar por inatividade", "Ocultar após", "Ícones das ações", "Ocultar sem uso",
        "Posição / tamanho", "Opacidade", "Restaurar posições", "Restaurar tamanhos", "Restaurar tudo", "VOLTAR",
        "SIM", "NÃO", "ANALÓGICO", "D-PAD", "Arraste um controle; selecione para redimensionar. Gire para editar cada orientação.",
        "CONCLUIR", "Vertical", "Horizontal", "Controle", "Tamanho", "Restaurar controles de toque?", "SIM", "NÃO"
    };
    static readonly string[] TouchSpanish = {
        "CONTROLES TÁCTILES", "Direccional", "Analógico dinámico", "Bloquear diagonales", "Zona muerta",
        "Mostrar botones", "Ocultar por inactividad", "Ocultar después", "Iconos de acciones", "Ocultar sin uso",
        "Posición / tamaño", "Opacidad", "Restaurar posiciones", "Restaurar tamaños", "Restaurar todo", "VOLVER",
        "SÍ", "NO", "ANALÓGICO", "D-PAD", "Arrastra un control; selecciona para cambiar tamaño. Gira para editar cada orientación.",
        "LISTO", "Vertical", "Horizontal", "Control", "Tamaño", "¿Restaurar controles táctiles?", "SÍ", "NO"
    };
    static readonly string[] TouchJapanese = {
        "タッチ操作", "方向操作", "移動式スティック", "斜め入力を無効", "デッドゾーン",
        "ボタンを表示", "未使用時に隠す", "非表示までの秒数", "アクションアイコン", "不要なボタンを隠す",
        "位置・サイズ", "不透明度", "位置をリセット", "サイズをリセット", "すべてリセット", "戻る",
        "オン", "オフ", "スティック", "方向パッド", "ドラッグで移動。選択してサイズ変更。画面を回転して各方向を編集。",
        "完了", "縦画面", "横画面", "ボタン", "サイズ", "タッチ操作をリセットしますか？", "はい", "いいえ"
    };
}