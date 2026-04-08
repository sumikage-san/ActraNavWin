using System.Windows;

namespace ActraNavWin
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                var config = ActraNavWin.MainWindow.LoadConfig();
                var login = new StaffCodeWindow(config);

                if (login.ShowDialog() == true)
                {
                    var main = new MainWindow();
                    MainWindow = main;
                    main.Show();
                }
                else
                {
                    Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"起動エラー:\n{ex}", "ActraNavWin エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}
