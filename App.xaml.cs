using System.Windows;

namespace ActraNavWin
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var config = ActraNavWin.MainWindow.LoadConfig();
            var login = new StaffCodeWindow(config);

            if (login.ShowDialog() == true)
            {
                var main = new MainWindow();
                main.Show();
            }
            else
            {
                Shutdown();
            }
        }
    }
}
