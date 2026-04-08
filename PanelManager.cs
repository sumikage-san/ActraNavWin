using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

namespace ActraNavWin
{
    /// <summary>
    /// 全 Panel の一括管理を担う。
    /// 初期化・ナビゲーション・破棄を PanelManager 経由で行うことで、
    /// MainWindow が個別の Panel / WebView を直接操作する必要をなくす。
    /// </summary>
    public class PanelManager
    {
        private readonly Dictionary<PanelRole, Panel> _panels = new();

        public void Register(Panel panel)
        {
            _panels[panel.Role] = panel;
        }

        public Panel? Get(PanelRole role)
        {
            return _panels.TryGetValue(role, out var panel) ? panel : null;
        }

        /// <summary>
        /// 全 Panel の WebView2 を共有 Environment で順番に初期化する。
        /// 並列初期化ではなく順番に await することで安定性を確保する。
        /// </summary>
        public async Task InitializeAllAsync(CoreWebView2Environment env)
        {
            foreach (var panel in _panels.Values)
            {
                await panel.InitializeAsync(env);
            }
        }

        /// <summary>
        /// config.json の panels 設定に基づき、各 Panel に URL を割り当てる。
        /// PanelRole を小文字化して config キー（left/center/right）と照合する。
        /// </summary>
        public void NavigateAll(AppConfig config)
        {
            foreach (var (role, panel) in _panels)
            {
                var key = role.ToString().ToLower();

                if (config.Panels.TryGetValue(key, out var panelConfig))
                {
                    var url = panelConfig.Url.Replace("{baseUrl}", config.BaseUrl.TrimEnd('/'));
                    panel.Navigate(url);
                }
                else
                {
                    Debug.WriteLine($"PanelManager: config に panels[\"{key}\"] が未定義のためスキップ。");
                }
            }
        }

        public void DisposeAll()
        {
            foreach (var panel in _panels.Values)
            {
                panel.Dispose();
            }
        }
    }
}
