using DesktopClock.Native;

namespace DesktopClock.Services;

public static class DesktopLayerService
{
    public static bool AttachToWallpaperLayer(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var target = FindWallpaperWorker();
        if (target == IntPtr.Zero)
        {
            return false;
        }

        var style = Win32.GetWindowLongPtr(hwnd, Win32.GWL_STYLE).ToInt64();
        style &= ~Win32.WS_POPUP;
        style |= Win32.WS_CHILD;
        Win32.SetWindowLongPtr(hwnd, Win32.GWL_STYLE, new IntPtr(style));
        Win32.SetParent(hwnd, target);
        Win32.SetWindowPos(
            hwnd,
            Win32.HWND_BOTTOM,
            0,
            0,
            0,
            0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED | Win32.SWP_SHOWWINDOW);
        return Win32.GetParent(hwnd) == target;
    }

    public static void DetachFromWallpaperLayer(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || Win32.GetParent(hwnd) == IntPtr.Zero)
        {
            return;
        }

        Win32.SetParent(hwnd, IntPtr.Zero);
        var style = Win32.GetWindowLongPtr(hwnd, Win32.GWL_STYLE).ToInt64();
        style &= ~Win32.WS_CHILD;
        style |= Win32.WS_POPUP;
        Win32.SetWindowLongPtr(hwnd, Win32.GWL_STYLE, new IntPtr(style));
        Win32.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED | Win32.SWP_SHOWWINDOW);
    }

    private static IntPtr FindWallpaperWorker()
    {
        var progman = Win32.FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            Win32.SendMessageTimeout(
                progman,
                Win32.WM_SPAWN_WORKER,
                UIntPtr.Zero,
                IntPtr.Zero,
                Win32.SMTO_NORMAL,
                1000,
                out _);
        }

        var worker = IntPtr.Zero;
        while (true)
        {
            worker = Win32.FindWindowEx(IntPtr.Zero, worker, "WorkerW", null);
            if (worker == IntPtr.Zero)
            {
                break;
            }

            var shellView = Win32.FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
            {
                var wallpaperWorker = Win32.FindWindowEx(IntPtr.Zero, worker, "WorkerW", null);
                if (wallpaperWorker != IntPtr.Zero)
                {
                    return wallpaperWorker;
                }
            }
        }

        return IntPtr.Zero;
    }
}
