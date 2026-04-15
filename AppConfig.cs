namespace ActraNavWin
{
    public class QrConfig
    {
        public string Ip { get; set; } = "";
        public string Location { get; set; } = "";
        public string Protocol { get; set; } = "http";
        public int? Version { get; set; }
    }

    public class UrlEntry
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public bool IsDefault { get; set; }

        public override string ToString() => Name;
    }

    public class SlideConfig
    {
        public string Edge { get; set; } = "right";
        public double Position { get; set; } = 0.5;
    }

    public class AppConfig
    {
        public bool IsInitialized { get; set; }
        public QrConfig? Qr { get; set; }
        public SlideConfig Slide { get; set; } = new();
        public string ApiBaseUrl { get; set; } = "";
        public string DefaultUrl { get; set; } = "";
        public List<UrlEntry> UrlList { get; set; } = new();

        /// <summary>
        /// QR 設定から URL 群を生成して自身のフィールドに反映する。
        /// </summary>
        public void ApplyQrConfig()
        {
            if (Qr == null) return;

            var baseUrl = $"{Qr.Protocol}://{Qr.Ip}/company-wide/html/Actra";
            ApiBaseUrl = baseUrl;
            DefaultUrl = $"{baseUrl}/worklog/view/index_main.php";

            UrlList = new List<UrlEntry>
            {
                new() { Name = "Worklog", Url = $"{baseUrl}/worklog/view/index_main.php", IsDefault = true },
                new() { Name = "管理画面", Url = $"{baseUrl}/worklog/manage/index_main.php" }
            };
        }
    }
}
