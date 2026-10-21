using System;
using System.Windows.Forms;
using IdleWalker.Domain;

namespace IdleWalker.Platform
{
    /// <summary>
    /// Carries out a hot-corner action the way the keyboard shortcut would,
    /// or through the Win32 call where there is one.
    /// </summary>
    static class CornerActions
    {
        /// <summary>Menu names, in <see cref="CornerAction"/> order.</summary>
        public static readonly string[] Names =
        {
            "Nothing", "Task view", "Show desktop", "Start menu", "Notification centre",
            "Quick settings", "Lock screen", "Screen saver", "Monitor off", "Call the Minions"
        };

        static NativeWindow saverWindow;   // a hidden window to send SC_SCREENSAVE to

        /// <summary>
        /// Performs the action. CallMinions is the application's business
        /// and does nothing here.
        /// The shortcuts are synthetic keystrokes, and Windows silently drops
        /// those while an app running as administrator has the focus (UIPI) -
        /// many portable tools ask for it. Then the shell is asked directly
        /// instead: Explorer runs at our level, so it takes the request and
        /// acts on every window, elevated ones included.
        /// </summary>
        public static void Perform(CornerAction action)
        {
            if (ForegroundWatch.IsElevated() && PerformThroughShell(action)) return;

            switch (action)
            {
                case CornerAction.TaskView:
                    Chord(Native.VK_LWIN, Native.VK_TAB);
                    break;
                case CornerAction.ShowDesktop:
                    Chord(Native.VK_LWIN, Native.VK_D);
                    break;
                case CornerAction.StartMenu:
                    Chord(Native.VK_CONTROL, Native.VK_ESCAPE);
                    break;
                case CornerAction.Notifications:
                    Chord(Native.VK_LWIN, Native.VK_N);
                    break;
                case CornerAction.QuickSettings:
                    Chord(Native.VK_LWIN, Native.VK_A);
                    break;
                case CornerAction.LockScreen:
                    Native.LockWorkStation();
                    break;
                case CornerAction.ScreenSaver:
                    Native.SendMessage(SaverWindow(), Native.WM_SYSCOMMAND, Native.SC_SCREENSAVE, IntPtr.Zero);
                    break;
                case CornerAction.MonitorOff:
                    Native.SendMessage(Native.HWND_BROADCAST, Native.WM_SYSCOMMAND, Native.SC_MONITORPOWER, new IntPtr(2));
                    break;
            }
        }

        /// <summary>
        /// The keyboard-free route for the shell's own actions; false when
        /// there is none (or it failed), and the shortcut is tried after all.
        /// </summary>
        static bool PerformThroughShell(CornerAction action)
        {
            try
            {
                switch (action)
                {
                    case CornerAction.TaskView:
                        return ShellCall("WindowSwitcher");
                    case CornerAction.ShowDesktop:
                        return ShellCall("ToggleDesktop");
                    case CornerAction.StartMenu:
                        IntPtr taskbar = Native.FindWindow("Shell_TrayWnd", null);
                        return taskbar != IntPtr.Zero && Native.PostMessage(taskbar, Native.WM_SYSCOMMAND, Native.SC_TASKLIST, IntPtr.Zero);
                    case CornerAction.Notifications:
                        return OpenUri("ms-actioncenter:");
                    case CornerAction.QuickSettings:
                        return OpenUri("ms-actioncenter:controlcenter/&showFooter=true");
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        // Shell.Application, late-bound: the same object a script gets.
        static bool ShellCall(string method)
        {
            Type type = Type.GetTypeFromProgID("Shell.Application");
            if (type == null) return false;
            object shell = Activator.CreateInstance(type);
            try
            {
                type.InvokeMember(method, System.Reflection.BindingFlags.InvokeMethod, null, shell, null);
                return true;
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
            }
        }

        static bool OpenUri(string uri)
        {
            using (System.Diagnostics.Process.Start(uri)) { }
            return true;
        }

        /// <summary>True while any mouse button is held: the cursor is dragging something.</summary>
        public static bool MouseButtonDown()
        {
            return (Native.GetAsyncKeyState(Native.VK_LBUTTON) & 0x8000) != 0 ||
                   (Native.GetAsyncKeyState(Native.VK_RBUTTON) & 0x8000) != 0 ||
                   (Native.GetAsyncKeyState(Native.VK_MBUTTON) & 0x8000) != 0;
        }

        // Modifier down, key down, key up, modifier up: a keyboard shortcut.
        static void Chord(byte modifier, byte key)
        {
            Native.keybd_event(modifier, 0, 0, UIntPtr.Zero);
            Native.keybd_event(key, 0, 0, UIntPtr.Zero);
            Native.keybd_event(key, 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
            Native.keybd_event(modifier, 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        // Any window of ours will do: DefWindowProc starts the configured
        // screen saver when it gets SC_SCREENSAVE.
        static IntPtr SaverWindow()
        {
            if (saverWindow == null)
            {
                saverWindow = new NativeWindow();
                saverWindow.CreateHandle(new CreateParams());
            }
            return saverWindow.Handle;
        }
    }
}
