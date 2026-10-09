namespace IdleWalker
{
    /// <summary>
    /// The knobs worth turning. Speeds are in pixels per second at 100%
    /// display scaling; they are scaled up automatically on high-DPI monitors.
    /// </summary>
    static class Settings
    {
        /// <summary>Cursor rest time before the walker appears, until the user picks another from the tray menu.</summary>
        public const int IdleSeconds = 2;

        /// <summary>The rest times the tray menu offers, in seconds: one to five seconds, one to five minutes.</summary>
        public static readonly int[] IdleChoices = { 1, 2, 3, 4, 5, 60, 120, 180, 240, 300 };

        /// <summary>Pixels of sensor jitter that don't count as movement.</summary>
        public const int MoveTolerance = 2;

        /// <summary>Size of one sprite cell at 100% display scaling.</summary>
        public const int BasePixelSize = 3;

        /// <summary>How many walkers can be out at once (the tray menu offers 1..MaxWalkers).</summary>
        public const int MaxWalkers = 10;

        /// <summary>How many come out until the user picks another number.</summary>
        public const int DefaultWalkers = 5;

        /// <summary>
        /// The overalls colours the Dress menu offers, by name, as 0xRRGGBB. The
        /// first is the default - the classic blue. The menu also offers Random
        /// and a colour picker for anything else.
        /// </summary>
        public static readonly string[] DressNames = { "Classic blue", "Red", "Green", "Purple", "Orange", "Pink", "Teal", "Black" };
        public static readonly int[] DressColors = { 0x2B59B8, 0xC0392B, 0x2E8B57, 0x6E3FB0, 0xE07A1F, 0xE2559A, 0x1F8A8A, 0x2A2A30 };

        /// <summary>The Dress menu's "Random" entry: every Minion picks its own colour each time it comes out.</summary>
        public const int RandomDress = -1;

        /// <summary>Whether "Start with Windows" is switched on the first time the program runs.</summary>
        public const bool StartWithWindowsByDefault = true;

        public const double WalkSpeed = 90;
        public const double Acceleration = 420;  // easing into a walk
        public const double Braking = 520;       // easing to a stop
        public const double Gravity = 1100;
        public const double HopSpeed = 320;      // take-off speed of the occasional hop
        public const double StartleSpeed = 210;  // the little jump when the mouse moves
        public const double ExitGravity = 2200;  // how briskly it dives away afterwards
    }
}
