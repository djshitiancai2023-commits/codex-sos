using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace CodexSOS.App.Services;

public sealed class CodexWindowCapture
{
    public sealed record WindowCandidate(
        IntPtr Handle,
        string ProcessName,
        bool IsForeground,
        long Area);

    public static WindowCandidate? ChooseCandidate(IEnumerable<WindowCandidate> candidates)
    {
        var eligible = candidates
            .Where(candidate => IsSupportedDesktopProcess(candidate.ProcessName))
            .ToArray();
        if (eligible.Length == 0) return null;

        var foreground = eligible.Where(candidate => candidate.IsForeground).ToArray();
        if (foreground.Length == 1) return foreground[0];

        var codex = eligible.Where(candidate =>
            string.Equals(candidate.ProcessName, "Codex", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (codex.Length == 1) return codex[0];
        if (codex.Length > 1) return null;

        var chatGpt = eligible.Where(candidate =>
            string.Equals(candidate.ProcessName, "ChatGPT", StringComparison.OrdinalIgnoreCase)).ToArray();
        return chatGpt.Length == 1 ? chatGpt[0] : null;
    }

    public BitmapSource? Capture(out string message)
    {
        var window = FindTargetWindow();
        if (window == IntPtr.Zero)
        {
            message = "没有找到正在显示的 Codex 窗口。你也可以直接粘贴已有截图。";
            return null;
        }

        if (!GetWindowRect(window, out var rect) || rect.Right <= rect.Left || rect.Bottom <= rect.Top)
        {
            message = "暂时无法读取 Codex 窗口画面。你也可以直接粘贴已有截图。";
            return null;
        }

        var width = Math.Min(rect.Right - rect.Left, 8000);
        var height = Math.Min(rect.Bottom - rect.Top, 8000);
        var windowDc = GetWindowDC(window);
        if (windowDc == IntPtr.Zero)
        {
            message = "暂时无法读取 Codex 窗口画面。你也可以直接粘贴已有截图。";
            return null;
        }

        var memoryDc = CreateCompatibleDC(windowDc);
        var bitmap = CreateCompatibleBitmap(windowDc, width, height);
        var previous = SelectObject(memoryDc, bitmap);
        try
        {
            var captured = PrintWindow(window, memoryDc, 2);
            if (!captured)
            {
                captured = BitBlt(memoryDc, 0, 0, width, height, windowDc, 0, 0, 0x00CC0020);
            }

            if (!captured)
            {
                message = "暂时无法读取 Codex 窗口画面。你也可以直接粘贴已有截图。";
                return null;
            }

            BitmapSource? source = null;
            try
            {
                source = Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
            }
            catch (Exception)
            {
                source = null;
            }

            if (source == null)
            {
                message = "暂时无法读取 Codex 窗口画面。你也可以直接粘贴已有截图。";
                return null;
            }

            message = "已截取 Codex 窗口。图片只在本机处理，不会放进公开材料。";
            return source;
        }
        finally
        {
            SelectObject(memoryDc, previous);
            DeleteObject(bitmap);
            DeleteDC(memoryDc);
            ReleaseDC(window, windowDc);
        }
    }

    private static IntPtr FindTargetWindow()
    {
        var current = Process.GetCurrentProcess().Id;
        var foreground = GetForegroundWindow();
        var candidates = new List<WindowCandidate>();
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle) || IsIconic(handle)) return true;
            GetWindowThreadProcessId(handle, out var processId);
            if (processId == current || processId == 0) return true;

            try
            {
                using var process = Process.GetProcessById((int)processId);
                var name = process.ProcessName;
                if (!IsSupportedDesktopProcess(name)) return true;

                if (GetWindowRect(handle, out var rect) && rect.Right > rect.Left && rect.Bottom > rect.Top)
                {
                    candidates.Add(new WindowCandidate(
                        handle,
                        name,
                        handle == foreground,
                        (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top)));
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                return true;
            }

            return true;
        }, IntPtr.Zero);

        return ChooseCandidate(candidates)?.Handle ?? IntPtr.Zero;
    }

    private static bool IsSupportedDesktopProcess(string processName) =>
        string.Equals(processName, "Codex", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(processName, "ChatGPT", StringComparison.OrdinalIgnoreCase);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hWnd, IntPtr hDc, uint flags);
    [DllImport("user32.dll")] private static extern bool BitBlt(IntPtr destination, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hDc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDc, IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hDc);
}
