using System.Runtime.InteropServices;

namespace VibeOpenGothic.Core.Diagnostics;

internal static class NativeMessageBox
{
    private const uint MbIconError = 0x00000010;
    private const uint MbOk = 0x00000000;

    public static void ShowError(string title, string message)
    {
        if (!Environment.UserInteractive)
        {
            Console.Error.WriteLine($"{title}: {message}");
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine($"{title}: {message}");
            return;
        }

        _ = MessageBoxW(IntPtr.Zero, message, title, MbIconError | MbOk);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
