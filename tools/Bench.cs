using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using IdleWalker.Domain;
using IdleWalker.Rendering;

namespace IdleWalker.Tools
{
    sealed class BenchWorld : IWorld
    {
        public int Left { get { return 0; } }
        public int Right { get { return 1920; } }
        static readonly int[] edges = { 0, 1920 };
        public int[] MonitorEdges { get { return edges; } }
        public Rectangle MonitorBoundsAt(Point point) { return new Rectangle(0, 0, 1920, 1040); }
        public double GroundAt(double x) { return 1000; }
        public int ScaleAt(double x) { return 3; }
    }

    /// <summary>
    /// Headless benchmark: steps the troupe at 60 Hz with everyone out and
    /// paints every frame that changed (no coalescing), so the numbers are
    /// the true paint demand of the cast - what the render thread would
    /// have to do on a machine fast enough to never skip.
    /// Usage: Bench.exe [seconds=20] [cast=full|minions5|minions10] [seed=7]
    /// </summary>
    static class Bench
    {
        static int Main(string[] args)
        {
            double seconds = args.Length > 0 ? double.Parse(args[0]) : 20;
            string cast = args.Length > 1 ? args[1] : "full";
            int seed = args.Length > 2 ? int.Parse(args[2]) : 7;

            BenchWorld world = new BenchWorld();
            Troupe troupe = new Troupe(world, new Random(seed));
            troupe.EnsureCast();
            troupe.MinionsEnabled = true;
            if (cast == "full")
            {
                troupe.Count = Settings.MaxWalkers;
                troupe.BirdsEnabled = true;
                troupe.BirdCount = Settings.MaxBirds;
                troupe.BeanEnabled = true;
                troupe.PantherEnabled = true;
            }
            else if (cast == "minions10")
            {
                troupe.Count = 10;
            }
            else
            {
                troupe.Count = 5;
            }

            IList<Walker> walkers = troupe.Walkers;
            List<FrameRenderer> renderers = new List<FrameRenderer>();
            List<Surface> surfaces = new List<Surface>();
            for (int i = 0; i < walkers.Count; i++)
            {
                renderers.Add(new FrameRenderer());
                surfaces.Add(new Surface());
            }

            Point cursor = new Point(960, 200);
            double dt = 1.0 / 60.0;
            int frames = (int)(seconds * 60);
            // Let everyone come out first (one every ~1 s), unmeasured.
            for (int f = 0; f < 60 * 30; f++) troupe.Step(dt, cursor, true, false);

            Stopwatch step = new Stopwatch();
            Stopwatch paint = new Stopwatch();
            long paints = 0, skipped = 0, hidden = 0;
            Dictionary<string, double> figureMs = new Dictionary<string, double>();
            Dictionary<string, long> figureN = new Dictionary<string, long>();
            double maxFrameMs = 0;

            AppDomain.MonitoringIsEnabled = true;
            long allocBefore = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);

            Stopwatch wall = Stopwatch.StartNew();
            for (int f = 0; f < frames; f++)
            {
                step.Start();
                troupe.Step(dt, cursor, true, false);
                step.Stop();

                double frameMs = 0;
                for (int i = 0; i < walkers.Count; i++)
                {
                    Walker w = walkers[i];
                    if (w.IsHidden)
                    {
                        renderers[i].Invalidate();
                        hidden++;
                        continue;
                    }
                    RenderJob job;
                    if (!renderers[i].Prepare(w, out job))
                    {
                        skipped++;
                        continue;
                    }
                    long t0 = paint.ElapsedTicks;
                    paint.Start();
                    FrameRenderer.PaintJob(ref job, surfaces[i]);
                    paint.Stop();
                    double ms = (paint.ElapsedTicks - t0) * 1000.0 / Stopwatch.Frequency;
                    frameMs += ms;
                    paints++;
                    string fig = job.Look.Figure.ToString();
                    double acc;
                    figureMs.TryGetValue(fig, out acc);
                    figureMs[fig] = acc + ms;
                    long n;
                    figureN.TryGetValue(fig, out n);
                    figureN[fig] = n + 1;
                }
                if (frameMs > maxFrameMs) maxFrameMs = frameMs;
            }
            wall.Stop();

            long allocAfter = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            int visible = 0;
            foreach (Walker w in walkers) if (w.IsVisible) visible++;

            double paintMs = paint.Elapsed.TotalMilliseconds;
            double stepMs = step.Elapsed.TotalMilliseconds;
            Console.WriteLine("cast={0} visible={1} simulated={2:0.0}s wall={3:0.0}s", cast, visible, seconds, wall.Elapsed.TotalSeconds);
            Console.WriteLine("step: total {0:0.0} ms, {1:0.000} ms/frame", stepMs, stepMs / frames);
            Console.WriteLine("paint: total {0:0.0} ms, paints={1} skipped(unchanged)={2} hidden={3}", paintMs, paints, skipped, hidden);
            Console.WriteLine("paint: {0:0.000} ms/paint, {1:0.00} ms/frame, worst frame {2:0.00} ms", paintMs / Math.Max(1, paints), paintMs / frames, maxFrameMs);
            Console.WriteLine("paint demand: {0:0.0} ms per simulated second = {1:0.0}% of one core at 60 Hz", paintMs / seconds, 100 * paintMs / (seconds * 1000));
            foreach (KeyValuePair<string, double> kv in figureMs)
                Console.WriteLine("  {0,-8} {1:0.000} ms/paint over {2} paints", kv.Key, kv.Value / figureN[kv.Key], figureN[kv.Key]);
            Console.WriteLine("alloc: {0:0.0} MB total, {1:0.00} MB per simulated second; GC gen0={2} gen1={3} gen2={4}",
                (allocAfter - allocBefore) / 1048576.0, (allocAfter - allocBefore) / 1048576.0 / seconds,
                GC.CollectionCount(0) - g0, GC.CollectionCount(1) - g1, GC.CollectionCount(2) - g2);
            return 0;
        }
    }
}
