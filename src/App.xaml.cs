using System.Windows;

namespace StellasoraPotentialOverlay;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // メインウィンドウを表示
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
