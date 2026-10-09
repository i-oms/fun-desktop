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
        const string StartupValue = "StartupDecided";
        const string DressValue = "Dress";

        public static int LoadWalkerCount()
        {
            int count = Load(CountValue, Settings.DefaultWalkers);
            return Math.Max(1, Math.Min(Settings.MaxWalkers, count));
        }

        /// <summary>
        /// True once "Start with Windows" has been set up - by the default
        /// on the first run, or by the user clicking it - so the default is
        /// applied only once and an unticked box stays unticked.
        /// </summary>
        public static bool StartupDecided()
        {
            return Load(StartupValue, 0) != 0;
        }

        public static void MarkStartupDecided()
        {
            Save(StartupValue, 1);
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

        /// <summary>The chosen overalls colour; anything not on the menu falls back to the default.</summary>
        public static int LoadDress()
        {
            int dress = Load(DressValue, Settings.DressColors[0]);
            if (dress == Settings.RandomDress || Array.IndexOf(Settings.DressColors, dress) >= 0) return dress;
            return Settings.DressColors[0];
        }

        public static void SaveDress(int dress)
        {
            Save(DressValue, dress);
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
