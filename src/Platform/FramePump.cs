using System;
using System.Diagnostics;
using System.Threading;

namespace IdleWalker.Platform
{
    /// <summary>
    /// Paces the animation: one tick per screen refresh while animating, a
    /// lazy poll while idle. Ticks are delivered on the UI thread.
    /// </summary>
    sealed class FramePump
    {
        const int WatchIntervalMs = 100;

        readonly SynchronizationContext sync;
        readonly SendOrPostCallback callback;
        readonly Thread thread;
        volatile bool animating;
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
