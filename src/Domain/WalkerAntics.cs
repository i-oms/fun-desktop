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
        const double ShoulderV = 10.2;   // height of the shoulders in units, for aiming an arm (matches the painter)
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

                case Antic.Dance: anticSeconds = 2.6; broadcast = Antic.Dance; break;
                case Antic.Twirl: anticSeconds = 1.7; break;
                case Antic.Yawn: anticSeconds = 2.1; broadcast = Antic.Yawn; break;
                case Antic.Doze: anticSeconds = 2.1 + Between(2.5, 4.5); broadcast = Antic.Yawn; break;
                case Antic.Scratch: anticSeconds = 2.0; break;
                case Antic.Whistle: anticSeconds = 2.7; break;
                case Antic.Sneeze: anticSeconds = 1.6; break;
                case Antic.Box: anticSeconds = 2.2; break;
                case Antic.Stomp: anticSeconds = 1.1; break;
                case Antic.Flex: anticSeconds = 2.4; break;
                case Antic.Joy: anticSeconds = 1; break;   // it jumps on the first step
                case Antic.Hiccups: anticSeconds = 2.6; break;
                case Antic.Shrug: anticSeconds = 1.3; break;
                case Antic.Trip: anticSeconds = 1.8; break;
                case Antic.Boo: anticSeconds = 1.6; break;
            }
            Stop(Mode.Antic);
        }

        // Stops, turning round first if the target is behind.
        void StopFacing(double targetX, Mode then)
        {
            bool behind = (targetX - cx) * facing < -2 * HalfBodyCells * scale;
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
                        EmitDust(cx + facing * 7 * scale, feetY - 8 * scale, 3, 0.9);
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
                    walkPhase += Math.Abs(vx) * dt / (StrideCells * scale);
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
                    if (goalTarget != null) LookAt(goalTarget.CentreX, goalTarget.FeetY - 12 * scale, out gx, out gy);
                    else gx = 0.7;
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
                default: return 0;   // yawning, dozing, whistling, taunting, laughing, hiccuping: facing you
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
            }
        }

        // A jig on the spot: bouncing on the beat, feet stepping, arms
        // pumping up and down in turn, a note floating off every two beats.
        void DancePose(ref Pose pose, double t)
        {
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
            double dy = (feetY - y) / scale - ShoulderV;
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
