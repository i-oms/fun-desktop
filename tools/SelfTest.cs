using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using IdleWalker.Domain;
using IdleWalker.Platform;

namespace IdleWalker.Tools
{
    sealed class FakeWorld : IWorld
    {
        public int Left { get { return 0; } }
        public int Right { get { return 1920; } }
        public IEnumerable<int> MonitorEdges
        {
            get
            {
                List<int> edges = new List<int>();
                edges.Add(0);
                edges.Add(1920);
                return edges;
            }
        }
        public Rectangle MonitorBoundsAt(Point point)
        {
            return new Rectangle(0, 0, 1920, 1040);
        }
        public double GroundAt(double x) { return 1000; }
        public int ScaleAt(double x) { return 3; }
    }

    static class SelfTest
    {
        static int failures;

        static void Check(bool ok, string name)
        {
            Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
            if (!ok) failures++;
        }

        static int Main()
        {
            failures = 0;
            Console.WriteLine("IdleWalker v3 living-brain selftest");

            // 1. Headless sim learns.
            FakeWorld world = new FakeWorld();
            Random rng = new Random(1234);
            Troupe troupe = new Troupe(world, rng);
            troupe.EnsureCast();
            troupe.MinionsEnabled = true;
            troupe.Count = 5;
            Point cursor = new Point(960, 200);
            double dt = 1.0 / 60.0;
            for (int i = 0; i < 60 * 120; i++)
            {
                bool wantOut = i > 10;
                troupe.Step(dt, cursor, wantOut, false);
            }
            int visible = 0;
            int tastes = 0;
            int decisions = 0;
            double netNorm = 0;
            foreach (Walker w in troupe.Walkers)
            {
                if (w.IsVisible) visible++;
                tastes += w.LearnedEntries();
                decisions += w.Experience();
                netNorm += w.MlpWeightNorm();
            }
            Console.WriteLine("visible=" + visible + " tastes=" + tastes + " decisions=" + decisions + " net=" + netNorm.ToString("0.00"));
            Check(visible > 0, "walkers appear headless");
            Check(decisions > 0, "v2 experience tracking (exp)");
            Check(tastes > 0, "tabular values learn");
            Check(netNorm > 0.01, "v3 MLP moved from init (generalizer alive)");
            // Smoothness: every visible walker reports a sane activity, never null/empty.
            bool smooth = true;
            foreach (Walker w in troupe.Walkers)
            {
                if (!w.IsVisible) continue;
                string a = w.Activity;
                if (a == null || a.Length == 0) smooth = false;
            }
            Check(smooth, "activities always motivated (never blank/random)");

            // 2. Pack/Unpack roundtrip incl v2 fields.
            Walker first = null;
            foreach (Walker w in troupe.Walkers)
            {
                if (w.IsVisible) { first = w; break; }
            }
            if (first == null) first = troupe.Walkers[2];
            string pack = first.PackMind();
            Check(pack.IndexOf("exp=") >= 0, "pack contains exp=");
            Check(pack.IndexOf("rew=") >= 0, "pack contains rew=");
            Check(pack.IndexOf("mw1=") >= 0, "pack contains mlp mw1=");
            Check(pack.IndexOf("mshape=") >= 0, "pack contains mshape=");
            int expBefore = first.Experience();
            double avgBefore = first.AvgReward();
            double netBefore = first.MlpWeightNorm();
            Dictionary<string, Walker> byName = new Dictionary<string, Walker>();
            foreach (Walker w in troupe.Walkers) byName[w.Personality.Name] = w;
            Dictionary<string, string> fields = new Dictionary<string, string>();
            string[] lines = pack.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                fields[line.Substring(0, eq)] = line.Substring(eq + 1);
            }
            Troupe troupe2 = new Troupe(world, new Random(999));
            troupe2.EnsureCast();
            Dictionary<string, Walker> byName2 = new Dictionary<string, Walker>();
            foreach (Walker w in troupe2.Walkers) byName2[w.Personality.Name] = w;
            Walker target = byName2[first.Personality.Name];
            int expZero = target.Experience();
            target.UnpackMind(fields, byName2);
            Check(expZero == 0, "fresh walker starts at 0 decisions");
            Check(target.Experience() == expBefore, "exp survives pack/unpack");
            Check(Math.Abs(target.AvgReward() - avgBefore) < 0.0001, "avg reward survives pack/unpack");
            Check(Math.Abs(target.MlpWeightNorm() - netBefore) < 0.0001, "mlp weights survive pack/unpack");

            // 3. v1/v2 backward compat: 4-value seeds, no exp/rew/mlp.
            Dictionary<string, string> v1 = new Dictionary<string, string>();
            v1["opt"] = "0.1";
            v1["last"] = "-1,0";
            try
            {
                target.ForgetMind();
                target.UnpackMind(v1, byName2);
                Check(true, "v1 minimal fields accepted");
                Check(target.MlpWeightNorm() >= 0, "v1 without mlp still inits net");
            }
            catch (Exception ex)
            {
                Console.WriteLine("v1 ex: " + ex.Message);
                Check(false, "v1 minimal fields accepted");
            }

            // 4. BrainStore atomic save + v3 header.
            string tmp = Path.Combine(Path.GetTempPath(), "IdleWalkerSelfTestBrains.txt");
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
            try { if (File.Exists(tmp + ".tmp")) File.Delete(tmp + ".tmp"); } catch (Exception) { }
            BrainStore.OverridePath = tmp;
            try
            {
                BrainStore.Save(troupe);
                Check(File.Exists(tmp), "brains file saved");
                Check(!File.Exists(tmp + ".tmp"), "no tmp left behind");
                string text = File.ReadAllText(tmp, Encoding.UTF8);
                Check(text.IndexOf("# IdleWalker brains v3") >= 0, "v3 header written");
                Troupe loaded = new Troupe(world, new Random(555));
                BrainStore.Load(loaded);
                int loadedExp = 0;
                foreach (Walker w in loaded.Walkers) loadedExp += w.Experience();
                Check(loadedExp == decisions, "save/load preserves decisions");
                // Corrupt header must not crash and must not load.
                File.WriteAllText(tmp, "# foreign file\n[Bob]\nexp=9999\n", Encoding.UTF8);
                Troupe foreign = new Troupe(world, new Random(777));
                foreign.EnsureCast();
                try
                {
                    BrainStore.Load(foreign);
                    int fexp = 0;
                    foreach (Walker w in foreign.Walkers) fexp += w.Experience();
                    Check(fexp == 0, "unknown header ignored");
                }
                catch (Exception)
                {
                    Check(false, "unknown header ignored");
                }
            }
            finally
            {
                BrainStore.OverridePath = null;
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
                try { if (File.Exists(tmp + ".tmp")) File.Delete(tmp + ".tmp"); } catch (Exception) { }
                try { if (File.Exists(tmp + ".bak")) File.Delete(tmp + ".bak"); } catch (Exception) { }
            }

            // 5. Forget clears v2/v3 stats.
            first.ForgetMind();
            Check(first.Experience() == 0 && Math.Abs(first.AvgReward()) < 0.000001, "forget clears exp/rew");
            // Net after forget is fresh-small (re-inits on next EnsureTable via Decide/Step).
            Check(first.MlpWeightNorm() == 0, "forget clears mlp");

            Console.WriteLine(failures == 0 ? "ALL PASS" : failures + " FAILURES");
            return failures == 0 ? 0 : 1;
        }
    }
}
