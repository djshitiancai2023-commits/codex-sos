using System.Windows;
using System.IO;
using CodexSOS.App.Testing;

namespace CodexSOS.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        FixtureSession? fixture = null;
        var fixtureIndex = Array.FindIndex(e.Args,
            argument => string.Equals(argument, "--fixture", StringComparison.OrdinalIgnoreCase));
        if (fixtureIndex >= 0 && fixtureIndex + 1 < e.Args.Length)
        {
            try
            {
                fixture = FixtureSession.Load(e.Args[fixtureIndex + 1]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            {
                MessageBox.Show("虚构验收场景无法读取，Codex SOS 将以普通模式打开。", "Codex SOS",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        var window = new MainWindow(fixture);
        MainWindow = window;
        window.Show();
    }
}
