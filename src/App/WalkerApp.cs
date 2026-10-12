using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using IdleWalker.Domain;
using IdleWalker.Platform;
using IdleWalker.Rendering;

namespace IdleWalker.App
{
    /// <summary>
    /// Wires everything together: watches the cursor, fires the hot corners,
    /// steps the troupe once per frame, and renders each walker into its own
    /// sprite window.
    /// </summary>
    sealed class WalkerApp : ApplicationContext
    {
        /// <summary>One walker's renderer and window.</summary>
        sealed class View
        {
            public readonly FrameRenderer Renderer = new FrameRenderer();
            public readonly SpriteWindow Window = new SpriteWindow();
        }

        readonly MonitorWorld monitors = new MonitorWorld();
        readonly CursorWatcher cursor = new CursorWatcher();
        readonly HotCorners corners = new HotCorners();
        readonly CornerRipple ripple = new CornerRipple();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly List<View> views = new List<View>();
        readonly Troupe troupe;
        readonly TrayMenu tray;
        readonly FramePump pump;
        readonly RenderThread paint = new RenderThread();

        double lastTick;
        double nextTopmost;
        double nextCheck;           // when to look again at the foreground window and the power
        double nextBrainSave;       // when to write what the walkers learned to disk
        int paintEvery = 1;         // snapshots on every n-th tick: 1 at full rate, more when the painter is over budget
        int tick;                   // tick counter, for paintEvery
        double loadSince;           // start of the current paint-load window
        double loadPaintMs;         // painter time spent in that window
        bool brainsDirty;           // somebody has been out since the last save: there is something new to write
        bool fullScreen;            // a full-screen app is in front: the Minions keep away
        bool summoned;              // a hot corner called the Minions out; cleared when the cursor moves
        AboutToast about;           // the card while it is showing
        BrainDialog brains;         // the brains card while it is open

        public WalkerApp()
        {
            troupe = new Troupe(monitors, new Random());
            troupe.Count = UserSettings.LoadWalkerCount();
            troupe.BirdCount = UserSettings.LoadBirdCount();
            troupe.Dress = UserSettings.LoadDress();
            troupe.MinionsEnabled = UserSettings.LoadMinionsEnabled();
            troupe.BeanEnabled = UserSettings.LoadBeanEnabled();
            troupe.BirdsEnabled = UserSettings.LoadBirdsEnabled();
            troupe.PantherEnabled = UserSettings.LoadPantherEnabled();
            BrainStore.Load(troupe);

            CornerAction[] actions = new CornerAction[4];
            for (int c = 0; c < actions.Length; c++) actions[c] = UserSettings.LoadCornerAction((Corner)c);
            int delayMs = UserSettings.LoadHotCornerDelay();
            corners.DwellSeconds = delayMs / 1000.0;

            // Before the menu reads the tick state.
            StartupRegistration.ApplyDefault();

            tray = new TrayMenu(troupe.Count, troupe.BirdCount, UserSettings.LoadIdleSeconds(), troupe.Dress,
                                UserSettings.LoadWalkersEnabled(), troupe.MinionsEnabled, troupe.BeanEnabled, troupe.BirdsEnabled, troupe.PantherEnabled,
                                UserSettings.LoadHideOnMove(),
                                UserSettings.LoadHotCornersEnabled(), actions, delayMs);
            tray.ExitRequested += delegate { ExitThread(); };
            tray.WalkersToggled += delegate { UserSettings.SaveWalkersEnabled(tray.WalkersEnabled); };
            tray.MinionsToggled += delegate
            {
                troupe.MinionsEnabled = tray.MinionsEnabled;
                UserSettings.SaveMinionsEnabled(tray.MinionsEnabled);
            };
            tray.CountChanged += delegate
            {
                troupe.Count = tray.Count;
                UserSettings.SaveWalkerCount(tray.Count);
            };
            tray.IdleChanged += delegate { UserSettings.SaveIdleSeconds(tray.IdleSeconds); };
            tray.HideOnMoveToggled += delegate { UserSettings.SaveHideOnMove(tray.HideOnMove); };
            tray.DressChanged += delegate
            {
                troupe.Dress = tray.Dress;
                UserSettings.SaveDress(tray.Dress);
            };
            tray.BeanToggled += delegate
            {
                troupe.BeanEnabled = tray.BeanEnabled;
                UserSettings.SaveBeanEnabled(tray.BeanEnabled);
            };
            tray.PantherToggled += delegate
            {
                troupe.PantherEnabled = tray.PantherEnabled;
                UserSettings.SavePantherEnabled(tray.PantherEnabled);
            };
            tray.BirdsToggled += delegate
            {
                troupe.BirdsEnabled = tray.BirdsEnabled;
                UserSettings.SaveBirdsEnabled(tray.BirdsEnabled);
            };
            tray.BirdCountChanged += delegate
            {
                troupe.BirdCount = tray.BirdCount;
                UserSettings.SaveBirdCount(tray.BirdCount);
            };
            tray.HotCornersToggled += delegate
            {
                corners.Reset();
                UserSettings.SaveHotCornersEnabled(tray.HotCornersEnabled);
            };
            tray.CornerChanged += delegate(Corner corner) { UserSettings.SaveCornerAction(corner, tray.CornerActionFor(corner)); };
            tray.DelayChanged += delegate
            {
                corners.DwellSeconds = tray.DelayMs / 1000.0;
                UserSettings.SaveHotCornerDelay(tray.DelayMs);
            };
            tray.AboutRequested += delegate { ShowAbout(); };
            tray.BrainsRequested += delegate { ShowBrains(); };
            StartupRegistration.RefreshPath();

            if (SynchronizationContext.Current == null)
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            pump = new FramePump(SynchronizationContext.Current, OnTick);
            paint.Start();
            pump.Start();
        }

        void OnTick()
        {
            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(now - lastTick, 0.05);
            lastTick = now;

            monitors.Refresh();
            bool moved = cursor.Poll(now);
            if (moved) summoned = false;

            // Twice a second: a full-screen app in front keeps the Minions
            // away (and sends them off if they are out), and on battery the
            // animation runs at half rate. Checked before the hot corners so
            // the corners can honour it too (like WinXCorners: no firing
            // while a game or full-screen video is in front).
            if (now >= nextCheck)
            {
                nextCheck = now + 0.5;
                bool wasFull = fullScreen;
                fullScreen = ForegroundWatch.IsFullScreen();
                if (fullScreen && !wasFull) LogNote("foreground fullscreen: corners held");
                else if (!fullScreen && wasFull) LogNote("foreground windowed: corners live");
                pump.HalfRate = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
            }

            // The hot corners: the cursor resting in a corner of any monitor.
            // (The ripple ticks by itself once fired, so it needs no stepping here.)
            if (tray.HotCornersEnabled && !fullScreen)
            {
                int firedMonitor;
                Corner corner;
                if (corners.Step(now, cursor.Latest, monitors.MonitorBounds, monitors.MonitorScales, CornerActions.MouseButtonDown(), out firedMonitor, out corner))
                {
                    CornerAction action = tray.CornerActionFor(corner);
                    LogFire(firedMonitor, corner, action, cursor.Latest);
                    if (action != CornerAction.None) RippleFor(firedMonitor, corner);
                    if (action == CornerAction.CallMinions) summoned = tray.WalkersEnabled;
                    else CornerActions.Perform(action);
                }
            }
            else if (fullScreen)
            {
                corners.Reset();
            }

            // Every minute: write what they learned to disk, so a restart
            // (or a crash) keeps their taste, friendships and walks. Only
            // when somebody has been out since the last save - a hidden
            // walker learns nothing - and the disk work runs off this thread.
            if (now >= nextBrainSave)
            {
                nextBrainSave = now + 60;
                if (brainsDirty)
                {
                    brainsDirty = false;
                    BrainStore.SaveInBackground(troupe);
                }
            }

            // The master switch; which of the Minions and Mr. Bean actually come out is the troupe's business.
            // A mouse move only sends them away while "Hide on mouse move" is ticked.
            bool enabled = tray.WalkersEnabled && !fullScreen;
            bool wantOut = enabled && (summoned || cursor.IdleFor(now, tray.IdleSeconds));
            troupe.Step(dt, cursor.Position, wantOut, (moved && tray.HideOnMove) || !enabled);

            // The walkers are re-raised once a second, except while the
            // About card is up: that belongs above them.
            bool raise = now >= nextTopmost && about == null;
            if (raise) nextTopmost = now + 1;

            // The paint budget. Once a second the painter is asked how long it
            // spent; scaled back to full rate, that is the cast's demand as a
            // share of one core. Over Settings.PaintBudget, snapshots are
            // taken every 2nd (3rd, 4th) tick instead of every tick: the cast
            // keeps moving at full rate and is drawn at a lower, steady one,
            // whatever its size and whatever the machine. The rate comes back
            // up only once the demand would fit comfortably (hysteresis), so
            // it never flickers between two rates.
            loadPaintMs += paint.TakePaintMs();
            if (now - loadSince >= 1)
            {
                double demand = loadPaintMs / ((now - loadSince) * 1000) * paintEvery;
                int want = (int)Math.Ceiling(demand / Settings.PaintBudget);
                if (want < 1) want = 1; else if (want > Settings.MaxPaintEvery) want = Settings.MaxPaintEvery;
                if (want > paintEvery || (want < paintEvery && demand <= 0.8 * Settings.PaintBudget * want))
                    paintEvery = want;
                loadSince = now;
                loadPaintMs = 0;
            }
            tick++;
            bool snapshot = tick % paintEvery == 0;

            // Painting is split across threads: snapshots are cheap and made
            // here for every walker that changed, the render thread turns
            // them into pixels, and only the finished frames are shown -
            // a memcpy plus one window call each. The UI thread never does
            // pixel math, so menus, dialogs and the desktop stay fluid while
            // a full cast animates at full rate.
            IList<Walker> walkers = troupe.Walkers;
            for (int i = 0; i < walkers.Count; i++)
            {
                while (views.Count <= i) views.Add(new View());
                View view = views[i];
                Walker walker = walkers[i];

                if (walker.IsHidden)
                {
                    view.Window.Hide();
                    view.Renderer.Invalidate();
                    continue;
                }

                if (snapshot)
                {
                    RenderJob job;
                    if (view.Renderer.Prepare(walker, out job)) paint.Request(i, job);
                }
                if (raise) view.Window.StayOnTop();
            }

            PaintResult result;
            while (paint.TryTake(out result))
            {
                if (result.Walker < 0 || result.Walker >= views.Count)
                {
                    paint.Release(result.Surface);
                    continue;
                }
                View view = views[result.Walker];
                if (result.Walker >= walkers.Count || walkers[result.Walker].IsHidden ||
                    result.Stamp != view.Renderer.Stamp)
                {
                    paint.Release(result.Surface);   // stale: hid or moved on mid-paint
                    continue;
                }
                view.Window.Present(result.Surface, result.Left, result.Top);
                paint.Release(result.Surface);
            }

            bool anyVisible = troupe.AnyVisible;
            if (anyVisible) brainsDirty = true;
            pump.Animating = anyVisible;
        }

        /// <summary>
        /// One line per corner fire (and full-screen transition) in the temp
        /// folder, so an open-close-open report can be settled by counting:
        /// three toggles with one "fire" line is Windows, three "fire" lines
        /// is us. Fires are human-paced, so the file stays tiny.
        /// </summary>
        static void LogFire(int firedMonitor, Corner corner, CornerAction action, Point cursor)
        {
            try
            {
                string line = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} fire monitor={1} corner={2} action={3} cursor={4},{5}\r\n",
                    DateTime.Now, firedMonitor, corner, action, cursor.X, cursor.Y);
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "IdleWalker-hotcorners.log"), line);
            }
            catch (Exception)
            {
            }
        }

        static void LogNote(string note)
        {
            try
            {
                string line = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} {1}\r\n", DateTime.Now, note);
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "IdleWalker-hotcorners.log"), line);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>Starts the ripple on the monitor corner that just fired (no re-lookup: the seam neighbour may differ).</summary>
        void RippleFor(int firedMonitor, Corner corner)
        {
            Rectangle bounds;
            int scale;
            Rectangle[] all = monitors.MonitorBounds;
            int[] scales = monitors.MonitorScales;
            if (firedMonitor >= 0 && firedMonitor < all.Length)
            {
                bounds = all[firedMonitor];
                scale = (scales != null && firedMonitor < scales.Length && scales[firedMonitor] > 0) ? scales[firedMonitor] : monitors.ScaleAtPoint(new Point(bounds.Left, bounds.Top));
            }
            else
            {
                bounds = monitors.MonitorBoundsAt(cursor.Latest);
                scale = monitors.ScaleAtPoint(cursor.Latest);
            }
            int x, y;
            switch (corner)
            {
                case Corner.TopRight:
                    x = bounds.Right - 1;
                    y = bounds.Top;
                    break;
                case Corner.BottomLeft:
                    x = bounds.Left;
                    y = bounds.Bottom - 1;
                    break;
                case Corner.BottomRight:
                    x = bounds.Right - 1;
                    y = bounds.Bottom - 1;
                    break;
                default:
                    x = bounds.Left;
                    y = bounds.Top;
                    break;
            }
            ripple.Show(x, y, scale);
        }

        /// <summary>Shows the About card; a second click while it is already up is ignored.</summary>
        void ShowAbout()
        {
            if (about != null) return;
            about = new AboutToast(cursor.Position);
            about.FormClosed += delegate
            {
                about.Dispose();
                about = null;
            };
            about.Present();
        }

        /// <summary>Shows the brains card, modally: rendering continues on its
        /// own thread while it is open, so the card and the cast share
        /// nothing dear. A second click while it is up brings it forward.</summary>
        void ShowBrains()
        {
            if (brains != null)
            {
                try
                {
                    brains.Focus();
                }
                catch (Exception)
                {
                }
                return;
            }
            BrainDialog dialog = new BrainDialog(troupe);
            brains = dialog;
            try
            {
                dialog.ShowDialog();
            }
            finally
            {
                brains = null;
                dialog.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    BrainStore.Save(troupe);
                }
                catch (Exception)
                {
                }
                pump.Stop();
                try
                {
                    paint.Stop();
                }
                catch (Exception)
                {
                }
                try
                {
                    if (brains != null) brains.Dispose();
                }
                catch (Exception)
                {
                }
                if (about != null) about.Dispose();
                tray.Dispose();
                ripple.Dispose();
                foreach (View view in views) view.Window.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
