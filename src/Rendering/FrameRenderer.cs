using System;
using IdleWalker.Domain;

namespace IdleWalker.Rendering
{
    /// <summary>One dust speck as plain values: safe to hand to another thread.</summary>
    struct PuffSnapshot
    {
        public int X, Y, Size, Alpha;
    }

    /// <summary>
    /// Everything a frame needs, as plain values: geometry the UI thread
    /// worked out, the pose struct, and the immutable look. No walker is
    /// touched while painting, so this may cross to the render thread.
    /// </summary>
    struct RenderJob
    {
        public uint Stamp;
        public int Width, Height, Left, Top;
        public bool HasShadow;
        public double ShadowCx, ShadowGy, ShadowRx, ShadowRy, ShadowAlpha;
        public PuffSnapshot[] Puffs;
        public Pose Pose;
        public Look Look;
        public int Dress;
        public bool FaceLeft;
        public double CharCx, SoleY, Scale;
    }

    /// <summary>
    /// Turns one walker's state into pixels, in two halves: <see cref="Prepare"/>
    /// runs on the UI thread and only does cheap arithmetic (layout and the
    /// unchanged check - a walker that stands still costs almost nothing),
    /// while <see cref="PaintJob"/> does the dear raster work from the
    /// snapshot and may run anywhere. Finished frames are shown by the UI
    /// thread, which owns the windows.
    /// </summary>
    sealed class FrameRenderer
    {
        const int CanvasColumns = 40;
        const int RowsBelowFeet = 18; // canvas kept under the feet during high jumps

        bool hasLast;
        Pose lastPose;
        int lastLeft, lastTop, lastFeet, lastGround, lastScale;
        bool lastFaceLeft, lastHadDust;
        int lastDress;
        uint stamp = 1;

        /// <summary>Stamp of the latest snapshot; results with an older one are stale.</summary>
        public uint Stamp { get { return stamp; } }

        /// <summary>Forget the last frame, so the next one is drawn for sure.</summary>
        public void Invalidate()
        {
            hasLast = false;
            stamp++;
        }

        /// <summary>
        /// Snapshots the walker; false when the picture would be identical to
        /// the last frame and there is nothing to paint.
        /// </summary>
        public bool Prepare(Walker walker, out RenderJob job)
        {
            // The canvas rests on the ground line, so anything underground is
            // simply cut off. In a high jump it follows the character up.
            job = new RenderJob();
            int scale = walker.Scale;
            Anatomy anatomy = walker.Personality.Anatomy;
            int width = CanvasColumns * scale;
            int height = anatomy.CanvasRows * scale;
            int ground = Round(walker.GroundLine);
            int top = Round(Math.Min(walker.GroundLine, walker.FeetY + RowsBelowFeet * scale)) - height;
            int left = Round(walker.CentreX) - width / 2;
            int feet = Round(walker.FeetY);
            bool hasDust = walker.Puffs.Count > 0;
            Pose pose = walker.CurrentPose();
            int dress = walker.Dress;
            bool faceLeft = walker.FacesLeft;

            bool unchanged = hasLast && !hasDust && !lastHadDust &&
                             left == lastLeft && top == lastTop && feet == lastFeet && ground == lastGround &&
                             scale == lastScale && faceLeft == lastFaceLeft && dress == lastDress &&
                             pose.SameAs(lastPose);
            if (unchanged) return false;

            hasLast = true;
            lastPose = pose;
            lastLeft = left;
            lastTop = top;
            lastFeet = feet;
            lastGround = ground;
            lastScale = scale;
            lastFaceLeft = faceLeft;
            lastDress = dress;
            lastHadDust = hasDust;

            job.Stamp = stamp;
            job.Width = width;
            job.Height = height;
            job.Left = left;
            job.Top = top;

            // The shadow stays on the ground and thins out as it jumps.
            double lift = (walker.GroundLine - walker.FeetY) / (40.0 * scale);
            if (lift >= 0 && lift < 1)
            {
                job.HasShadow = true;
                job.ShadowCx = walker.CentreX - left;
                job.ShadowGy = walker.GroundLine - top;
                job.ShadowRx = (anatomy.ShadowRadius - 2.2 * lift) * scale * pose.StretchX;
                job.ShadowRy = 1.4 * scale;
                job.ShadowAlpha = 0.42 * (1 - lift);
            }

            if (hasDust)
            {
                PuffSnapshot[] snaps = new PuffSnapshot[walker.Puffs.Count];
                for (int k = 0; k < snaps.Length; k++)
                {
                    Puff puff = walker.Puffs[k];
                    double age = puff.Age;
                    snaps[k].X = Round(puff.X) - left;
                    snaps[k].Y = Round(puff.Y) - top;
                    snaps[k].Size = Math.Max(1, Round(scale * (0.7 + 0.7 * age)));
                    snaps[k].Alpha = (int)(190 * (1 - age));
                }
                job.Puffs = snaps;
            }

            job.Pose = pose;
            job.Look = walker.Personality.Look;
            job.Dress = dress;
            job.FaceLeft = faceLeft;
            // The soles sit just above the ground line, leaving room for the halo.
            job.CharCx = walker.CentreX - left;
            job.SoleY = walker.FeetY - top - 1;
            job.Scale = scale;
            return true;
        }

        /// <summary>Paints a snapshot into a surface; touches only the two.</summary>
        public static void PaintJob(ref RenderJob job, Surface surface)
        {
            surface.Reset(job.Width, job.Height);
            if (job.HasShadow)
                surface.Shadow(job.ShadowCx, job.ShadowGy, job.ShadowRx, job.ShadowRy, job.ShadowAlpha);
            if (job.Puffs != null)
            {
                for (int k = 0; k < job.Puffs.Length; k++)
                    surface.Puff(job.Puffs[k].X, job.Puffs[k].Y, job.Puffs[k].Size, job.Puffs[k].Alpha);
            }
            DrawPose(surface, job.Pose, job.Look, job.Dress, job.FaceLeft, job.CharCx, job.SoleY, job.Scale);
        }

        /// <summary>
        /// Draws a pose with its soles resting on soleY, centred on centreX,
        /// and commits it to the canvas, with the painter the look calls
        /// for. Also used for the About card.
        /// </summary>
        public static void DrawPose(Surface surface, Pose pose, Look look, int dress, bool faceLeft,
                                    double centreX, double soleY, double scale)
        {
            if (look.Figure == Figure.Bean) BeanPainter.Draw(surface, pose, faceLeft, centreX, soleY, scale);
            else if (look.Figure == Figure.Bird) BirdPainter.Draw(surface, pose, look, faceLeft, centreX, soleY, scale);
            else if (look.Figure == Figure.Panther) PantherPainter.Draw(surface, pose, faceLeft, centreX, soleY, scale);
            else MinionPainter.Draw(surface, pose, look, dress, faceLeft, centreX, soleY, scale);
            surface.CommitCharacter(1);
        }

        static int Round(double value)
        {
            return (int)Math.Floor(value + 0.5);
        }
    }
}
