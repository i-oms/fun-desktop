namespace IdleWalker
{
    /// <summary>
    /// The knobs worth turning. Speeds are in pixels per second at 100%
    /// display scaling; they are scaled up automatically on high-DPI monitors.
    /// </summary>
    static class Settings
    {
        /// <summary>Cursor rest time before the walker appears.</summary>
        public const double IdleSeconds = 2.0;

        /// <summary>Pixels of sensor jitter that don't count as movement.</summary>
        public const int MoveTolerance = 2;

        /// <summary>Size of one sprite cell at 100% display scaling.</summary>
        public const int BasePixelSize = 3;

        public const double WalkSpeed = 90;
        public const double Acceleration = 420;  // easing into a walk
        public const double Braking = 520;       // easing to a stop
        public const double Gravity = 1100;
        public const double HopSpeed = 320;      // take-off speed of the occasional hop
        public const double StartleSpeed = 210;  // the little jump when the mouse moves
        public const double ExitGravity = 2200;  // how briskly it dives away afterwards
    }
}
