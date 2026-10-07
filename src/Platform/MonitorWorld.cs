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
        int[] edges = new int[0];
        int left, right;

        /// <summary>Re-reads the monitor layout. Call once at the start of each frame.</summary>
        public void Refresh()
        {
            Screen[] screens = Screen.AllScreens;
            if (monitors.Length != screens.Length)
            {
                monitors = new Monitor[screens.Length];
                edges = new int[screens.Length * 2];
            }

            left = int.MaxValue;
            right = int.MinValue;
            for (int i = 0; i < screens.Length; i++)
            {
                Rectangle bounds = screens[i].Bounds;
                monitors[i].Bounds = bounds;
                monitors[i].Ground = screens[i].WorkingArea.Bottom;
                monitors[i].Scale = ScaleFor(bounds);
                edges[2 * i] = bounds.Left;
                edges[2 * i + 1] = bounds.Right;
                left = Math.Min(left, bounds.Left);
                right = Math.Max(right, bounds.Right);
            }
        }

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
