using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using IdleWalker.Domain;
using IdleWalker.Platform;
using IdleWalker.Rendering;

namespace IdleWalker.App
{
    /// <summary>
    /// Wires everything together: watches the cursor, steps the walker once
    /// per frame, renders it and shows it in the sprite window.
    /// </summary>
    sealed class WalkerApp : ApplicationContext
    {
        readonly SpriteWindow window = new SpriteWindow();
        readonly FrameRenderer renderer = new FrameRenderer();
        readonly CursorWatcher cursor = new CursorWatcher();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Walker walker;
        readonly TrayMenu tray;
        readonly FramePump pump;

        double lastTick;
        double nextTopmost;

        public WalkerApp()
        {
            walker = new Walker(new MonitorWorld(), new Random());

            tray = new TrayMenu();
            tray.ExitRequested += delegate { ExitThread(); };
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

            bool moved = cursor.Poll(now);
            bool paused = tray.Paused;

            if (walker.IsHidden)
            {
                if (paused || !cursor.IdleFor(now, Settings.IdleSeconds)) return;
                walker.Spawn(cursor.Position);
                pump.Animating = true;
            }
            else if (moved || paused)
            {
                walker.Startle();
            }

            walker.Step(dt, cursor.Position);
            if (walker.IsHidden)
            {
                window.Hide();
                pump.Animating = false;
                return;
            }

            renderer.Render(walker);
            window.Present(renderer.Surface, renderer.Left, renderer.Top);

            if (now >= nextTopmost)
            {
                window.StayOnTop();
                nextTopmost = now + 1;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pump.Stop();
                tray.Dispose();
                window.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
