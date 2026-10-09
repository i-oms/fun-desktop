namespace IdleWalker.Domain
{
    /// <summary>
    /// One frame's worth of body positions. Angles are in degrees, lengths
    /// in units (a Minion is 18 units tall, Mr. Bean 28); the painter turns
    /// it all into pixels. The head and reach fields only mean something to
    /// a figure with a neck and long arms (Mr. Bean); the Minion painter
    /// ignores them.
    /// </summary>
    struct Pose
    {
        public Expression Face;
        public double GazeX, GazeY;       // where the pupils point, -1..1: +X ahead, +Y up
        public double Yaw;                // degrees turned about the vertical axis: 0 facing the viewer, 90 side on, ahead
        public bool Walking;              // legs and arms swing with WalkPhase
        public double WalkPhase;          // in strides
        public double LegTuck;            // 0 standing, 1 knees pulled up for a jump
        public double ArmNear, ArmFar;    // upper arm: 0 hanging, 90 straight ahead, 180 straight up
        public double ForeNear, ForeFar;  // forearm bend added to the upper arm
        public double ReachNear, ReachFar;// upper arm brought forward, out of the picture plane, degrees (Mr. Bean)
        public bool PointNear;            // the near hand has its index finger out
        public double Lean;               // forward lean, -1..1
        public double BodyDy;             // upper body offset in units, positive is down
        public double StretchX, StretchY; // squash and stretch
        public double HeadTurn;           // the head turned beyond the body, degrees (Mr. Bean)
        public double HeadDip;            // the head nodded forward (+) or tipped back (-), units (Mr. Bean)
        public double Tiptoe;             // heels off the ground, 0..1 (Mr. Bean)
        public bool Teddy;                // Teddy in the near hand (Mr. Bean)
        public double TeddyWave;          // Teddy's paw raised, 0..1
        public bool Bubble;               // the "!" overhead
        public double Sweat;              // a drop beside the head: 0 none, else how far it has slid, 0..1
        public bool Zzz, Note, Heart;     // sleep letters floating up; a music note from the mouth; a heart from the mouth
        public double DecorPhase;         // where the floating letters / note / heart are in their drift, 0..1

        public static Pose Standing()
        {
            Pose pose = new Pose();
            pose.Face = Expression.Neutral;
            pose.GazeX = 0.5;
            pose.StretchX = 1;
            pose.StretchY = 1;
            return pose;
        }

        /// <summary>True if drawing this pose would give the same picture as drawing other.</summary>
        public bool SameAs(Pose o)
        {
            return Face.SameAs(o.Face) &&
                   GazeX == o.GazeX && GazeY == o.GazeY && Yaw == o.Yaw &&
                   Walking == o.Walking && WalkPhase == o.WalkPhase && LegTuck == o.LegTuck &&
                   ArmNear == o.ArmNear && ArmFar == o.ArmFar && ForeNear == o.ForeNear && ForeFar == o.ForeFar &&
                   ReachNear == o.ReachNear && ReachFar == o.ReachFar &&
                   PointNear == o.PointNear &&
                   Lean == o.Lean && BodyDy == o.BodyDy && StretchX == o.StretchX && StretchY == o.StretchY &&
                   HeadTurn == o.HeadTurn && HeadDip == o.HeadDip && Tiptoe == o.Tiptoe &&
                   Teddy == o.Teddy && TeddyWave == o.TeddyWave &&
                   Bubble == o.Bubble && Sweat == o.Sweat &&
                   Zzz == o.Zzz && Note == o.Note && Heart == o.Heart && DecorPhase == o.DecorPhase;
        }
    }
}
