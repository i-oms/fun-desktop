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

        public WalkerApp()
        {
            troupe = new Troupe(monitors, new Random());
            troupe.Count = UserSettings.LoadWalkerCount();

            tray = new TrayMenu(troupe.Count);
            tray.ExitRequested += delegate { ExitThread(); };
            tray.CountChanged += delegate
            {
                troupe.Count = tray.Count;
                UserSettings.SaveWalkerCount(tray.Count);
            };
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
            bool wantOut = !paused && cursor.IdleFor(now, Settings.IdleSeconds);

            troupe.Step(dt, cursor.Position, wantOut, moved || paused);

            bool raise = now >= nextTopmost;
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pump.Stop();
                tray.Dispose();
                foreach (View view in views) view.Window.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
