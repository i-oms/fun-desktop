using System;
using IdleWalker.Domain;

namespace IdleWalker.Rendering
{
    /// <summary>
    /// Turns the walker's state into pixels: decides where the canvas goes on
    /// screen and draws the dust and the posed character into it.
    /// </summary>
    sealed class FrameRenderer
    {
        const int CanvasColumns = 40;
        const int CanvasRows = 48;
        const int RowsBelowFeet = 18; // canvas kept under the feet during high jumps

        readonly Surface surface = new Surface();

        /// <summary>The rendered frame.</summary>
        public Surface Surface { get { return surface; } }

        /// <summary>Screen position of the frame's top-left corner.</summary>
        public int Left { get; private set; }
        public int Top { get; private set; }

        public void Render(Walker walker)
        {
            // The canvas rests on the ground line, so anything underground is
            // simply cut off. In a high jump it follows the character up.
            int scale = walker.Scale;
            int width = CanvasColumns * scale;
            int height = CanvasRows * scale;
            Top = Round(Math.Min(walker.GroundLine, walker.FeetY + RowsBelowFeet * scale)) - height;
            Left = Round(walker.CentreX) - width / 2;

            surface.Reset(width, height);
            foreach (Puff puff in walker.Puffs)
            {
                double age = puff.Age;
                int size = Math.Max(1, Round(scale * (0.7 + 0.7 * age)));
                surface.Puff(Round(puff.X) - Left, Round(puff.Y) - Top, size, (int)(190 * (1 - age)));
            }

            // The soles sit one outline-width above the ground.
            DrawPose(surface, walker.CurrentPose(), walker.FacesLeft, width / 2, Round(walker.FeetY) - Top - scale, scale);
            surface.CommitCharacter(scale);
        }

        /// <summary>
        /// Draws a pose with its soles resting on soleY, centred on centreX.
        /// Also used for the tray icon.
        /// </summary>
        public static void DrawPose(Surface surface, Pose pose, bool faceLeft, double centreX, double soleY, int scale)
        {
            double w = scale * pose.StretchX, h = scale * pose.StretchY;
            surface.DrawPart(pose.Tail, faceLeft, centreX, soleY, w, h, pose.BodyDy);
            surface.DrawPart(pose.Legs, faceLeft, centreX, soleY, w, h, 0);
            surface.DrawPart(pose.Torso, faceLeft, centreX, soleY, w, h, pose.BodyDy);
            surface.DrawPart(pose.Head, faceLeft != pose.HeadTurned, centreX, soleY, w, h, pose.BodyDy);
            if (pose.Overlay != null) surface.DrawPart(pose.Overlay, faceLeft, centreX, soleY, w, h, pose.BodyDy);
            if (pose.Bubble) surface.DrawPart(Art.Bubble, false, centreX, soleY, w, h, pose.BodyDy);
        }

        static int Round(double value)
        {
            return (int)Math.Floor(value + 0.5);
        }
    }
}
