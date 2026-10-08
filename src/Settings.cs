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
        public const int MaxWalkers = 5;

        public const double WalkSpeed = 90;
        public const double Acceleration = 420;  // easing into a walk
        public const double Braking = 520;       // easing to a stop
        public const double Gravity = 1100;
        public const double HopSpeed = 320;      // take-off speed of the occasional hop
        public const double StartleSpeed = 210;  // the little jump when the mouse moves
        public const double ExitGravity = 2200;  // how briskly it dives away afterwards
    }
}
