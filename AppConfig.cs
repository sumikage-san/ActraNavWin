namespace ActraNavWin
{
    /// <summary>
    /// 各パネルの設定。URL により表示コンテンツを決定する。
    /// 将来 Panel クラス化した際に、そのまま Panel の初期化パラメータとして使える構造。
    /// </summary>
    public class PanelConfig
    {
        public string Url { get; set; } = "";
    }

    /// <summary>
    /// config.json のルート構造。
    /// mode は legacy／nexus の切替に使用する（現時点では保持のみ）。
    /// panels は役割名（left/center/right）をキーとしたパネル設定の辞書。
    /// </summary>
    public class AppConfig
    {
        public string Mode { get; set; } = "legacy";
        public Dictionary<string, PanelConfig> Panels { get; set; } = new();
    }
}
