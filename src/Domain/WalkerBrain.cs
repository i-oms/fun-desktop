using System;
using System.Drawing;

namespace IdleWalker.Domain
{
    /// <summary>
    /// What a walker wants and what it does about it. Three simple drives -
    /// energy, the wish for company, and curiosity about the cursor - rise
    /// and fall with what it has been doing, and when an idle moment comes
    /// they decide between an errand (walking somewhere to do something with
    /// somebody) and one of its habits on the spot. Also here: the chats,
    /// arguments and comforting that errands lead to, pranks, games of tag,
    /// and the yawns and dances that are catching.
    /// </summary>
    partial class Walker
    {
        // ---- Wants ------------------------------------------------------------------

        // Energy drains with exertion and comes back with rest; the wish for
        // company and the curiosity about the cursor simply grow until they
        // are satisfied.
        void StepMood(double dt)
        {
            // Exertion costs energy; everything else slowly gives it back,
            // a rest more so, a doze most of all.
            double drain = 0, gain = 0.02;
            switch (mode)
            {
                case Mode.Walk:
                    gain = 0.015;
                    break;
                case Mode.Charge:
                case Mode.Flee:
                    drain = 0.08;
                    gain = 0;
                    break;
                case Mode.Rest:
                    gain = 0.08;
                    break;
                case Mode.Greet:
                    if (greetStyle == GreetStyle.Dance)
                    {
                        drain = 0.08;
                        gain = 0;
                    }
                    break;
                case Mode.Chat:
                    if (chatStyle == ChatStyle.Argue)
                    {
                        drain = 0.06;
                        gain = 0;
                    }
                    break;
                case Mode.Antic:
                    switch (antic)
                    {
                        case Antic.Dance: case Antic.Twirl: case Antic.Joy: case Antic.Box: case Antic.Flex:
                        case Antic.Stomp: case Antic.Laugh: case Antic.Boo: case Antic.Trip:
                        case Antic.Strum: case Antic.Pushups: case Antic.Jog: case Antic.Flap:
                        case Antic.Dig: case Antic.Moonwalk: case Antic.Jive: case Antic.Sneak:
                            drain = 0.08;
                            gain = 0;
                            break;
                        case Antic.Doze:
                            if (modeTime > 2.1) gain = 0.3;
                            break;
                        case Antic.Teddy:
                            gain = 0.04;   // a comfort
                            break;
                        case Antic.Sleepwalk:
                            gain = 0.2;
                            break;
                        case Antic.Daydream:
                            gain = 0.05;
                            break;
                    }
                    break;
            }
            energy = Math.Max(0, Math.Min(1, energy + (gain - drain / personality.Stamina) * dt));
            social = Math.Min(1, social + 0.015 * personality.Sociable * dt);
            curiosity = Math.Min(1, curiosity + 0.012 * personality.Curious * dt);
        }

        /// <summary>How much go it has left, 0..1.</summary>
        public double Energy { get { return energy; } }

        // An idle moment: first the errands (each in character and only
        // when the wish for it has grown), otherwise one of its habits on
        // the spot, weighted by how it is feeling.
        void Decide()
        {
            Walker other = company;
            bool same = other != null && other.IsVisible && other.IsGrounded && !other.IsLeaving &&
                        Math.Abs(other.FeetY - feetY) <= 2;
            double d = same ? Math.Abs(other.CentreX - cx) : double.MaxValue;
            Temperament tm = personality.Temperament;

            // The timid one keeps well away from the bullies: hides if one is
            // close, otherwise turns and walks the other way, glancing back.
            if (same && tm == Temperament.Timid && other.Temperament == Temperament.Aggressive && d < 380 * sizeFactor)
            {
                if (d < 160 * sizeFactor || rng.NextDouble() < 0.35) StartAntic(Antic.Hide);
                else if ((other.CentreX - cx) * facing > 0)
                {
                    afterTurn = Mode.Walk;
                    Brake(Mode.Turn);
                }
                else StartAntic(Antic.Look);
                return;
            }

            // A bully with somebody standing quietly in range creeps up on
            // them to shout boo; Mr. Bean creeps up to give them a poke.
            if (same && (tm == Temperament.Aggressive || tm == Temperament.Eccentric) && other.IsQuiet &&
                d > 1.6 * BodyWidth && d < 450 * sizeFactor && energy > 0.4 && rng.NextDouble() < (bean ? 0.5 : 0.6))
            {
                SetGoal(Goal.Prank, other);
                return;
            }

            // A friendly one goes to comfort anyone who has been shoved.
            if (same && tm == Temperament.Friendly && other.IsUpset && d > 1.2 * BodyWidth && d < 900 * sizeFactor)
            {
                SetGoal(Goal.Visit, other);
                return;
            }

            // Wanting company: go over for a chat.
            if (same && tm != Temperament.Aggressive && d > 1.6 * BodyWidth && d < 900 * sizeFactor &&
                rng.NextDouble() < social * personality.Sociable * 0.9)
            {
                SetGoal(Goal.Visit, other);
                return;
            }

            // Two friendly ones with energy to burn play tag.
            if (same && personality.Warm && other.Personality.Warm && other.IsWalking &&
                !other.HasGoal && d < 500 * sizeFactor && energy > 0.55 && rng.NextDouble() < 0.4)
            {
                StartChase(other);
                return;
            }

            // The cursor has become too interesting to ignore.
            if (curiosity > 0.6 && rng.NextDouble() < curiosity * personality.Curious * 0.5 && CursorOnThisGround())
            {
                if (Math.Abs(cursorX - cx) < 60 * sizeFactor)
                {
                    curiosity = 0;
                    StartAntic(InspectAntic());
                }
                else
                {
                    SetGoal(Goal.Inspect, null);
                }
                return;
            }

            Habit[] habits = personality.Habits;
            double total = 0;
            for (int i = 0; i < habits.Length; i++) total += HabitWeight(habits[i]);
            double roll = rng.NextDouble() * total;
            for (int i = 0; i < habits.Length; i++)
            {
                roll -= HabitWeight(habits[i]);
                if (roll < 0 || i == habits.Length - 1)
                {
                    StartAntic(habits[i].Antic);
                    return;
                }
            }
        }

        // Tired walkers rest more and caper less; a curious one looks and points more.
        double HabitWeight(Habit habit)
        {
            switch (habit.Antic)
            {
                case Antic.Rest:
                case Antic.Yawn:
                case Antic.Doze:
                case Antic.Sleepwalk:
                    return habit.Weight * (0.4 + 2.5 * (1 - energy));
                case Antic.Dance:
                case Antic.Twirl:
                case Antic.Joy:
                case Antic.Box:
                case Antic.Flex:
                case Antic.Stomp:
                case Antic.Strum:
                case Antic.Pushups:
                case Antic.Jog:
                case Antic.Flap:
                case Antic.Moonwalk:
                case Antic.Dig:
                case Antic.Jive:
                    return habit.Weight * (0.25 + 1.2 * energy);
                case Antic.Wave:
                case Antic.Point:
                case Antic.Look:
                case Antic.Peekaboo:
                case Antic.Kiss:
                case Antic.Hello:
                case Antic.Waggle:
                    return habit.Weight * (0.5 + curiosity);
                default:
                    return habit.Weight;
            }
        }

        // What each one does on reaching the cursor.
        Antic InspectAntic()
        {
            return personality.CursorAntic;
        }

        bool CursorOnThisGround()
        {
            return Math.Abs(world.GroundAt(cursorX) - feetY) <= 2;
        }

        // ---- Errands -----------------------------------------------------------------

        void SetGoal(Goal next, Walker target)
        {
            goal = next;
            goalTarget = target;
            goalTime = 0;
            if (next == Goal.Inspect)
            {
                Rectangle bounds = world.MonitorBoundsAt(new Point(cursorX, cursorY));
                goalX = Math.Max(bounds.Left + 12 * scale, Math.Min(bounds.Right - 12 * scale, cursorX));
            }
        }

        /// <summary>Walking speed while on an errand: creeping up, or hurrying over.</summary>
        double GoalPace()
        {
            switch (goal)
            {
                case Goal.Prank: return 0.65;
                case Goal.Visit: return 1.15;
                default: return 1;
            }
        }

        // Each frame of walking while on an errand: give up if it has gone
        // stale, turn round if it is behind, stop on arrival.
        void PursueGoal(double dt)
        {
            goalTime += dt;
            if (!GoalValid())
            {
                AbandonGoal();
                return;
            }
            double gx = GoalX();
            double d = gx - cx;
            bool close = goal != Goal.Inspect && Math.Abs(goalTarget.CentreX - cx) < 1.5 * BodyWidth;
            if (close || Math.Abs(d) <= vx * vx / (2 * Settings.Braking * sizeFactor) + 2)
            {
                Arrive();
                return;
            }
            if (d * facing < 0)
            {
                afterTurn = Mode.Walk;
                Brake(Mode.Turn);
            }
        }

        bool GoalValid()
        {
            if (goalTime > 12) return false;
            if (goal == Goal.Inspect) return CursorOnThisGround();
            Walker t = goalTarget;
            return t != null && t.IsVisible && !t.IsLeaving && (!t.IsGrounded || Math.Abs(t.FeetY - feetY) <= 2);
        }

        // Where to walk to: a little short of the other walker, or under the cursor.
        double GoalX()
        {
            if (goal == Goal.Inspect) return goalX;
            double tx = goalTarget.CentreX;
            int dir = tx >= cx ? 1 : -1;
            return tx - dir * 1.35 * BodyWidth;
        }

        // Nothing came of it: a foiled prankster stamps, a would-be visitor shrugs.
        void AbandonGoal()
        {
            Goal was = goal;
            goal = Goal.None;
            goalTarget = null;
            if (was == Goal.Prank) StartAntic(Antic.Stomp);
            else if (was == Goal.Visit) StartAntic(Antic.Shrug);
        }

        void Arrive()
        {
            Goal was = goal;
            goal = Goal.None;
            switch (was)
            {
                case Goal.Visit:
                {
                    Walker t = goalTarget;
                    goalTarget = null;
                    social = 0.1;
                    if (t.IsUpset && t.CanGreet) BeginChat(t, ChatStyle.Pat);
                    else if (t.CanGreet) BeginChat(t, ChatStyle.Chat);
                    else StartAntic(Antic.Shrug);   // busy, dozing, mid-twirl...
                    break;
                }
                case Goal.Prank:
                    // The target is kept for the shout (or the poke) itself.
                    antic = bean ? Antic.Poke : Antic.Boo;
                    anticSeconds = bean ? 2.8 : 1.6;
                    anticStage = 0;
                    StopFacing(goalTarget.CentreX, Mode.Antic);
                    break;
                case Goal.Inspect:
                    curiosity = 0;
                    StartAntic(InspectAntic());
                    break;
            }
        }

        // The face while walking on an errand.
        void GoalFace(ref Expression target, ref double gx, ref double gy, ref bool atCursor)
        {
            switch (goal)
            {
                case Goal.Prank:
                    target = Expression.Mischievous;
                    if (goalTarget != null) LookAt(goalTarget.CentreX, goalTarget.EyeY, out gx, out gy);
                    break;
                case Goal.Visit:
                    target = personality.Warm ? Expression.Happy : Expression.Curious;
                    if (goalTarget != null) LookAt(goalTarget.CentreX, goalTarget.EyeY, out gx, out gy);
                    break;
                default:
                    target = Expression.Curious;
                    atCursor = true;
                    break;
            }
        }

        // ---- Chats, arguments, comforting ---------------------------------------------

        /// <summary>Called by another walker that has come over: stop and face it.</summary>
        public bool JoinChat(Walker partner, ChatStyle style, int facingDir, double seconds)
        {
            if (!CanGreet) return false;
            chatPartner = partner;
            chatStyle = style;
            chatLead = false;
            chatFacing = facingDir;
            chatSeconds = seconds;
            lastStomp = -1;
            goal = Goal.None;
            goalTarget = null;
            social = 0.1;
            Stop(Mode.Chat);
            return true;
        }

        /// <summary>Square up to another aggressive walker for a shouting match.</summary>
        public void Argue(Walker other)
        {
            if (!CanGreet) return;
            BeginChat(other, ChatStyle.Argue);
        }

        void BeginChat(Walker partner, ChatStyle style)
        {
            int dir = partner.CentreX >= cx ? 1 : -1;
            double seconds = style == ChatStyle.Chat ? Between(2.8, 4.6) : style == ChatStyle.Argue ? 3.2 : 2.2;
            if (!partner.JoinChat(this, style, -dir, seconds))
            {
                StartAntic(Antic.Shrug);
                return;
            }
            chatPartner = partner;
            chatStyle = style;
            chatLead = true;
            chatFacing = dir;
            chatSeconds = seconds;
            lastStomp = -1;
            GreetCooldown = 8;
            partner.GreetCooldown = 8;
            if (style == ChatStyle.Argue)
            {
                ChargeCooldown = 8;
                partner.ChargeCooldown = 8;
            }
            Stop(Mode.Chat);
        }

        /// <summary>Whose turn it is: they swap every 0.9 s, the one who came over going first.</summary>
        bool Talking
        {
            get { return (((int)(modeTime / 0.9)) % 2 == 0) == chatLead; }
        }

        void StepChat()
        {
            // The one doing the shouting stamps a foot every so often.
            if (chatStyle == ChatStyle.Argue && Talking)
            {
                int n = (int)(modeTime / 0.65);
                if (n != lastStomp)
                {
                    lastStomp = n;
                    Slam(0.45);
                }
            }
        }

        void EndChat()
        {
            Walker partner = chatPartner;
            chatPartner = null;
            double px = partner != null ? partner.CentreX : cx + facing;
            switch (chatStyle)
            {
                case ChatStyle.Chat:
                    if (personality.Warm)
                    {
                        ReactWith(Reaction.Chuckle, px, 1.3, false);
                    }
                    else
                    {
                        SetMood(Expression.Happy, 1.5);
                        Enter(Mode.Walk);
                    }
                    break;
                case ChatStyle.Argue:
                    ChargeCooldown = 6;
                    ReactWith(Reaction.Huff, px, 1.1, true);
                    break;
                default:
                    // Comforted (or having comforted): cheered up.
                    SetMood(Expression.Happy, 2.5);
                    Enter(Mode.Walk);
                    break;
            }
        }

        void ChatFace(ref Expression target, ref double rate, ref double gx, ref double gy)
        {
            double t = modeTime;
            bool friendly = personality.Warm;
            if (chatPartner != null) LookAt(chatPartner.CentreX, chatPartner.EyeY, out gx, out gy);
            else
            {
                gx = 0.6;
                gy = 0;
            }
            switch (chatStyle)
            {
                case ChatStyle.Chat:
                    if (t > chatSeconds - 0.5) target = friendly ? Expression.Laugh : Expression.Happy;
                    else if (Talking) target = personality.BaseFace;
                    else target = friendly ? Expression.Happy : Expression.Curious;
                    break;
                case ChatStyle.Argue:
                    target = Talking ? Expression.Furious : Expression.Grumpy;
                    rate = 25;
                    break;
                default:
                    target = t < (chatLead ? 1.0 : 1.2) ? Expression.Sad : Expression.Happy;
                    rate = 10;
                    break;
            }
        }

        void ChatPose(ref Pose pose)
        {
            switch (chatStyle)
            {
                case ChatStyle.Chat:
                    if (Talking)
                    {
                        // Gesturing with one hand.
                        pose.ArmNear = Q2(45 + 25 * Math.Sin(time * 5.5));
                        pose.ForeNear = 40;
                        Breathe(ref pose);
                    }
                    else
                    {
                        // Nodding along.
                        pose.BodyDy = Quantise(0.3 * (1 - Math.Cos(2 * Math.PI * modeTime / 0.6)) / 2);
                    }
                    break;

                case ChatStyle.Argue:
                    if (Talking)
                    {
                        // Both arms flailing, leaning in.
                        double s = Math.Sin(time * 9);
                        pose.ArmNear = Q2(90 + 40 * s);
                        pose.ArmFar = -Q2(90 - 40 * s);
                        pose.ForeNear = 20;
                        pose.ForeFar = -20;
                        pose.Lean = 0.4;
                    }
                    else
                    {
                        HandsOnHips(ref pose);
                        pose.Lean = -0.2;
                    }
                    break;

                default:
                    if (chatLead)
                    {
                        // A hand reaching for the other's shoulder.
                        pose.ArmNear = chatPartner != null ? AimAt(chatPartner.CentreX, chatPartner.ShoulderY) : 90;
                        Breathe(ref pose);
                    }
                    else
                    {
                        // Slumped, being consoled.
                        pose.BodyDy = Quantise(0.4 + 0.15 * (1 + Math.Sin(time * 2.6)) / 2);
                        pose.Lean = 0.15;
                    }
                    break;
            }
        }

        double ChatYaw()
        {
            switch (chatStyle)
            {
                case ChatStyle.Argue: return 25;
                case ChatStyle.Pat: return 40;
                default: return 35;
            }
        }

        // ---- Pranks ------------------------------------------------------------------

        /// <summary>Somebody at fromX just shouted: jump, and turn to face them.</summary>
        public void Spook(double fromX)
        {
            if (!IsGrounded || mode == Mode.Charge || mode == Mode.Flee || mode == Mode.Chat) return;
            goal = Goal.None;
            goalTarget = null;
            hasPending = false;
            facing = fromX >= cx ? 1 : -1;
            wokeUp = true;
            spooked = true;
            vx = 0;
            vy = -0.6 * Settings.StartleSpeed * sizeFactor;
            EmitDust(cx, feetY, 3, 0.6);
            Enter(Mode.Air);
        }

        // ---- Tag ---------------------------------------------------------------------

        void StartChase(Walker target)
        {
            if (mode != Mode.Walk || braking) return;
            chargeTarget = target;
            playful = true;
            ChargeCooldown = 8;
            Enter(Mode.Charge);
        }

        /// <summary>Somebody is coming to tag you: run, laughing.</summary>
        public void Scamper(int direction)
        {
            if (!CanGreet) return;
            playful = true;
            facing = direction;
            fleeTime = ScamperSeconds;
            goal = Goal.None;
            goalTarget = null;
            Enter(Mode.Flee);
        }

        /// <summary>Caught them: "you're it!"</summary>
        public void Tag(double x)
        {
            playful = false;
            chargeTarget = null;
            ChargeCooldown = 8;
            vx = 0;
            ReactWith(Reaction.Jeer, x, 1.6, false);
        }

        /// <summary>Caught: laugh it off.</summary>
        public void Tagged(double x)
        {
            if (!IsGrounded) return;
            playful = false;
            vx = 0;
            ReactWith(Reaction.Chuckle, x, 1.6, false);
        }

        // ---- Things that are catching ----------------------------------------------------

        /// <summary>An antic that has just begun and that others nearby might catch; null if none.</summary>
        public Antic? TakeBroadcast()
        {
            Antic? b = broadcast;
            broadcast = null;
            return b;
        }

        /// <summary>
        /// Saw somebody start to yawn or dance: maybe do the same, a moment
        /// later. The mimic copies most anything it sees. Mr. Bean showing
        /// off Teddy draws a wave, a look or a laugh, depending on who sees.
        /// </summary>
        public void Catch(Antic what)
        {
            if (catchCooldown > 0 || hasPending) return;
            double chance;
            if (what == Antic.Yawn || what == Antic.Doze)
            {
                chance = personality.Temperament == Temperament.Aggressive ? 0.3 : 0.7;
                what = Antic.Yawn;
            }
            else if (what == Antic.Dance)
            {
                switch (personality.Temperament)
                {
                    case Temperament.Friendly: case Temperament.Goofy: chance = 0.85; break;
                    case Temperament.Eccentric: chance = 0.8; what = Antic.Jive; break;   // his own dance
                    case Temperament.Calm: chance = 0.45; break;
                    case Temperament.Timid: chance = 0.25; break;
                    default: chance = 0; break;
                }
            }
            else if (what == Antic.Teddy)
            {
                switch (personality.Temperament)
                {
                    case Temperament.Friendly: case Temperament.Goofy: chance = 0.7; what = Antic.Wave; break;
                    case Temperament.Aggressive: chance = 0.5; what = Antic.Laugh; break;
                    case Temperament.Calm: case Temperament.Timid: chance = 0.5; what = Antic.Look; break;
                    default: return;
                }
            }
            else if (personality.Mimic && what != Antic.Toot)
            {
                chance = 0.65;
            }
            else
            {
                return;
            }
            if (rng.NextDouble() >= chance) return;
            hasPending = true;
            pendingAntic = what;
            pendingIn = what == Antic.Yawn ? Between(0.8, 1.6) : what == Antic.Dance ? Between(0.3, 0.7) : Between(0.5, 1.1);
            catchCooldown = what == Antic.Yawn || what == Antic.Dance ? 12 : 9;
        }

        void StepPending(double dt)
        {
            if (!hasPending) return;
            pendingIn -= dt;
            if (pendingIn > 0) return;
            hasPending = false;
            if (CanGreet) StartAntic(pendingAntic);
        }

        // ---- Reactions -------------------------------------------------------------------

        /// <summary>A short reaction aimed at x (or, for a huff, away from it).</summary>
        void ReactWith(Reaction what, double x, double seconds, bool away)
        {
            reaction = what;
            reactX = x;
            reactSeconds = seconds;
            reactFacing = (x >= cx ? 1 : -1) * (away ? -1 : 1);
            Enter(Mode.React);
        }
    }
}
