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
                        case Antic.Drive: case Antic.Photo:
                        case Antic.Peck: case Antic.Fly: case Antic.Launch: case Antic.Rage:
                        case Antic.Inflate: case Antic.Blast:
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
            // Sleep restores better in the small hours; everything else restores
            // a little worse - the body keeps time even underground.
            bool deepRest = mode == Mode.Antic && (antic == Antic.Doze || antic == Antic.Sleepwalk);
            double night = NightTiredness();
            double rest = deepRest ? gain * (1 + 0.3 * night) : gain * (1 - 0.25 * night);
            energy = Math.Max(0, Math.Min(1, energy + (rest - drain / personality.Stamina) * dt));
            social = Math.Min(1, social + 0.015 * personality.Sociable * dt);
            curiosity = Math.Min(1, curiosity + 0.012 * personality.Curious * dt);
            StepEmotion(dt);
        }

        /// <summary>How much go it has left, 0..1.</summary>
        public double Energy { get { return energy; } }

        // An idle moment: survival first (the timid one), then play (tag),
        // then a choice of its own - each habit, or setting off to visit,
        // prank or inspect - picked by learned taste with an occasional
        // experiment. The previous choice is judged first, by how its
        // drives moved since and what happened meanwhile (see WalkerMind).
        int FindHabit(Antic antic)
        {
            Habit[] all = personality.Habits;
            for (int i = 0; i < all.Length; i++)
                if (all[i].Antic == antic) return i;
            return -1;
        }

        void Decide()
        {
            Walker other = company;
            bool same = other != null && other.IsVisible && other.IsGrounded && !other.IsLeaving &&
                        Math.Abs(other.FeetY - feetY) <= 2;
            double d = same ? Math.Abs(other.CentreX - cx) : double.MaxValue;
            Temperament tm = personality.Temperament;

            // Always judge the last choice first: even a reflex interrupts
            // life, it should not erase the lesson from what came before.
            int state = MindState();
            EnsureTable();
            SettlePrevious();

            // The timid one keeps well away from the bullies - and the birds,
            // who are just as scary with a beak: hides if one is close,
            // otherwise turns and walks the other way, glancing back.
            // Hiding is remembered so safety learns its value; a bare turn
            // is a reflex and claims no credit.
            if (same && tm == Temperament.Timid &&
                (other.Temperament == Temperament.Aggressive || other.Temperament == Temperament.Feisty) &&
                d < 380 * sizeFactor)
            {
                if (d < 160 * sizeFactor || rng.NextDouble() < 0.35)
                {
                    int hide = FindHabit(Antic.Hide);
                    if (hide >= 0) RememberChoice(state, hide);
                    StartAntic(Antic.Hide);
                }
                else if ((other.CentreX - cx) * facing > 0)
                {
                    afterTurn = Mode.Walk;
                    Brake(Mode.Turn);
                }
                else
                {
                    int look = FindHabit(Antic.Look);
                    if (look >= 0) RememberChoice(state, look);
                    StartAntic(Antic.Look);
                }
                return;
            }

            // Anyone gives a wide berth to someone who hurt them very recently -
            // grudges walk, not just the timid. Bullies never back down.
            if (same && tm != Temperament.Aggressive &&
                RecentFeeling(other, EvHurt, 90) > 0.5 && d < 220 * sizeFactor)
            {
                if ((other.CentreX - cx) * facing > 0)
                {
                    afterTurn = Mode.Walk;
                    Brake(Mode.Turn);
                }
                else
                {
                    int look = FindHabit(Antic.Look);
                    if (look >= 0) RememberChoice(state, look);
                    StartAntic(Antic.Look);
                }
                return;
            }

            // Two friendly ones with energy to burn play tag. Remembered as
            // a social outing so the fun that follows teaches its value.
            if (same && personality.Warm && other.Personality.Warm && other.IsWalking &&
                !other.HasGoal && d < 500 * sizeFactor && energy > 0.55 && rng.NextDouble() < 0.4)
            {
                RememberChoice(state, VisitChoice);
                StartChase(other);
                return;
            }

            // Choose: every habit is always an option; the errands join in
            // only when they make sense (nearby company, a quiet victim,
            // a cursor worth a look). The last choice was already judged above.

            double visitReach = 900 * sizeFactor;
            if (same && other.IsUpset && personality.Warm) visitReach = 1200 * sizeFactor;   // hurry to comfort
            if (same && other == BestFond()) visitReach = Math.Max(visitReach, 1200 * sizeFactor);   // best friends cross the desktop
            bool visitValid = same && tm != Temperament.Aggressive &&
                              d > 1.6 * BodyWidth && d < visitReach;
            bool prankValid = same && (tm == Temperament.Aggressive || tm == Temperament.Eccentric ||
                              tm == Temperament.Feisty || tm == Temperament.Suave) && other.IsQuiet &&
                              d > 1.6 * BodyWidth && d < 450 * sizeFactor && energy > 0.4;
            bool inspectValid = curiosity > 0.6 && CursorOnThisGround();
            bool inspectNear = inspectValid && Math.Abs(cursorX - cx) < 60 * sizeFactor;

            Habit[] habits = personality.Habits;
            int[] acts = new int[habits.Length + 3];
            int n = 0;
            for (int i = 0; i < habits.Length; i++) acts[n++] = i;
            if (visitValid) acts[n++] = VisitChoice;
            if (prankValid) acts[n++] = PrankChoice;
            if (inspectValid) acts[n++] = InspectChoice;
            if (n == 0) return;

            int choice;
            if (rng.NextDouble() < Explore())
            {
                // Upgrade v3: motivated experiment, not dice - try what the
                // personality fancies + what is untried, minus what just repeated.
                // Still an experiment (not the best value), but never looks random.
                double total = 0;
                double[] w = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double pw = PriorFor(acts[i]) + NoveltyBonus(acts[i]) - HabituationPenalty(acts[i]) + 0.05;
                    if (pw < 0.05) pw = 0.05;
                    // Scared: scary show-offs are not even tried.
                    if (fear > 0.4 && acts[i] < habits.Length)
                    {
                        Antic a = habits[acts[i]].Antic;
                        if (a == Antic.Dance || a == Antic.Twirl || a == Antic.Strum || a == Antic.Jive ||
                            a == Antic.Flex || a == Antic.Box || a == Antic.Taunt || a == Antic.Launch ||
                            a == Antic.Blast || a == Antic.Rage)
                            pw *= 1 - 0.6 * fear;
                    }
                    w[i] = pw;
                    total += pw;
                }
                double r = rng.NextDouble() * total;
                choice = acts[n - 1];
                for (int i = 0; i < n; i++)
                {
                    r -= w[i];
                    if (r <= 0) { choice = acts[i]; break; }
                }
            }
            else
            {
                choice = acts[0];
                double best = OptionValue(state, choice, other);
                for (int i = 1; i < n; i++)
                {
                    double v = OptionValue(state, acts[i], other);
                    if (v > best)
                    {
                        best = v;
                        choice = acts[i];
                    }
                }
            }
            RememberChoice(state, choice);

            if (choice < habits.Length)
            {
                StartAntic(habits[choice].Antic);
            }
            else if (choice == VisitChoice)
            {
                wantAtVisit = social;
                SetGoal(Goal.Visit, other);
            }
            else if (choice == PrankChoice)
            {
                SetGoal(Goal.Prank, other);
            }
            else if (inspectNear)
            {
                curiosity = 0;
                StartAntic(InspectAntic());
            }
            else
            {
                SetGoal(Goal.Inspect, null);
            }
        }

        /// <summary>Learned value plus inborn taste, current drives, mood and who it would deal with.</summary>
        double OptionValue(int state, int choice, Walker other)
        {
            bool eccentric = personality.Temperament == Temperament.Eccentric;
            // Upgrade v3: continuous generalizer. Same feat for all choices this Decide,
            // small at birth so newborns act from personality, elders from lived nuance.
            double[] featNow = new double[12];
            FillFeatures(featNow);
            double mlp = MlpFor(featNow, choice);
            double v;
            if (choice < HabitCount)
            {
                // Utility AI: the body's current needs scale the inborn taste.
                // A tired one means rest; a curious one means look and point.
                Habit h = personality.Habits[choice];
                double drive = h.Weight > 0 ? HabitWeight(h) / h.Weight : 1;
                v = values[state, choice] + mlp + PriorFor(choice) * drive;
                if (eccentric && h.Antic == Antic.Teddy)
                {
                    v += social * 0.4;   // lonely: cling to the only friend
                }
                // Mood colours the habits: cheer invites show-offs, gloom invites rest,
                // fear invites hiding and kills showing off.
                if (cheer > 0)
                {
                    switch (h.Antic)
                    {
                        case Antic.Dance: case Antic.Joy: case Antic.Strum: case Antic.Jive:
                        case Antic.Wave: case Antic.Laugh: case Antic.Twirl: case Antic.Fly:
                            v += cheer * 0.20;
                            break;
                        case Antic.Rest: case Antic.Doze: case Antic.Hide: case Antic.Fret:
                            v -= cheer * 0.10;
                            break;
                    }
                }
                else if (cheer < 0)
                {
                    switch (h.Antic)
                    {
                        case Antic.Rest: case Antic.Doze: case Antic.Teddy: case Antic.Daydream:
                            v += -cheer * 0.12;
                            break;
                        case Antic.Dance: case Antic.Joy: case Antic.Jive: case Antic.Strum:
                            v += cheer * 0.15;
                            break;
                    }
                }
                if (fear > 0)
                {
                    switch (h.Antic)
                    {
                        case Antic.Hide: case Antic.Fret: case Antic.Rest: case Antic.Look:
                            v += fear * (tmIsTimid() ? 0.6 : 0.25);
                            break;
                        case Antic.Dance: case Antic.Twirl: case Antic.Strum: case Antic.Jive:
                        case Antic.Flex: case Antic.Box: case Antic.Taunt: case Antic.Launch:
                        case Antic.Blast: case Antic.Rage:
                            v -= fear * 0.4;
                            break;
                    }
                }
                // Curiosity for the rarely-tried, boredom with the repeated (habituation).
                v += NoveltyBonus(choice) - HabituationPenalty(choice);
            }
            else
            {
                v = values[state, choice] + mlp + PriorFor(choice);
                if (choice == VisitChoice && other != null)
                {
                    v += FondOf(other) * 0.5;
                    if (other.IsUpset && personality.Warm) v += 0.3;
                    v += RecentFeeling(other, EvKind, 45) * 0.5;   // fresh warmth pulls harder
                    v -= RecentFeeling(other, EvHurt, 90) * 0.8;   // fresh hurt repels
                    if (other == BestFond()) v += 0.4;             // best friends first
                    else if (other == Rival()) v -= 0.3;           // enemies are avoided
                    if (other.IsUpset && other == BestFond()) v += 0.3;   // a best friend's tears hurry
                    // Scared of who it would visit: no errand into dread.
                    if (fear > 0.3 && (other.Temperament == Temperament.Aggressive ||
                                       other.Temperament == Temperament.Feisty))
                        v -= fear * 0.8;
                    else if (cheer < -0.3 && personality.Warm)
                        v += 0.15;   // miserable and friendly: seek comfort
                    v += NoveltyBonus(choice) - HabituationPenalty(choice);
                }
                else if (choice == PrankChoice && other != null)
                {
                    // Bullies pick on the disliked; Bean pays back his tormentors.
                    v += personality.Temperament == Temperament.Aggressive ? -FondOf(other) * 0.3 :
                         eccentric ? (FondOf(other) < -0.15 ? 0.35 : FondOf(other) * 0.05) :
                         FondOf(other) * 0.1;
                    if (personality.Temperament == Temperament.Aggressive && other == Rival())
                        v += 0.4;   // a vendetta: the old enemy gets it
                    if (fear > 0.3) v -= fear * 0.5;
                    if (cheer > 0.4) v += cheer * 0.15;   // cheeky when happy
                    v += NoveltyBonus(choice) - HabituationPenalty(choice);
                }
                else if (choice == InspectChoice)
                {
                    if (fear > 0.4) v -= fear * 0.4;   // dread kills curiosity
                    v += NoveltyBonus(choice) - HabituationPenalty(choice);
                }
            }
            return v;
        }

        bool tmIsTimid()
        {
            return personality.Temperament == Temperament.Timid;
        }

        // Tired walkers rest more and caper less; a curious one looks and points more.
        // And the small hours weigh on everyone: rest calls louder at night,
        // showing off quieter - the cast keeps a circadian rhythm.
        double HabitWeight(Habit habit)
        {
            double night = NightTiredness();
            switch (habit.Antic)
            {
                case Antic.Rest:
                case Antic.Yawn:
                case Antic.Doze:
                case Antic.Sleepwalk:
                    return habit.Weight * (0.4 + 2.5 * (1 - energy)) * (1 + 0.8 * night);
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
                case Antic.Drive:
                case Antic.Fly:
                case Antic.Launch:
                case Antic.Inflate:
                case Antic.Blast:
                    return habit.Weight * (0.25 + 1.2 * energy) * (1 - 0.4 * night);
                case Antic.Wave:
                case Antic.Point:
                case Antic.Look:
                case Antic.Peekaboo:
                case Antic.Kiss:
                case Antic.Hello:
                case Antic.Waggle:
                case Antic.Photo:
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
            if (was == Goal.Prank) pendingBonus -= 0.15;
            else if (was == Goal.Visit)
            {
                // Stood up: stings, and worse the lonelier the walk over was.
                pendingBonus -= 0.1 + (personality.Temperament == Temperament.Eccentric ? 0.3 * (0.5 + social) : 0);
                if (pendingBonus < -MaxReward) pendingBonus = -MaxReward;
            }
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
                    // The target is kept for the shout (or the poke, or the peck) itself.
                    antic = bird ? Antic.Peck : (bean || panther) ? Antic.Poke : Antic.Boo;
                    anticSeconds = bird ? 2.2 : (bean || panther) ? 2.8 : 1.6;
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
                    target = bird ? Expression.Determined : Expression.Mischievous;
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
            NoteChatJoined(partner, style);
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
            NoteChatBegun(partner, style);
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
        /// off Teddy draws a wave, a look or a laugh, depending on who sees;
        /// Matilda laying an egg draws the same crowd, and one bird's rage
        /// sets the other birds off.
        /// </summary>
        public void Catch(Antic what)
        {
            if (catchCooldown > 0 || hasPending) return;
            double chance;
            if (what == Antic.Yawn || what == Antic.Doze)
            {
                chance = personality.Temperament == Temperament.Aggressive ? 0.3 :
                         personality.Temperament == Temperament.Feisty ? 0.5 : 0.7;
                what = Antic.Yawn;
            }
            else if (what == Antic.Dance)
            {
                switch (personality.Temperament)
                {
                    case Temperament.Friendly: case Temperament.Goofy: chance = 0.85; break;
                    case Temperament.Eccentric: chance = 0.8; what = Antic.Jive; break;   // his own dance
                    case Temperament.Feisty: chance = 0.6; break;
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
                    case Temperament.Feisty: chance = 0.5; what = Antic.Look; break;
                    case Temperament.Calm: case Temperament.Timid: chance = 0.5; what = Antic.Look; break;
                    default: return;
                }
            }
            else if (what == Antic.Egg)
            {
                // Just like Teddy: everyone comes to look at the egg.
                switch (personality.Temperament)
                {
                    case Temperament.Friendly: case Temperament.Goofy: chance = 0.6; what = Antic.Wave; break;
                    case Temperament.Aggressive: chance = 0.4; what = Antic.Laugh; break;
                    case Temperament.Feisty: chance = 0.5; what = Antic.Look; break;
                    case Temperament.Calm: case Temperament.Timid: chance = 0.5; what = Antic.Look; break;
                    default: return;
                }
            }
            else if (what == Antic.Rage)
            {
                // Anger is catching, but only among birds.
                if (personality.Temperament != Temperament.Feisty) return;
                chance = 0.6;
            }
            else if (personality.Mimic && what != Antic.Toot && what != Antic.Drive && what != Antic.Photo &&
                     what != Antic.Peck && what != Antic.Fly && what != Antic.Launch && what != Antic.Rage &&
                     what != Antic.Egg && what != Antic.Inflate && what != Antic.Blast)
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
