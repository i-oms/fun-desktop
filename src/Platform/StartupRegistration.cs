using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace IdleWalker.Platform
{
    /// <summary>
    /// "Start with Windows", via the current user's Run key. No admin rights
    /// are needed and it only affects this user account.
    /// </summary>
    static class StartupRegistration
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "IdleWalker";

        public static bool IsEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                return key != null && key.GetValue(ValueName) != null;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (key == null) throw new InvalidOperationException("The Run key could not be opened.");
                if (enabled) key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                else key.DeleteValue(ValueName, false);
            }
        }

        /// <summary>
        /// If the setting is on, points it at wherever the program is running
        /// from now - so moving or renaming the exe doesn't break it.
        /// </summary>
        public static void RefreshPath()
        {
            try
            {
                if (IsEnabled()) SetEnabled(true);
            }
            catch (Exception)
            {
                // Not important enough to bother the user about at start-up.
            }
        }
    }
}
