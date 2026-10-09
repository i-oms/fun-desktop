using System;

namespace IdleWalker.Domain
{
    /// <summary>
    /// The idle antics and the reactions: how each is started, what happens
    /// over its course, the face it pulls, the way the body turns and how
    /// the limbs are arranged. Which antics a walker goes in for is down to
    /// its <see cref="Personality"/> and its mood (WalkerBrain.cs).
    /// </summary>
    partial class Walker
    {
        const double DanceBeat = 0.32;   // seconds

        // ---- Starting, stepping, ending -----------------------------------------------

        void StartAntic(Antic choice)
        {
            antic = choice;
            anticStage = 0;
            // Having a look at the cursor takes the edge off the curiosity.
            if (choice == Antic.Look || choice == Antic.Wave || choice == Antic.Point) curiosity *= 0.4;
            switch (choice)
            {
                case Antic.Look:
                    Stop(Mode.Look);
                    return;

                case Antic.Rest:
                    restDuration = Between(0.8, 2.0);
                    Stop(Mode.Rest);
                    return;

                case Antic.Wave:
                    // Wave at a nearby walker if there is one, otherwise at the cursor.
                    waveAtCursor = !companyNearby;
                    StopFacing(companyNearby ? companyX : cursorX, Mode.Wave);
                    return;

                // These three are aimed at you, so they turn round first if need be.
                case Antic.Point:
                    anticSeconds = 2.0;
                    StopFacing(cursorX, Mode.Antic);
                    return;
                case Antic.Taunt:
                    anticSeconds = 2.0;
                    StopFacing(cursorX, Mode.Antic);
                    return;
                case Antic.Laugh:
                    anticSeconds = 1.6;
                    StopFacing(cursorX, Mode.Antic);
                    return;
                case Antic.Salute:
                    anticSeconds = 2.0;
                    broadcast = Antic.Salute;
                    StopFacing(cursorX, Mode.Antic);
                    return;
                case Antic.Peekaboo:
                    anticSeconds = 2.2;
                    StopFacing(cursorX, Mode.Antic);
                    return;
                case Antic.Kiss:
                    anticSeconds = 2.2;
                    StopFacing(cursorX, Mode.Antic);
                    return;

                // Mr. Bean's: the hello and the eyebrows are for you; the poke
                // is for whoever he has crept up on, or the cursor.
                case Antic.Hello:
                    anticSeconds = 2.2;
                    StopFacing(cursorX, Mode.Antic);
                    return;
                case Antic.Waggle:
                    anticSeconds = 2.4;
                    StopFacing(cursorX, Mode.Antic);
                    return;
                case Antic.Poke:
                    anticSeconds = 2.8;
                    StopFacing(goalTarget != null ? goalTarget.CentreX : cursorX, Mode.Antic);
                    return;
                case Antic.Teddy: anticSeconds = 3.6; broadcast = Antic.Teddy; break;   // the Minions come to look
                case Antic.Sneak: anticSeconds = 3.4; break;
                case Antic.Fuss: anticSeconds = 2.8; break;
                case Antic.Ponder: anticSeconds = 3.0; break;
                case Antic.Jive: anticSeconds = 3.2; broadcast = Antic.Dance; break;

                // The busker's playing is catching: whoever is near wants to dance.
                case Antic.Strum: anticSeconds = 3.4; broadcast = Antic.Dance; break;
                case Antic.Pushups: anticSeconds = 3.2; broadcast = Antic.Pushups; break;
                case Antic.Jog: anticSeconds = 2.8; broadcast = Antic.Jog; break;
                case Antic.Moonwalk: anticSeconds = 2.4; broadcast = Antic.Moonwalk; break;
                case Antic.Toot: anticSeconds = 2.2; broadcast = Antic.Toot; break;
                case Antic.Flap: anticSeconds = 2.4; broadcast = Antic.Flap; break;
                case Antic.Hide: anticSeconds = 2.2; break;
                case Antic.Fret: anticSeconds = 2.4; break;
                case Antic.Faint: anticSeconds = 3.0; break;
                case Antic.Dig: anticSeconds = 3.0; break;
                case Antic.Sleepwalk: anticSeconds = Between(3, 5); broadcast = Antic.Yawn; break;
                case Antic.Daydream: anticSeconds = 2.8; break;

                case Antic.Dance: anticSeconds = 2.6; broadcast = Antic.Dance; break;
                // (The ones that announce themselves can be copied by the mimic.)
                case Antic.Twirl: anticSeconds = 1.7; broadcast = choice; break;
                case Antic.Yawn: anticSeconds = 2.1; broadcast = Antic.Yawn; break;
                case Antic.Doze: anticSeconds = 2.1 + Between(2.5, 4.5); broadcast = Antic.Yawn; break;
                case Antic.Scratch: anticSeconds = 2.0; broadcast = choice; break;
                case Antic.Whistle: anticSeconds = 2.7; broadcast = choice; break;
                case Antic.Sneeze: anticSeconds = 1.6; break;
                case Antic.Box: anticSeconds = 2.2; broadcast = choice; break;
                case Antic.Stomp: anticSeconds = 1.1; broadcast = choice; break;
                case Antic.Flex: anticSeconds = 2.4; broadcast = choice; break;
                case Antic.Joy: anticSeconds = 1; broadcast = choice; break;   // it jumps on the first step
                case Antic.Hiccups: anticSeconds = 2.6; break;
                case Antic.Shrug: anticSeconds = 1.3; broadcast = choice; break;
                case Antic.Trip: anticSeconds = 1.8; break;
                case Antic.Boo: anticSeconds = 1.6; break;
            }
            Stop(Mode.Antic);
        }

        // Stops, turning round first if the target is behind.
        void StopFacing(double targetX, Mode then)
        {
            bool behind = (targetX - cx) * facing < -BodyWidth;
            if (behind && mode == Mode.Walk)
            {
                afterTurn = then;
                Brake(Mode.Turn);
            }
            else if (behind)
            {
                afterTurn = then;
                Enter(Mode.Turn);
            }
            else
            {
                Stop(then);
            }
        }

        // A trip happens mid-stride, without stopping first.
        void StartTrip()
        {
            antic = Antic.Trip;
            anticSeconds = 1.8;
            anticStage = 0;
            Enter(Mode.Antic);
        }

        // The one-off effects along the way: a stomp lands, a sneeze sprays,
        // a shout makes somebody jump.
        void StepAntic(double dt)
        {
            double t = modeTime;
            switch (antic)
            {
                case Antic.Joy:
                    if (anticStage == 0)
                    {
                        anticStage = 1;
                        celebrating = true;
                        joyHops = 1;
                        StartJump(0.85 * Settings.HopSpeed * sizeFactor, 0);
                    }
                    break;

                case Antic.Stomp:
                    if (anticStage == 0 && t >= 0.25)
                    {
                        anticStage = 1;
                        Slam(0.6);
                    }
                    else if (anticStage == 1 && t >= 0.65)
                    {
                        anticStage = 2;
                        Slam(0.8);
                    }
                    break;

                case Antic.Sneeze:
                    if (anticStage == 0 && t >= 0.75)
                    {
                        anticStage = 1;
                        EmitDust(cx + facing * 7 * scale, feetY - body.MouthV * scale, 3, 0.9);
                        StartSweat();
                    }
                    break;

                case Antic.Box:
                    if (anticStage == 0 && t >= 1.5)
                    {
                        anticStage = 1;
                        StartSweat();
                    }
                    break;

                case Antic.Twirl:
                    if (anticStage == 0 && t >= 0.35)
                    {
                        anticStage = 1;
                        EmitDust(cx, feetY, 3, 0.5);
                    }
                    break;

                case Antic.Trip:
                    // Still travelling as it goes over, then skidding to a halt.
                    vx = Approach(vx, 0, Settings.Braking * 1.5 * sizeFactor * dt);
                    MoveX(vx * dt);
                    walkPhase += Math.Abs(vx) * dt / (body.StrideCells * scale);
                    if (anticStage == 0 && t >= 0.12)
                    {
                        anticStage = 1;
                        Slam(0.9);
                        vx *= 0.3;
                    }
                    break;

                case Antic.Boo:
                    if (anticStage == 0)
                    {
                        anticStage = 1;
                        if (goalTarget != null) goalTarget.Spook(cx);
                    }
                    break;

                case Antic.Pushups:
                    if (anticStage == 0 && t >= 2.0)
                    {
                        anticStage = 1;
                        StartSweat();
                    }
                    break;

                case Antic.Moonwalk:
                    // Sliding backwards; the feet step forwards as if walking.
                    if (t > 0.3)
                    {
                        double slide = 0.5 * Settings.WalkSpeed * sizeFactor * dt;
                        if (!MoveX(-facing * slide)) modeTime = anticSeconds;   // ran out of floor
                        walkPhase += slide / (body.StrideCells * scale);
                    }
                    break;

                case Antic.Sleepwalk:
                    // Shuffling forward with its eyes shut until it bumps into something.
                    if (t > 1.0)
                    {
                        double shuffle = 0.35 * Settings.WalkSpeed * sizeFactor * dt;
                        if (!MoveX(facing * shuffle)) modeTime = anticSeconds;
                        walkPhase += shuffle / (body.StrideCells * scale);
                    }
                    break;

                case Antic.Sneak:
                    // Creeping forward on tiptoe, small quick steps, until the floor runs out.
                    if (t > 0.9 && t < 2.6)
                    {
                        double creep = 0.3 * Settings.WalkSpeed * sizeFactor * dt;
                        if (!MoveX(facing * creep)) modeTime = Math.Max(modeTime, 2.6);
                        walkPhase += 1.5 * creep / (body.StrideCells * scale);
                    }
                    break;

                case Antic.Poke:
                    // The prod lands: whoever it was jumps.
                    if (anticStage == 0 && t >= 0.7)
                    {
                        anticStage = 1;
                        if (goalTarget != null) goalTarget.Spook(cx);
                    }
                    break;

                case Antic.Toot:
                    if (anticStage == 0 && t >= 0.55)
                    {
                        anticStage = 1;
                        squash = 0.25;
                        EmitDust(cx - facing * 5 * scale, feetY - body.SeatV * scale, 3, 0.5);
                    }
                    break;

                case Antic.Dig:
                {
                    // A puff of dirt flies up on each scoop.
                    int scoop = (int)(t / 0.5);
                    if (scoop > anticStage && scoop <= 5)
                    {
                        anticStage = scoop;
                        EmitDust(cx + facing * 5 * scale, feetY, 2, 0.7);
                    }
                    break;
                }

                case Antic.Faint:
                    if (anticStage == 0 && t >= 0.45)
                    {
                        anticStage = 1;
                        Slam(0.8);
                    }
                    break;
            }
        }

        void Slam(double strength)
        {
            squash = strength;
            EmitDust(cx, feetY, 4, strength);
        }

        void EndAntic()
        {
            switch (antic)
            {
                case Antic.Doze:
                    // Jerks awake with a little hop.
                    wokeUp = true;
                    vx = 0;
                    vy = -0.5 * Settings.StartleSpeed * sizeFactor;
                    EmitDust(cx, feetY, 2, 0.5);
                    Enter(Mode.Air);
                    return;

                case Antic.Box: SetMood(Expression.Panting, 1.3); break;
                case Antic.Twirl: SetMood(Expression.Dizzy, 0.9); break;
                case Antic.Sneeze: SetMood(Expression.Dizzy, 0.5); break;
                case Antic.Taunt: SetMood(Expression.Mischievous, 1.4); break;
                case Antic.Scratch: SetMood(Expression.Happy, 1.0); break;
                case Antic.Stomp: SetMood(Expression.Grumpy, 1.2); break;
                case Antic.Laugh: SetMood(Expression.Giggle, 0.8); break;
                case Antic.Trip: SetMood(Expression.Giggle, 1.0); break;   // embarrassed
                case Antic.Boo: SetMood(Expression.Mischievous, 1.5); goalTarget = null; break;

                case Antic.Sleepwalk:
                    // Bumped into something, or just came to: jerks awake.
                    wokeUp = true;
                    vx = 0;
                    vy = -0.5 * Settings.StartleSpeed * sizeFactor;
                    EmitDust(cx, feetY, 2, 0.5);
                    Enter(Mode.Air);
                    return;
                case Antic.Dig:
                    // Nothing there after all.
                    StartAntic(Antic.Shrug);
                    return;
                case Antic.Strum: SetMood(Expression.Cool, 1.5); break;
                case Antic.Pushups: SetMood(Expression.Proud, 1.5); break;
                case Antic.Jog: SetMood(Expression.Panting, 1.2); break;
                case Antic.Moonwalk: SetMood(Expression.Smug, 1.4); break;
                case Antic.Toot: SetMood(Expression.Giggle, 1.0); break;
                case Antic.Peekaboo: SetMood(Expression.Giggle, 1.0); break;
                case Antic.Flap: SetMood(Expression.Silly, 1.0); break;
                case Antic.Hide: SetMood(Expression.Worried, 1.5); break;
                case Antic.Fret: SetMood(Expression.Nervous, 1.5); break;
                case Antic.Faint: SetMood(Expression.Dizzy, 1.2); break;
                case Antic.Daydream: SetMood(Expression.Curious, 1.0); break;
                case Antic.Kiss: SetMood(Expression.Happy, 1.5); break;

                case Antic.Teddy: SetMood(Expression.Fond, 1.5); break;
                case Antic.Hello: SetMood(Expression.Grin, 1.0); break;
                case Antic.Waggle: SetMood(Expression.Knowing, 1.2); break;
                case Antic.Sneak: SetMood(Expression.Mischievous, 1.5); break;
                case Antic.Fuss: SetMood(Expression.Proud, 1.5); break;
                case Antic.Ponder: SetMood(Expression.Happy, 1.2); break;
                case Antic.Jive: SetMood(Expression.Grin, 1.2); break;
                case Antic.Poke: SetMood(Expression.Mischievous, 1.5); goalTarget = null; break;
            }
            Enter(Mode.Walk);
        }

        // ---- The face during an antic -----------------------------------------------------

        void AnticFace(ref Expression target, ref double rate, ref double gx, ref double gy, ref bool atCursor)
        {
            double t = modeTime;
            switch (antic)
            {
                case Antic.Dance:
                    target = (int)(t / DanceBeat) % 4 == 3 ? Expression.Winking : Expression.Grin;
                    rate = 18;
                    atCursor = true;
                    break;

                case Antic.Twirl:
                    target = t < 0.35 ? Expression.Focused : t < 1.05 ? Expression.Excited : Expression.Proud;
                    rate = 20;
                    if (t >= 1.05) atCursor = true;
                    else
                    {
                        gx = 0.6;
                        gy = 0;
                    }
                    break;

                case Antic.Point:
                    target = t < 0.9 ? Expression.Curious : Expression.Giggle;
                    atCursor = true;
                    break;

                case Antic.Yawn:
                    target = t < 1.45 ? Expression.Yawn : Expression.Sleepy;
                    rate = t < 1.45 ? 9 : 6;
                    gy = 0.3;
                    break;

                case Antic.Doze:
                    target = t < 1.45 ? Expression.Yawn : Expression.Asleep;
                    rate = 6;
                    gy = 0.3;
                    break;

                case Antic.Scratch:
                    // Puzzling it over, then: an idea!
                    target = t < 1.55 ? Expression.Thinking : Expression.Surprised;
                    rate = t < 1.55 ? 12 : 30;
                    gx = t < 1.55 ? 0.25 : 0;
                    gy = t < 1.55 ? 0.35 : 0.1;
                    break;

                case Antic.Whistle:
                    target = Expression.Whistling;
                    rate = 10;
                    gx = 0.3;
                    gy = 0.55;
                    break;

                case Antic.Sneeze:
                    target = t < 0.75 ? Expression.Inhale : t < 1.05 ? Expression.Sneeze : Expression.Dizzy;
                    rate = t < 0.75 ? 8 : 40;
                    gy = t < 0.75 ? 0.5 : -0.4;
                    break;

                case Antic.Box:
                    target = Expression.Determined;
                    gx = 0.8;
                    gy = 0;
                    break;

                case Antic.Taunt:
                    target = t < 1.3 ? Expression.Raspberry : Expression.Mischievous;
                    rate = 18;
                    atCursor = true;
                    break;

                case Antic.Stomp:
                    target = Expression.Furious;
                    rate = 30;
                    gx = 0.4;
                    gy = -0.3;
                    break;

                case Antic.Flex:
                    if (t < 1.4)
                    {
                        target = Expression.Proud;
                        atCursor = true;
                    }
                    else
                    {
                        // Admiring the near arm.
                        target = Expression.Winking;
                        gx = 0.9;
                        gy = 0.2;
                    }
                    break;

                case Antic.Joy:
                    target = Expression.Excited;
                    atCursor = true;
                    break;

                case Antic.Laugh:
                    target = Expression.Laugh;
                    rate = 20;
                    atCursor = true;
                    break;

                case Antic.Hiccups:
                    // Holding its breath between the jolts.
                    target = HiccupJolt(t) > 0 ? Expression.Surprised : Expression.Focused;
                    rate = HiccupJolt(t) > 0 ? 40 : 12;
                    atCursor = true;
                    break;

                case Antic.Shrug:
                    target = Expression.Curious;
                    atCursor = true;
                    break;

                case Antic.Trip:
                    target = t < 0.12 ? Expression.Shocked : t < 1.0 ? Expression.Dizzy : Expression.Giggle;
                    rate = t < 0.12 ? 40 : 14;
                    gx = 0.3;
                    gy = t < 1.0 ? -0.5 : 0;
                    break;

                case Antic.Boo:
                    target = t < 0.6 ? Expression.Excited : Expression.Laugh;
                    rate = 40;
                    if (goalTarget != null) LookAt(goalTarget.CentreX, goalTarget.EyeY, out gx, out gy);
                    else gx = 0.7;
                    break;

                case Antic.Strum:
                    // Grinning, then laughing, head down over the strings.
                    target = (int)(t / 0.8) % 2 == 0 ? Expression.Grin : Expression.Laugh;
                    rate = 18;
                    gx = 0.3;
                    gy = -0.3;
                    break;

                case Antic.Pushups:
                    target = Expression.Determined;
                    gx = 0.2;
                    gy = -0.4;
                    break;

                case Antic.Jog:
                    target = Expression.Focused;
                    gx = 0.5;
                    gy = 0;
                    break;

                case Antic.Salute:
                    target = t < 1.7 ? Expression.Serious : Expression.Winking;
                    rate = 20;
                    atCursor = true;
                    break;

                case Antic.Moonwalk:
                    target = Expression.Cool;
                    rate = 12;
                    atCursor = true;
                    break;

                case Antic.Toot:
                    // Concentrating, a little start, then the picture of innocence.
                    target = t < 0.55 ? Expression.Focused : t < 0.8 ? Expression.Surprised : Expression.Whistling;
                    rate = t < 0.8 ? 30 : 10;
                    gx = t < 0.8 ? 0.3 : Math.Sin(t * 3) > 0 ? 0.7 : -0.7;
                    gy = t < 0.8 ? -0.2 : 0.5;
                    break;

                case Antic.Peekaboo:
                    target = t < 1.1 ? Expression.Asleep : t < 1.5 ? Expression.Excited : Expression.Laugh;
                    rate = t < 1.1 ? 12 : 40;
                    atCursor = true;
                    break;

                case Antic.Flap:
                    target = Expression.Silly;
                    rate = 14;
                    gx = 0.6;
                    gy = 0;
                    break;

                case Antic.Hide:
                    target = Expression.Scared;
                    rate = 20;
                    if (t > 1.0 && t < 1.5) atCursor = true;
                    else
                    {
                        gx = 0;
                        gy = -0.2;
                    }
                    break;

                case Antic.Fret:
                    // Eyes darting from side to side.
                    target = Expression.Worried;
                    rate = 14;
                    gx = Math.Sin(t * 6) > 0 ? 0.7 : -0.7;
                    gy = 0.1;
                    break;

                case Antic.Faint:
                    target = t < 0.45 ? Expression.Dizzy : t < 2.2 ? Expression.Asleep : Expression.Dizzy;
                    rate = t < 0.45 ? 30 : 8;
                    gy = 0.3;
                    break;

                case Antic.Dig:
                    target = Expression.Focused;
                    gx = 0.4;
                    gy = -0.6;
                    break;

                case Antic.Sleepwalk:
                    target = Expression.Asleep;
                    rate = 6;
                    gy = -0.2;
                    break;

                case Antic.Daydream:
                    // Miles away, then a start.
                    target = t < 2.4 ? Expression.Dreamy : Expression.Surprised;
                    rate = t < 2.4 ? 8 : 40;
                    gx = 0.3;
                    gy = t < 2.4 ? 0.7 : 0;
                    break;

                case Antic.Kiss:
                    target = t < 0.9 ? Expression.Smooch : Expression.Winking;
                    rate = 16;
                    atCursor = true;
                    break;

                // ---- Mr. Bean's ----

                case Antic.Teddy:
                    if (t < 0.6)
                    {
                        // Rummaging in the jacket.
                        target = Expression.Gormless;
                        gx = 0.3;
                        gy = -0.4;
                    }
                    else if (t < 1.5 || t >= 2.4)
                    {
                        // Gazing fondly at Teddy.
                        target = Expression.Fond;
                        rate = 10;
                        gx = 0.45;
                        gy = -0.4;
                    }
                    else
                    {
                        // Making Teddy wave at you.
                        target = Expression.Grin;
                        rate = 18;
                        atCursor = true;
                    }
                    break;

                case Antic.Hello:
                    target = t < 0.35 ? Expression.Surprised : Expression.Grin;
                    rate = 22;
                    atCursor = true;
                    break;

                case Antic.Waggle:
                {
                    // The eyebrows go up and down on the beat, over the knowing smirk.
                    Expression e = Expression.Knowing;
                    e.BrowRaise = (int)(t / 0.25) % 2 == 0 ? 0.95 : -0.15;
                    e.MouthDx = 0.4;
                    target = e;
                    rate = 40;
                    atCursor = true;
                    break;
                }

                case Antic.Sneak:
                    if (t < 0.9)
                    {
                        // Is the coast clear? Eyes one way, then the other.
                        target = Expression.Shifty;
                        rate = 16;
                        gx = t < 0.45 ? -0.9 : 0.9;
                        gy = 0.05;
                    }
                    else if (t < 2.6)
                    {
                        target = Expression.Mischievous;
                        rate = 16;
                        gx = 0.6;
                        gy = -0.1;
                    }
                    else
                    {
                        // Caught in the act: the picture of innocence.
                        target = Expression.Whistling;
                        rate = 20;
                        atCursor = true;
                    }
                    break;

                case Antic.Fuss:
                    target = t < 1.8 ? Expression.Serious : Expression.Smug;
                    rate = 12;
                    gx = 0.2;
                    gy = t < 1.8 ? -0.5 : 0.1;
                    break;

                case Antic.Ponder:
                    if (t < 2.1)
                    {
                        target = Expression.Thinking;
                        rate = 10;
                        gx = 0.2;
                        gy = 0.6;
                    }
                    else if (t < 2.4)
                    {
                        target = Expression.Surprised;
                        rate = 40;
                        gx = 0.2;
                        gy = 0.1;
                    }
                    else
                    {
                        target = Expression.Happy;
                        rate = 20;
                        atCursor = true;
                    }
                    break;

                case Antic.Jive:
                    target = (int)(t / 0.8) % 2 == 0 ? Expression.Grin : Expression.Laugh;
                    rate = 18;
                    atCursor = true;
                    break;

                case Antic.Poke:
                    if (t < 1.2)
                    {
                        target = Expression.Mischievous;
                        if (goalTarget != null) LookAt(goalTarget.CentreX, goalTarget.EyeY, out gx, out gy);
                        else atCursor = true;
                    }
                    else if (t < 1.9)
                    {
                        // Examining the finger.
                        target = Expression.Curious;
                        gx = 0.5;
                        gy = -0.3;
                    }
                    else
                    {
                        // Wasn't me.
                        target = Expression.Whistling;
                        rate = 12;
                        atCursor = true;
                    }
                    break;
            }
        }

        // ---- Which way it turns ----------------------------------------------------------

        double AnticYaw(out bool snap)
        {
            snap = false;
            double t = modeTime;
            switch (antic)
            {
                case Antic.Dance:
                    return DanceYaw(t, 0);

                case Antic.Twirl:
                {
                    if (t < 0.35) return 58;
                    if (t >= 1.05) return 10;
                    // One full turn, taken directly rather than eased; it
                    // comes back round to where it started.
                    snap = true;
                    double y = 58 + 360 * Ease((t - 0.35) / 0.7);
                    return y >= 417.5 ? 58 : y;
                }

                case Antic.Point: return 15;
                case Antic.Scratch: return 12;
                case Antic.Sneeze: return 30;
                case Antic.Box: return 45;
                case Antic.Stomp: return 15;
                case Antic.Flex: return t < 1.4 ? 20 : 38;
                case Antic.Shrug: return 10;
                case Antic.Trip: return t < 1.0 ? 58 : 30;
                case Antic.Boo: return 30;
                case Antic.Strum: return 25;
                case Antic.Pushups: return 70;   // side on, so the press-ups read
                case Antic.Jog: return 35;
                case Antic.Salute: return 5;
                case Antic.Moonwalk: return 58;
                case Antic.Toot: return t < 0.8 ? 40 : t < 1.5 ? -30 : 30;   // glancing about afterwards
                case Antic.Flap: return 35;
                case Antic.Fret: return 10;
                case Antic.Faint: return t < 0.45 ? 20 : 0;
                case Antic.Dig: return 55;
                case Antic.Sleepwalk: return 58;
                case Antic.Daydream: return 20;
                case Antic.Kiss: return 10;

                case Antic.Teddy: return t >= 1.5 && t < 2.4 ? 5 : 20;
                case Antic.Hello: return 5;
                case Antic.Sneak: return t < 0.9 ? 15 : t < 2.6 ? 58 : 0;
                case Antic.Fuss: return 10;
                case Antic.Ponder: return 20;
                case Antic.Jive: return DanceYaw(t, 0);
                case Antic.Poke: return t < 1.2 ? 40 : t < 1.9 ? 25 : 0;
                default: return 0;   // yawning, dozing, whistling, taunting, laughing, hiccuping, peekaboo, hiding, eyebrows: facing you
            }
        }

        // A dancer sways from side to side every two beats.
        static double DanceYaw(double t, double centre)
        {
            return centre + (Math.Sin(t / DanceBeat * Math.PI / 2) >= 0 ? 25 : -25);
        }

        // ---- The body during an antic -----------------------------------------------------

        void AnticPose(ref Pose pose)
        {
            double t = modeTime;
            switch (antic)
            {
                case Antic.Dance:
                    DancePose(ref pose, t);
                    break;

                case Antic.Twirl:
                    if (t < 0.35)
                    {
                        // Arms out, a little crouch to wind up.
                        double e = Ease(t / 0.35);
                        pose.ArmNear = Q2(70 * e);
                        pose.ArmFar = -Q2(70 * e);
                        pose.StretchY = 1 - 0.05 * e;
                        pose.StretchX = 1 + 0.03 * e;
                    }
                    else if (t < 1.05)
                    {
                        pose.ArmNear = 95;
                        pose.ArmFar = -95;
                        pose.StretchY = 1.03;
                    }
                    else
                    {
                        // Ta-da.
                        double e = Ease(Math.Min(1, (t - 1.05) / 0.25));
                        pose.ArmNear = Q2(95 + 60 * e);
                        pose.ArmFar = -Q2(95 + 60 * e);
                        pose.ForeNear = -Q2(20 * e);
                        pose.ForeFar = Q2(20 * e);
                        pose.StretchY = 1 + 0.04 * e;
                        pose.BodyDy = -Quantise(0.25 * e);
                    }
                    break;

                case Antic.Point:
                    pose.ArmNear = AimAt(cursorX, cursorY);
                    pose.PointNear = true;
                    if (t > 1.0) pose.BodyDy = Quantise(0.25 * (1 - Math.Cos(2 * Math.PI * (t - 1.0) / 0.22)) / 2); // giggling
                    break;

                case Antic.Yawn:
                    StretchPose(ref pose, YawnLift(t));
                    break;

                case Antic.Doze:
                    if (t < 2.1)
                    {
                        StretchPose(ref pose, YawnLift(t));
                    }
                    else
                    {
                        // Slumped, nodding further off every so often and
                        // jerking back up, with letters floating away.
                        double n = ((t - 2.1) % 1.3) / 1.3;
                        double droop = n < 0.35 ? n / 0.35 : n < 0.42 ? 1 - (n - 0.35) / 0.07 : 0;
                        pose.BodyDy = Quantise(0.45 + 0.6 * droop);
                        pose.Lean = 0.2 + Math.Floor(droop * 4) / 16;
                        pose.HeadDip = Quantise(0.3 + 0.9 * droop);   // (a head on a neck nods right off)
                        pose.Zzz = true;
                        pose.DecorPhase = Q16(((t - 2.1) / 1.6) % 1);
                    }
                    break;

                case Antic.Scratch:
                    pose.ArmNear = 170;
                    pose.ForeNear = -50 + (t > 0.3 && t < 1.55 ? Q2(7 * Math.Sin(t * 2 * Math.PI * 3.5)) : 0);
                    pose.Bubble = t >= 1.55;
                    Breathe(ref pose);
                    break;

                case Antic.Whistle:
                    pose.Note = true;
                    pose.DecorPhase = Q16((t / 0.9) % 1);
                    pose.Lean = Math.Round(0.15 * Math.Sin(t * 2.2) * 8) / 8; // rocking on its heels
                    Breathe(ref pose);
                    break;

                case Antic.Sneeze:
                    if (t < 0.75)
                    {
                        // The breath in: up on tiptoe, head back.
                        double e = Ease(t / 0.75);
                        pose.StretchY = 1 + 0.08 * e;
                        pose.BodyDy = -Quantise(0.4 * e);
                        pose.Lean = -Math.Round(0.3 * e * 8) / 8;
                        pose.ArmNear = Q2(25 * e);
                        pose.ArmFar = -Q2(25 * e);
                    }
                    else if (t < 0.95)
                    {
                        // The sneeze: folded forward, squashed.
                        pose.Lean = 0.8;
                        pose.StretchY = 0.9;
                        pose.StretchX = 1.1;
                        pose.BodyDy = 0.5;
                        pose.ArmNear = 50;
                        pose.ArmFar = -50;
                        pose.ForeNear = 40;
                        pose.ForeFar = -40;
                    }
                    else
                    {
                        double e = 1 - Ease(Math.Min(1, (t - 0.95) / 0.4));
                        pose.Lean = Math.Round(0.8 * e * 8) / 8;
                        pose.BodyDy = Quantise(0.5 * e);
                        pose.StretchY = 1 - 0.1 * e;
                        pose.StretchX = 1 + 0.1 * e;
                        pose.ArmNear = Q2(50 * e);
                        pose.ArmFar = -Q2(50 * e);
                        pose.ForeNear = Q2(40 * e);
                        pose.ForeFar = -Q2(40 * e);
                    }
                    break;

                case Antic.Box:
                {
                    // Fists up by the face, bouncing on the toes; the arms
                    // take turns shooting out straight and coming back.
                    const double beat = 0.3;
                    double b = t / beat;
                    int n = (int)b;
                    double f = b - n;
                    double punch = t < 1.9 ? (f < 0.5 ? f / 0.5 : 1 - (f - 0.5) / 0.5) : 0;
                    bool nearPunch = n % 2 == 0;
                    pose.ArmNear = Q2(60 + (nearPunch ? 35 * punch : 0));
                    pose.ForeNear = Q2(100 - (nearPunch ? 100 * punch : 0));
                    pose.ArmFar = Q2(60 + (nearPunch ? 0 : 35 * punch));
                    pose.ForeFar = Q2(100 - (nearPunch ? 0 : 100 * punch));
                    pose.Lean = 0.3;
                    pose.StretchX = 1.02;
                    pose.BodyDy = Quantise(0.25 * (1 - Math.Cos(b * 2 * Math.PI)) / 2);
                    break;
                }

                case Antic.Taunt:
                    // One hand beckoning, the other on the hip, hips wiggling.
                    pose.ArmNear = 75;
                    pose.ForeNear = Q2(30 + 35 * Math.Sin(t * 2 * Math.PI * 2.5));
                    pose.ArmFar = -40;
                    pose.ForeFar = 85;
                    pose.StretchX = 1 + 0.03 * Math.Round(Math.Sin(t * 2 * Math.PI * 2.5) * 2) / 2;
                    break;

                case Antic.Stomp:
                {
                    // Knees up, then down hard - twice. Fists clenched at the sides.
                    double lift = t < 0.25 ? t / 0.25 : t >= 0.4 && t < 0.65 ? (t - 0.4) / 0.25 : 0;
                    pose.LegTuck = Math.Round(0.5 * lift * 8) / 8;
                    pose.BodyDy = -Quantise(0.4 * lift);
                    pose.StretchY = 1 + 0.05 * lift;
                    pose.ArmNear = 25;
                    pose.ArmFar = -25;
                    pose.ForeNear = 10;
                    pose.ForeFar = -10;
                    pose.Lean = 0.15;
                    break;
                }

                case Antic.Flex:
                {
                    // Both arms curl up, then pump while it admires them.
                    double e = Ease(Math.Min(1, t / 0.5));
                    double pump = t > 0.5 ? Q2(8 * Math.Sin(t * 2 * Math.PI * 2)) : 0;
                    pose.ArmNear = Q2(95 * e);
                    pose.ArmFar = -Q2(95 * e);
                    pose.ForeNear = Q2(110 * e) + pump;
                    pose.ForeFar = -(Q2(110 * e) + pump);
                    pose.Lean = -0.3;
                    pose.StretchX = 1.06;
                    break;
                }

                case Antic.Joy:
                    break; // over in a step: the jumps themselves are Crouch and Air

                case Antic.Laugh:
                    LaughPose(ref pose, t, false);
                    break;

                case Antic.Hiccups:
                {
                    // Each hiccup is a jolt: up on the toes, arms twitching out.
                    double jolt = HiccupJolt(t);
                    pose.StretchY = 1 + 0.09 * jolt;
                    pose.StretchX = 1 - 0.05 * jolt;
                    pose.BodyDy = -Quantise(0.5 * jolt);
                    pose.ArmNear = Q2(25 * jolt);
                    pose.ArmFar = -Q2(25 * jolt);
                    if (jolt <= 0) Breathe(ref pose);
                    break;
                }

                case Antic.Shrug:
                {
                    // Arms out, shoulders up: oh well. Then the arms drop.
                    double e = t < 0.7 ? 1 : 1 - Ease(Math.Min(1, (t - 0.7) / 0.4));
                    pose.ArmNear = Q2(60 * e);
                    pose.ArmFar = -Q2(60 * e);
                    pose.ForeNear = Q2(50 * e);
                    pose.ForeFar = -Q2(50 * e);
                    pose.StretchY = 1 + 0.03 * e;
                    break;
                }

                case Antic.Trip:
                    if (t < 0.12)
                    {
                        // Pitching forward mid-stride, arms out to catch itself.
                        Walk(ref pose);
                        pose.Lean = 0.6;
                        pose.ArmNear = 70;
                        pose.ArmFar = 70;
                        pose.ForeNear = 20;
                        pose.ForeFar = 20;
                    }
                    else if (t < 1.0)
                    {
                        // Flat out.
                        pose.Lean = 1.0;
                        pose.StretchY = 0.72;
                        pose.StretchX = 1.18;
                        pose.BodyDy = 0.9;
                        pose.ArmNear = 80;
                        pose.ArmFar = 80;
                        pose.ForeNear = 10;
                        pose.ForeFar = 10;
                    }
                    else
                    {
                        // Picking itself up.
                        double e = 1 - Ease(Math.Min(1, (t - 1.0) / 0.6));
                        pose.Lean = Math.Round(e * 8) / 8;
                        pose.StretchY = 1 - 0.28 * e;
                        pose.StretchX = 1 + 0.18 * e;
                        pose.BodyDy = Quantise(0.9 * e);
                        pose.ArmNear = Q2(80 * e);
                        pose.ArmFar = Q2(80 * e);
                        pose.ForeNear = Q2(10 * e);
                        pose.ForeFar = Q2(10 * e);
                    }
                    break;

                case Antic.Boo:
                    if (t < 0.6)
                    {
                        // The shout: lunging, arms thrown up, a "!" overhead.
                        pose.ArmNear = 150;
                        pose.ArmFar = -150;
                        pose.ForeNear = -20;
                        pose.ForeFar = 20;
                        pose.Lean = 0.3;
                        pose.StretchY = 1.06;
                        pose.Bubble = true;
                    }
                    else
                    {
                        LaughPose(ref pose, t - 0.6, false);
                    }
                    break;

                case Antic.Strum:
                {
                    // Far hand up on the neck, near hand strumming over the
                    // belly, head banging on the beat, notes flying.
                    double b = t / 0.4;
                    double bang = (1 - Math.Cos(b * 2 * Math.PI)) / 2;
                    pose.ArmFar = 45;
                    pose.ForeFar = 50;
                    pose.ArmNear = 30;
                    pose.ForeNear = Q2(70 + 30 * Math.Sin(b * 4 * Math.PI));
                    pose.Lean = Math.Round(0.35 * bang * 8) / 8;
                    pose.BodyDy = Quantise(0.35 * bang);
                    pose.StretchX = 1.02;
                    pose.Note = true;
                    pose.DecorPhase = Q16((b / 2) % 1);
                    break;
                }

                case Antic.Pushups:
                {
                    // Down flat, then pressing up and sinking down, twice a second.
                    double n = Ease(Math.Min(1, t / 0.3));
                    double up = t < 0.3 ? 0 : Math.Round((1 - Math.Cos((t - 0.3) / 0.6 * 2 * Math.PI)) / 2 * 8) / 8;
                    pose.Lean = Math.Round(n * 8) / 8;
                    pose.StretchY = 1 - 0.28 * n + 0.1 * up * n;
                    pose.StretchX = 1 + 0.18 * n;
                    pose.BodyDy = Quantise(0.9 * n - 0.4 * up * n);
                    pose.ArmNear = Q2(80 * n);
                    pose.ArmFar = Q2(80 * n);
                    pose.ForeNear = Q2((10 + 50 * (1 - up)) * n);
                    pose.ForeFar = pose.ForeNear;
                    break;
                }

                case Antic.Jog:
                {
                    // Running on the spot: feet stepping, arms pumping, bobbing.
                    double b = t / 0.45;
                    double s = Math.Sin(b * 2 * Math.PI);
                    pose.Walking = true;
                    pose.WalkPhase = Math.Floor(b * 12) / 12;
                    pose.BodyDy = Quantise(0.3 * (1 - Math.Cos(b * 4 * Math.PI)) / 2);
                    pose.ArmNear = Q2(40 + 25 * s);
                    pose.ForeNear = 95;
                    pose.ArmFar = -Q2(40 - 25 * s);
                    pose.ForeFar = -95;
                    pose.Lean = 0.15;
                    break;
                }

                case Antic.Salute:
                {
                    // Snaps to attention, chest out, the hand up at the brow.
                    double e = Ease(Math.Min(1, t / 0.25));
                    pose.StretchY = 1 + 0.04 * e;
                    pose.Lean = -Math.Round(0.15 * e * 8) / 8;
                    pose.ArmNear = Q2(120 * e);
                    pose.ForeNear = Q2(100 * e);
                    break;
                }

                case Antic.Moonwalk:
                    // Leaning back, feet stepping forward while it slides back.
                    pose.Walking = t > 0.3;
                    pose.WalkPhase = walkPhase;
                    pose.Lean = -0.25;
                    pose.ArmNear = 20;
                    pose.ArmFar = -20;
                    pose.ForeNear = -30;
                    pose.ForeFar = 30;
                    pose.BodyDy = Quantise(0.2 * (1 - Math.Cos(walkPhase * 4 * Math.PI)) / 2);
                    break;

                case Antic.Toot:
                    if (t < 0.55)
                    {
                        // A little squat and a look of concentration.
                        double e = Ease(t / 0.55);
                        pose.StretchY = 1 - 0.06 * e;
                        pose.StretchX = 1 + 0.04 * e;
                        pose.BodyDy = Quantise(0.3 * e);
                        pose.Lean = Math.Round(0.2 * e * 8) / 8;
                    }
                    else
                    {
                        // Hands behind the back, rocking on the heels: wasn't me.
                        pose.ArmNear = -25;
                        pose.ArmFar = 25;
                        pose.ForeNear = -60;
                        pose.ForeFar = 60;
                        pose.Lean = -Math.Round(0.15 * Math.Sin(t * 2.2) * 8) / 8;
                        Breathe(ref pose);
                    }
                    break;

                case Antic.Peekaboo:
                    if (t < 1.1)
                    {
                        CoverEyes(ref pose, 0);
                    }
                    else
                    {
                        // Peekaboo! Arms flung wide, up on the toes.
                        double e = Ease(Math.Min(1, (t - 1.1) / 0.15));
                        pose.ArmNear = Q2(178 - 58 * e);
                        pose.ArmFar = -pose.ArmNear;
                        pose.ForeNear = Q2(92 - 92 * e);
                        pose.ForeFar = -pose.ForeNear;
                        pose.StretchY = 1 + 0.05 * e;
                        pose.BodyDy = -Quantise(0.2 * e);
                        pose.Bubble = t < 1.6;
                    }
                    break;

                case Antic.Flap:
                {
                    // Elbows out and flapping, head jutting on each beat, strutting.
                    double b = t / 0.3;
                    int n = (int)b;
                    double f = b - n;
                    double flap = f < 0.5 ? f / 0.5 : 1 - (f - 0.5) / 0.5;
                    pose.ArmNear = Q2(50 + 30 * flap);
                    pose.ForeNear = Q2(150 - 20 * flap);
                    pose.ArmFar = -pose.ArmNear;
                    pose.ForeFar = -pose.ForeNear;
                    pose.Lean = n % 2 == 0 ? 0.3 : 0.1;
                    pose.Walking = true;
                    pose.WalkPhase = Math.Floor(b * 6) / 24;
                    pose.BodyDy = Quantise(0.2 * flap);
                    break;
                }

                case Antic.Hide:
                {
                    // Hands clapped over the eyes, shaking; a peek through the fingers.
                    CoverEyes(ref pose, Math.Sin(t * 28) > 0 ? 3 : -3);   // shaking: two positions, nine times a second
                    if (t > 1.0 && t < 1.5)
                    {
                        pose.ArmNear = 140;
                        pose.ForeNear = 60;
                    }
                    pose.StretchY = 0.96;
                    pose.BodyDy = 0.35;
                    pose.Lean = 0.1;
                    break;
                }

                case Antic.Fret:
                    // Nibbling the fingers of one hand, the other arm wrapped round the belly.
                    pose.ArmNear = 25;
                    pose.ForeNear = 170 + (Math.Sin(t * 20) > 0 ? 4 : 0);
                    pose.ArmFar = 40;
                    pose.ForeFar = 100;
                    pose.Lean = 0.1;
                    Breathe(ref pose);
                    break;

                case Antic.Faint:
                    if (t < 0.45)
                    {
                        // Swaying, arms out for balance, then over it goes.
                        pose.Lean = Math.Round(0.3 * Math.Sin(t * 14) * 8) / 8;
                        pose.ArmNear = 50;
                        pose.ArmFar = -50;
                        pose.ForeNear = 20;
                        pose.ForeFar = -20;
                    }
                    else if (t < 2.2)
                    {
                        // Flat on its back, arms spread.
                        pose.Lean = -1.0;
                        pose.StretchY = 0.72;
                        pose.StretchX = 1.18;
                        pose.BodyDy = 0.9;
                        pose.ArmNear = 85;
                        pose.ArmFar = -85;
                    }
                    else
                    {
                        // Coming round and sitting up.
                        double e = 1 - Ease(Math.Min(1, (t - 2.2) / 0.6));
                        pose.Lean = -Math.Round(e * 8) / 8;
                        pose.StretchY = 1 - 0.28 * e;
                        pose.StretchX = 1 + 0.18 * e;
                        pose.BodyDy = Quantise(0.9 * e);
                        pose.ArmNear = Q2(85 * e);
                        pose.ArmFar = -Q2(85 * e);
                    }
                    break;

                case Antic.Dig:
                {
                    // Bent double, both arms scooping at the ground in front.
                    double f = (t / 0.5) % 1;
                    double scoop = f < 0.4 ? f / 0.4 : 1 - (f - 0.4) / 0.6;
                    pose.Lean = 0.6;
                    pose.StretchY = 0.9;
                    pose.StretchX = 1.05;
                    pose.BodyDy = Quantise(0.5 + 0.2 * scoop);
                    pose.ArmNear = Q2(70 - 30 * scoop);
                    pose.ArmFar = pose.ArmNear;
                    pose.ForeNear = Q2(20 + 50 * scoop);
                    pose.ForeFar = pose.ForeNear;
                    break;
                }

                case Antic.Sleepwalk:
                    // Arms straight out ahead, feet shuffling, eyes shut, Zzz.
                    pose.ArmNear = 90;
                    pose.ArmFar = 90;
                    pose.ForeNear = -5;
                    pose.ForeFar = 5;
                    pose.Lean = 0.2;
                    pose.Walking = t > 1.0;
                    pose.WalkPhase = walkPhase;
                    pose.BodyDy = Quantise(0.4 + 0.15 * (1 + Math.Sin(time * 2.6)) / 2);
                    pose.Zzz = true;
                    pose.DecorPhase = Q16((t / 1.6) % 1);
                    break;

                case Antic.Daydream:
                    if (t < 2.4)
                    {
                        // Hands clasped behind, head back, swaying.
                        pose.ArmNear = -20;
                        pose.ArmFar = 20;
                        pose.ForeNear = -70;
                        pose.ForeFar = 70;
                        pose.Lean = -Quantise(0.1 + 0.15 * (1 + Math.Sin(t * 1.3)) / 2);
                        Breathe(ref pose);
                    }
                    else
                    {
                        // Snapping out of it with a little jolt.
                        double e = 1 - Ease(Math.Min(1, (t - 2.4) / 0.3));
                        pose.StretchY = 1 + 0.05 * e;
                        pose.BodyDy = -Quantise(0.3 * e);
                        pose.ArmNear = Q2(30 * e);
                        pose.ArmFar = -Q2(30 * e);
                        pose.Bubble = t < 2.7;
                    }
                    break;

                case Antic.Kiss:
                    if (t < 0.9)
                    {
                        // Hand to the lips.
                        double e = Ease(Math.Min(1, t / 0.3));
                        pose.ArmNear = Q2(25 * e);
                        pose.ForeNear = Q2(165 * e);
                        pose.Lean = Math.Round(0.1 * e * 8) / 8;
                    }
                    else
                    {
                        // ...and blown towards the cursor, a heart floating off.
                        double e = Ease(Math.Min(1, (t - 0.9) / 0.25));
                        double aim = AimAt(cursorX, cursorY);
                        pose.ArmNear = Q2(25 + (aim - 25) * e);
                        pose.ForeNear = Q2(165 * (1 - e));
                        pose.Heart = true;
                        pose.DecorPhase = Q16(Math.Min(0.999, (t - 0.9) / 1.3));
                        pose.Lean = -0.1;
                    }
                    break;

                // ---- Mr. Bean's ----

                case Antic.Teddy:
                    if (t < 0.6)
                    {
                        // The near hand goes into the jacket.
                        double e = Ease(t / 0.6);
                        pose.ArmNear = Q2(20 * e);
                        pose.ForeNear = Q2(220 * e);
                        pose.ReachNear = Q2(60 * e);
                        pose.HeadDip = Quantise(0.4 * e);
                    }
                    else if (t < 3.1)
                    {
                        // Out comes Teddy, held up in front of the chest.
                        double e = Ease(Math.Min(1, (t - 0.6) / 0.4));
                        TeddyPose(ref pose, e);
                        pose.Teddy = e > 0.4;
                        pose.HeadDip = Quantise(0.4 + 0.2 * e);
                        if (t >= 1.5 && t < 2.4)
                        {
                            // Teddy waves at you; so does his owner's grin.
                            pose.TeddyWave = Q16(0.5 + 0.5 * Math.Sin((t - 1.5) * 2 * Math.PI * 3));
                            pose.HeadDip = 0;
                        }
                        Breathe(ref pose);
                    }
                    else
                    {
                        // Back into the jacket.
                        double e = 1 - Ease(Math.Min(1, (t - 3.1) / 0.5));
                        TeddyPose(ref pose, e);
                        pose.Teddy = e > 0.4;
                        pose.HeadDip = Quantise(0.4 * e);
                    }
                    break;

                case Antic.Hello:
                    HelloPose(ref pose, t);
                    break;

                case Antic.Waggle:
                    // Leaning in with the hands behind the back, head cocked forward.
                    HandsBehind(ref pose);
                    pose.Lean = 0.2;
                    pose.HeadDip = 0.25;
                    Breathe(ref pose);
                    break;

                case Antic.Sneak:
                    if (t < 0.9)
                    {
                        // A look over each shoulder, hands up at the chest.
                        pose.HeadTurn = t < 0.45 ? -55 : 55;
                        pose.ArmNear = 35;
                        pose.ForeNear = 95;
                        pose.ArmFar = 35;
                        pose.ForeFar = 95;
                        Breathe(ref pose);
                    }
                    else if (t < 2.6)
                    {
                        // Tiptoeing: hunched, knees high, hands up like a cartoon burglar.
                        pose.Tiptoe = 1;
                        pose.Lean = 0.35;
                        pose.Walking = true;
                        pose.WalkPhase = walkPhase;
                        pose.ArmNear = 45;
                        pose.ForeNear = 100;
                        pose.ArmFar = 45;
                        pose.ForeFar = 100;
                        pose.HeadDip = 0.3;
                        pose.BodyDy = Quantise(0.4 + 0.3 * (1 - Math.Cos(walkPhase * 4 * Math.PI)) / 2);
                    }
                    else
                    {
                        // Frozen: hands behind the back, rocking on the heels, whistling.
                        HandsBehind(ref pose);
                        pose.Lean = -Math.Round(0.1 * Math.Sin(t * 2.2) * 8) / 8;
                        pose.HeadDip = -0.15;
                        Breathe(ref pose);
                    }
                    break;

                case Antic.Fuss:
                    if (t < 1.0)
                    {
                        // Both hands at the collar, straightening the tie, chin up.
                        double e = Ease(Math.Min(1, t / 0.3));
                        double jig = t > 0.3 ? Q2(5 * Math.Sin(t * 2 * Math.PI * 3)) : 0;
                        pose.ArmNear = Q2(10 * e);
                        pose.ForeNear = Q2(205 * e) + jig;
                        pose.ReachNear = Q2(70 * e);
                        pose.ArmFar = -Q2(10 * e);
                        pose.ForeFar = -Q2(205 * e) + jig;
                        pose.ReachFar = Q2(70 * e);
                        pose.HeadDip = -Quantise(0.35 * e);
                    }
                    else if (t < 1.8)
                    {
                        // The near hand brushes down the lapel, twice.
                        double f = ((t - 1.0) / 0.4) % 1;
                        pose.ArmNear = Q2(10 + 15 * f);
                        pose.ForeNear = Q2(205 - 55 * f);
                        pose.ReachNear = 70;
                        pose.ArmFar = -10;
                        pose.ForeFar = -205;
                        pose.ReachFar = 70;
                        pose.HeadDip = -0.2;
                    }
                    else
                    {
                        // A pat of the hair.
                        double e = Ease(Math.Min(1, (t - 1.8) / 0.3));
                        double pat = t > 2.1 ? Q2(6 * Math.Sin(t * 2 * Math.PI * 3)) : 0;
                        pose.ArmNear = Q2(25 + 125 * e);
                        pose.ForeNear = Q2(150 - 108 * e) + pat;
                        pose.ReachNear = Q2(40 * e);
                        pose.Lean = -Math.Round(0.1 * e * 8) / 8;
                        pose.HeadTurn = Q2(-12 * e);
                    }
                    break;

                case Antic.Ponder:
                    if (t < 2.1)
                    {
                        // Finger to the chin, other arm across the belly, tapping.
                        double e = Ease(Math.Min(1, t / 0.4));
                        double tap = t > 0.5 && Math.Sin(t * 2 * Math.PI * 2) > 0 ? 4 : 0;
                        pose.ArmNear = Q2(10 * e);
                        pose.ForeNear = Q2(212 * e) + tap;
                        pose.ReachNear = Q2(75 * e);
                        pose.PointNear = e > 0.5;
                        pose.ArmFar = -Q2(10 * e);
                        pose.ForeFar = Q2(30 * e);
                        pose.ReachFar = Q2(50 * e);
                        pose.HeadDip = -Quantise(0.3 * e);
                        pose.HeadTurn = Q2(15 * e);
                        Breathe(ref pose);
                    }
                    else
                    {
                        // An idea: finger in the air, up on the toes, "!".
                        double e = Ease(Math.Min(1, (t - 2.1) / 0.2));
                        pose.ArmNear = Q2(10 + 150 * e);
                        pose.ForeNear = Q2(212 - 232 * e);
                        pose.ReachNear = Q2(75 * (1 - e));
                        pose.PointNear = true;
                        pose.ArmFar = -10;
                        pose.ForeFar = 30;
                        pose.ReachFar = 50;
                        pose.Bubble = t < 2.7;
                        pose.StretchY = 1 + 0.03 * e;
                        pose.BodyDy = -Quantise(0.2 * e);
                        pose.Tiptoe = Math.Round(e * 4) / 4;
                    }
                    break;

                case Antic.Jive:
                    JivePose(ref pose, t);
                    break;

                case Antic.Poke:
                {
                    double aim = goalTarget != null ? AimAt(goalTarget.CentreX, goalTarget.ShoulderY) : AimAt(cursorX, cursorY);
                    if (t < 0.5)
                    {
                        // The finger comes out and the arm extends.
                        double e = Ease(t / 0.5);
                        pose.ArmNear = Q2(aim * e);
                        pose.PointNear = e > 0.4;
                        pose.Lean = Math.Round(0.3 * e * 8) / 8;
                    }
                    else if (t < 1.2)
                    {
                        // Two prods.
                        double f = ((t - 0.5) / 0.35) % 1;
                        double jab = f < 0.5 ? f / 0.5 : 1 - (f - 0.5) / 0.5;
                        pose.ArmNear = aim;
                        pose.ForeNear = -Q2(25 * (1 - jab));
                        pose.PointNear = true;
                        pose.Lean = Math.Round((0.3 + 0.2 * jab) * 8) / 8;
                    }
                    else if (t < 1.9)
                    {
                        // Examining the finger.
                        pose.ArmNear = 40;
                        pose.ForeNear = 120;
                        pose.ReachNear = 50;
                        pose.PointNear = true;
                        pose.HeadDip = 0.35;
                        pose.HeadTurn = 10;
                    }
                    else
                    {
                        // Hands behind the back: wasn't me.
                        HandsBehind(ref pose);
                        pose.Lean = -Math.Round(0.12 * Math.Sin(t * 2.2) * 8) / 8;
                        pose.HeadDip = -0.2;
                        Breathe(ref pose);
                    }
                    break;
                }
            }
        }

        // Teddy held up beside the chest in the near hand, where he can be
        // seen (e is how far the arm has come out of the jacket, 0..1).
        static void TeddyPose(ref Pose pose, double e)
        {
            pose.ArmNear = Q2(20 + 40 * e);
            pose.ForeNear = Q2(220 - 70 * e);
            pose.ReachNear = Q2(60 * (1 - e));
            pose.ArmFar = Q2(15 * e);
            pose.ForeFar = Q2(20 * e);
        }

        // Mr. Bean's hello: the arm shoots straight up beside the head and
        // the hand wags stiffly from the wrist, up on the toes, chin up.
        static void HelloPose(ref Pose pose, double t)
        {
            double e = Ease(Math.Min(1, t / 0.25));
            pose.ArmNear = Q2(160 * e);
            pose.ForeNear = t > 0.3 ? Q2(-15 + 14 * Math.Sin(t * 2 * Math.PI * 4)) : Q2(-15 * e);
            pose.Tiptoe = Math.Round(e * 4) / 4;
            pose.HeadDip = -Quantise(0.3 * e);
            pose.StretchY = 1 + 0.02 * e;
        }

        // The Bean dance: elbows tucked and pumping, hips twisting, knees
        // bouncing, head bobbing, a note on every other beat.
        static void JivePose(ref Pose pose, double t)
        {
            double b = t / DanceBeat;
            double bounce = (1 - Math.Cos(b * 2 * Math.PI)) / 2;
            double side = Math.Sin(b * Math.PI);
            pose.BodyDy = Quantise(0.6 * bounce);
            pose.ArmNear = Q2(35 + 25 * side);
            pose.ForeNear = Q2(100 - 20 * side);
            pose.ReachNear = Q2(30 * side);
            pose.ArmFar = -Q2(35 - 25 * side);
            pose.ForeFar = -Q2(100 + 20 * side);
            pose.ReachFar = -Q2(30 * side);
            pose.Walking = true;
            pose.WalkPhase = Math.Floor(b * 12) / 24;
            pose.HeadDip = Quantise(0.3 * bounce);
            pose.Lean = Math.Round(0.15 * side * 8) / 8;
            pose.Note = true;
            pose.DecorPhase = Q16((b / 2) % 1);
        }

        // Both hands clapped over the goggles, shaking by `tremble` degrees.
        static void CoverEyes(ref Pose pose, double tremble)
        {
            pose.ArmNear = 178 + tremble;
            pose.ArmFar = -178 - tremble;
            pose.ForeNear = 92;
            pose.ForeFar = -92;
        }

        // A jig on the spot: bouncing on the beat, feet stepping, arms
        // pumping up and down in turn, a note floating off every two beats.
        void DancePose(ref Pose pose, double t)
        {
            if (bean)
            {
                JivePose(ref pose, t);
                return;
            }
            double b = t / DanceBeat;
            double bounce = (1 - Math.Cos(b * 2 * Math.PI)) / 2; // down on the beat, up between
            double side = Math.Sin(b * Math.PI);                  // which arm is up, swapping each beat
            pose.BodyDy = Quantise(0.5 * bounce);
            pose.ArmNear = Q2(60 + 60 * side);
            pose.ArmFar = -Q2(60 - 60 * side);
            pose.ForeNear = 35;
            pose.ForeFar = -35;
            pose.Walking = true;
            pose.WalkPhase = Math.Floor(b * 12) / 24;
            pose.StretchY = 1 + 0.03 * Math.Round(side * 2) / 2;
            pose.Note = true;
            pose.DecorPhase = Q16((b / 2) % 1);
        }

        // Shaking with laughter, hands on the belly - or one of them pointing.
        void LaughPose(ref Pose pose, double t, bool pointing)
        {
            pose.BodyDy = Quantise(0.4 * (1 - Math.Cos(2 * Math.PI * t / 0.26)) / 2);
            pose.Lean = t < 1.1 ? -0.25 : 0.3;
            pose.ArmFar = -20;
            pose.ForeFar = 80;
            if (pointing)
            {
                pose.ArmNear = AimAt(reactX, feetY - 9 * scale);
                pose.PointNear = true;
            }
            else
            {
                pose.ArmNear = 20;
                pose.ForeNear = -80;
            }
        }

        // Stretching up and yawning: the arms rise over the first 0.8 s,
        // hold, then drop.
        static double YawnLift(double t)
        {
            return t < 0.8 ? Ease(t / 0.8) : t < 1.4 ? 1 : 1 - Ease(Math.Min(1, (t - 1.4) / 0.7));
        }

        static void StretchPose(ref Pose pose, double up)
        {
            pose.ArmNear = Q2(165 * up);
            pose.ArmFar = -Q2(165 * up);
            pose.ForeNear = -Q2(15 * up);
            pose.ForeFar = Q2(15 * up);
            pose.StretchY = 1 + 0.06 * up;
            pose.BodyDy = -Quantise(0.3 * up);
            pose.Lean = -Math.Round(0.25 * up * 8) / 8;
            pose.HeadDip = -Quantise(0.3 * up);
        }

        // The hiccups come at 0.4, 1.0, 1.7 and 2.3 s; each is a sharp jolt
        // that dies away over 0.14 s.
        static double HiccupJolt(double t)
        {
            double[] at = { 0.4, 1.0, 1.7, 2.3 };
            for (int i = 0; i < at.Length; i++)
                if (t >= at[i] && t < at[i] + 0.14) return 1 - (t - at[i]) / 0.14;
            return 0;
        }

        void Breathe(ref Pose pose)
        {
            pose.BodyDy = Quantise(0.3 * (1 + Math.Sin(time * 2.6)) / 2);
        }

        /// <summary>The upper-arm angle that points the near arm at a screen point.</summary>
        double AimAt(double x, double y)
        {
            double dx = (x - cx) * facing / scale;
            double dy = (feetY - y) / scale - body.ShoulderV;
            double raise = Math.Atan2(dx, -dy) * 180 / Math.PI;
            if (raise < 15) raise = 15; else if (raise > 165) raise = 165;
            return Q2(raise);
        }

        // ---- Reactions ---------------------------------------------------------------------

        void ReactFace(ref Expression target, ref double rate, ref double gx, ref double gy)
        {
            double t = modeTime;
            switch (reaction)
            {
                case Reaction.Gasp:
                    target = t < 0.9 ? Expression.Shocked : Expression.Sad;
                    rate = t < 0.9 ? 30 : 10;
                    break;
                case Reaction.Headshake:
                    target = Expression.Disapproving;
                    rate = 12;
                    break;
                case Reaction.Huff:
                    target = Expression.Grumpy;
                    rate = 16;
                    gx = 0.5;   // pointedly looking the other way
                    gy = 0.1;
                    return;
                case Reaction.Cower:
                    target = t < 1.3 ? Expression.Scared : Expression.Worried;
                    rate = t < 1.3 ? 30 : 10;
                    break;
                case Reaction.Wince:
                    target = t < 1.0 ? Expression.Wince : Expression.Disapproving;
                    rate = t < 1.0 ? 30 : 10;
                    break;
                case Reaction.Waft:
                    target = Expression.Disgust;
                    rate = 20;
                    break;
                default:
                    target = Expression.Laugh;
                    rate = 20;
                    break;
            }
            LookAt(reactX, feetY - 12 * scale, out gx, out gy);
        }

        void ReactPose(ref Pose pose)
        {
            double t = modeTime;
            switch (reaction)
            {
                case Reaction.Gasp:
                {
                    // Hands fly up beside the face, then sink as the worry sets in.
                    double up = t < 0.15 ? Ease(t / 0.15) : t < 0.9 ? 1 : 1 - Ease(Math.Min(1, (t - 0.9) / 0.4));
                    pose.ArmNear = Q2(30 + 125 * up);
                    pose.ArmFar = -Q2(30 + 125 * up);
                    pose.ForeNear = Q2(58 * up);
                    pose.ForeFar = -Q2(58 * up);
                    pose.Lean = -Math.Round(0.3 * up * 8) / 8;
                    pose.StretchY = 1 + 0.05 * up;
                    break;
                }
                case Reaction.Headshake:
                    HandsOnHips(ref pose);
                    pose.Lean = -0.15;
                    Breathe(ref pose);
                    break;
                case Reaction.Huff:
                    HandsOnHips(ref pose);
                    pose.Lean = -0.25;
                    pose.StretchY = 1.03;
                    Breathe(ref pose);
                    break;
                case Reaction.Chuckle:
                    LaughPose(ref pose, t, false);
                    break;
                case Reaction.Cower:
                {
                    // Drops into a crouch with the hands up beside the face, shaking.
                    double down = Ease(Math.Min(1, t / 0.15));
                    double tremble = t < 1.6 ? (Math.Sin(t * 26) > 0 ? 3 : -3) : 0;
                    pose.StretchY = 1 - 0.15 * down;
                    pose.StretchX = 1 + 0.1 * down;
                    pose.BodyDy = Quantise(0.6 * down);
                    pose.ArmNear = Q2(160 * down) + tremble;
                    pose.ArmFar = -Q2(160 * down) - tremble;
                    pose.ForeNear = Q2(80 * down);
                    pose.ForeFar = -Q2(80 * down);
                    pose.Lean = 0.2;
                    break;
                }
                case Reaction.Wince:
                {
                    // Both hands fly up to the cheeks and stay there.
                    double up = t < 0.15 ? Ease(t / 0.15) : 1;
                    pose.ArmNear = Q2(150 * up);
                    pose.ForeNear = Q2(100 * up);
                    pose.ReachNear = Q2(60 * up);
                    pose.ArmFar = -Q2(150 * up);
                    pose.ForeFar = -Q2(100 * up);
                    pose.ReachFar = Q2(60 * up);
                    pose.Lean = -Math.Round(0.2 * up * 8) / 8;
                    pose.HeadDip = Quantise(0.2 * up);
                    break;
                }
                case Reaction.Waft:
                {
                    // Recoiling, head turned away, one hand fanning in front of the nose.
                    double up = Ease(Math.Min(1, t / 0.2));
                    double fan = t > 0.2 ? Q2(12 * Math.Sin(t * 2 * Math.PI * 3)) : 0;
                    pose.ArmNear = Q2(120 * up);
                    pose.ForeNear = Q2(120 * up) + fan;
                    pose.ReachNear = Q2(50 * up);
                    pose.Lean = -Math.Round(0.25 * up * 8) / 8;
                    pose.HeadTurn = Q2(-25 * up);
                    pose.HeadDip = -Quantise(0.2 * up);
                    break;
                }
                default:
                    LaughPose(ref pose, t, true);
                    break;
            }
        }

        double ReactYaw(out bool snap)
        {
            snap = false;
            switch (reaction)
            {
                case Reaction.Gasp:
                    return 30;
                case Reaction.Headshake:
                {
                    // Two shakes of the head, taken directly.
                    double t = modeTime;
                    if (t < 0.2 || t >= 1.2) return 0;
                    snap = true;
                    return 20 * Math.Sin(2 * Math.PI * (t - 0.2) / 0.5);
                }
                case Reaction.Huff:
                    return 45;
                case Reaction.Chuckle:
                    return 20;
                case Reaction.Cower:
                    return 10;
                case Reaction.Wince:
                    return 10;
                case Reaction.Waft:
                    return 25;
                default:
                    return 35;
            }
        }

        // ---- Small helpers --------------------------------------------------------------

        /// <summary>Smoothstep: 0..1 in, 0..1 out with soft ends.</summary>
        static double Ease(double s)
        {
            if (s <= 0) return 0;
            if (s >= 1) return 1;
            return s * s * (3 - 2 * s);
        }

        /// <summary>Angles stepped to two degrees, so a slow motion is not repainted for every fraction.</summary>
        static double Q2(double degrees)
        {
            return Math.Round(degrees / 2) * 2;
        }

        /// <summary>A phase stepped to sixteenths.</summary>
        static double Q16(double phase)
        {
            return Math.Floor(phase * 16) / 16;
        }
    }
}
