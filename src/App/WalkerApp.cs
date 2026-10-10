using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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

            // The hot corners: the cursor resting in a corner of any monitor.
            // (The ripple ticks by itself once fired, so it needs no stepping here.)
            if (tray.HotCornersEnabled)
            {
                Corner corner;
                if (corners.Step(now, cursor.Latest, monitors.MonitorBounds, CornerActions.MouseButtonDown(), out corner))
                {
                    CornerAction action = tray.CornerActionFor(corner);
                    if (action != CornerAction.None) RippleFor(corner);
                    if (action == CornerAction.CallMinions) summoned = tray.WalkersEnabled;
                    else CornerActions.Perform(action);
                }
            }

            // Twice a second: a full-screen app in front keeps the Minions
            // away (and sends them off if they are out), and on battery the
            // animation runs at half rate.
            if (now >= nextCheck)
            {
                nextCheck = now + 0.5;
                fullScreen = ForegroundWatch.IsFullScreen();
                pump.HalfRate = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
            }

            // Every minute: write what they learned to disk, so a restart
            // (or a crash) keeps their taste, friendships and walks.
            if (now >= nextBrainSave)
            {
                nextBrainSave = now + 60;
                BrainStore.Save(troupe);
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

                RenderJob job;
                if (view.Renderer.Prepare(walker, out job)) paint.Request(i, job);
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

            pump.Animating = troupe.AnyVisible;
        }

        /// <summary>Starts the ripple on the corner that just fired.</summary>
        void RippleFor(Corner corner)
        {
            Rectangle bounds = monitors.MonitorBoundsAt(cursor.Latest);
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
            ripple.Show(x, y, monitors.ScaleAt(x));
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
