using System;
using Microsoft.Win32;
using IdleWalker.Domain;

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
        const string WalkersValue = "WalkersEnabled";
        const string MinionsValue = "MinionsEnabled";
        const string BeanValue = "MrBeanEnabled";
        const string BirdsValue = "BirdsEnabled";
        const string BirdCountValue = "BirdCount";
        const string CornersValue = "HotCornersEnabled";
        const string DelayValue = "HotCornerDelayMs";
        static readonly string[] CornerValues = { "CornerTopLeft", "CornerTopRight", "CornerBottomLeft", "CornerBottomRight" };

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

        /// <summary>The chosen overalls colour: random, or any 0xRRGGBB (the menu's or a custom one); anything else falls back to the default.</summary>
        public static int LoadDress()
        {
            int dress = Load(DressValue, Settings.DressColors[0]);
            if (dress == Settings.RandomDress || (dress >= 0 && dress <= 0xFFFFFF)) return dress;
            return Settings.DressColors[0];
        }

        public static void SaveDress(int dress)
        {
            Save(DressValue, dress);
        }

        /// <summary>The master switch over the Minions and Mr. Bean.</summary>
        public static bool LoadWalkersEnabled()
        {
            return Load(WalkersValue, Settings.WalkersEnabledByDefault ? 1 : 0) != 0;
        }

        public static void SaveWalkersEnabled(bool on)
        {
            Save(WalkersValue, on ? 1 : 0);
        }

        public static bool LoadMinionsEnabled()
        {
            return Load(MinionsValue, Settings.MinionsEnabledByDefault ? 1 : 0) != 0;
        }

        public static void SaveMinionsEnabled(bool on)
        {
            Save(MinionsValue, on ? 1 : 0);
        }

        public static bool LoadBeanEnabled()
        {
            return Load(BeanValue, Settings.MrBeanByDefault ? 1 : 0) != 0;
        }

        public static void SaveBeanEnabled(bool on)
        {
            Save(BeanValue, on ? 1 : 0);
        }

        public static bool LoadBirdsEnabled()
        {
            return Load(BirdsValue, Settings.BirdsEnabledByDefault ? 1 : 0) != 0;
        }

        public static void SaveBirdsEnabled(bool on)
        {
            Save(BirdsValue, on ? 1 : 0);
        }

        public static int LoadBirdCount()
        {
            int count = Load(BirdCountValue, Settings.DefaultBirds);
            return Math.Max(1, Math.Min(Settings.MaxBirds, count));
        }

        public static void SaveBirdCount(int count)
        {
            Save(BirdCountValue, count);
        }

        public static bool LoadHotCornersEnabled()
        {
            return Load(CornersValue, Settings.HotCornersByDefault ? 1 : 0) != 0;
        }

        public static void SaveHotCornersEnabled(bool on)
        {
            Save(CornersValue, on ? 1 : 0);
        }

        /// <summary>What a corner does; anything unknown falls back to the default for that corner.</summary>
        public static CornerAction LoadCornerAction(Corner corner)
        {
            CornerAction fallback = Settings.DefaultCornerActions[(int)corner];
            int value = Load(CornerValues[(int)corner], (int)fallback);
            return Enum.IsDefined(typeof(CornerAction), value) ? (CornerAction)value : fallback;
        }

        public static void SaveCornerAction(Corner corner, CornerAction action)
        {
            Save(CornerValues[(int)corner], (int)action);
        }

        /// <summary>The corner dwell time; anything not on the menu falls back to the default.</summary>
        public static int LoadHotCornerDelay()
        {
            int ms = Load(DelayValue, Settings.HotCornerDelayMs);
            return Array.IndexOf(Settings.HotCornerDelayChoices, ms) >= 0 ? ms : Settings.HotCornerDelayMs;
        }

        public static void SaveHotCornerDelay(int ms)
        {
            Save(DelayValue, ms);
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
