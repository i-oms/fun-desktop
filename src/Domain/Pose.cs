namespace IdleWalker.Domain
{
    /// <summary>One frame's worth of body positions.</summary>
    struct Pose
    {
        public Art.Part Legs, Torso, Head, Overlay;
        public bool HeadTurned; // head looks the opposite way to the body
        public bool Bubble;
        public int BodyDy;      // upper-body offset in pixels (positive is down)
        public double StretchX, StretchY;

        public static Pose Standing(Art.Faces faces)
        {
            Pose pose = new Pose();
            pose.Legs = Art.LegsStand;
            pose.Torso = Art.ArmMid;
            pose.Head = faces.Normal;
            pose.StretchX = 1;
            pose.StretchY = 1;
            return pose;
        }

        /// <summary>True if drawing this pose would give the same picture as drawing other.</summary>
        public bool SameAs(Pose other)
        {
            return Legs == other.Legs && Torso == other.Torso &&
                   Head == other.Head && Overlay == other.Overlay &&
                   HeadTurned == other.HeadTurned && Bubble == other.Bubble &&
                   BodyDy == other.BodyDy && StretchX == other.StretchX && StretchY == other.StretchY;
        }
    }
}
