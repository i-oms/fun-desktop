using System;
using System.Drawing;

namespace IdleWalker.Domain
{
    /// <summary>A corner of a monitor. The order is the order of the tray menu and the registry values.</summary>
    enum Corner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>What pushing the cursor into a hot corner does.</summary>
    enum CornerAction
    {
        None,
        TaskView,       // Win+Tab
        ShowDesktop,    // Win+D
        StartMenu,      // Ctrl+Esc
        Notifications,  // Win+N, the notification centre
        QuickSettings,  // Win+A
        LockScreen,     // the lock screen
        ScreenSaver,    // start the screen saver
        MonitorOff,     // put the monitors to sleep
        CallMinions     // bring the Minions out without waiting for the rest time
    }

    /// <summary>
    /// Watches for the cursor resting in a corner of any monitor. A corner
    /// fires once when the cursor has sat in it for the dwell time, and not
    /// again until the cursor has left it; a drag through a corner (a mouse
    /// button held) never fires. No Windows code in here: the caller feeds
    /// in the cursor, the monitor rectangles and (optionally) each monitor's
    /// sprite scale so the trigger zone stays the same physical size on
    /// high-DPI displays.
    ///
    /// Multi-monitor notes (why this looks the way it does):
    ///  - The armed spot keeps its monitor identity, not just the corner
    ///    type. Two monitors side by side share an edge: the left monitor's
    ///    TopRight and the right monitor's TopLeft are 1 px apart. Only
    ///    remembering TopRight vs TopLeft would reset the dwell on a 1 px
    ///    jitter across the seam, so inner corners could never fire. Here
    ///    co-located seam corners are treated as one physical hotspot.
    ///  - Arming uses a small box (Reach) but disarming uses a bigger one
    ///    (Reach + ReleaseExtra). Sensor jitter of a few px no longer resets
    ///    the dwell, on the main screen or anywhere else.
    ///  - The nearest corner across ALL monitors wins, not the first
    ///    monitor in the list, so overlapping / duplicate layouts behave.
    ///  - After firing (or being reset while armed, e.g. a full-screen app
    ///    takes over), the cursor must leave the spot's release zone before
    ///    anything can arm again, and the same hotspot stays quiet for a
    ///    short cooldown. Toggle actions (Task view, Show desktop) therefore
    ///    cannot bounce open-close-open under a held-still cursor.
    /// Zero allocations per call, integer math only: cheap enough to sample
    /// at 30 Hz without being felt.
    /// </summary>
    sealed class HotCorners
    {
        /// <summary>How long the cursor must rest in a corner before it fires.</summary>
        public double DwellSeconds = Settings.HotCornerDelayMs / 1000.0;

        /// <summary>Fallback trigger size when no per-monitor scale is given.</summary>
        public int Reach = Settings.HotCornerReach;

        int armedMonitor = -1;  // index into the monitors array, or -1 when idle
        int armedCorner = -1;   // as a Corner
        int armedX;             // the exact corner pixel the dwell belongs to
        int armedY;
        int armedRelease;       // how far the cursor may stray before disarming
        double armedSince;
        bool fired;

        double lastFireTime = -1000;    // when the last fire happened
        int lastFireMonitor = -1;       // which hotspot fired last
        int lastFireCorner = -1;

        bool needLeave;         // fired or reset while armed: leave the zone before re-arming
        int leaveX, leaveY, leaveR;

        /// <summary>Trigger size for one monitor, keeping the physical target constant across DPIs.</summary>
        public static int ReachFor(int scale)
        {
            int reach = scale * 2;
            if (reach < Settings.HotCornerReach) reach = Settings.HotCornerReach;
            return reach;
        }

        static int Chebyshev(int x1, int y1, int x2, int y2)
        {
            int dx = x1 - x2;
            if (dx < 0) dx = -dx;
            int dy = y1 - y2;
            if (dy < 0) dy = -dy;
            return dx > dy ? dx : dy;
        }

        int ReleaseFor(int monitor, int[] scales)
        {
            if (scales != null && monitor >= 0 && monitor < scales.Length && scales[monitor] > 0)
                return ReachFor(scales[monitor]) + Settings.HotCornerReleaseExtra;
            return Reach + Settings.HotCornerReleaseExtra;
        }

        /// <summary>
        /// Claims a completed dwell. Hands back true exactly when the action
        /// should run; anything inside the refire cooldown is swallowed (but
        /// still counts as fired, so it cannot retry on the next sample).
        /// </summary>
        bool ClaimFire(double now, int mon, int c, int ax, int ay, int release, out int monitor, out Corner corner)
        {
            monitor = -1;
            corner = Corner.TopLeft;
            fired = true;
            if (mon == lastFireMonitor && c == lastFireCorner &&
                now - lastFireTime < Settings.HotCornerRefireCooldownMs / 1000.0) return false;
            lastFireTime = now;
            lastFireMonitor = mon;
            lastFireCorner = c;
            needLeave = true;
            leaveX = ax;
            leaveY = ay;
            leaveR = release;
            monitor = mon;
            corner = (Corner)c;
            return true;
        }

        /// <summary>
        /// Feeds one cursor sample. True when a corner has just fired, with
        /// which monitor and which corner in `monitor` / `corner`.
        /// </summary>
        public bool Step(double now, Point cursor, Rectangle[] monitors, int[] scales, bool buttonDown, out int monitor, out Corner corner)
        {
            monitor = -1;
            corner = Corner.TopLeft;

            // A fired (or reset) spot must be left before anything arms
            // again: holding the cursor still can never re-fire.
            if (needLeave)
            {
                if (Chebyshev(cursor.X, cursor.Y, leaveX, leaveY) <= leaveR) return false;
                needLeave = false;
            }

            int foundMonitor = -1;
            int foundCorner = -1;
            int foundX = 0;
            int foundY = 0;
            int foundDist = int.MaxValue;

            for (int i = 0; i < monitors.Length; i++)
            {
                Rectangle b = monitors[i];
                int reach;
                if (scales != null && i < scales.Length && scales[i] > 0) reach = ReachFor(scales[i]);
                else reach = Reach;

                bool left = cursor.X >= b.Left && cursor.X < b.Left + reach;
                bool right = cursor.X < b.Right && cursor.X >= b.Right - reach;
                bool top = cursor.Y >= b.Top && cursor.Y < b.Top + reach;
                bool bottom = cursor.Y < b.Bottom && cursor.Y >= b.Bottom - reach;
                if (!((top && left) || (top && right) || (bottom && left) || (bottom && right))) continue;

                // One monitor matched: work out which corner and how central.
                int c;
                int ax;
                int ay;
                if (top && left) { c = (int)Corner.TopLeft; ax = b.Left; ay = b.Top; }
                else if (top && right) { c = (int)Corner.TopRight; ax = b.Right - 1; ay = b.Top; }
                else if (bottom && left) { c = (int)Corner.BottomLeft; ax = b.Left; ay = b.Bottom - 1; }
                else { c = (int)Corner.BottomRight; ax = b.Right - 1; ay = b.Bottom - 1; }

                int dist = Chebyshev(cursor.X, cursor.Y, ax, ay);
                if (dist < foundDist)
                {
                    foundDist = dist;
                    foundMonitor = i;
                    foundCorner = c;
                    foundX = ax;
                    foundY = ay;
                }
            }

            if (foundMonitor >= 0)
            {
                if (armedMonitor < 0)
                {
                    armedMonitor = foundMonitor;
                    armedCorner = foundCorner;
                    armedX = foundX;
                    armedY = foundY;
                    armedRelease = ReleaseFor(foundMonitor, scales);
                    armedSince = now;
                    fired = false;
                }
                else if (foundMonitor != armedMonitor || foundCorner != armedCorner)
                {
                    // A different monitor corner: same physical hotspot
                    // (seam neighbours are 1 px apart) keeps the dwell,
                    // a truly different corner restarts it.
                    int gap = Chebyshev(foundX, foundY, armedX, armedY);
                    armedMonitor = foundMonitor;
                    armedCorner = foundCorner;
                    armedX = foundX;
                    armedY = foundY;
                    armedRelease = ReleaseFor(foundMonitor, scales);
                    if (gap > Settings.HotCornerCoLocate)
                    {
                        armedSince = now;
                        fired = false;
                    }
                }
                // else: same spot, dwell continues.

                if (buttonDown)
                {
                    armedSince = now;   // dragging through: the dwell starts over once the button is up
                    return false;
                }
                if (fired || now - armedSince < DwellSeconds - 1e-6) return false;   // a hair of slack for summed frame times
                return ClaimFire(now, foundMonitor, foundCorner, foundX, foundY, ReleaseFor(foundMonitor, scales), out monitor, out corner);
            }

            // Not inside any arm box: hysteresis keeps the dwell through a
            // few px of jitter (or a step across a seam) instead of
            // disarming at once.
            if (armedMonitor >= 0)
            {
                if (Chebyshev(cursor.X, cursor.Y, armedX, armedY) <= armedRelease)
                {
                    if (buttonDown)
                    {
                        armedSince = now;
                        return false;
                    }
                    if (fired || now - armedSince < DwellSeconds - 1e-6) return false;
                    return ClaimFire(now, armedMonitor, armedCorner, armedX, armedY, armedRelease, out monitor, out corner);
                }
                armedMonitor = -1;
                armedCorner = -1;
            }
            return false;
        }

        /// <summary>
        /// Feeds one cursor sample. True when a corner has just fired, with
        /// which one in `corner`. The monitor is not reported.
        /// </summary>
        public bool Step(double now, Point cursor, Rectangle[] monitors, bool buttonDown, out Corner corner)
        {
            int monitor;
            return Step(now, cursor, monitors, null, buttonDown, out monitor, out corner);
        }

        /// <summary>
        /// Forget the corner the cursor is in. If one was armed, the cursor
        /// must still leave its zone before anything arms again, so a reset
        /// (full-screen app, switch toggled) can never cause a re-fire under
        /// a held-still cursor.
        /// </summary>
        public void Reset()
        {
            if (armedMonitor >= 0)
            {
                needLeave = true;
                leaveX = armedX;
                leaveY = armedY;
                leaveR = armedRelease;
            }
            armedMonitor = -1;
            armedCorner = -1;
            fired = false;
        }
    }
}
