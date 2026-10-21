using System;
using System.Runtime.InteropServices;

namespace IdleWalker.Platform
{
    /// <summary>Win32 declarations used by the platform layer.</summary>
    static class Native
    {
        public const uint WS_POPUP = 0x80000000;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const int SW_HIDE = 0;
        public const int SW_SHOWNOACTIVATE = 4;

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        public const int ULW_ALPHA = 2;
        public const byte AC_SRC_ALPHA = 1;
        public const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
            public POINT(int x, int y) { X = x; Y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int Width;
            public int Height;
            public SIZE(int width, int height) { Width = width; Height = height; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        // Hot corners: keyboard shortcuts, the lock screen, the screen saver, monitor power.
        public const uint WM_SYSCOMMAND = 0x0112;
        public static readonly IntPtr SC_SCREENSAVE = new IntPtr(0xF140);
        public static readonly IntPtr SC_MONITORPOWER = new IntPtr(0xF170);
        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
        public const int VK_LBUTTON = 0x01, VK_RBUTTON = 0x02, VK_MBUTTON = 0x04;
        public const byte VK_TAB = 0x09, VK_CONTROL = 0x11, VK_ESCAPE = 0x1B, VK_A = 0x41, VK_D = 0x44, VK_N = 0x4E, VK_LWIN = 0x5B;
        public const byte VK_F15 = 0x7E;   // a key no application binds: safe to tap just to count as activity
        public const uint KEYEVENTF_KEYUP = 0x0002;

        // The foreground window, to stay out of the way of full-screen apps.
        public const int GWL_STYLE = -16;
        public const int WS_CAPTION = 0x00C00000;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]   // the 32-bit read is enough for style bits and exists on every Windows
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int capacity);

        public const int GWL_EXSTYLE = -20;
        public const int DWMWA_CLOAKED = 14;

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool IsZoomed(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        public static readonly IntPtr SC_TASKLIST = new IntPtr(0xF130);   // to the taskbar: opens Start

        // ---- Integrity levels (UIPI) ----
        // Windows drops synthetic input (keybd_event / SendInput) aimed at a
        // window of a higher integrity level than ours - typically an app run
        // as administrator, which many portable tools ask for. The call
        // "succeeds" and nothing happens, so we look before we inject.
        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        const uint TOKEN_QUERY = 0x0008;
        const int TokenIntegrityLevel = 25;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, int length, out int returned);

        [DllImport("advapi32.dll")]
        static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);

        [DllImport("advapi32.dll")]
        static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);

        static int ownIntegrity = -1;

        /// <summary>
        /// The integrity RID of a process (0x2000 medium, 0x3000 high, ...),
        /// or int.MaxValue when its token cannot even be read - which, from a
        /// normal process, means it sits above us. Never throws.
        /// </summary>
        static int IntegrityOf(IntPtr process)
        {
            IntPtr token;
            if (!OpenProcessToken(process, TOKEN_QUERY, out token)) return int.MaxValue;
            IntPtr buffer = IntPtr.Zero;
            try
            {
                int length;
                GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out length);
                if (length <= 0) return int.MaxValue;
                buffer = Marshal.AllocHGlobal(length);
                if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, length, out length)) return int.MaxValue;
                IntPtr sid = Marshal.ReadIntPtr(buffer);   // TOKEN_MANDATORY_LABEL.Label.Sid
                int count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                return Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
            }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                CloseHandle(token);
            }
        }

        /// <summary>
        /// True when the window belongs to a process above our integrity
        /// level (an elevated app), so synthetic keystrokes would be silently
        /// dropped while it has the focus. Never throws; false when unsure.
        /// </summary>
        public static bool IsAboveUs(IntPtr hwnd)
        {
            try
            {
                if (hwnd == IntPtr.Zero) return false;
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                if (pid == 0) return false;
                if (ownIntegrity < 0) ownIntegrity = IntegrityOf(GetCurrentProcess());
                IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (process == IntPtr.Zero) return true;   // not even a limited query: protected or above us
                try
                {
                    return IntegrityOf(process) > ownIntegrity;
                }
                finally
                {
                    CloseHandle(process);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---- The system's own idle clock ----
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

        /// <summary>
        /// Seconds since the last input of any kind, as Windows counts it for
        /// the screen saver, auto-lock and "Away" presence. Keyboard input
        /// counts, and so does nothing that merely moves the cursor (an app
        /// calling SetCursorPos), which is why this, not the cursor, is the
        /// clock the anti-idle nudge watches. 0 when unknown.
        /// </summary>
        public static double SystemIdleSeconds()
        {
            try
            {
                LASTINPUTINFO info = new LASTINPUTINFO();
                info.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
                if (!GetLastInputInfo(ref info)) return 0;
                return unchecked((uint)Environment.TickCount - info.dwTime) / 1000.0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extraInfo);

        [DllImport("user32.dll")]
        static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extraInfo);

        const uint MOUSEEVENTF_MOVE = 0x0001;

        [DllImport("user32.dll")]
        public static extern bool LockWorkStation();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vk);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst,
            ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter,
            int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT point, uint flags);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hicon);

        // Screenshot / screen-capture exclusion. WDA_EXCLUDEFROMCAPTURE needs
        // Windows 10 2004+; on older builds it fails and we fall back to
        // WDA_MONITOR, which also keeps the window out of captures.
        public const uint WDA_NONE = 0;
        public const uint WDA_MONITOR = 1;
        public const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

        [DllImport("user32.dll")]
        static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

        /// <summary>
        /// Keeps a window visible on screen but out of screenshots, Snipping
        /// Tool, Game Bar, and most screen-share / recording captures.
        /// Never throws: on systems without support the window just stays capturable.
        /// </summary>
        public static void ExcludeFromCapture(IntPtr hwnd)
        {
            try
            {
                if (hwnd == IntPtr.Zero) return;
                if (!SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE))
                    SetWindowDisplayAffinity(hwnd, WDA_MONITOR);
            }
            catch (Exception)
            {
            }
        }

        // ---- Keeping the machine awake ----
        // SetThreadExecutionState is the official "stay awake" call: it tells
        // Windows this thread needs the system and/or display up, so the idle
        // timers that trigger sleep, the screen saver, and monitor-off stop
        // counting. It fakes no input. The state lives on the calling thread
        // and lasts until changed, so it must be set from a thread that stays
        // alive - here the UI thread running Application.Run.
        [Flags]
        public enum ExecutionState : uint
        {
            Continuous = 0x80000000,
            SystemRequired = 0x00000001,
            DisplayRequired = 0x00000002,
        }

        [DllImport("kernel32.dll")]
        static extern ExecutionState SetThreadExecutionState(ExecutionState state);

        /// <summary>
        /// Keeps the machine (and, when <paramref name="keepDisplayOn"/>, the
        /// screen) from going idle to sleep, the screen saver, or monitor-off,
        /// until <see cref="AllowSleep"/> is called. Idempotent: calling it
        /// again just renews the request. Never throws.
        /// </summary>
        public static void KeepAwake(bool keepDisplayOn)
        {
            try
            {
                ExecutionState state = ExecutionState.Continuous | ExecutionState.SystemRequired;
                if (keepDisplayOn) state |= ExecutionState.DisplayRequired;
                SetThreadExecutionState(state);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Releases the awake request so the normal power timers resume.
        /// Safe to call when nothing was held. Never throws.
        /// </summary>
        public static void AllowSleep()
        {
            try
            {
                SetThreadExecutionState(ExecutionState.Continuous);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Resets the "last input" timer with a single F15 key tap, so the
        /// screen saver, auto-lock and "Away"/"Idle" presence never trigger
        /// while the user is away. SetThreadExecutionState only blocks sleep
        /// and monitor-off; this is what keeps the session counted as active.
        /// F15 is a real key that no application binds, so it moves nothing
        /// on screen, types nothing into the focused window, and leaves every
        /// open page and document untouched.
        /// A zero-distance mouse move goes along with it: it resets the same
        /// timer and moves nothing, but is delivered to the window under the
        /// cursor rather than the focused one, so it still counts when the
        /// focused app runs as administrator and the F15 tap is dropped
        /// (UIPI), or when an app's keyboard hook swallows injected keys
        /// (remote-desktop and VM clients). Never throws.
        /// </summary>
        public static void NudgeActivity()
        {
            try
            {
                keybd_event(VK_F15, 0, 0, UIntPtr.Zero);
                keybd_event(VK_F15, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_MOVE, 0, 0, 0, UIntPtr.Zero);
            }
            catch (Exception)
            {
            }
        }

        [DllImport("user32.dll")]
        static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        /// <summary>Blocks until the desktop compositor has presented a frame.</summary>
        [DllImport("dwmapi.dll")]
        public static extern int DwmFlush();

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER header, uint usage,
            out IntPtr bits, IntPtr section, uint offset);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr obj);

        /// <summary>
        /// Makes every coordinate the process sees a physical pixel on its own
        /// monitor. Falls back to system-wide awareness on old Windows 10.
        /// </summary>
        public static void EnablePerMonitorDpi()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
                if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware();
            }
            catch (EntryPointNotFoundException)
            {
                SetProcessDPIAware();
            }
        }
    }
}
