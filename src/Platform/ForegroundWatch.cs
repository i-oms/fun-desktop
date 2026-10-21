using System;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace IdleWalker.Platform
{
    /// <summary>
    /// Tells whether the window in front is full screen - a game, a film, a
    /// presentation - so the Minions can stay out of its way and off its CPU.
    /// </summary>
    static class ForegroundWatch
    {
        static readonly StringBuilder className = new StringBuilder(64);

        // The desktop, the taskbar and the shell's own full-screen surfaces
        // (Task view, Start, search, the notification and quick-settings
        // flyouts): borderless and monitor-sized, but not apps. Task view in
        // particular must not count, or the corner that opened it could
        // never close it again.
        static readonly string[] ShellClasses =
        {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
            "XamlExplorerHostIslandWindow", "Windows.UI.Core.CoreWindow",
            "MultitaskingViewFrame", "ForegroundStaging", "TopLevelWindowForOverflowXamlIsland",
            "ControlCenterWindow", "NotifyIconOverflowWindow"
        };

        /// <summary>
        /// True when the foreground window covers the whole of its monitor,
        /// taskbar included, and has no title bar - the shape of a game, a
        /// full-screen video or a slide show. The desktop and the shell do not
        /// count, nor does a window that is merely maximised (with or without
        /// a title bar: custom-drawn apps maximise borderless, and over an
        /// auto-hidden taskbar they cover the monitor too), nor one the user
        /// cannot see - hidden, cloaked, or a click-through overlay that some
        /// tools park in front of everything.
        /// </summary>
        public static bool IsFullScreen()
        {
            IntPtr hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;
            if (!Native.IsWindowVisible(hwnd) || Native.IsZoomed(hwnd)) return false;

            int cloaked;
            if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_CLOAKED, out cloaked, sizeof(int)) == 0 && cloaked != 0) return false;

            Native.RECT r;
            if (!Native.GetWindowRect(hwnd, out r)) return false;
            Rectangle window = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
            Rectangle monitor = Screen.FromHandle(hwnd).Bounds;
            if (!window.Contains(monitor)) return false;

            int style = Native.GetWindowLong(hwnd, Native.GWL_STYLE);
            if ((style & Native.WS_CAPTION) == Native.WS_CAPTION) return false;
            int exStyle = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
            if ((exStyle & Native.WS_EX_TRANSPARENT) != 0) return false;

            return Array.IndexOf(ShellClasses, ClassOf(hwnd)) < 0;
        }

        /// <summary>
        /// True when the foreground app runs above us (as administrator), so
        /// keystrokes we synthesise would be dropped by Windows.
        /// </summary>
        public static bool IsElevated()
        {
            return Native.IsAboveUs(Native.GetForegroundWindow());
        }

        /// <summary>"process.exe [WindowClass]" of the foreground window, for the log.</summary>
        public static string Describe()
        {
            IntPtr hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return "(none)";
            string process = "?";
            try
            {
                uint pid;
                Native.GetWindowThreadProcessId(hwnd, out pid);
                using (Process p = Process.GetProcessById((int)pid)) process = p.ProcessName;
            }
            catch (Exception)
            {
            }
            return string.Format("{0} [{1}]{2}", process, ClassOf(hwnd), Native.IsAboveUs(hwnd) ? " elevated" : "");
        }

        static string ClassOf(IntPtr hwnd)
        {
            className.Length = 0;
            Native.GetClassName(hwnd, className, className.Capacity);
            return className.ToString();
        }
    }
}
