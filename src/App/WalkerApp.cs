using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly List<View> views = new List<View>();
        readonly Troupe troupe;
        readonly TrayMenu tray;
        readonly FramePump pump;

        double lastTick;
        double nextTopmost;
        double nextCheck;           // when to look again at the foreground window and the power
        bool fullScreen;            // a full-screen app is in front: the Minions keep away
        bool summoned;              // a hot corner called the Minions out; cleared when the cursor moves
        AboutToast about;           // the card while it is showing

        public WalkerApp()
        {
            troupe = new Troupe(monitors, new Random());
            troupe.Count = UserSettings.LoadWalkerCount();
            troupe.Dress = UserSettings.LoadDress();
            troupe.BeanEnabled = UserSettings.LoadBeanEnabled();

            CornerAction[] actions = new CornerAction[4];
            for (int c = 0; c < actions.Length; c++) actions[c] = UserSettings.LoadCornerAction((Corner)c);
            int delayMs = UserSettings.LoadHotCornerDelay();
            corners.DwellSeconds = delayMs / 1000.0;

            // Before the menu reads the tick state.
            StartupRegistration.ApplyDefault();

            tray = new TrayMenu(troupe.Count, UserSettings.LoadIdleSeconds(), troupe.Dress,
                                UserSettings.LoadMinionsEnabled(), troupe.BeanEnabled,
                                UserSettings.LoadHotCornersEnabled(), actions, delayMs);
            tray.ExitRequested += delegate { ExitThread(); };
            tray.MinionsToggled += delegate { UserSettings.SaveMinionsEnabled(tray.MinionsEnabled); };
            tray.CountChanged += delegate
            {
                troupe.Count = tray.Count;
                UserSettings.SaveWalkerCount(tray.Count);
            };
            tray.IdleChanged += delegate { UserSettings.SaveIdleSeconds(tray.IdleSeconds); };
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
            StartupRegistration.RefreshPath();

            if (SynchronizationContext.Current == null)
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            pump = new FramePump(SynchronizationContext.Current, OnTick);
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
            if (tray.HotCornersEnabled)
            {
                Corner corner;
                if (corners.Step(now, cursor.Latest, monitors.MonitorBounds, CornerActions.MouseButtonDown(), out corner))
                {
                    CornerAction action = tray.CornerActionFor(corner);
                    if (action == CornerAction.CallMinions) summoned = tray.MinionsEnabled;
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

            bool enabled = tray.MinionsEnabled && !fullScreen;
            bool wantOut = enabled && (summoned || cursor.IdleFor(now, tray.IdleSeconds));
            troupe.Step(dt, cursor.Position, wantOut, moved || !enabled);

            // The walkers are re-raised once a second, except while the
            // About card is up: that belongs above them.
            bool raise = now >= nextTopmost && about == null;
            if (raise) nextTopmost = now + 1;

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

                if (view.Renderer.Render(walker))
                    view.Window.Present(view.Renderer.Surface, view.Renderer.Left, view.Renderer.Top);
                if (raise) view.Window.StayOnTop();
            }

            pump.Animating = troupe.AnyVisible;
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pump.Stop();
                if (about != null) about.Dispose();
                tray.Dispose();
                foreach (View view in views) view.Window.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
