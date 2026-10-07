using System;
using Microsoft.Win32;

namespace IdleWalker.Platform
{
    /// <summary>Remembers the user's choices between runs (per user, in the registry).</summary>
    static class UserSettings
    {
        const string Key = @"Software\IdleWalker";
        const string CountValue = "WalkerCount";

        public static int LoadWalkerCount()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(Key, false))
                {
                    object value = key == null ? null : key.GetValue(CountValue);
                    if (value is int) return Math.Max(1, Math.Min(Settings.MaxWalkers, (int)value));
                }
            }
            catch (Exception)
            {
                // Fall through to the default.
            }
            return 1;
        }

        public static void SaveWalkerCount(int count)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Key))
                {
                    if (key != null) key.SetValue(CountValue, count, RegistryValueKind.DWord);
                }
            }
            catch (Exception)
            {
                // Not worth interrupting the user; the choice just won't stick.
            }
        }
    }
}
