using System;
using Microsoft.Win32;

namespace IdleWalker.Platform
{
    /// <summary>Remembers the user's choices between runs (per user, in the registry).</summary>
    static class UserSettings
    {
        const string Key = @"Software\IdleWalker";
        const string CountValue = "WalkerCount";
        const string IdleValue = "IdleSeconds";

        public static int LoadWalkerCount()
        {
            int count = Load(CountValue, 1);
            return Math.Max(1, Math.Min(Settings.MaxWalkers, count));
        }

        public static void SaveWalkerCount(int count)
        {
            Save(CountValue, count);
        }

        /// <summary>The chosen rest time; anything not on the menu falls back to the default.</summary>
        public static int LoadIdleSeconds()
        {
            int seconds = Load(IdleValue, Settings.IdleSeconds);
            return Array.IndexOf(Settings.IdleChoices, seconds) >= 0 ? seconds : Settings.IdleSeconds;
        }

        public static void SaveIdleSeconds(int seconds)
        {
            Save(IdleValue, seconds);
        }

        static int Load(string name, int fallback)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(Key, false))
                {
                    object value = key == null ? null : key.GetValue(name);
                    if (value is int) return (int)value;
                }
            }
            catch (Exception)
            {
                // Fall through to the default.
            }
            return fallback;
        }

        static void Save(string name, int value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Key))
                {
                    if (key != null) key.SetValue(name, value, RegistryValueKind.DWord);
                }
            }
            catch (Exception)
            {
                // Not worth interrupting the user; the choice just won't stick.
            }
        }
    }
}
