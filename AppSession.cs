namespace ActraNavWin
{
    /// <summary>
    /// アプリ実行中のセッション情報を保持する。
    /// </summary>
    public static class AppSession
    {
        public static StaffInfo? CurrentUser { get; set; }
    }
}
