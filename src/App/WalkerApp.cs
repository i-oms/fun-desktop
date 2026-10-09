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
    /// Wires everything together: watches the cursor, steps the troupe once
    /// per frame, and renders each walker into its own sprite window.
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
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly List<View> views = new List<View>();
        readonly Troupe troupe;
        readonly TrayMenu tray;
        readonly FramePump pump;

        double lastTick;
        double nextTopmost;
        AboutToast about;           // the card while it is showing

        public WalkerApp()
        {
            troupe = new Troupe(monitors, new Random());
            troupe.Count = UserSettings.LoadWalkerCount();
            troupe.Dress = UserSettings.LoadDress();

            // Before the menu reads the tick state.
            StartupRegistration.ApplyDefault();

            tray = new TrayMenu(troupe.Count, UserSettings.LoadIdleSeconds(), troupe.Dress);
            tray.ExitRequested += delegate { ExitThread(); };
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
            bool paused = tray.Paused;
            bool wantOut = !paused && cursor.IdleFor(now, tray.IdleSeconds);

            troupe.Step(dt, cursor.Position, wantOut, moved || paused);

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
