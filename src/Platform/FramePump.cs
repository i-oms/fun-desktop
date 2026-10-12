using System;
using System.Diagnostics;
using System.Threading;

namespace IdleWalker.Platform
{
    /// <summary>
    /// Paces the animation: one tick per screen refresh while animating (but
    /// never faster than about 100 Hz), a lazy poll while idle. Ticks are
    /// delivered on the UI thread.
    /// The idle poll also drives the hot corners, so it must sample well
    /// within the shortest corner dwell (100 ms): 30 ms gives three samples
    /// per dwell with negligible cost (GetCursorPos plus a few integer
    /// compares; the DPI lookup is cached in MonitorWorld).
    /// </summary>
    sealed class FramePump
    {
        const int WatchIntervalMs = 30;

        // The animation never ticks faster than about 100 Hz: on a 120, 144
        // or 240 Hz display a tick waits for the second (or third) vertical
        // blank. The walkers move a pixel or two per tick either way, and
        // the cost of a frame no longer grows with the refresh rate. On
        // battery the floor doubles: every other refresh of a 60 Hz panel.
        const double MinTickMs = 9;
        const double HalfRateMinTickMs = 20;
        const int MaxFlushesPerTick = 8;

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
                    // DwmFlush returns at the next vertical blank; keep waiting
                    // for blanks until the tick is long enough. Guard against
                    // it failing or returning at once (no compositor, remote
                    // session): then pace by the clock instead.
                    double minMs = halfRate ? HalfRateMinTickMs : MinTickMs;
                    for (int n = 0; n < MaxFlushesPerTick; n++)
                    {
                        if (Native.DwmFlush() != 0)
                        {
                            Thread.Sleep((int)minMs + 3);
                            break;
                        }
                        if (frame.Elapsed.TotalMilliseconds >= minMs) break;
                    }
                    if (frame.Elapsed.TotalMilliseconds < 3) Thread.Sleep((int)minMs + 3);
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
