using System;
using IdleWalker.Domain;
using IdleWalker.Rendering;

namespace IdleWalker.Tools
{
    /// <summary>
    /// Prints an FNV-1a hash of the painted canvas for a fixed grid of poses
    /// across all four figures. Compile once against the pre-change sources
    /// and once against the changed ones; a diff of the outputs is empty
    /// exactly when the painters are pixel-identical.
    /// </summary>
    static class Hash
    {
        static int Main()
        {
            Look[] looks =
            {
                Personality.Roster[0].Look, Personality.Roster[1].Look, Personality.Roster[4].Look, Personality.Roster[7].Look,
                Personality.Bean.Look, Personality.Panther.Look,
                Personality.BirdRoster[0].Look, Personality.BirdRoster[3].Look, Personality.BirdRoster[7].Look
            };
            Anatomy[] anatomies =
            {
                Anatomy.Minion, Anatomy.Minion, Anatomy.Minion, Anatomy.Minion,
                Anatomy.Bean, Anatomy.Panther,
                Anatomy.Bird, Anatomy.Bird, Anatomy.Bird
            };
            double[] yaws = { -45, 0, 30, 58, 90, 150 };
            double[] phases = { 0, 0.3, 0.7 };
            Expression[] faces = { Expression.Neutral, Expression.Happy, Expression.Scared, Expression.Laugh, Expression.Sleepy };
            int[] dresses = { 0x2B59B8, 0xC0392B };

            Surface surface = new Surface();
            int count = 0;
            for (int l = 0; l < looks.Length; l++)
            {
                int scale = 3;
                int width = 40 * scale, height = anatomies[l].CanvasRows * scale;
                for (int yi = 0; yi < yaws.Length; yi++)
                for (int pi = 0; pi < phases.Length; pi++)
                for (int fi = 0; fi < faces.Length; fi++)
                {
                    Pose pose = Pose.Standing();
                    pose.Face = faces[fi];
                    pose.Yaw = yaws[yi];
                    pose.Walking = pi > 0;
                    pose.WalkPhase = phases[pi];
                    pose.GazeX = 0.25 * yi - 0.5;
                    pose.GazeY = 0.1 * fi;
                    pose.Lean = 0.1 * pi;
                    pose.BodyDy = 0.15 * pi;
                    if (fi == 1) { pose.ArmNear = 120; pose.ForeNear = 30; pose.PointNear = true; }
                    if (fi == 2) { pose.ArmNear = 70; pose.ArmFar = -70; pose.Bubble = true; pose.Sweat = 0.4; }
                    if (fi == 3) { pose.Note = true; pose.DecorPhase = 0.35; pose.Teddy = true; pose.TeddyWave = 0.5; }
                    if (fi == 4) { pose.Zzz = true; pose.DecorPhase = 0.6; pose.HeadDip = 0.3; pose.HeadTurn = 20; }
                    if (yi == 5) { pose.LegTuck = 0.6; pose.StretchX = 0.95; pose.StretchY = 1.06; pose.Heart = true; pose.DecorPhase = 0.5; }
                    if (yi == 2 && pi == 1) { pose.Wheel = true; pose.WheelTurn = 0.4; pose.Egg = true; pose.PuffUp = 0.5; pose.Tiptoe = 0.5; }
                    if (yi == 4 && pi == 2) { pose.Camera = true; pose.Flash = fi == 0; pose.BlastFlash = fi == 1; }
                    bool faceLeft = (yi + pi) % 2 == 1;
                    int dress = dresses[(yi + fi) % 2];

                    surface.Reset(width, height);
                    surface.Shadow(width / 2.0, height - 1.0, anatomies[l].ShadowRadius * scale, 1.4 * scale, 0.4);
                    surface.Puff(width / 2 - 8, height - 2, 4, 120);
                    FrameRenderer.DrawPose(surface, pose, looks[l], dress, faceLeft, width / 2.0 + 0.3 * pi, height - 2.0, scale);
                    ulong h = Fnv(surface.Layer, surface.Width * surface.Height);
                    ulong p = Fnv(surface.Pixels, surface.Width * surface.Height);
                    Console.WriteLine("{0} look={1} yaw={2} ph={3} face={4} L={5:X16} P={6:X16} b={7}", count++, l, yaws[yi], phases[pi], fi, h, p, surface.Bounds);
                }
            }
            return 0;
        }

        static ulong Fnv(int[] data, int n)
        {
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < n; i++)
            {
                h ^= (uint)data[i];
                h *= 1099511628211UL;
            }
            return h;
        }
    }
}
