using System;
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

        /// <summary>
        /// True when the foreground window covers the whole of its monitor,
        /// taskbar included, and has no title bar - the shape of a game, a
        /// full-screen video or a slide show. The desktop itself does not
        /// count, nor does a window that is merely maximised: that keeps its
        /// title bar, even over an auto-hidden taskbar.
        /// </summary>
        public static bool IsFullScreen()
        {
            IntPtr hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;

            Native.RECT r;
            if (!Native.GetWindowRect(hwnd, out r)) return false;
            Rectangle window = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
            Rectangle monitor = Screen.FromHandle(hwnd).Bounds;
            if (!window.Contains(monitor)) return false;

            int style = Native.GetWindowLong(hwnd, Native.GWL_STYLE);
            if ((style & Native.WS_CAPTION) == Native.WS_CAPTION) return false;

            // The desktop and the shell's own full-screen hosts are not apps.
            className.Length = 0;
            Native.GetClassName(hwnd, className, className.Capacity);
            string name = className.ToString();
            return name != "Progman" && name != "WorkerW" && name != "Shell_TrayWnd";
        }
    }
}
