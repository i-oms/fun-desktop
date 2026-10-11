using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using IdleWalker.Domain;

namespace IdleWalker.Platform
{
    /// <summary>
    /// The real monitors, as terrain for the walkers. Monitor data is read
    /// once per frame by <see cref="Refresh"/>; every query after that is a
    /// lookup in a tiny array, however many walkers are asking.
    /// </summary>
    sealed class MonitorWorld : IWorld
    {
        struct Monitor
        {
            public Rectangle Bounds;
            public int Ground; // top of the taskbar, or the screen bottom where there is none
            public int Scale;  // sprite cell size for this monitor's DPI
        }

        Monitor[] monitors = new Monitor[0];
        Rectangle[] monitorBounds = new Rectangle[0];
        int[] monitorScales = new int[0];
        int[] edges = new int[0];
        int left, right;

        /// <summary>Re-reads the monitor layout. Call once at the start of each frame.</summary>
        public void Refresh()
        {
            Screen[] screens = Screen.AllScreens;
            if (monitors.Length != screens.Length)
            {
                monitors = new Monitor[screens.Length];
                monitorBounds = new Rectangle[screens.Length];
                monitorScales = new int[screens.Length];
                edges = new int[screens.Length * 2];
            }

            left = int.MaxValue;
            right = int.MinValue;
            for (int i = 0; i < screens.Length; i++)
            {
                Rectangle bounds = screens[i].Bounds;
                // The DPI lookup (MonitorFromPoint + GetDpiForMonitor) is the
                // only expensive part here, so only pay it when the layout
                // actually changed. Bounds themselves stay fresh every tick
                // so plug/unplug and resolution changes apply at once.
                if (monitors[i].Bounds.Equals(bounds) && monitors[i].Scale > 0)
                {
                    monitorBounds[i] = bounds;
                }
                else
                {
                    monitors[i].Bounds = bounds;
                    monitorBounds[i] = bounds;
                    monitors[i].Scale = ScaleFor(bounds);
                    monitorScales[i] = monitors[i].Scale;
                }
                if (monitorScales[i] <= 0) monitorScales[i] = monitors[i].Scale;
                monitors[i].Ground = screens[i].WorkingArea.Bottom;
                edges[2 * i] = bounds.Left;
                edges[2 * i + 1] = bounds.Right;
                left = Math.Min(left, bounds.Left);
                right = Math.Max(right, bounds.Right);
            }
        }

        /// <summary>Every monitor's bounds, as of the last Refresh (for the hot corners).</summary>
        public Rectangle[] MonitorBounds { get { return monitorBounds; } }

        /// <summary>Every monitor's sprite scale, parallel to MonitorBounds (for DPI-aware hot corners).</summary>
        public int[] MonitorScales { get { return monitorScales; } }

        public int Left { get { return left; } }
        public int Right { get { return right; } }
        public IEnumerable<int> MonitorEdges { get { return edges; } }

        public Rectangle MonitorBoundsAt(Point point)
        {
            return Screen.FromPoint(point).Bounds;
        }

        public double GroundAt(double x)
        {
            return monitors[IndexUnder((int)Math.Floor(x))].Ground;
        }

        public int ScaleAt(double x)
        {
            return monitors[IndexUnder((int)Math.Floor(x))].Scale;
        }

        public int ScaleAtPoint(Point point)
        {
            int best = 0;
            int bestDist = int.MaxValue;
            for (int i = 0; i < monitors.Length; i++)
            {
                Rectangle b = monitors[i].Bounds;
                if (point.X >= b.Left && point.X < b.Right && point.Y >= b.Top && point.Y < b.Bottom) return monitors[i].Scale;
                int dx = point.X < b.Left ? b.Left - point.X : (point.X >= b.Right ? point.X - b.Right + 1 : 0);
                int dy = point.Y < b.Top ? b.Top - point.Y : (point.Y >= b.Bottom ? point.Y - b.Bottom + 1 : 0);
                int dist = dx > dy ? dx : dy;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }
            return monitors.Length > 0 ? monitors[best].Scale : Settings.BasePixelSize;
        }

        // The monitor whose horizontal span contains x. When monitors are
        // stacked vertically the lowest one wins; in a gap, the nearest one.
        int IndexUnder(int x)
        {
            int best = 0;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < monitors.Length; i++)
            {
                Rectangle b = monitors[i].Bounds;
                int distance = x < b.Left ? b.Left - x : (x >= b.Right ? x - b.Right + 1 : 0);
                if (distance < bestDistance ||
                    (distance == bestDistance && b.Bottom > monitors[best].Bounds.Bottom))
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // Sprite cell size for a monitor, following its display scaling.
        static int ScaleFor(Rectangle bounds)
        {
            Native.POINT centre = new Native.POINT(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            IntPtr monitor = Native.MonitorFromPoint(centre, Native.MONITOR_DEFAULTTONEAREST);
            uint dpiX, dpiY;
            if (Native.GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) != 0) dpiX = 96;
            return Math.Max(2, (int)Math.Round(Settings.BasePixelSize * dpiX / 96.0));
        }
    }
}
