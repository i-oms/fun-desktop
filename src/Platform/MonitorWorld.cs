using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using IdleWalker.Domain;

namespace IdleWalker.Platform
{
    /// <summary>The real monitors, as terrain for the walker.</summary>
    sealed class MonitorWorld : IWorld
    {
        public int Left
        {
            get
            {
                int left = int.MaxValue;
                foreach (Screen screen in Screen.AllScreens) left = Math.Min(left, screen.Bounds.Left);
                return left;
            }
        }

        public int Right
        {
            get
            {
                int right = int.MinValue;
                foreach (Screen screen in Screen.AllScreens) right = Math.Max(right, screen.Bounds.Right);
                return right;
            }
        }

        public IEnumerable<int> MonitorEdges
        {
            get
            {
                foreach (Screen screen in Screen.AllScreens)
                {
                    yield return screen.Bounds.Left;
                    yield return screen.Bounds.Right;
                }
            }
        }

        public Rectangle MonitorBoundsAt(Point point)
        {
            return Screen.FromPoint(point).Bounds;
        }

        // The surface to stand on at x: the top of the taskbar, or the bottom
        // of the screen where there is none.
        public double GroundAt(double x)
        {
            return ScreenUnder((int)Math.Floor(x)).WorkingArea.Bottom;
        }

        public int ScaleAt(double x)
        {
            return ScaleFor(ScreenUnder((int)Math.Floor(x)));
        }

        // The monitor whose horizontal span contains x. When monitors are
        // stacked vertically the lowest one wins; in a gap, the nearest one.
        static Screen ScreenUnder(int x)
        {
            Screen best = null;
            int bestDistance = int.MaxValue;
            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle b = screen.Bounds;
                int distance = x < b.Left ? b.Left - x : (x >= b.Right ? x - b.Right + 1 : 0);
                if (distance < bestDistance ||
                    (distance == bestDistance && b.Bottom > best.Bounds.Bottom))
                {
                    best = screen;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // Sprite cell size for a monitor, following its display scaling.
        static int ScaleFor(Screen screen)
        {
            Rectangle b = screen.Bounds;
            Native.POINT centre = new Native.POINT(b.Left + b.Width / 2, b.Top + b.Height / 2);
            IntPtr monitor = Native.MonitorFromPoint(centre, Native.MONITOR_DEFAULTTONEAREST);
            uint dpiX, dpiY;
            if (Native.GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) != 0) dpiX = 96;
            return Math.Max(2, (int)Math.Round(Settings.BasePixelSize * dpiX / 96.0));
        }
    }
}
