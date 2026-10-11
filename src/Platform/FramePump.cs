using System;
using System.Diagnostics;
using System.Threading;

namespace IdleWalker.Platform
{
    /// <summary>
    /// Paces the animation: one tick per screen refresh while animating, a
    /// lazy poll while idle. Ticks are delivered on the UI thread.
    /// The idle poll also drives the hot corners, so it must sample well
    /// within the shortest corner dwell (100 ms): 30 ms gives three samples
    /// per dwell with negligible cost (GetCursorPos plus a few integer
    /// compares; the DPI lookup is cached in MonitorWorld).
    /// </summary>
    sealed class FramePump
    {
        const int WatchIntervalMs = 30;

        readonly SynchronizationContext sync;
        readonly SendOrPostCallback callback;
        readonly Thread thread;
        volatile bool animating;
        volatile bool halfRate;
        volatile bool stopping;
        int pending;

        public FramePump(SynchronizationContext sync, Action onTick)
        {
            this.sync = sync;
            callback = delegate
            {
                Interlocked.Exchange(ref pending, 0);
                if (!stopping) onTick();
            };
            thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Name = "IdleWalker frame pump";
        }

        /// <summary>True to tick at the monitor's refresh rate, false to poll slowly.</summary>
        public bool Animating
        {
            get { return animating; }
            set { animating = value; }
        }

        /// <summary>True to tick every other refresh instead of every one (on battery).</summary>
        public bool HalfRate
        {
            get { return halfRate; }
            set { halfRate = value; }
        }

        public void Start()
        {
            thread.Start();
        }

        public void Stop()
        {
            stopping = true;
        }

        void Loop()
        {
            Stopwatch frame = Stopwatch.StartNew();
            while (!stopping)
            {
                if (animating)
                {
                    frame.Restart();
                    // DwmFlush returns at the next vertical blank. Guard against
                    // it returning at once (no compositor, remote session).
                    if (Native.DwmFlush() != 0 || frame.ElapsedMilliseconds < 3) Thread.Sleep(12);
                    if (halfRate && (Native.DwmFlush() != 0 || frame.ElapsedMilliseconds < 20)) Thread.Sleep(12);
                }
                else
                {
                    Thread.Sleep(WatchIntervalMs);
                }

                // Never queue a second tick while one is waiting.
                if (Interlocked.Exchange(ref pending, 1) != 0) continue;
                try
                {
                    sync.Post(callback, null);
                }
                catch (InvalidOperationException)
                {
                    return; // the UI thread has gone away
                }
            }
        }
    }
}
