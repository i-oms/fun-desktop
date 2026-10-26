using System.Collections.Generic;
using System.Drawing;

namespace IdleWalker.Domain
{
    /// <summary>
    /// The terrain the walker lives on: the bottom edges of the monitors.
    /// Keeps the behaviour code free of any Windows specifics.
    /// </summary>
    interface IWorld
    {
        /// <summary>Outer left edge of the whole desktop, in pixels.</summary>
        int Left { get; }

        /// <summary>Outer right edge of the whole desktop, in pixels.</summary>
        int Right { get; }

        /// <summary>Every monitor's left and right edge. Read every tick by
        /// every walking walker, so an array: no enumerator to allocate.</summary>
        int[] MonitorEdges { get; }

        /// <summary>Bounds of the monitor containing a point.</summary>
        Rectangle MonitorBoundsAt(Point point);

        /// <summary>The surface to stand on at x (top of the taskbar, or screen bottom).</summary>
        double GroundAt(double x);

        /// <summary>Sprite cell size in pixels for the monitor under x.</summary>
        int ScaleAt(double x);
    }
}
