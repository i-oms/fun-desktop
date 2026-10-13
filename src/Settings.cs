using IdleWalker.Domain;

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

        /// <summary>How many walkers can be ticked on at once (one menu entry per Minion).</summary>
        public const int MaxWalkers = 10;

        /// <summary>How many come out on a fresh install (the first ones ticked).</summary>
        public const int DefaultWalkers = 5;

        /// <summary>
        /// The share of one CPU core the painter may use (0..1). When the cast
        /// needs more than this - many walkers, a slow machine - they are drawn
        /// every 2nd (3rd, 4th) refresh instead of every one, while still moving
        /// at full rate. The frame rate degrades; the computer does not.
        /// </summary>
        public const double PaintBudget = 0.5;

        /// <summary>The furthest the budget may thin the drawing: every n-th refresh at most.</summary>
        public const int MaxPaintEvery = 4;

        /// <summary>How many Angry Birds can be ticked on at once (one menu entry per bird).</summary>
        public const int MaxBirds = 10;

        /// <summary>How many birds come out on a fresh install (the first ones ticked).</summary>
        public const int DefaultBirds = 3;

        /// <summary>
        /// The overalls colours the Dress menu offers, by name, as 0xRRGGBB. The
        /// first is the default - the classic blue. The menu also offers Random
        /// and a colour picker for anything else.
        /// </summary>
        public static readonly string[] DressNames = { "Classic blue", "Red", "Green", "Purple", "Orange", "Pink", "Teal", "Black" };
        public static readonly int[] DressColors = { 0x2B59B8, 0xC0392B, 0x2E8B57, 0x6E3FB0, 0xE07A1F, 0xE2559A, 0x1F8A8A, 0x2A2A30 };

        /// <summary>The Dress menu's "Random" entry: every Minion picks its own colour each time it comes out.</summary>
        public const int RandomDress = -1;

        /// <summary>The master switch: whether anyone comes out at all until the tray menu says otherwise.</summary>
        public const bool WalkersEnabledByDefault = true;

        /// <summary>Whether the Minions are on until the tray menu says otherwise.</summary>
        public const bool MinionsEnabledByDefault = true;

        /// <summary>Whether Mr. Bean is on until the tray menu says otherwise. He and the Minions are independent of each other.</summary>
        public const bool MrBeanByDefault = false;

        /// <summary>Whether the Angry Birds are on until the tray menu says otherwise. Independent of the Minions and Mr. Bean.</summary>
        public const bool BirdsEnabledByDefault = false;

        /// <summary>Whether the Pink Panther is on until the tray menu says otherwise. Independent of the rest.</summary>
        public const bool PantherByDefault = false;

        /// <summary>Whether a mouse move sends everyone back underground until the tray menu says otherwise.</summary>
        public const bool HideOnMoveByDefault = true;

        /// <summary>Whether the hot corners are on until the tray menu says otherwise.</summary>
        public const bool HotCornersByDefault = true;

        /// <summary>
        /// Whether "Keep system awake" is on until the tray menu says otherwise.
        /// Off by default: blocking sleep is the user's choice, never silent.
        /// </summary>
        public const bool KeepAwakeByDefault = false;

        /// <summary>
        /// While "Keep system awake" is on and the real mouse has sat still
        /// this many seconds, a harmless F15 tap is sent this often to reset
        /// the idle timer. Kept well under a one-minute screen-saver timeout,
        /// so the session never reads as idle. Only fires once the user is
        /// away, so it never disturbs active work.
        /// </summary>
        public const double AntiIdleSeconds = 30.0;

        /// <summary>What each corner does until the tray menu says otherwise, in Corner order.</summary>
        public static readonly CornerAction[] DefaultCornerActions =
        {
            CornerAction.TaskView, CornerAction.Notifications, CornerAction.StartMenu, CornerAction.ShowDesktop
        };

        /// <summary>How far in from a corner's two edges (pixels at 100% scaling, grown with DPI) still counts as being in it.</summary>
        public const int HotCornerReach = 6;

        /// <summary>How many extra pixels of wobble past the trigger box still count as holding the corner (hysteresis).</summary>
        public const int HotCornerReleaseExtra = 8;

        /// <summary>Two corner points this close together are one physical hotspot (neighbouring monitors on a shared seam).</summary>
        public const int HotCornerCoLocate = 6;

        /// <summary>How long the cursor must rest in a corner before it fires (ms), and the choices the menu offers.</summary>
        public const int HotCornerDelayMs = 300;
        public static readonly int[] HotCornerDelayChoices = { 100, 200, 300, 500, 1000 };

        /// <summary>After a corner fires, the same corner cannot fire again for this long (ms): toggle actions can't bounce.</summary>
        public const int HotCornerRefireCooldownMs = 1500;

        /// <summary>Whether "Start with Windows" is switched on the first time the program runs.</summary>
        public const bool StartWithWindowsByDefault = true;

        /// <summary>How fast each walker updates its learned preferences (0..1). Higher = changes its mind faster.</summary>
        public const double LearnRate = 0.12;

        /// <summary>How often a walker tries something unexpected instead of its favourite (0..1).</summary>
        public const double ExploreRate = 0.15;

        public const double WalkSpeed = 90;
        public const double Acceleration = 420;  // easing into a walk
        public const double Braking = 520;       // easing to a stop
        public const double Gravity = 1100;
        public const double HopSpeed = 320;      // take-off speed of the occasional hop
        public const double StartleSpeed = 210;  // the little jump when the mouse moves
        public const double ExitGravity = 2200;  // how briskly it dives away afterwards
    }
}
