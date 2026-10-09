using System;

namespace IdleWalker.Domain
{
    /// <summary>
    /// The same class, continued: where the eyes go. A Minion standing about
    /// does not just stare at the cursor. Its eyes follow whoever walks past,
    /// meet the eyes of the one it passes, give the viewer a look, slide
    /// sideways, roll, and cross on a cursor parked under its nose - each in
    /// the manner of its temperament. The eyes move first and the head
    /// follows, a big jump of the gaze brings a blink with it, and the pupils
    /// grow for something liked and shrink for a hard stare, as real eyes do.
    /// </summary>
    partial class Walker
    {
        /// <summary>A short piece of eye acting laid over the resting face.</summary>
        enum EyeBeat
        {
            None,
            SideEye,    // eyes slide to one side without the head, hold, come back
            Shifty,     // a quick look one way, then the other: is the coast clear?
            EyeRoll,    // up and over to the other side, ending half-lidded and bored
            Viewer,     // straight out at the viewer, with a look to match
            CrossEyed,  // eyes cross for a moment, then a shake and a blink
            DoubleTake  // a passer-by: look, look away, snap back wide-eyed
        }

        const double GazeRate = 22;        // the eyes reach a new target in a few frames
        const double BeatGazeRate = 34;    // ... faster still when acting

        EyeBeat eyeBeat;
        double beatTime;                   // seconds into the current beat
        double beatTimer;                  // until the next one may start
        double beatDir;                    // which way a side-eye goes: +1 ahead, -1 behind
        bool wasWatching;                  // the eyes were on a passer-by last frame
        double lastGx, lastGy;             // last frame's gaze target, to notice big jumps
        double crossEye;                   // eased convergence, 0..1
        double pupilInterest;              // eased: +1 something liked, -1 a hard stare

        /// <summary>How long this one leaves between blinks compared with the average Minion.</summary>
        double BlinkPace
        {
            get
            {
                switch (personality.Temperament)
                {
                    case Temperament.Timid: return 0.6;        // nervous: blinks a lot
                    case Temperament.Calm: return 1.25;        // unhurried
                    case Temperament.Aggressive: return 1.5;   // stares people down
                    case Temperament.Goofy: return 0.85;
                    case Temperament.Eccentric: return 0.9;
                    default: return 1;
                }
            }
        }

        void ResetEyes()
        {
            eyeBeat = EyeBeat.None;
            beatTimer = Between(1.5, 4);
            wasWatching = false;
            crossEye = 0;
            pupilInterest = 0;
            lastGx = 0.55;
            lastGy = 0;
        }

        /// <summary>
        /// Eyes on the company: a standing Minion follows a walker going by
        /// (movingOnly), a walking one meets the eyes of the one it passes.
        /// True when the gaze has been taken.
        /// </summary>
        bool WatchCompany(bool movingOnly, ref double gx, ref double gy)
        {
            if (company == null || !company.IsGrounded) return false;
            double range = (movingOnly ? 420 : 200) * sizeFactor;
            if (Math.Abs(company.CentreX - cx) > range) return false;
            if (movingOnly && Math.Abs(company.vx) < 20 * sizeFactor) return false;
            LookAt(company.CentreX, company.FeetY - 12 * scale, out gx, out gy);
            return true;
        }

        /// <summary>
        /// Standing about (Rest or Look, head not turned) or strolling with
        /// nothing particular in mind: keeps the current beat going, or starts
        /// one when it is time and nothing else has the eyes. Returns the rate
        /// the gaze should move at.
        /// </summary>
        double EyeBeats(double dt, bool watching, bool strolling, ref Expression target, ref double rate, ref double gx, ref double gy)
        {
            if (eyeBeat == EyeBeat.None)
            {
                beatTimer -= dt;
                if (beatTimer > 0 || watching || moodLeft > 0) return GazeRate;
                StartEyeBeat(strolling);
            }
            beatTime += dt;
            double t = beatTime;
            double side = beatDir;
            switch (eyeBeat)
            {
                case EyeBeat.SideEye:
                {
                    // Slide over, hold, and come back; the head stays put.
                    double hold = personality.Temperament == Temperament.Aggressive ? 1.6 : 1.1;
                    if (t > hold) { EndEyeBeat(); break; }
                    gx = side * 0.95;
                    gy = 0.05;
                    switch (personality.Temperament)
                    {
                        case Temperament.Aggressive: target = Expression.Suspicious; rate = 10; break;
                        case Temperament.Goofy: target = Expression.Smug; rate = 10; break;
                        case Temperament.Friendly: target = Expression.Happy; rate = 10; break;
                        case Temperament.Eccentric: target = Expression.Suspicious; rate = 10; break;
                    }
                    break;
                }

                case EyeBeat.Shifty:
                {
                    // Behind, then ahead, quickly, then back to business.
                    if (t > 0.9) { EndEyeBeat(); break; }
                    gx = t < 0.45 ? -0.95 : 0.95;
                    gy = 0.05;
                    target = Expression.Shifty;
                    rate = 16;
                    break;
                }

                case EyeBeat.EyeRoll:
                {
                    // Up from one side and over the top to the other, then a
                    // bored half-lidded look held off to that side.
                    const double sweep = 0.7;
                    if (t > sweep + 0.9) { EndEyeBeat(); break; }
                    if (t < sweep)
                    {
                        double a = Math.PI * t / sweep;
                        gx = -side * 0.85 * Math.Cos(a);
                        gy = 0.15 + 0.8 * Math.Sin(a);
                        target = Expression.Disapproving;
                        rate = 14;
                    }
                    else
                    {
                        gx = side * 0.7;
                        gy = 0.1;
                        target = Expression.Bored;
                        rate = 10;
                    }
                    break;
                }

                case EyeBeat.Viewer:
                {
                    // A look straight out at you (the head comes round too).
                    double hold = personality.Warm ? 1.5 : 1.1;
                    if (t > hold) { EndEyeBeat(); break; }
                    gx = 0;
                    gy = 0;
                    switch (personality.Temperament)
                    {
                        case Temperament.Friendly: target = Expression.Happy; break;
                        case Temperament.Goofy: target = Expression.Silly; break;
                        case Temperament.Aggressive: target = Expression.Stern; break;
                        case Temperament.Eccentric: target = Expression.Gormless; break;
                        default: target = Expression.Knowing; break;
                    }
                    rate = 10;
                    break;
                }

                case EyeBeat.CrossEyed:
                {
                    // Eyes cross and go a little vacant, then a double blink shakes it off.
                    if (t > 1.0) { EndEyeBeat(); break; }
                    if (t < 0.7)
                    {
                        target = Expression.Dopey;
                        rate = 12;
                    }
                    else if (blinkLeft <= 0 && !blinkAgain && t < 0.75)
                    {
                        blinkLeft = BlinkSeconds;
                        blinkAgain = true;
                    }
                    break;
                }

                case EyeBeat.DoubleTake:
                {
                    // Look at the passer-by, back to the cursor, then snap
                    // back wide-eyed; after that the ordinary following goes on.
                    if (t > 1.3 || !watching) { EndEyeBeat(); break; }
                    if (t >= 0.35 && t < 0.6) LookAt(cursorX, cursorY, out gx, out gy);
                    if (t >= 0.6)
                    {
                        target = Expression.Surprised;
                        rate = 30;
                    }
                    break;
                }
            }
            return eyeBeat == EyeBeat.None ? GazeRate : BeatGazeRate;
        }

        // Each temperament has its own repertoire: the aggressive ones
        // side-eye and roll their eyes, the timid one checks over both
        // shoulders, the goofy one goes cross-eyed, the friendly ones look
        // at you, the calm ones mostly side-eye. On the move there is only
        // time for a glance at you - or, for the timid one, over the shoulder.
        void StartEyeBeat(bool strolling)
        {
            double r = rng.NextDouble();
            beatTime = 0;
            if (strolling)
            {
                eyeBeat = personality.Temperament == Temperament.Timid && r < 0.6 ? EyeBeat.Shifty : EyeBeat.Viewer;
                beatDir = 1;
                return;
            }
            switch (personality.Temperament)
            {
                case Temperament.Aggressive:
                    eyeBeat = r < 0.55 ? EyeBeat.SideEye : r < 0.85 ? EyeBeat.EyeRoll : EyeBeat.Viewer;
                    break;
                case Temperament.Timid:
                    eyeBeat = r < 0.75 ? EyeBeat.Shifty : EyeBeat.SideEye;
                    break;
                case Temperament.Goofy:
                    eyeBeat = r < 0.3 ? EyeBeat.CrossEyed : r < 0.65 ? EyeBeat.Viewer : EyeBeat.SideEye;
                    break;
                case Temperament.Friendly:
                    eyeBeat = r < 0.55 ? EyeBeat.Viewer : EyeBeat.SideEye;
                    break;
                case Temperament.Eccentric:
                    eyeBeat = r < 0.4 ? EyeBeat.Shifty : r < 0.7 ? EyeBeat.SideEye : EyeBeat.Viewer;
                    break;
                default:
                    eyeBeat = r < 0.6 ? EyeBeat.SideEye : r < 0.9 ? EyeBeat.Viewer : EyeBeat.EyeRoll;
                    break;
            }
            // A side-eye goes towards the company if there is any, else away from the cursor.
            beatDir = company != null ? Math.Sign((company.CentreX - cx) * facing)
                                      : -Math.Sign((cursorX - cx) * facing);
            if (beatDir == 0) beatDir = 1;
        }

        void EndEyeBeat()
        {
            if (eyeBeat == EyeBeat.None) return;
            bool onTheMove = mode == Mode.Walk;
            eyeBeat = EyeBeat.None;
            beatTimer = (onTheMove ? Between(5, 11) : Between(2.5, 6)) * (personality.Temperament == Temperament.Calm ? 1.3 : 1);
        }

        /// <summary>A passer-by has just come into view: the warm ones sometimes do a double take.</summary>
        void NoticePasser(bool watching)
        {
            if (watching && !wasWatching && eyeBeat == EyeBeat.None && personality.Warm &&
                beatTimer < 2.5 && rng.NextDouble() < 0.6)
            {
                eyeBeat = EyeBeat.DoubleTake;
                beatTime = 0;
            }
            wasWatching = watching;
        }

        /// <summary>Real eyes blink when they jump a long way; a glance dart is too small to count.</summary>
        void GazeBlink(double gx, double gy)
        {
            if (eyeBeat == EyeBeat.None && blinkLeft <= 0 && face.Lid < 0.9 &&
                Math.Abs(gx - lastGx) + Math.Abs(gy - lastGy) > 0.75)
            {
                blinkLeft = BlinkSeconds;
                if (blinkTimer < 1.2) blinkTimer = 1.2;
            }
            lastGx = gx;
            lastGy = gy;
        }

        // Eases the eyes crossing (only ever on purpose: the goofy one's
        // beat) and the pupils' interest in what they are looking at. Both
        // eyes always point the same way otherwise.
        void StepEyeExtras(double dt, bool atCursor, bool watching)
        {
            double cross = eyeBeat == EyeBeat.CrossEyed && beatTime < 0.7 ? 1 : 0;
            crossEye = Settle(crossEye, cross, 1 - Math.Exp(-dt * 10));

            double interest = 0;
            if (personality.Temperament == Temperament.Aggressive)
            {
                if (watching || eyeBeat == EyeBeat.SideEye) interest = -1;
            }
            else if (watching && company != null && company.Personality.Warm && personality.Warm)
            {
                interest = 1;
            }
            else if (atCursor && Math.Abs(cursorX - cx) < 160 * sizeFactor)
            {
                interest = 0.7;
            }
            pupilInterest = Settle(pupilInterest, interest, 1 - Math.Exp(-dt * 4));
        }
    }
}
