using System;
using IdleWalker.Domain;

namespace IdleWalker.Rendering
{
    /// <summary>
    /// Turns one walker's state into pixels: decides where the canvas goes on
    /// screen and draws the dust and the posed character into it. Skips the
    /// work entirely when the picture would be identical to the last frame.
    /// </summary>
    sealed class FrameRenderer
    {
        const int CanvasColumns = 40;
        const int CanvasRows = 48;
        const int RowsBelowFeet = 18; // canvas kept under the feet during high jumps

        readonly Surface surface = new Surface();

        bool hasLast;
        Pose lastPose;
        int lastLeft, lastTop, lastFeet, lastScale;
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
            int top = Round(Math.Min(walker.GroundLine, walker.FeetY + RowsBelowFeet * scale)) - height;
            int left = Round(walker.CentreX) - width / 2;
            int feet = Round(walker.FeetY);
            bool hasDust = walker.Puffs.Count > 0;
            Pose pose = walker.CurrentPose();

            bool unchanged = hasLast && !hasDust && !lastHadDust &&
                             left == lastLeft && top == lastTop && feet == lastFeet && scale == lastScale &&
                             walker.FacesLeft == lastFaceLeft && pose.SameAs(lastPose);
            if (unchanged) return false;

            hasLast = true;
            lastPose = pose;
            lastLeft = left;
            lastTop = top;
            lastFeet = feet;
            lastScale = scale;
            lastFaceLeft = walker.FacesLeft;
            lastHadDust = hasDust;
            Left = left;
            Top = top;

            surface.Reset(width, height);
            foreach (Puff puff in walker.Puffs)
            {
                double age = puff.Age;
                int size = Math.Max(1, Round(scale * (0.7 + 0.7 * age)));
                surface.Puff(Round(puff.X) - left, Round(puff.Y) - top, size, (int)(190 * (1 - age)));
            }

            // The soles sit one outline-width above the ground.
            DrawPose(surface, pose, walker.Personality.Palette, walker.FacesLeft, width / 2, feet - top - scale, scale);
            surface.CommitCharacter(scale);
            return true;
        }

        /// <summary>
        /// Draws a pose with its soles resting on soleY, centred on centreX.
        /// Also used for the tray icon.
        /// </summary>
        public static void DrawPose(Surface surface, Pose pose, Palette palette, bool faceLeft,
                                    double centreX, double soleY, int scale)
        {
            double w = scale * pose.StretchX, h = scale * pose.StretchY;
            surface.DrawPart(pose.Legs, palette, faceLeft, centreX, soleY, w, h, 0);
            surface.DrawPart(pose.Torso, palette, faceLeft, centreX, soleY, w, h, pose.BodyDy);
            surface.DrawPart(pose.Head, palette, faceLeft != pose.HeadTurned, centreX, soleY, w, h, pose.BodyDy);
            if (pose.Overlay != null) surface.DrawPart(pose.Overlay, palette, faceLeft, centreX, soleY, w, h, pose.BodyDy);
            if (pose.Bubble) surface.DrawPart(Art.Bubble, palette, false, centreX, soleY, w, h, pose.BodyDy);
        }

        static int Round(double value)
        {
            return (int)Math.Floor(value + 0.5);
        }
    }
}
