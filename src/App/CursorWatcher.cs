using System;
using System.Drawing;
using IdleWalker.Platform;

namespace IdleWalker.App
{
    /// <summary>
    /// Tracks the mouse cursor and how long it has been still. Small
    /// movements below the tolerance are ignored so a jittery sensor can't
    /// keep resetting the clock.
    /// </summary>
    sealed class CursorWatcher
    {
        Native.POINT anchor;
        Native.POINT latest;   // the last sample, exactly
        double lastMoveTime;

        public CursorWatcher()
        {
            Native.GetCursorPos(out anchor);
            latest = anchor;
        }

        /// <summary>Where the cursor was at the last sample, to the pixel.</summary>
        public Point Latest
        {
            get { return new Point(latest.X, latest.Y); }
        }

        /// <summary>Where the cursor is resting.</summary>
        public Point Position
        {
            get { return new Point(anchor.X, anchor.Y); }
        }

        /// <summary>Samples the cursor; true if it has moved since the last sample.</summary>
        public bool Poll(double now)
        {
            Native.POINT cursor;
            // GetCursorPos fails on the secure desktop (UAC, lock screen);
            // that counts as not moving.
            if (!Native.GetCursorPos(out cursor)) return false;
            latest = cursor;

            bool moved = Math.Abs(cursor.X - anchor.X) > Settings.MoveTolerance ||
                         Math.Abs(cursor.Y - anchor.Y) > Settings.MoveTolerance;
            if (moved)
            {
                anchor = cursor;
                lastMoveTime = now;
            }
            return moved;
        }

        public bool IdleFor(double now, double seconds)
        {
            return now - lastMoveTime >= seconds;
        }
    }
}
