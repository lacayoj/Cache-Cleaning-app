using System;
using System.Windows;

namespace WinCleanPro
{
    public class App : Application
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                App app = new App();
                MainWindow window = new MainWindow();
                app.Run(window);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error fatal al iniciar SS Clan Cleaner Pro:\n\n" + ex.ToString(), "SS Clan Cleaner Pro - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
