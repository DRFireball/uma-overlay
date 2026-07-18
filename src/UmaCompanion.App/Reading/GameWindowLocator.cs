using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace UmaCompanion.App.Reading;

/// <summary>
/// Finds the game's top-level window. A window qualifies when its title is exactly "umamusume"
/// or its owning process name contains it — the title-exact rule avoids latching onto e.g. a
/// browser tab about the game, while the process rule survives title variations across builds.
/// </summary>
public static class GameWindowLocator
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>The game window's handle, or <see cref="IntPtr.Zero"/> when the game is not running.</summary>
    public static IntPtr FindGameWindow()
    {
        var found = IntPtr.Zero;
        var ownPid = (uint)Environment.ProcessId;

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
            {
                return true;
            }
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == ownPid)
            {
                return true;
            }

            var buffer = new StringBuilder(256);
            if (GetWindowText(hwnd, buffer, buffer.Capacity) == 0)
            {
                return true;
            }

            var title = buffer.ToString();
            if (title.Equals("umamusume", StringComparison.OrdinalIgnoreCase)
                || ProcessNameContainsUmamusume(pid))
            {
                found = hwnd;
                return false; // stop enumerating
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }

    private static bool ProcessNameContainsUmamusume(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName.Contains("umamusume", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false; // process exited between enumeration and lookup
        }
    }
}
