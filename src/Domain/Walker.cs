using System;
using System.Collections.Generic;
using System.Drawing;

namespace IdleWalker.Domain
{
    /// <summary>
    /// One character: where it is, what it is doing and how it moves. Pure
    /// simulation - it knows nothing about windows or pixels on screen beyond
    /// what <see cref="IWorld"/> tells it. Walkers only meet each other
    /// through the <see cref="Troupe"/>.
    /// </summary>
    sealed class Walker
    {
        const double StrideCells = 10;    // distance covered by one full walk cycle (short legs, quick steps)
        const double BlinkSeconds = 0.16;
        const int HalfBodyCells = 9;
        const int HiddenDepthCells = 20;  // feet this far underground hides everything
        const int PeekDepthCells = 10;    // feet this far underground shows the eyes
        const double GreetSeconds = 1.3;
        const double GloatSeconds = 0.7;
        const double FleeSeconds = 1.4;
        const double ChargeSpeedFactor = 2.0;
        const double FleeSpeedFactor = 1.6;

        enum Mode
        {
            Hidden, PeekRise, PeekLook, PeekDuck, Air, Walk, Crouch, Turn, Look, Wave, Rest, Exit,
            Greet,   // stopped to wave or nod at another walker
            Charge,  // running at another walker to shove it
            Gloat,   // standing over a shoved walker
            Flee     // running away after being shoved
        }

        readonly IWorld world;
        readonly Random rng;
        readonly Personality personality;
        readonly List<Puff> puffs = new List<Puff>();

        Mode mode = Mode.Hidden;
        double modeTime;
        double time;

        int scale = Settings.BasePixelSize;
        double sizeFactor = 1;      // scale relative to 100% display scaling
        int worldLeft, worldRight;  // outer edges of the whole desktop
        int cursorX, cursorY;
        bool companyNearby;         // another walker is close
        double companyX;
        bool waveAtCursor;          // the current wave is for the cursor rather than company

        Expression face = Expression.Neutral;  // what the face is doing, eased towards a target each frame
        double gazeX = 0.5, gazeY;             // where the pupils point, +X ahead
        double yaw;                            // degrees turned from facing the viewer towards where it is going

        double cx;                  // centre of the character, screen pixels
        double feetY;               // where its feet are
        double vx, vy;
        double clipY;               // ground line; nothing is drawn below it
        int facing = 1;
        bool headTurned;

        double walkPhase;           // in walk cycles; advanced by distance so feet don't slide
        double squash;              // 1 right after a landing, easing back to 0
        bool braking;
        Mode afterBrake = Mode.Walk;
        Mode afterTurn = Mode.Walk;
        double pendingJump, pendingAirSpeed;
        double landPause;
        double restDuration;
        double idleTimer, hopTimer;
        double blinkTimer, blinkLeft;
        bool startled;

        int greetFacing;
        bool greetWave;
        bool knocked;               // airborne because somebody shoved it
        double fleeTime;
        Walker chargeTarget;

        public Walker(IWorld world, Random rng, Personality personality)
        {
            this.world = world;
            this.rng = rng;
            this.personality = personality;
        }

        // ---- What others need to know ----------------------------------------------

        public Personality Personality { get { return personality; } }
        public Temperament Temperament { get { return personality.Temperament; } }

        public bool IsHidden { get { return mode == Mode.Hidden; } }
        public bool IsVisible { get { return mode != Mode.Hidden; } }
        public bool IsLeaving { get { return mode == Mode.Exit; } }
        public int Scale { get { return scale; } }
        public double CentreX { get { return cx; } }
        public double FeetY { get { return feetY; } }
        public int Facing { get { return facing; } }
        public bool FacesLeft { get { return facing < 0; } }
        public double BodyWidth { get { return 2 * HalfBodyCells * scale; } }
        public IList<Puff> Puffs { get { return puffs; } }

        /// <summary>The ground line under the character; nothing shows below it.</summary>
        public double GroundLine { get { return clipY; } }

        /// <summary>Feet on the ground (not underground, airborne or leaving).</summary>
        public bool IsGrounded
        {
            get
            {
                switch (mode)
                {
                    case Mode.Walk: case Mode.Crouch: case Mode.Turn: case Mode.Look: case Mode.Wave:
                    case Mode.Rest: case Mode.Greet: case Mode.Charge: case Mode.Gloat: case Mode.Flee:
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>Walking along at speed (as opposed to stopping or standing).</summary>
        public bool IsWalking
        {
            get { return mode == Mode.Walk && !braking && Math.Abs(vx) > 6 * sizeFactor; }
        }

        /// <summary>Free to stop and say hello.</summary>
        public bool CanGreet
        {
            get { return (mode == Mode.Walk && !braking) || mode == Mode.Rest || mode == Mode.Look; }
        }

        public bool IsCharging { get { return mode == Mode.Charge; } }
        public Walker ChargeTarget { get { return chargeTarget; } }

        /// <summary>Seconds before this walker will greet or charge again.</summary>
        public double GreetCooldown { get; set; }
        public double ChargeCooldown { get; set; }

        // ---- Commands ---------------------------------------------------------------

        /// <summary>Come up out of the ground at x.</summary>
        public void Spawn(Point cursor, double x)
        {
            cursorX = cursor.X;
            cursorY = cursor.Y;
            cx = x;
            UpdateWorld();

            facing = rng.Next(2) == 0 ? -1 : 1;
            face = personality.BaseFace;
            gazeX = 0.5;
            gazeY = 0;
            yaw = 0;
            clipY = world.GroundAt(cx);
            feetY = clipY + HiddenDepthCells * scale;
            vx = vy = 0;
            squash = 0;
            walkPhase = 0;
            landPause = 0;
            startled = false;
            knocked = false;
            chargeTarget = null;
            afterTurn = Mode.Walk;
            idleTimer = Between(4, 9);
            hopTimer = Between(3, 8);
            blinkTimer = Between(1, 3);
            GreetCooldown = 2;
            ChargeCooldown = 3;
            puffs.Clear();
            Enter(Mode.PeekRise);
        }

        /// <summary>The mouse moved: jump in fright, then drop out of sight.</summary>
        public void Startle()
        {
            BeginExit(true);
        }

        /// <summary>Asked to go (the count was lowered): hop and drop out of sight.</summary>
        public void Leave()
        {
            BeginExit(false);
        }

        void BeginExit(bool withBubble)
        {
            if (mode == Mode.Hidden || mode == Mode.Exit) return;
            bool underground = mode == Mode.PeekRise || mode == Mode.PeekLook || mode == Mode.PeekDuck;
            startled = withBubble && !underground;
            if (underground) vy = 0;
            else if (mode != Mode.Air) vy = -Settings.StartleSpeed * sizeFactor;
            vx = 0;
            Enter(Mode.Exit);
        }

        /// <summary>Another walker is near: tells this one where, for waving at.</summary>
        public void SetCompany(bool present, double x)
        {
            companyNearby = present;
            companyX = x;
        }

        /// <summary>Stop and face `direction` to wave (friendly) or nod (calm) at someone.</summary>
        public void Greet(int direction, bool wave)
        {
            if (!CanGreet) return;
            greetFacing = direction;
            greetWave = wave;
            if (mode == Mode.Walk) Brake(Mode.Greet);
            else Enter(Mode.Greet);
        }

        /// <summary>Run at another walker.</summary>
        public void StartCharge(Walker target)
        {
            if (mode != Mode.Walk || braking) return;
            chargeTarget = target;
            Enter(Mode.Charge);
        }

        /// <summary>Shoved: fly off in `direction`, land, run away.</summary>
        public void Knock(int direction)
        {
            if (!IsVisible || mode == Mode.Exit) return;
            if (IsGrounded) EmitDust(cx, feetY, 3, 0.8);
            vx = direction * 240 * sizeFactor;
            vy = -200 * sizeFactor;
            knocked = true;
            chargeTarget = null;
            Enter(Mode.Air);
        }

        /// <summary>Stand proud for a moment after a successful shove.</summary>
        public void Gloat()
        {
            chargeTarget = null;
            ChargeCooldown = 10;
            vx = 0;
            Enter(Mode.Gloat);
        }

        /// <summary>Advances the simulation by dt seconds.</summary>
        public void Step(double dt, Point cursor)
        {
            cursorX = cursor.X;
            cursorY = cursor.Y;
            time += dt;
            modeTime += dt;
            squash = Math.Max(0, squash - dt / 0.18);
            GreetCooldown -= dt;
            ChargeCooldown -= dt;
            UpdateWorld();

            blinkTimer -= dt;
            blinkLeft -= dt;
            if (blinkTimer <= 0)
            {
                blinkLeft = BlinkSeconds;
                blinkTimer = Between(1.8, 4.5);
            }

            switch (mode)
            {
                case Mode.PeekRise:
                {
                    // Ease the top of the head up over the edge.
                    double t = Math.Min(1, modeTime / 0.4);
                    double eased = 1 - Math.Pow(1 - t, 3);
                    feetY = clipY + (HiddenDepthCells + (PeekDepthCells - HiddenDepthCells) * eased) * scale;
                    if (t >= 1) Enter(Mode.PeekLook);
                    break;
                }

                case Mode.PeekLook:
                    // Check that the coast is clear.
                    headTurned = modeTime > 0.3 && modeTime < 0.8;
                    if (modeTime >= 1.1) Enter(Mode.PeekDuck);
                    break;

                case Mode.PeekDuck:
                {
                    // Dip down, then spring out.
                    double t = Math.Min(1, modeTime / 0.12);
                    feetY = clipY + (PeekDepthCells + 3 * t) * scale;
                    if (t >= 1)
                    {
                        double rise = feetY - clipY + 10 * scale;
                        vy = -Math.Sqrt(2 * Settings.Gravity * sizeFactor * rise);
                        vx = facing * Settings.WalkSpeed * sizeFactor * 0.6;
                        landPause = 0.5;
                        EmitDust(cx, clipY, 6, 1);
                        Enter(Mode.Air);
                    }
                    break;
                }

                case Mode.Air:
                {
                    vy += Settings.Gravity * sizeFactor * dt;
                    feetY += vy * dt;
                    MoveX(vx * dt);
                    double ground = world.GroundAt(cx);
                    clipY = ground;
                    if (vy > 0 && feetY >= ground) Land(ground);
                    break;
                }

                case Mode.Walk:
                    StepWalk(dt, 1, true);
                    break;

                case Mode.Crouch:
                    // A beat of anticipation before a jump.
                    if (!StayOnGround()) break;
                    MoveX(vx * dt);
                    if (modeTime >= 0.08)
                    {
                        vy = -pendingJump;
                        vx = pendingAirSpeed;
                        Enter(Mode.Air);
                    }
                    break;

                case Mode.Turn:
                    // The head turns first, the body follows.
                    if (!StayOnGround()) break;
                    if (modeTime < 0.16)
                    {
                        headTurned = true;
                    }
                    else if (headTurned)
                    {
                        // The body catches up with the head: what was "looking
                        // back" is now "looking ahead", so the eased values flip too.
                        headTurned = false;
                        facing = -facing;
                        yaw = -yaw;
                        gazeX = -gazeX;
                    }
                    if (modeTime >= 0.28)
                    {
                        Mode next = afterTurn;
                        afterTurn = Mode.Walk;
                        Enter(next);
                    }
                    break;

                case Mode.Look:
                    if (!StayOnGround()) break;
                    headTurned = modeTime > 0.35 && modeTime < 1.1;
                    if (modeTime >= 1.5) Enter(rng.NextDouble() < 0.3 ? Mode.Turn : Mode.Walk);
                    break;

                case Mode.Wave:
                    if (!StayOnGround()) break;
                    if (modeTime >= 1.35) Enter(Mode.Walk);
                    break;

                case Mode.Rest:
                    if (!StayOnGround()) break;
                    if (modeTime >= restDuration) Enter(Mode.Walk);
                    break;

                case Mode.Greet:
                    if (!StayOnGround()) break;
                    facing = greetFacing;
                    if (modeTime >= GreetSeconds) Enter(Mode.Walk);
                    break;

                case Mode.Charge:
                {
                    if (!StayOnGround()) break;
                    Walker target = chargeTarget;
                    if (target == null || !target.IsVisible || target.IsLeaving ||
                        Math.Abs(target.CentreX - cx) > 520 * sizeFactor || modeTime > 3)
                    {
                        AbortCharge();
                        break;
                    }
                    if (!braking) facing = target.CentreX >= cx ? 1 : -1;
                    StepWalk(dt, ChargeSpeedFactor, false);
                    break;
                }

                case Mode.Gloat:
                    if (!StayOnGround()) break;
                    if (modeTime >= GloatSeconds) Enter(Mode.Walk);
                    break;

                case Mode.Flee:
                    if (!StayOnGround()) break;
                    fleeTime -= dt;
                    StepWalk(dt, FleeSpeedFactor, false);
                    if (fleeTime <= 0 && mode == Mode.Flee) Enter(Mode.Walk);
                    break;

                case Mode.Exit:
                {
                    vy += Settings.ExitGravity * sizeFactor * dt;
                    feetY += vy * dt;
                    double tall = (HiddenDepthCells + (startled ? 8 : 0)) * scale * 1.2;
                    if (feetY - clipY >= tall)
                    {
                        puffs.Clear();
                        Enter(Mode.Hidden);
                    }
                    break;
                }
            }

            StepDust(dt);
            UpdateFace(dt);
        }

        // ---- The face --------------------------------------------------------------

        /// <summary>Gaze towards a screen point, as -1..1 with +X ahead and +Y up.</summary>
        void LookAt(double x, double y, out double gx, out double gy)
        {
            double eyeY = feetY - 12.5 * scale;
            double reach = 260 * sizeFactor;
            gx = Math.Max(-1, Math.Min(1, (x - cx) / reach)) * facing;
            gy = Math.Max(-1, Math.Min(1, (eyeY - y) / reach));
        }

        // Picks the expression and gaze for the current situation and eases
        // the face towards them, so moods change over a few frames.
        void UpdateFace(double dt)
        {
            Expression target = personality.BaseFace;
            double gx = 0.55, gy = -0.05;
            bool atCursor = false;
            bool friendly = personality.Temperament == Temperament.Friendly;

            switch (mode)
            {
                case Mode.PeekRise:
                case Mode.PeekLook:
                case Mode.PeekDuck:
                    target = Expression.Curious;
                    atCursor = true;
                    break;

                case Mode.Walk:
                    atCursor = Math.Abs(cursorX - cx) < 260 * sizeFactor;
                    break;

                case Mode.Charge:
                    target = Expression.Determined;
                    if (chargeTarget != null) LookAt(chargeTarget.CentreX, chargeTarget.FeetY - 12 * scale, out gx, out gy);
                    break;

                case Mode.Flee:
                    target = fleeTime > FleeSeconds * 0.45 ? Expression.Scared : Expression.Sad;
                    gx = -0.75;
                    gy = 0.1;
                    break;

                case Mode.Crouch:
                    target = Expression.Focused;
                    gy = 0.5;
                    break;

                case Mode.Air:
                    if (knocked) target = Expression.Dizzy;
                    else if (Math.Abs(vy) > 1.2 * Settings.HopSpeed * sizeFactor) target = Expression.Excited;
                    gy = vy < 0 ? 0.5 : -0.5;
                    break;

                case Mode.Exit:
                    target = startled ? Expression.Surprised : Expression.Sad;
                    atCursor = startled;
                    break;

                case Mode.Wave:
                    target = friendly ? (modeTime > 0.5 && modeTime < 1.0 ? Expression.Winking : Expression.Grin) : Expression.Happy;
                    if (!waveAtCursor && companyNearby) LookAt(companyX, feetY - 12 * scale, out gx, out gy);
                    else atCursor = true;
                    break;

                case Mode.Greet:
                    target = greetWave ? Expression.Laugh : Expression.Happy;
                    LookAt(companyX, feetY - 12 * scale, out gx, out gy);
                    break;

                case Mode.Gloat:
                    target = Expression.Smug;
                    gx = 0.7;
                    gy = -0.45;
                    break;

                case Mode.Look:
                    target = Expression.Curious;
                    atCursor = true;
                    break;

                case Mode.Rest:
                    if (restDuration > 1.2 && modeTime > 0.4) target = Expression.Sleepy;
                    atCursor = true;
                    break;

                case Mode.Turn:
                    atCursor = true;
                    break;
            }

            if (atCursor) LookAt(cursorX, cursorY, out gx, out gy);
            if (headTurned)
            {
                gx = -0.8;
                gy = 0;
            }

            face = Expression.Towards(face, target, 1 - Math.Exp(-dt * 14));
            double k = 1 - Math.Exp(-dt * 10);
            gazeX = Settle(gazeX, gx, k);
            gazeY = Settle(gazeY, gy, k);
            yaw = Settle(yaw, YawTarget(), 1 - Math.Exp(-dt * 9));
        }

        // How far round the body should be turned: towards where it is going
        // while on the move, towards whoever it is dealing with when stopped,
        // and towards you the rest of the time.
        double YawTarget()
        {
            switch (mode)
            {
                case Mode.PeekLook:
                    return modeTime < 0.3 ? 0 : modeTime < 0.8 ? -45 : 45;
                case Mode.PeekDuck:
                    return 45;
                case Mode.Walk:
                case Mode.Crouch:
                case Mode.Air:
                    return 58;
                case Mode.Charge:
                case Mode.Flee:
                    return 64;
                case Mode.Turn:
                    return headTurned ? -45 : 58;
                case Mode.Look:
                    return headTurned ? -45 : 0;
                case Mode.Wave:
                    return waveAtCursor || !companyNearby ? 0 : 45;
                case Mode.Greet:
                    return 40;
                case Mode.Gloat:
                    return 35;
                default:
                    return 0; // peeking up, resting, leaving: face the viewer
            }
        }

        static double Settle(double value, double target, double k)
        {
            double next = value + (target - value) * k;
            return Math.Abs(target - next) < 0.004 ? target : next;
        }

        /// <summary>How the body is arranged right now.</summary>
        public Pose CurrentPose()
        {
            Pose pose = Pose.Standing();
            pose.Face = face;
            pose.GazeX = gazeX;
            pose.GazeY = gazeY;
            pose.Yaw = yaw;
            bool breathe = false;

            switch (mode)
            {
                case Mode.Walk:
                    Walk(ref pose);
                    break;

                case Mode.Charge:
                    Walk(ref pose);
                    pose.Lean = 0.6;
                    break;

                case Mode.Flee:
                    // Running with the arms flailing overhead.
                    Walk(ref pose);
                    pose.Lean = 0.4;
                    pose.ArmNear = 115 + 25 * Math.Sin(time * 22);
                    pose.ArmFar = -115 + 25 * Math.Sin(time * 22);
                    pose.ForeNear = 30;
                    pose.ForeFar = -30;
                    break;

                case Mode.Crouch:
                    // Wound up for the jump: squashed, arms out for balance.
                    pose.StretchX = 1.12;
                    pose.StretchY = 0.84;
                    pose.ArmNear = 30;
                    pose.ArmFar = -30;
                    pose.ForeNear = 15;
                    pose.ForeFar = -15;
                    break;

                case Mode.Air:
                case Mode.Exit:
                {
                    bool rising = vy < 0;
                    pose.LegTuck = rising ? 1 : 0.3;
                    pose.ArmNear = pose.ArmFar = rising ? -60 : 55;
                    pose.ForeNear = pose.ForeFar = rising ? 25 : 30;
                    double stretch = Math.Min(1, Math.Abs(vy) / (600 * sizeFactor)) * 0.1;
                    pose.StretchY = 1 + stretch;
                    pose.StretchX = 1 - stretch * 0.7;
                    if (knocked)
                    {
                        // Flung into the air, arms thrown up.
                        pose.ArmNear = 140;
                        pose.ArmFar = -140;
                        pose.ForeNear = 15;
                        pose.ForeFar = -15;
                        pose.LegTuck = 0.6;
                        pose.Bubble = true;
                    }
                    else if (mode == Mode.Exit)
                    {
                        pose.Bubble = startled;
                    }
                    break;
                }

                case Mode.Wave:
                    WaveArm(ref pose);
                    break;

                case Mode.Greet:
                    if (greetWave) WaveArm(ref pose);
                    else pose.BodyDy = Quantise(0.45 * (1 - Math.Cos(modeTime * 2 * Math.PI / 0.5)) / 2); // a nod
                    break;

                case Mode.Gloat:
                    // Chest out, hands on hips.
                    pose.ArmNear = 40;
                    pose.ArmFar = -40;
                    pose.ForeNear = -85;
                    pose.ForeFar = 85;
                    pose.StretchY = 1.04;
                    pose.StretchX = 1.03;
                    pose.Lean = -0.3;
                    break;

                case Mode.Turn:
                case Mode.Look:
                case Mode.Rest:
                    breathe = true;
                    break;
            }

            if (breathe) pose.BodyDy = Quantise(0.3 * (1 + Math.Sin(time * 2.6)) / 2);
            pose.StretchY *= 1 - 0.22 * squash;
            pose.StretchX *= 1 + 0.16 * squash;

            // A blink is a quick close-and-open laid over whatever the face is doing.
            if (blinkLeft > 0)
            {
                double blink = Math.Sin(Math.PI * (1 - blinkLeft / BlinkSeconds));
                if (blink > pose.Face.Lid) pose.Face.Lid = blink;
            }
            return pose;
        }

        void Walk(ref Pose pose)
        {
            if (Math.Abs(vx) <= 6 * sizeFactor) return;
            pose.Walking = true;
            pose.WalkPhase = walkPhase;
            pose.BodyDy = 0.3 * (1 - Math.Cos(walkPhase * 4 * Math.PI)) / 2; // two bobs per stride
        }

        void WaveArm(ref Pose pose)
        {
            // Arm up, forearm out to the side so the hand clears the goggles.
            pose.ArmNear = 150 + 8 * Math.Sin(modeTime * 16);
            pose.ForeNear = -25 + 25 * Math.Sin(modeTime * 16);
        }

        // Slow motions are stepped in eighths of a unit, so a walker that is
        // only breathing is not repainted every single frame.
        static double Quantise(double units)
        {
            return Math.Floor(units * 8 + 0.5) / 8;
        }

        // ---- Terrain ------------------------------------------------------------

        void UpdateWorld()
        {
            worldLeft = world.Left;
            worldRight = world.Right;

            int newScale = world.ScaleAt(cx);
            if (newScale != scale)
            {
                double ratio = (double)newScale / scale;
                vx *= ratio;
                vy *= ratio;
                scale = newScale;
            }
            sizeFactor = scale / (double)Settings.BasePixelSize;
        }

        // Moves horizontally unless that would mean walking into the side of
        // higher ground (a neighbouring monitor whose bottom edge is higher).
        bool MoveX(double dx)
        {
            double half = HalfBodyCells * scale;
            double next = Math.Max(worldLeft + half, Math.Min(worldRight - half, cx + dx));
            if (world.GroundAt(next) < feetY - scale) return false;
            cx = next;
            return true;
        }

        // Looks for a step up ahead, e.g. the edge of a monitor that sits higher.
        bool WallAhead(out double height, out double distance)
        {
            height = 0;
            distance = double.MaxValue;
            double reach = 400 * sizeFactor;
            foreach (int edge in world.MonitorEdges)
            {
                double d = (edge - cx) * facing;
                if (d <= 0 || d > reach || d >= distance) continue;
                double beyond = world.GroundAt(facing > 0 ? edge : edge - 1);
                if (beyond < feetY - scale)
                {
                    distance = d;
                    height = feetY - beyond;
                }
            }
            return distance != double.MaxValue;
        }

        // Keeps the feet on the ground; returns false if there is none (it
        // has walked off the edge of a higher monitor) and a fall has begun.
        bool StayOnGround()
        {
            double ground = world.GroundAt(cx);
            clipY = ground;
            if (ground > feetY + 1.5)
            {
                vy = 0;
                Enter(Mode.Air);
                return false;
            }
            feetY = ground;
            return true;
        }

        // ---- Behaviour ----------------------------------------------------------

        double Between(double low, double high)
        {
            return low + rng.NextDouble() * (high - low);
        }

        static double Approach(double value, double target, double step)
        {
            return value < target ? Math.Min(target, value + step) : Math.Max(target, value - step);
        }

        void Enter(Mode next)
        {
            mode = next;
            modeTime = 0;
            headTurned = false;
            braking = false;
        }

        void Brake(Mode next)
        {
            braking = true;
            afterBrake = next;
        }

        void AbortCharge()
        {
            chargeTarget = null;
            ChargeCooldown = 3;
            Enter(Mode.Walk);
        }

        // Walking, charging and fleeing all share this: ease towards the
        // target speed, turn at the ends of the desktop, jump up onto higher
        // monitors. `antics` allows the random hops and idle stops.
        void StepWalk(double dt, double speedFactor, bool antics)
        {
            if (!StayOnGround()) return;

            double full = Settings.WalkSpeed * sizeFactor * personality.SpeedFactor * speedFactor;
            double target = braking ? 0 : facing * full;
            vx = Approach(vx, target, (braking ? Settings.Braking : Settings.Acceleration) * sizeFactor * dt);
            bool blocked = !MoveX(vx * dt);
            walkPhase += Math.Abs(vx) * dt / (StrideCells * scale);

            if (braking)
            {
                if (Math.Abs(vx) < 1)
                {
                    vx = 0;
                    Enter(afterBrake);
                }
                return;
            }

            // Turn around at the outer edge of the first and last monitor,
            // slowing down in time to stop right at it.
            double half = HalfBodyCells * scale;
            double room = facing > 0 ? worldRight - half - cx : cx - (worldLeft + half);
            if (room <= vx * vx / (2 * Settings.Braking * sizeFactor) + 1)
            {
                if (Math.Abs(vx) > 0.7 * full) EmitDust(cx + facing * 4 * scale, feetY, 2, 0.6);
                afterTurn = Mode.Walk;
                Brake(Mode.Turn);
                return;
            }

            // A monitor with a higher bottom edge ahead: jump up onto it,
            // timing the take-off so the top of the arc is over the edge.
            double wallHeight, wallDistance;
            if (WallAhead(out wallHeight, out wallDistance))
            {
                double gravity = Settings.Gravity * sizeFactor;
                double launch = Math.Sqrt(2 * gravity * (wallHeight + 6 * scale));
                if (blocked || wallDistance <= full * launch / gravity)
                {
                    StartJump(launch, facing * full);
                    return;
                }
            }
            else if (blocked)
            {
                afterTurn = Mode.Walk;
                Brake(Mode.Turn);
                return;
            }

            if (!antics) return;

            idleTimer -= dt;
            hopTimer -= dt;
            if (idleTimer <= 0)
            {
                idleTimer = Between(personality.IdleMin, personality.IdleMax);
                PickIdle();
            }
            else if (hopTimer <= 0)
            {
                hopTimer = Between(personality.HopMin, personality.HopMax);
                if (Math.Abs(vx) > 0.8 * full) StartJump(Settings.HopSpeed * sizeFactor, vx);
            }
        }

        void StartJump(double speed, double airSpeed)
        {
            pendingJump = speed;
            pendingAirSpeed = airSpeed;
            Enter(Mode.Crouch);
        }

        void Land(double ground)
        {
            double impact = vy;
            feetY = ground;
            vy = 0;
            squash = Math.Max(0.35, Math.Min(1, impact / (Settings.HopSpeed * sizeFactor)));
            if (impact > 0.5 * Settings.HopSpeed * sizeFactor) EmitDust(cx, ground, 4, squash);

            if (knocked)
            {
                // Run away from whoever did that.
                knocked = false;
                if (vx != 0) facing = vx > 0 ? 1 : -1;
                fleeTime = FleeSeconds;
                GreetCooldown = Math.Max(GreetCooldown, FleeSeconds + 2);
                Enter(Mode.Flee);
            }
            else if (landPause > 0)
            {
                vx = 0;
                restDuration = landPause;
                landPause = 0;
                Enter(Mode.Rest);
            }
            else
            {
                Enter(Mode.Walk);
            }
        }

        // Every so often: stop and look around, wave at someone, or rest.
        void PickIdle()
        {
            double roll = rng.NextDouble();
            if (roll < personality.LookWeight)
            {
                Brake(Mode.Look);
            }
            else if (roll < personality.LookWeight + personality.WaveWeight)
            {
                // Wave at a nearby walker if there is one, otherwise at the cursor.
                double targetX = companyNearby ? companyX : cursorX;
                waveAtCursor = !companyNearby;
                bool targetBehind = (targetX - cx) * facing < -2 * HalfBodyCells * scale;
                if (targetBehind)
                {
                    afterTurn = Mode.Wave;
                    Brake(Mode.Turn);
                }
                else
                {
                    Brake(Mode.Wave);
                }
            }
            else
            {
                restDuration = Between(0.8, 2.0);
                Brake(Mode.Rest);
            }
        }

        // ---- Dust ---------------------------------------------------------------

        void EmitDust(double x, double y, int count, double strength)
        {
            for (int i = 0; i < count; i++)
            {
                double side = i % 2 == 0 ? -1 : 1;
                Puff puff = new Puff();
                puff.X = x + side * Between(2, 6) * scale;
                puff.Y = y - Between(0, 1) * scale;
                puff.Vx = side * Between(25, 70) * sizeFactor * strength;
                puff.Vy = -Between(8, 35) * sizeFactor;
                puff.Life = puff.MaxLife = Between(0.28, 0.5);
                puffs.Add(puff);
            }
        }

        void StepDust(double dt)
        {
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                Puff puff = puffs[i];
                puff.Life -= dt;
                if (puff.Life <= 0)
                {
                    puffs.RemoveAt(i);
                    continue;
                }
                puff.X += puff.Vx * dt;
                puff.Y += puff.Vy * dt;
                double drag = Math.Max(0, 1 - 3 * dt);
                puff.Vx *= drag;
                puff.Vy *= drag;
            }
        }
    }
}
