using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace IdleWalker.Rendering
{
    /// <summary>A finished frame, ready to show.</summary>
    struct PaintResult
    {
        public int Walker;
        public uint Stamp;
        public Surface Surface;
        public int Left, Top;
    }

    /// <summary>
    /// The rasterizer: one background thread turning render snapshots into
    /// pixels, so the UI thread never does pixel math. The UI thread prepares
    /// cheap snapshots (<see cref="FrameRenderer.Prepare"/>), the worker
    /// paints them, and the UI thread shows the finished frames (windows keep
    /// their thread affinity: only pixels cross threads, never window calls).
    ///
    /// Per-walker coalescing keeps the queue bounded: if a walker changes
    /// twice before its frame is painted, only the latest snapshot is kept.
    /// When the worker falls behind, animation degrades gracefully instead
    /// of slowing the computer - frames are skipped, never waited for.
    /// A Surface is owned by exactly one thread at a time (pool hands it to
    /// the worker, the result hands it back, presenting returns it).
    /// </summary>
    sealed class RenderThread : IDisposable
    {
        // Room for every canvas the full cast can have in flight at once
        // (one per walker between painting and showing). A smaller pool
        // throws canvases away every frame once the cast outgrows it, which
        // at twenty walkers is tens of megabytes of garbage a second.
        const int PoolCap = 32;

        readonly object gate = new object();
        readonly Queue<int> queue = new Queue<int>();
        RenderJob[] slots = new RenderJob[0];
        bool[] waiting = new bool[0];
        readonly Queue<PaintResult> done = new Queue<PaintResult>();
        readonly Stack<Surface> pool = new Stack<Surface>();
        Thread thread;
        volatile bool stopping;
        bool disposed;
        long paintTicks;      // Stopwatch ticks spent painting since the last TakePaintMs

        public void Start()
        {
            thread = new Thread(Loop);
            thread.IsBackground = true;
            // Pixels are the one dear thing here, and they are never urgent:
            // below normal priority, the painter yields to whatever the user
            // is actually doing and the animation skips a frame instead.
            thread.Priority = ThreadPriority.BelowNormal;
            thread.Name = "IdleWalker paint";
            thread.Start();
        }

        /// <summary>
        /// Milliseconds the painter has spent painting since the last call:
        /// the load the cast puts on the machine, for the paint budget.
        /// </summary>
        public double TakePaintMs()
        {
            long ticks;
            lock (gate)
            {
                ticks = paintTicks;
                paintTicks = 0;
            }
            return ticks * 1000.0 / Stopwatch.Frequency;
        }

        /// <summary>Queues a snapshot; a newer one for the same walker wins.</summary>
        public void Request(int walker, RenderJob job)
        {
            lock (gate)
            {
                if (stopping || disposed) return;
                if (walker >= slots.Length)
                {
                    Array.Resize(ref slots, walker + 4);
                    Array.Resize(ref waiting, walker + 4);
                }
                slots[walker] = job;
                if (!waiting[walker])
                {
                    waiting[walker] = true;
                    queue.Enqueue(walker);
                    Monitor.Pulse(gate);
                }
            }
        }

        /// <summary>Takes a finished frame, if any.</summary>
        public bool TryTake(out PaintResult result)
        {
            lock (gate)
            {
                if (done.Count > 0)
                {
                    result = done.Dequeue();
                    return true;
                }
            }
            result = new PaintResult();
            return false;
        }

        /// <summary>Returns a shown surface to the pool.</summary>
        public void Release(Surface surface)
        {
            if (surface == null) return;
            lock (gate)
            {
                if (pool.Count < PoolCap) pool.Push(surface);
            }
        }

        public void Stop()
        {
            lock (gate)
            {
                stopping = true;
                Monitor.PulseAll(gate);
            }
            Thread t = thread;
            thread = null;
            if (t != null && t.IsAlive)
            {
                try
                {
                    t.Join(1000);
                }
                catch (Exception)
                {
                }
            }
        }

        public void Dispose()
        {
            disposed = true;
            Stop();
        }

        void Loop()
        {
            while (true)
            {
                int w;
                RenderJob job;
                lock (gate)
                {
                    while (!stopping && queue.Count == 0) Monitor.Wait(gate);
                    if (stopping) return;
                    w = queue.Dequeue();
                    waiting[w] = false;
                    job = slots[w];
                }
                Surface surface;
                lock (gate)
                {
                    surface = pool.Count > 0 ? pool.Pop() : null;
                }
                if (surface == null) surface = new Surface();
                long started = Stopwatch.GetTimestamp();
                try
                {
                    FrameRenderer.PaintJob(ref job, surface);
                }
                catch (Exception)
                {
                    Release(surface);
                    continue;   // one bad frame must never kill the painter
                }
                long spent = Stopwatch.GetTimestamp() - started;
                PaintResult result = new PaintResult();
                result.Walker = w;
                result.Stamp = job.Stamp;
                result.Surface = surface;
                result.Left = job.Left;
                result.Top = job.Top;
                lock (gate)
                {
                    paintTicks += spent;
                    done.Enqueue(result);
                }
            }
        }
    }
}
