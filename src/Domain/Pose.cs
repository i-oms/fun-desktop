namespace IdleWalker.Domain
{
    /// <summary>One frame's worth of body positions.</summary>
    struct Pose
    {
        public Art.Part Legs, Torso, Tail, Head, Overlay;
        public bool HeadTurned; // head looks the opposite way to the body
        public bool Bubble;
        public int BodyDy;      // upper-body offset in pixels (positive is down)
        public double StretchX, StretchY;

        public static Pose Standing()
        {
            Pose pose = new Pose();
            pose.Legs = Art.LegsStand;
            pose.Torso = Art.ArmMid;
            pose.Tail = Art.TailDown;
            pose.Head = Art.HeadNormal;
            pose.StretchX = 1;
            pose.StretchY = 1;
            return pose;
        }
    }
}
