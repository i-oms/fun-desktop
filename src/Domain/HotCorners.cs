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
    /// in the cursor and the monitor rectangles.
    /// </summary>
    sealed class HotCorners
    {
        /// <summary>How long the cursor must rest in a corner before it fires.</summary>
        public double DwellSeconds = Settings.HotCornerDelayMs / 1000.0;

        /// <summary>How far in from the two edges (pixels) still counts as the corner.</summary>
        public int Reach = Settings.HotCornerReach;

        int armed = -1;       // the corner the cursor is in, as a Corner, or -1
        double armedSince;
        bool fired;

        /// <summary>
        /// Feeds one cursor sample. True when a corner has just fired, with
        /// which one in `corner`.
        /// </summary>
        public bool Step(double now, Point cursor, Rectangle[] monitors, bool buttonDown, out Corner corner)
        {
            corner = Corner.TopLeft;
            int found = -1;
            for (int i = 0; i < monitors.Length && found < 0; i++)
            {
                Rectangle b = monitors[i];
                bool left = cursor.X >= b.Left && cursor.X < b.Left + Reach;
                bool right = cursor.X < b.Right && cursor.X >= b.Right - Reach;
                bool top = cursor.Y >= b.Top && cursor.Y < b.Top + Reach;
                bool bottom = cursor.Y < b.Bottom && cursor.Y >= b.Bottom - Reach;
                if (top && left) found = (int)Corner.TopLeft;
                else if (top && right) found = (int)Corner.TopRight;
                else if (bottom && left) found = (int)Corner.BottomLeft;
                else if (bottom && right) found = (int)Corner.BottomRight;
            }

            if (found < 0)
            {
                armed = -1;
                return false;
            }
            if (found != armed)
            {
                armed = found;
                armedSince = now;
                fired = false;
            }
            if (buttonDown)
            {
                armedSince = now;   // dragging through: the dwell starts over once the button is up
                return false;
            }
            if (fired || now - armedSince < DwellSeconds - 1e-6) return false;   // a hair of slack for summed frame times
            fired = true;
            corner = (Corner)found;
            return true;
        }

        /// <summary>Forget the corner the cursor is in, so it has to leave and come back.</summary>
        public void Reset()
        {
            armed = -1;
            fired = false;
        }
    }
}
