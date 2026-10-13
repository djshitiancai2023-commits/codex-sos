using System.Windows;
using System.IO;
using CodexSOS.App.Testing;

namespace CodexSOS.App;

public partial class App : Application
{
    private Board.BoardHost? _board;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        FixtureSession? fixture = null;
        var fixtureIndex = Array.FindIndex(e.Args,
            argument => string.Equals(argument, "--fixture", StringComparison.OrdinalIgnoreCase));
        if (fixtureIndex >= 0)
        {
            try
            {
                if (fixtureIndex + 1 >= e.Args.Length) throw new ArgumentException("Fixture path is missing.");
                fixture = FixtureSession.Load(e.Args[fixtureIndex + 1]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            {
                MessageBox.Show("虚构验收场景无法读取，已停止启动；没有改用真实账号检查。", "Codex SOS",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }
        }

        if (!e.Args.Contains("--classic") && (fixture is null || e.Args.Contains("--board")) &&
            File.Exists(Path.Combine(AppContext.BaseDirectory, "dot-assistant", "runtime", "node.exe")))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _board = new Board.BoardHost(fixture);
            try { if (await _board.StartAsync()) return; Shutdown(); return; }
            catch { _board.Dispose(); _board = null; ShutdownMode = ShutdownMode.OnLastWindowClose; }
        }
        var window = new MainWindow(fixture);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e) { _board?.Dispose(); base.OnExit(e); }
}
