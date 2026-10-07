using System;
using IdleWalker.Domain;

namespace IdleWalker.Rendering
{
    /// <summary>
    /// Turns one walker's state into pixels: decides where the canvas goes on
    /// screen and draws the dust, the shadow and the posed character into it.
    /// Skips the work entirely when the picture would be identical to the
    /// last frame.
    /// </summary>
    sealed class FrameRenderer
    {
        const int CanvasColumns = 40;
        const int CanvasRows = 48;
        const int RowsBelowFeet = 18; // canvas kept under the feet during high jumps

        readonly Surface surface = new Surface();

        bool hasLast;
        Pose lastPose;
        int lastLeft, lastTop, lastFeet, lastGround, lastScale;
        bool lastFaceLeft, lastHadDust;

        /// <summary>The rendered frame.</summary>
        public Surface Surface { get { return surface; } }

        /// <summary>Screen position of the frame's top-left corner.</summary>
        public int Left { get; private set; }
        public int Top { get; private set; }

        /// <summary>Forget the last frame, so the next one is drawn for sure.</summary>
        public void Invalidate()
        {
            hasLast = false;
        }

        /// <summary>Renders the walker; false if nothing changed and the old frame still stands.</summary>
        public bool Render(Walker walker)
        {
            // The canvas rests on the ground line, so anything underground is
            // simply cut off. In a high jump it follows the character up.
            int scale = walker.Scale;
            int width = CanvasColumns * scale;
            int height = CanvasRows * scale;
            int ground = Round(walker.GroundLine);
            int top = Round(Math.Min(walker.GroundLine, walker.FeetY + RowsBelowFeet * scale)) - height;
            int left = Round(walker.CentreX) - width / 2;
            int feet = Round(walker.FeetY);
            bool hasDust = walker.Puffs.Count > 0;
            Pose pose = walker.CurrentPose();

            bool unchanged = hasLast && !hasDust && !lastHadDust &&
                             left == lastLeft && top == lastTop && feet == lastFeet && ground == lastGround &&
                             scale == lastScale && walker.FacesLeft == lastFaceLeft && pose.SameAs(lastPose);
            if (unchanged) return false;

            hasLast = true;
            lastPose = pose;
            lastLeft = left;
            lastTop = top;
            lastFeet = feet;
            lastGround = ground;
            lastScale = scale;
            lastFaceLeft = walker.FacesLeft;
            lastHadDust = hasDust;
            Left = left;
            Top = top;

            surface.Reset(width, height);

            // The shadow stays on the ground and thins out as it jumps.
            double lift = (walker.GroundLine - walker.FeetY) / (40.0 * scale);
            if (lift >= 0 && lift < 1)
            {
                surface.Shadow(walker.CentreX - left, walker.GroundLine - top,
                               (6.4 - 2.2 * lift) * scale * pose.StretchX, 1.4 * scale, 0.42 * (1 - lift));
            }

            foreach (Puff puff in walker.Puffs)
            {
                double age = puff.Age;
                int size = Math.Max(1, Round(scale * (0.7 + 0.7 * age)));
                surface.Puff(Round(puff.X) - left, Round(puff.Y) - top, size, (int)(190 * (1 - age)));
            }

            // The soles sit just above the ground line, leaving room for the halo.
            DrawPose(surface, pose, walker.Personality.Look, walker.FacesLeft, walker.CentreX - left, walker.FeetY - top - 1, scale);
            return true;
        }

        /// <summary>
        /// Draws a pose with its soles resting on soleY, centred on centreX,
        /// and commits it to the canvas. Also used for the tray icon.
        /// </summary>
        public static void DrawPose(Surface surface, Pose pose, Look look, bool faceLeft,
                                    double centreX, double soleY, double scale)
        {
            MinionPainter.Draw(surface, pose, look, faceLeft, centreX, soleY, scale);
            surface.CommitCharacter(1);
        }

        static int Round(double value)
        {
            return (int)Math.Floor(value + 0.5);
        }
    }
}
