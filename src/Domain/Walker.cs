using System;
using System.Collections.Generic;
using System.Drawing;

namespace IdleWalker.Domain
{
    /// <summary>How a walker says hello: a nod, a wave, or a little dance together.</summary>
    enum GreetStyle { Nod, Wave, Dance }

    /// <summary>A short, in-character response to something that just happened.</summary>
    enum Reaction
    {
        Gasp,      // hands up by the face, then a worried look
        Headshake, // hands on hips, shaking its head
        Jeer,      // laughing and pointing
        Chuckle,   // laughing, hands on the belly
        Huff,      // turned away, hands on hips, grumpy
        Cower,     // crouched with the arms over the head, trembling
        Wince,     // hands clapped to the cheeks, one eye screwed shut: that looked painful (Mr. Bean)
        Waft,      // recoiling, fanning a hand in front of the nose (Mr. Bean)
        Bristle    // feathers puffed, wings half out, beak open: back off (Angry Birds)
    }

    /// <summary>Something a walker has set out to do that means walking somewhere first.</summary>
    enum Goal
    {
        None,
        Visit,    // walk up to another walker for a chat, or to comfort it
        Prank,    // sneak up on somebody standing quietly and shout boo
        Inspect   // walk over to underneath the cursor and have a look at it
    }

    /// <summary>What two walkers standing face to face are doing.</summary>
    enum ChatStyle
    {
        Chat,     // taking turns to talk, with gestures; ends in a laugh
        Argue,    // a shouting match with stomping; ends in a huff
        Pat       // one comforting the other
    }

    /// <summary>
    /// One character: where it is, what it is doing and how it moves. Pure
    /// simulation - it knows nothing about windows or pixels on screen beyond
    /// what <see cref="IWorld"/> tells it. Walkers only meet each other
    /// through the <see cref="Troupe"/>. The idle antics and the reactions
    /// live in WalkerAntics.cs; what it wants and the errands and chats that
    /// come of it live in WalkerBrain.cs.
    /// </summary>
    sealed partial class Walker
    {
        const double BlinkSeconds = 0.16;
        const double GreetSeconds = 1.3;
        const double DanceGreetSeconds = 2.6;
        const double GloatSeconds = 0.7;
        const double FleeSeconds = 1.4;
        const double ChargeSpeedFactor = 2.0;
        const double FleeSpeedFactor = 1.6;
        const double ChaseSeconds = 4;          // a game of tag: how long the chaser keeps it up
        const double ScamperSeconds = 3.5;
        const double ChaseSpeedFactor = 1.5;
        const double ScamperSpeedFactor = 1.3;

        enum Mode
        {
            Hidden, PeekRise, PeekLook, PeekDuck, Air, Walk, Crouch, Turn, Look, Wave, Rest, Exit,
            Greet,   // stopped to wave, nod or dance at another walker
            Charge,  // running at another walker to shove it (or, playfully, to tag it)
            Gloat,   // standing over a shoved walker
            Flee,    // running away after being shoved (or, playfully, from a chaser)
            Antic,   // one of the idle antics (dancing, boxing, dozing, ...)
            React,   // a short reaction: to a shove it saw, to being tagged, ...
            Chat     // face to face with another walker: talking, arguing or comforting
        }

        readonly IWorld world;
        readonly Random rng;
        readonly Personality personality;
        readonly Anatomy body;      // its measurements: eye height, stride, how deep it hides, ...
        readonly bool bean;         // it is Mr. Bean: a head on a neck, long arms, his own ways
        readonly bool bird;         // it is an Angry Bird: round, hoppy, flutters down slowly
        readonly bool panther;      // it is the Pink Panther: a sleek cat head on a neck, long tail
        readonly List<Puff> puffs = new List<Puff>();

        Mode mode = Mode.Hidden;
        double modeTime;
        double time;

        int scale = Settings.BasePixelSize;
        double sizeFactor = 1;      // scale relative to 100% display scaling
        int worldLeft, worldRight;  // outer edges of the whole desktop
        int cursorX, cursorY;
        Walker company;             // the nearest other walker on the same ground, if any
        bool companyNearby;         // ... and it is close
        double companyX, companyEyeY;
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
        double idleTimer, hopTimer, tripTimer;
        double blinkTimer, blinkLeft;
        bool blinkAgain;            // a second, quick blink right after this one
        bool startled;

        int greetFacing;
        GreetStyle greetStyle;
        bool knocked;               // airborne because somebody shoved it
        bool playful;               // the current charge or flight is a game of tag
        double fleeTime;
        Walker chargeTarget;

        // Antics and reactions (WalkerAntics.cs).
        Antic antic;
        double anticSeconds;
        int anticStage;             // which of an antic's one-off effects have happened
        Reaction reaction;
        double reactSeconds;
        int reactFacing;
        double reactX;
        int joyHops;                // hops still to come in a jump for joy
        bool celebrating;           // airborne with the arms thrown up
        bool wokeUp;                // airborne because it just jerked awake (or was made to jump)
        bool spooked;               // ... by somebody shouting at it

        // What it wants, and the errands and chats that follow (WalkerBrain.cs).
        double energy = 1, social, curiosity;
        Goal goal;
        Walker goalTarget;
        double goalX, goalTime;
        Antic? broadcast;           // an antic that has just started and others might catch
        bool hasPending;            // a caught yawn or dance, about to start
        Antic pendingAntic;
        double pendingIn, catchCooldown;
        ChatStyle chatStyle;
        Walker chatPartner;
        bool chatLead;
        int chatFacing;
        double chatSeconds;
        int lastStomp;

        // Small touches laid over whatever else is going on.
        double sweat;               // 0 none, else how far the drop has slid, 0..1
        Expression moodFace;        // a face that lingers for a while after an antic
        double moodLeft;
        bool upset;                 // the lingering face is a sad one (friends may come to help)
        double glanceTimer, glanceLeft, glanceDx, glanceDy;  // the eyes darting about while standing

        public Walker(IWorld world, Random rng, Personality personality)
        {
            this.world = world;
            this.rng = rng;
            this.personality = personality;
            body = personality.Anatomy;
            bean = personality.Figure == Figure.Bean;
            bird = personality.Figure == Figure.Bird;
            panther = personality.Figure == Figure.Panther;
        }

        // ---- What others need to know ----------------------------------------------

        public Personality Personality { get { return personality; } }

        /// <summary>The overalls colour (0xRRGGBB); the troupe hands it out.</summary>
        public int Dress = Settings.DressColors[0];
        public Temperament Temperament { get { return personality.Temperament; } }

        public bool IsHidden { get { return mode == Mode.Hidden; } }
        public bool IsVisible { get { return mode != Mode.Hidden; } }
        public bool IsLeaving { get { return mode == Mode.Exit; } }
        public int Scale { get { return scale; } }
        public double CentreX { get { return cx; } }
        public double FeetY { get { return feetY; } }
        public int Facing { get { return facing; } }
        public bool FacesLeft { get { return facing < 0; } }
        public double BodyWidth { get { return 2 * body.HalfWidth * scale; } }
        public IList<Puff> Puffs { get { return puffs; } }

        /// <summary>Where its eyes and shoulders are on screen, for others to look at or reach for.</summary>
        public double EyeY { get { return feetY - body.EyeV * scale; } }
        public double ShoulderY { get { return feetY - body.ShoulderV * scale; } }

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
                    case Mode.Antic: case Mode.React: case Mode.Chat:
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

        /// <summary>Free to stop and say hello, gawp at a shove, be drawn into a chat.</summary>
        public bool CanGreet
        {
            get { return (mode == Mode.Walk && !braking) || mode == Mode.Rest || mode == Mode.Look; }
        }

        /// <summary>Standing about not paying attention: fair game for a prank.</summary>
        public bool IsQuiet
        {
            get
            {
                if (mode == Mode.Rest || mode == Mode.Look) return true;
                if (mode != Mode.Antic) return false;
                return antic == Antic.Doze || antic == Antic.Whistle || antic == Antic.Scratch ||
                       antic == Antic.Yawn || antic == Antic.Hiccups || antic == Antic.Daydream ||
                       antic == Antic.Fret || antic == Antic.Dig || antic == Antic.Sleepwalk ||
                       antic == Antic.Teddy || antic == Antic.Ponder || antic == Antic.Egg;
            }
        }

        /// <summary>Still sad about being shoved.</summary>
        public bool IsUpset { get { return upset && moodLeft > 0; } }

        /// <summary>On an errand (so not to be interrupted by a charge).</summary>
        public bool HasGoal { get { return goal != Goal.None; } }

        public bool IsCharging { get { return mode == Mode.Charge; } }
        public bool IsFleeing { get { return mode == Mode.Flee; } }
        public bool IsPlayful { get { return playful; } }
        public Walker ChargeTarget { get { return chargeTarget; } }

        /// <summary>Seconds before this walker will greet or charge again.</summary>
        public double GreetCooldown { get; set; }
        public double ChargeCooldown { get; set; }

        /// <summary>What it is doing, in a word, for diagnostics.</summary>
        public string Activity
        {
            get
            {
                switch (mode)
                {
                    case Mode.Antic: return antic.ToString();
                    case Mode.React: return reaction.ToString();
                    case Mode.Chat: return chatStyle.ToString();
                    case Mode.Walk: return goal != Goal.None ? "Walk:" + goal : "Walk";
                    case Mode.Charge: return playful ? "Chase" : "Charge";
                    case Mode.Flee: return playful ? "Scamper" : "Flee";
                    case Mode.Greet: return "Greet:" + greetStyle;
                    default: return mode.ToString();
                }
            }
        }

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
            feetY = clipY + body.HiddenDepth * scale;
            vx = vy = 0;
            squash = 0;
            walkPhase = 0;
            landPause = 0;
            startled = false;
            knocked = false;
            chargeTarget = null;
            afterTurn = Mode.Walk;
            idleTimer = Between(4, 9);
            hopTimer = Between(personality.HopMin, personality.HopMax) * EffHop();
            tripTimer = Between(20, 60);
            blinkTimer = Between(1, 3);
            blinkAgain = false;
            glanceTimer = Between(0.5, 1.5);
            glanceLeft = 0;
            ResetEyes();
            energy = Between(0.7, 1);
            social = Between(0, 0.4);
            curiosity = Between(0, 0.4);
            catchCooldown = 0;
            ClearAntics();
            ResetMind();
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
            ClearAntics();
            Enter(Mode.Exit);
        }

        void ClearAntics()
        {
            joyHops = 0;
            celebrating = false;
            wokeUp = false;
            spooked = false;
            playful = false;
            sweat = 0;
            moodLeft = 0;
            upset = false;
            goal = Goal.None;
            goalTarget = null;
            hasPending = false;
            broadcast = null;
            chatPartner = null;
        }

        /// <summary>The nearest other walker on the same ground, or null.</summary>
        public void SetCompany(Walker nearest)
        {
            company = nearest;
            companyNearby = nearest != null && Math.Abs(nearest.CentreX - cx) < 350 * sizeFactor;
            companyX = nearest != null ? nearest.CentreX : 0;
            companyEyeY = nearest != null ? nearest.EyeY : 0;
        }

        /// <summary>Stop and face `direction` to greet someone there.</summary>
        public void Greet(int direction, GreetStyle style)
        {
            if (!CanGreet) return;
            greetFacing = direction;
            greetStyle = style;
            social = 0.1;
            if (style == GreetStyle.Dance) broadcast = Antic.Dance;
            NoteGreet(style);
            Stop(Mode.Greet);
        }

        /// <summary>Saw somebody shoved at x: stop and react in character.</summary>
        public void React(double x)
        {
            if (!CanGreet) return;
            switch (personality.Temperament)
            {
                case Temperament.Friendly:
                    ReactWith(Reaction.Gasp, x, 1.7, false);
                    break;
                case Temperament.Calm:
                    ReactWith(Reaction.Headshake, x, 1.6, false);
                    break;
                case Temperament.Timid:
                    ReactWith(Reaction.Cower, x, 2.0, false);
                    break;
                case Temperament.Goofy:
                    ReactWith(Reaction.Chuckle, x, 1.6, false);
                    break;
                case Temperament.Eccentric:
                    ReactWith(Reaction.Wince, x, 1.8, false);
                    break;
                case Temperament.Feisty:
                    ReactWith(Reaction.Bristle, x, 1.6, false);
                    break;
                case Temperament.Suave:
                    ReactWith(Reaction.Chuckle, x, 1.6, false);
                    break;
                default:
                    ReactWith(Reaction.Jeer, x, 1.8, false);
                    break;
            }
        }

        /// <summary>Somebody at x just let one off: stop and react in character.</summary>
        public void Smell(double x)
        {
            if (!CanGreet) return;
            switch (personality.Temperament)
            {
                case Temperament.Friendly:
                case Temperament.Timid:
                    ReactWith(Reaction.Gasp, x, 1.5, false);
                    break;
                case Temperament.Calm:
                    ReactWith(Reaction.Headshake, x, 1.4, false);
                    break;
                case Temperament.Goofy:
                    ReactWith(Reaction.Chuckle, x, 1.4, false);
                    break;
                case Temperament.Eccentric:
                    ReactWith(Reaction.Waft, x, 1.6, false);
                    break;
                case Temperament.Feisty:
                    ReactWith(Reaction.Bristle, x, 1.4, false);
                    break;
                case Temperament.Suave:
                    ReactWith(Reaction.Waft, x, 1.5, false);
                    break;
                default:
                    ReactWith(Reaction.Huff, x, 1.2, true);
                    break;
            }
        }

        /// <summary>A bully is coming: scream and run before it gets here.</summary>
        public void Panic(int direction)
        {
            if (!IsGrounded || mode == Mode.Flee || mode == Mode.Chat || mode == Mode.Charge) return;
            playful = false;
            facing = direction;
            fleeTime = FleeSeconds * 1.3;
            goal = Goal.None;
            goalTarget = null;
            hasPending = false;
            GreetCooldown = Math.Max(GreetCooldown, fleeTime + 2);
            StartSweat();
            EmitDust(cx, feetY, 2, 0.5);
            Enter(Mode.Flee);
        }

        /// <summary>Run at another walker to shove it.</summary>
        public void StartCharge(Walker target)
        {
            if (mode != Mode.Walk || braking) return;
            chargeTarget = target;
            playful = false;
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
            ClearAntics();
            Enter(Mode.Air);
        }

        /// <summary>Stand proud for a moment after a successful shove.</summary>
        public void Gloat()
        {
            chargeTarget = null;
            ChargeCooldown = Between(20, 40);   // that will do for a while
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
            catchCooldown -= dt;
            steerCooldown -= dt;
            bondCooldown -= dt;
            if (moodLeft > 0)
            {
                moodLeft -= dt;
                if (moodLeft <= 0) upset = false;
            }
            if (sweat > 0)
            {
                sweat += dt / 1.6;
                if (sweat >= 1) sweat = 0;
            }
            UpdateWorld();
            StepBlink(dt);
            StepMood(dt);
            StepStyle(dt);
            StepPending(dt);

            switch (mode)
            {
                case Mode.PeekRise:
                {
                    // Ease the top of the head up over the edge.
                    double t = Math.Min(1, modeTime / 0.4);
                    double eased = 1 - Math.Pow(1 - t, 3);
                    feetY = clipY + (body.HiddenDepth + (body.PeekDepth - body.HiddenDepth) * eased) * scale;
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
                    feetY = clipY + (body.PeekDepth + 3 * t) * scale;
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
                    // Birds flutter: they fall markedly slower, wings out.
                    vy += Settings.Gravity * sizeFactor * dt * (bird && vy > 0 ? 0.55 : 1);
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
                    if (modeTime >= (greetStyle == GreetStyle.Dance ? DanceGreetSeconds : GreetSeconds)) Enter(Mode.Walk);
                    break;

                case Mode.Charge:
                {
                    if (!StayOnGround()) break;
                    Walker target = chargeTarget;
                    if (target == null || !target.IsVisible || target.IsLeaving ||
                        Math.Abs(target.CentreX - cx) > 520 * sizeFactor || modeTime > (playful ? ChaseSeconds : 3))
                    {
                        AbortCharge();
                        break;
                    }
                    if (!braking) facing = target.CentreX >= cx ? 1 : -1;
                    StepWalk(dt, playful ? ChaseSpeedFactor : ChargeSpeedFactor, false);
                    break;
                }

                case Mode.Gloat:
                    if (!StayOnGround()) break;
                    if (modeTime >= GloatSeconds) Enter(Mode.Walk);
                    break;

                case Mode.Flee:
                    if (!StayOnGround()) break;
                    fleeTime -= dt;
                    StepWalk(dt, playful ? ScamperSpeedFactor : FleeSpeedFactor, false);
                    if (fleeTime <= 0 && mode == Mode.Flee) EndFlee();
                    break;

                case Mode.Antic:
                    if (!StayOnGround()) break;
                    StepAntic(dt);
                    if (mode == Mode.Antic && modeTime >= anticSeconds) EndAntic();
                    break;

                case Mode.React:
                    if (!StayOnGround()) break;
                    facing = reactFacing;
                    if (modeTime >= reactSeconds) Enter(Mode.Walk);
                    break;

                case Mode.Chat:
                    if (!StayOnGround()) break;
                    facing = chatFacing;
                    StepChat();
                    if (mode == Mode.Chat && modeTime >= chatSeconds) EndChat();
                    break;

                case Mode.Exit:
                {
                    vy += Settings.ExitGravity * sizeFactor * dt;
                    feetY += vy * dt;
                    double tall = (body.HiddenDepth + (startled ? 8 : 0)) * scale * 1.2;
                    if (feetY - clipY >= tall)
                    {
                        puffs.Clear();
                        Enter(Mode.Hidden);
                    }
                    break;
                }
            }

            // Reflection is not only for long walks: the idle timer keeps
            // running through standing pauses (rest, look, greetings,
            // reactions, gloating) so a crowd never starves anyone's
            // learning. It fires at the next moment free to act.
            if (mode == Mode.Rest || mode == Mode.Look || mode == Mode.Greet ||
                mode == Mode.React || mode == Mode.Gloat)
            {
                idleTimer -= dt;
                if (idleTimer <= 0)
                {
                    idleTimer = Between(personality.IdleMin, personality.IdleMax);
                    if (CanGreet) Decide();
                }
            }

            StepDust(dt);
            UpdateFace(dt);
        }

        // Blinks come in the rhythm of the mood: quick and frequent when
        // frightened, slow and heavy when drowsy, sometimes two in a row -
        // and not at all while the eyes are already shut.
        void StepBlink(double dt)
        {
            blinkTimer -= dt;
            blinkLeft -= dt;
            if (blinkLeft <= 0 && blinkAgain)
            {
                blinkAgain = false;
                blinkLeft = BlinkSeconds * 0.85;
                return;
            }
            if (blinkTimer > 0) return;

            bool tense = face.PupilSize < 0.8;
            bool heavy = face.Lid > 0.5;
            if (face.Lid < 0.9)
            {
                blinkLeft = BlinkSeconds * (heavy ? 1.9 : tense ? 0.8 : 1);
                blinkAgain = !heavy && rng.NextDouble() < 0.2;
            }
            blinkTimer = Between(1.8, 4.5) * (tense ? 0.4 : 1) * BlinkPace;
        }

        // ---- The face --------------------------------------------------------------

        /// <summary>Gaze towards a screen point, as -1..1 with +X ahead and +Y up.</summary>
        void LookAt(double x, double y, out double gx, out double gy)
        {
            double eyeY = feetY - body.EyeV * scale;
            double reach = 260 * sizeFactor;
            gx = Math.Max(-1, Math.Min(1, (x - cx) / reach)) * facing;
            gy = Math.Max(-1, Math.Min(1, (eyeY - y) / reach));
        }

        // Picks the expression and gaze for the current situation and eases
        // the face towards them, so moods change over a few frames - quickly
        // for a fright, slowly for drowsiness.
        void UpdateFace(double dt)
        {
            Expression target = personality.BaseFace;
            double rate = 14;
            double gx = 0.55, gy = -0.05;
            bool atCursor = false;
            bool steady = false;   // standing about: the eyes may wander a little
            bool watching = false; // the eyes are on another walker

            switch (mode)
            {
                case Mode.PeekRise:
                case Mode.PeekLook:
                case Mode.PeekDuck:
                    target = Expression.Curious;
                    atCursor = true;
                    break;

                case Mode.Walk:
                    if (goal != Goal.None) GoalFace(ref target, ref gx, ref gy, ref atCursor);
                    else
                    {
                        if (moodLeft > 0) target = moodFace;
                        // Passing someone: meet their eyes; otherwise the cursor if it is near.
                        watching = WatchCompany(false, ref gx, ref gy);
                        if (!watching) atCursor = Math.Abs(cursorX - cx) < 260 * sizeFactor;
                    }
                    break;

                case Mode.Charge:
                    target = playful ? Expression.Laugh : Expression.Determined;
                    if (chargeTarget != null) LookAt(chargeTarget.CentreX, chargeTarget.EyeY, out gx, out gy);
                    break;

                case Mode.Flee:
                    if (playful) target = Expression.Giggle;
                    else target = fleeTime > FleeSeconds * 0.45 ? Expression.Scared : Expression.Sad;
                    rate = 24;
                    gx = -0.75;
                    gy = 0.1;
                    break;

                case Mode.Crouch:
                    target = celebrating ? Expression.Excited : Expression.Focused;
                    gy = 0.5;
                    break;

                case Mode.Air:
                    if (knocked) target = Expression.Dizzy;
                    else if (wokeUp)
                    {
                        target = Expression.Shocked;
                        rate = 30;
                        atCursor = !spooked;
                        if (spooked) gx = 0.6;
                    }
                    else if (celebrating)
                    {
                        target = Expression.Excited;
                        atCursor = true;
                    }
                    else if (Math.Abs(vy) > 1.2 * Settings.HopSpeed * sizeFactor) target = Expression.Excited;
                    gy = vy < 0 ? 0.5 : -0.5;
                    break;

                case Mode.Exit:
                    target = startled ? Expression.Surprised : Expression.Sad;
                    rate = startled ? 30 : 14;
                    atCursor = startled;
                    break;

                case Mode.Wave:
                    target = personality.Warm
                        ? (modeTime > 0.5 && modeTime < 1.0 ? Expression.Winking : Expression.Grin)
                        : personality.Temperament == Temperament.Timid ? Expression.Nervous : Expression.Happy;
                    if (!waveAtCursor && companyNearby) LookAt(companyX, companyEyeY, out gx, out gy);
                    else atCursor = true;
                    steady = true;
                    break;

                case Mode.Greet:
                    switch (greetStyle)
                    {
                        case GreetStyle.Dance:
                            target = modeTime < DanceGreetSeconds * 0.5 ? Expression.Grin : Expression.Laugh;
                            rate = 18;
                            break;
                        case GreetStyle.Wave:
                            target = Expression.Laugh;
                            break;
                        default:
                            target = Expression.Happy;
                            break;
                    }
                    LookAt(companyX, companyEyeY, out gx, out gy);
                    steady = true;
                    break;

                case Mode.Gloat:
                    target = Expression.Smug;
                    gx = 0.7;
                    gy = -0.45;
                    break;

                case Mode.Look:
                    target = Expression.Curious;
                    atCursor = true;
                    steady = true;
                    break;

                case Mode.Rest:
                    if (restDuration > 1.2 && modeTime > 0.4)
                    {
                        target = personality.RestFace;
                        rate = 7;
                    }
                    atCursor = true;
                    steady = true;
                    break;

                case Mode.Turn:
                    atCursor = true;
                    break;

                case Mode.Antic:
                    AnticFace(ref target, ref rate, ref gx, ref gy, ref atCursor);
                    steady = true;
                    break;

                case Mode.React:
                    ReactFace(ref target, ref rate, ref gx, ref gy);
                    steady = true;
                    break;

                case Mode.Chat:
                    ChatFace(ref target, ref rate, ref gx, ref gy);
                    steady = true;
                    break;
            }

            if (atCursor) LookAt(cursorX, cursorY, out gx, out gy);
            if (headTurned)
            {
                gx = -0.8;
                gy = 0;
            }

            // Standing about: the eyes follow a passer-by, and now and then
            // act a little (WalkerEyes.cs). The darting only while they are free.
            bool standing = (mode == Mode.Rest || mode == Mode.Look) && !headTurned;
            bool strolling = mode == Mode.Walk && goal == Goal.None && moodLeft <= 0;
            if (standing) watching = WatchCompany(true, ref gx, ref gy);
            NoticePasser(standing && watching);
            double gazeRate = GazeRate;
            if (standing || strolling) gazeRate = EyeBeats(dt, watching, strolling, ref target, ref rate, ref gx, ref gy);
            else EndEyeBeat();
            Glance(dt, steady && target.Lid < 0.9 && eyeBeat == EyeBeat.None && !watching, ref gx, ref gy);
            GazeBlink(gx, gy);
            StepEyeExtras(dt, atCursor && !watching, watching);

            face = Expression.Towards(face, target, 1 - Math.Exp(-dt * rate));
            double k = 1 - Math.Exp(-dt * gazeRate);
            gazeX = Settle(gazeX, gx, k);
            gazeY = Settle(gazeY, gy, k);

            bool snap;
            double yawTarget = YawTarget(out snap);
            yaw = snap ? yawTarget : Settle(yaw, yawTarget, 1 - Math.Exp(-dt * 9));
        }

        // Real eyes never hold perfectly still: every second or two, while
        // standing about, the pupils dart a little way off and come back.
        void Glance(double dt, bool allowed, ref double gx, ref double gy)
        {
            glanceTimer -= dt;
            if (glanceLeft > 0)
            {
                glanceLeft -= dt;
            }
            else if (glanceTimer <= 0 && allowed)
            {
                glanceLeft = Between(0.18, 0.32);
                glanceDx = Between(-0.16, 0.16);
                glanceDy = Between(-0.08, 0.08);
                glanceTimer = Between(0.8, 2.4);
            }
            if (glanceLeft > 0 && allowed)
            {
                gx += glanceDx;
                gy += glanceDy;
            }
        }

        // How far round the body should be turned: towards where it is going
        // while on the move, towards whoever it is dealing with when stopped,
        // and towards you the rest of the time. `snap` means take the value
        // this frame instead of easing towards it (a spin, a head-shake).
        double YawTarget(out bool snap)
        {
            snap = false;
            switch (mode)
            {
                case Mode.PeekLook:
                    return modeTime < 0.3 ? 0 : modeTime < 0.8 ? -45 : 45;
                case Mode.PeekDuck:
                    return 45;
                case Mode.Walk:
                    return 58 + SwaggerYaw();
                case Mode.Crouch:
                case Mode.Air:
                    return 58;
                case Mode.Charge:
                case Mode.Flee:
                    return 64;
                case Mode.Turn:
                    return headTurned ? -45 : 58;
                case Mode.Look:
                    return headTurned ? ((bean || panther) ? -15 : -45) : 0;   // Bean and Panther turn the head, not the whole body
                case Mode.Wave:
                    return waveAtCursor || !companyNearby ? 0 : 45;
                case Mode.Greet:
                    return greetStyle == GreetStyle.Dance ? DanceYaw(modeTime, 40) : 40;
                case Mode.Gloat:
                    return 35;
                case Mode.Rest:
                    return RestYaw();
                case Mode.Antic:
                    return AnticYaw(out snap);
                case Mode.React:
                    return ReactYaw(out snap);
                case Mode.Chat:
                    return ChatYaw();
                default:
                    return 0; // peeking up, leaving: face the viewer
            }
        }

        // Standing still, each temperament holds itself differently: the
        // friendly ones rock gently, the calm one turns a little towards the
        // cursor, the aggressive ones stand half turned away.
        double RestYaw()
        {
            if (eyeBeat == EyeBeat.Viewer) return 0;   // the head comes round for a look at you
            switch (personality.Temperament)
            {
                case Temperament.Friendly:
                case Temperament.Goofy:
                    return 12 * Math.Sin(time * 1.4);
                case Temperament.Aggressive:
                    return 15;
                case Temperament.Timid:
                    return -12;   // half turned away, ready to run
                case Temperament.Eccentric:
                    return 5;     // square on; the head does the looking about
                case Temperament.Feisty:
                    return 8 * Math.Sin(time * 1.1);   // perched, shifting from foot to foot
                case Temperament.Suave:
                    return Math.Round(8 * Math.Sin(time * 0.9) * 4) / 4;   // suave sway, head does the work
                default:
                    return (cursorX - cx) * facing >= 0 ? 10 : -10;
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
            // Fine motion is stepped so a figure that is only easing into
            // place is not repainted for changes too small to see.
            pose.GazeX = Math.Round(gazeX * 32) / 32;
            pose.GazeY = Math.Round(gazeY * 32) / 32;
            pose.Yaw = Math.Round(yaw);
            bool breathe = false;

            switch (mode)
            {
                case Mode.Walk:
                    Walk(ref pose);
                    pose.Lean = EffLean();
                    if (goal == Goal.Prank)
                    {
                        // Creeping up: leaning in, fists up by the chest.
                        pose.Lean = 0.35;
                        pose.ArmNear = 60;
                        pose.ForeNear = 90;
                        pose.ArmFar = 60;
                        pose.ForeFar = 90;
                    }
                    break;

                case Mode.Charge:
                    Walk(ref pose);
                    pose.Lean = playful ? 0.4 : 0.6;
                    if (playful)
                    {
                        // Reaching out to tag.
                        pose.ArmNear = 80;
                        pose.ForeNear = 10;
                    }
                    break;

                case Mode.Flee:
                    // Running with the arms flailing overhead.
                    Walk(ref pose);
                    pose.Lean = 0.4;
                    pose.ArmNear = 115 + 25 * Math.Sin(time * 22);
                    pose.ArmFar = -115 - 25 * Math.Sin(time * 22);
                    pose.ForeNear = 30;
                    pose.ForeFar = -30;
                    break;

                case Mode.Crouch:
                    // Wound up for the jump: squashed, arms out for balance.
                    pose.StretchX = 1.12;
                    pose.StretchY = 0.84;
                    pose.ArmNear = celebrating ? 120 : 30;
                    pose.ArmFar = celebrating ? -120 : -30;
                    pose.ForeNear = 15;
                    pose.ForeFar = -15;
                    break;

                case Mode.Air:
                case Mode.Exit:
                {
                    bool rising = vy < 0;
                    pose.LegTuck = rising ? 1 : 0.3;
                    // Mirrored (one ahead, one behind) so both hands don't end
                    // up on the same side when jumping past sideways.
                    pose.ArmNear = rising ? 60 : 55;
                    pose.ArmFar = rising ? -60 : -55;
                    pose.ForeNear = rising ? 25 : 30;
                    pose.ForeFar = rising ? -25 : -30;
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
                    else if (celebrating)
                    {
                        // Jumping for joy: arms straight up, knees tucked.
                        pose.ArmNear = 160;
                        pose.ArmFar = -160;
                        pose.ForeNear = -10;
                        pose.ForeFar = 10;
                        pose.LegTuck = rising ? 1 : 0.5;
                    }
                    else if (wokeUp)
                    {
                        // Jerked awake or made to jump: arms flung out, a "!" overhead.
                        pose.ArmNear = 70;
                        pose.ArmFar = -70;
                        pose.ForeNear = 10;
                        pose.ForeFar = -10;
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
                    switch (greetStyle)
                    {
                        case GreetStyle.Dance:
                            DancePose(ref pose, modeTime);
                            break;
                        case GreetStyle.Wave:
                            WaveArm(ref pose);
                            break;
                        default:
                            pose.BodyDy = Quantise(0.45 * (1 - Math.Cos(modeTime * 2 * Math.PI / 0.5)) / 2); // a nod
                            break;
                    }
                    break;

                case Mode.Gloat:
                    // Chest out, hands on hips.
                    HandsOnHips(ref pose);
                    pose.StretchY = 1.04;
                    pose.StretchX = 1.03;
                    pose.Lean = -0.3;
                    break;

                case Mode.Rest:
                    // The aggressive ones wait with their hands on their hips.
                    if (personality.Temperament == Temperament.Aggressive)
                    {
                        HandsOnHips(ref pose);
                        pose.Lean = -0.15;
                    }
                    else if (personality.Temperament == Temperament.Timid)
                    {
                        // The nervous one wrings its hands.
                        pose.ArmNear = 35;
                        pose.ArmFar = 35;
                        double wring = Math.Sin(time * 7) > 0 ? 6 : 0;   // two positions, a few times a second
                        pose.ForeNear = 70 + wring;
                        pose.ForeFar = 76 - wring;
                        pose.Lean = 0.1;
                    }
                    else if (personality.Temperament == Temperament.Eccentric)
                    {
                        // Mr. Bean: hands clasped in front, rocking on his heels.
                        HandsClasped(ref pose);
                        pose.Lean = Math.Round(0.12 * Math.Sin(time * 1.1) * 8) / 8;
                    }
                    else if (personality.Temperament == Temperament.Suave)
                    {
                        // The Panther: one paw on the hip, suave lean, tail swaying.
                        pose.ArmNear = 40;
                        pose.ForeNear = -85;
                        pose.ArmFar = -10;
                        pose.ForeFar = 20;
                        pose.Lean = Math.Round((-0.12 + 0.08 * Math.Sin(time * 0.9)) * 8) / 8;
                    }
                    breathe = true;
                    break;

                case Mode.Antic:
                    AnticPose(ref pose);
                    break;

                case Mode.React:
                    ReactPose(ref pose);
                    break;

                case Mode.Chat:
                    ChatPose(ref pose);
                    break;

                case Mode.Turn:
                case Mode.Look:
                    breathe = true;
                    break;
            }

            if (breathe)
            {
                pose.BodyDy = Quantise(0.3 * (1 + Math.Sin(time * 2.6)) / 2);
                pose.Lean = Quantise(pose.Lean + FidgetLean());
            }
            if (bean || panther) BeanTouch(ref pose);
            pose.StretchY *= (1 - 0.22 * squash) * personality.BuildY;
            pose.StretchX *= (1 + 0.16 * squash) * personality.BuildX;
            if (sweat > 0) pose.Sweat = Math.Ceiling(sweat * 12) / 12;

            // A blink is a quick close-and-open laid over whatever the face is doing.
            if (blinkLeft > 0)
            {
                double blink = Math.Sin(Math.PI * (1 - blinkLeft / BlinkSeconds));
                if (blink > pose.Face.Lid) pose.Face.Lid = blink;
            }

            // Eyes crossing on a cursor under the nose (or for fun), and
            // pupils that grow for something liked and shrink for a stare.
            if (crossEye > 0.01) pose.Face.CrossEye = Math.Max(pose.Face.CrossEye, Math.Round(crossEye * 16) / 16);
            if (pupilInterest != 0) pose.Face.PupilSize *= 1 + 0.2 * Math.Round(pupilInterest * 8) / 8;

            // Talking: the mouth flaps over whatever the face is doing.
            if (mode == Mode.Chat && chatStyle != ChatStyle.Pat && Talking)
            {
                double amp = chatStyle == ChatStyle.Argue ? 0.8 : 0.45;
                double flap = Math.Round((0.5 + 0.5 * Math.Sin(time * 16)) * amp * 8) / 8;
                if (flap > pose.Face.MouthOpen) pose.Face.MouthOpen = flap;
            }
            return pose;
        }

        void Walk(ref Pose pose)
        {
            if (Math.Abs(vx) <= 6 * sizeFactor) return;
            pose.Walking = true;
            pose.WalkPhase = walkPhase;
            pose.BodyDy = EffBob() * (1 - Math.Cos(walkPhase * 4 * Math.PI)) / 2; // two bobs per stride
        }

        void WaveArm(ref Pose pose)
        {
            if (bean)
            {
                HelloPose(ref pose, modeTime);
                return;
            }
            if (bird)
            {
                // No wings to wave with: a happy bouncing bow instead.
                pose.BodyDy = Quantise(0.4 * (1 - Math.Cos(modeTime * 2 * Math.PI / 0.45)) / 2);
                pose.Lean = Math.Round(0.2 * Math.Sin(modeTime * 7) * 8) / 8;
                pose.StretchY = 1.03;
                return;
            }
            if (panther)
            {
                // Suave slow wave: paw high, elegant sway, slight bow of the head.
                pose.ArmNear = 145 + 10 * Math.Sin(modeTime * 8);
                pose.ForeNear = -20 + 18 * Math.Sin(modeTime * 8);
                pose.Lean = Math.Round(0.1 * Math.Sin(modeTime * 3.5) * 8) / 8;
                pose.HeadDip = Quantise(0.15 + 0.1 * Math.Sin(modeTime * 3.5));
                pose.BodyDy = Quantise(0.25 * (1 - Math.Cos(modeTime * 2 * Math.PI / 0.9)) / 2);
                return;
            }
            // Arm up, forearm out to the side so the hand clears the goggles.
            // Turned sideways the goggles swing out to the edge, so open the
            // arm up (more horizontal) to stay past them instead of waving
            // over the face.
            double widen = 1 - Math.Cos(yaw * Math.PI / 180);
            pose.ArmNear = 150 + 8 * Math.Sin(modeTime * 16) - 130 * widen;
            pose.ForeNear = -25 + 25 * Math.Sin(modeTime * 16);
        }

        static void HandsOnHips(ref Pose pose)
        {
            pose.ArmNear = 40;
            pose.ArmFar = -40;
            pose.ForeNear = -85;
            pose.ForeFar = 85;
        }

        // Mr. Bean's hands clasped in front of him at the belly.
        static void HandsClasped(ref Pose pose)
        {
            pose.ArmNear = 10;
            pose.ArmFar = -10;
            pose.ForeNear = -30;
            pose.ForeFar = 30;
            pose.ReachNear = 50;
            pose.ReachFar = 50;
        }

        // Mr. Bean's hands behind his back.
        static void HandsBehind(ref Pose pose)
        {
            pose.ArmNear = -25;
            pose.ArmFar = 25;
            pose.ForeNear = -60;
            pose.ForeFar = 60;
        }

        // Mr. Bean's touches on whatever the pose is: the head moves on its
        // own (the body stays put), he creeps on tiptoe when up to something,
        // and a nod is a nod of the head rather than a dip of the whole body.
        void BeanTouch(ref Pose pose)
        {
            switch (mode)
            {
                case Mode.PeekLook:
                    pose.HeadTurn = modeTime < 0.3 ? 0 : modeTime < 0.8 ? -40 : 40;
                    break;

                case Mode.Look:
                    pose.HeadTurn = headTurned ? -40 : 0;
                    break;

                case Mode.Walk:
                    pose.HeadDip = 0.15;
                    if (goal == Goal.Prank)
                    {
                        // Creeping up on tiptoe, hunched, hands up like a cartoon burglar.
                        pose.Tiptoe = 1;
                        pose.Lean = 0.3;
                        pose.ArmNear = 45;
                        pose.ForeNear = 100;
                        pose.ArmFar = 45;
                        pose.ForeFar = 100;
                        pose.HeadDip = 0.3;
                        pose.BodyDy = Quantise(0.3 + 0.3 * (1 - Math.Cos(walkPhase * 4 * Math.PI)) / 2);
                    }
                    break;

                case Mode.Rest:
                    // Looking slowly about, in steps so a rest is not repainted every frame.
                    pose.HeadTurn = Math.Round(20 * Math.Sin(time * 0.9) / 4) * 4;
                    break;

                case Mode.Greet:
                    if (greetStyle == GreetStyle.Nod)
                    {
                        pose.HeadDip = Quantise(0.7 * (1 - Math.Cos(modeTime * 2 * Math.PI / 0.5)) / 2);
                        pose.BodyDy = 0;
                    }
                    break;

                case Mode.Chat:
                    if (chatStyle == ChatStyle.Chat && !Talking)
                    {
                        // Nodding along with the head.
                        pose.HeadDip = Quantise(0.5 * (1 - Math.Cos(2 * Math.PI * modeTime / 0.6)) / 2);
                        pose.BodyDy = 0;
                    }
                    break;

                case Mode.Air:
                case Mode.Exit:
                    pose.HeadDip = vy < 0 ? -0.2 : 0.2;
                    break;
            }
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
            double half = body.HalfWidth * scale;
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

        /// <summary>Goes into `next`: braking to a halt first if on the move, at once if standing.</summary>
        void Stop(Mode next)
        {
            if (mode == Mode.Walk) Brake(next);
            else Enter(next);
        }

        void AbortCharge()
        {
            chargeTarget = null;
            ChargeCooldown = 8;
            if (playful)
            {
                // Couldn't catch them: give up, laughing and out of breath.
                playful = false;
                SetMood(Expression.Panting, 1.4);
            }
            Enter(Mode.Walk);
        }

        void EndFlee()
        {
            if (!playful)
            {
                // Still shaken for a while: friends may come to help.
                SetMood(Expression.Sad, 5);
                upset = true;
            }
            playful = false;
            Enter(Mode.Walk);
        }

        /// <summary>A face to keep for a while once back on the move.</summary>
        void SetMood(Expression mood, double seconds)
        {
            moodFace = mood;
            moodLeft = seconds;
            upset = false;
        }

        void StartSweat()
        {
            if (sweat <= 0) sweat = 0.001;
        }

        // Walking, charging and fleeing all share this: ease towards the
        // target speed, turn at the ends of the desktop, jump up onto higher
        // monitors. `antics` allows the errands, random hops and idle stops.
        void StepWalk(double dt, double speedFactor, bool antics)
        {
            if (!StayOnGround()) return;

            double full = Settings.WalkSpeed * sizeFactor * EffSpeed() * speedFactor;
            if (antics) full *= GoalPace();
            double target = braking ? 0 : facing * full;
            vx = Approach(vx, target, (braking ? Settings.Braking : Settings.Acceleration) * sizeFactor * dt);
            bool blocked = !MoveX(vx * dt);
            walkPhase += Math.Abs(vx) * dt / (body.StrideCells * scale);
            trialDist += Math.Abs(vx) * dt;

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
            double half = body.HalfWidth * scale;
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

            if (!antics)
            {
                goal = Goal.None;   // being chased or doing the chasing cancels any errand
                return;
            }

            // On an errand: head for it, and nothing else until it is done.
            if (goal != Goal.None)
            {
                PursueGoal(dt);
                return;
            }

            // The clumsy one trips now and then.
            if (personality.Clumsy > 0)
            {
                tripTimer -= dt;
                if (tripTimer <= 0)
                {
                    tripTimer = Between(40, 100) / personality.Clumsy;
                    if (Math.Abs(vx) > 0.7 * full)
                    {
                        StartTrip();
                        return;
                    }
                }
            }

            // Walking with a purpose, not just pacing: lonely with a friend
            // behind, turn back instead of marching away; now and then head
            // for the stretch of desktop where life has been good.
            if (!braking && steerCooldown <= 0)
            {
                bool together = company != null && company.IsVisible && !company.IsLeaving &&
                                Math.Abs(company.FeetY - feetY) <= 2;
                double cdx = together ? company.CentreX - cx : 0;
                double adx = Math.Abs(cdx);
                if (together && social > 0.6 && adx > 2.5 * BodyWidth &&
                    adx < 800 * sizeFactor && cdx * facing < 0)
                {
                    steerCooldown = Between(6, 12);
                    afterTurn = Mode.Walk;
                    Brake(Mode.Turn);
                    return;
                }
                if (rng.NextDouble() < dt * 0.05)
                {
                    double hx = BinCenter(BestPlace());
                    if ((hx - cx) * facing < -BodyWidth)
                    {
                        steerCooldown = Between(8, 15);
                        afterTurn = Mode.Walk;
                        Brake(Mode.Turn);
                        return;
                    }
                    steerCooldown = Between(4, 8);
                }
            }

            idleTimer -= dt;
            hopTimer -= dt;
            if (idleTimer <= 0)
            {
                idleTimer = Between(personality.IdleMin, personality.IdleMax);
                Decide();
            }
            else if (hopTimer <= 0)
            {
                // A hop only while it has the energy to spare.
                hopTimer = Between(personality.HopMin, personality.HopMax) * EffHop();
                if (energy > 0.6 && Math.Abs(vx) > 0.8 * full) StartJump(Settings.HopSpeed * sizeFactor, vx);
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
                StartSweat();
                Enter(Mode.Flee);
            }
            else if (celebrating && joyHops > 0)
            {
                // Straight back up for the next hop.
                joyHops--;
                StartJump(0.85 * Settings.HopSpeed * sizeFactor, 0);
            }
            else if (celebrating)
            {
                celebrating = false;
                SetMood(Expression.Giggle, 0.9);
                Enter(Mode.Walk);
            }
            else if (wokeUp)
            {
                wokeUp = false;
                if (spooked)
                {
                    // Each takes a fright its own way.
                    spooked = false;
                    switch (personality.Temperament)
                    {
                        case Temperament.Friendly:
                        case Temperament.Goofy:
                            SetMood(Expression.Giggle, 1.6);
                            break;
                        case Temperament.Calm:
                            SetMood(Expression.Grumpy, 2.5);
                            break;
                        case Temperament.Timid:
                            // Too much: out cold.
                            vx = 0;
                            StartAntic(Antic.Faint);
                            return;
                        case Temperament.Eccentric:
                            // A long hard stare at whoever did that.
                            SetMood(Expression.Glare, 2.5);
                            break;
                        case Temperament.Feisty:
                            // Bristling fury: pecking order to sort out.
                            SetMood(Expression.Furious, 2.5);
                            break;
                        default:
                            SetMood(Expression.Furious, 2);
                            ChargeCooldown = 0;   // and may well go for whoever did it
                            break;
                    }
                }
                else
                {
                    // Where am I?
                    SetMood(Expression.Curious, 1.0);
                }
                Enter(Mode.Walk);
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
