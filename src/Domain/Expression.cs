using System;

namespace IdleWalker.Domain
{
    /// <summary>
    /// A face, as numbers: how far the lids are down, where the brows sit,
    /// how the mouth is shaped. Because it is all continuous, one expression
    /// can melt into another (<see cref="Towards"/>) instead of snapping.
    /// </summary>
    struct Expression
    {
        public double Lid;        // upper lids, 0 open .. 1 shut
        public double Squint;     // lower lids pushed up, 0 .. 1
        public double Wink;       // upper lid of the eye nearest the viewer only, 0 .. 1
        public double BrowRaise;  // brows lifted (+) or pressed down (-), -1 .. 1
        public double BrowAngle;  // inner ends down (+, angry) or up (-, sad), -1 .. 1
        public double BrowAsym;   // one brow raised, 0 .. 1
        public double PupilSize;  // 1 normal; smaller when frightened
        public double PupilDx;    // extra pupil offset, units, + towards where it faces
        public double PupilDy;    // extra pupil offset, units, + up
        public double CrossEye;   // pupils drawn towards each other, 0 .. 1
        public double MouthCurve; // corners up (+, smile) or down (-, frown), -1 .. 1
        public double MouthOpen;  // 0 shut .. 1 wide open
        public double MouthWidth; // half-width in units
        public double MouthDx;    // mouth shifted sideways (a smirk), units
        public double Teeth;      // show teeth when open, 0 .. 1
        public double Wobble;     // wavy, trembling lips, 0 .. 1
        public double Tongue;     // tongue showing in (or hanging out of) an open mouth, 0 .. 1

        public Expression(double lid = 0, double squint = 0, double wink = 0,
                          double browRaise = 0, double browAngle = 0, double browAsym = 0,
                          double pupilSize = 1, double pupilDx = 0, double pupilDy = 0, double crossEye = 0,
                          double mouthCurve = 0, double mouthOpen = 0, double mouthWidth = 1.7, double mouthDx = 0,
                          double teeth = 0, double wobble = 0, double tongue = 0)
        {
            Lid = lid;
            Squint = squint;
            Wink = wink;
            BrowRaise = browRaise;
            BrowAngle = browAngle;
            BrowAsym = browAsym;
            PupilSize = pupilSize;
            PupilDx = pupilDx;
            PupilDy = pupilDy;
            CrossEye = crossEye;
            MouthCurve = mouthCurve;
            MouthOpen = mouthOpen;
            MouthWidth = mouthWidth;
            MouthDx = mouthDx;
            Teeth = teeth;
            Wobble = wobble;
            Tongue = tongue;
        }

        // ---- Resting faces ---------------------------------------------------------

        /// <summary>Minding its own business.</summary>
        public static readonly Expression Neutral = new Expression(mouthCurve: 0.15);

        /// <summary>A contented smile.</summary>
        public static readonly Expression Happy = new Expression(lid: 0.08, mouthCurve: 0.85, mouthWidth: 1.9);

        /// <summary>A wide, open-mouthed smile that never quite goes away.</summary>
        public static readonly Expression Cheerful = new Expression(lid: 0.05, browRaise: 0.15, mouthCurve: 0.9, mouthOpen: 0.2, mouthWidth: 2.0, teeth: 0.6);

        /// <summary>Brows down, lids heavy, mouth turned down.</summary>
        public static readonly Expression Grumpy = new Expression(lid: 0.3, browAngle: 0.8, mouthCurve: -0.6);

        /// <summary>A hard stare and a flat mouth: not looking for friends.</summary>
        public static readonly Expression Stern = new Expression(lid: 0.25, browAngle: 0.6, mouthCurve: -0.25, mouthWidth: 1.6);

        // ---- Pleased ---------------------------------------------------------------

        /// <summary>A toothy grin.</summary>
        public static readonly Expression Grin = new Expression(squint: 0.2, mouthCurve: 0.9, mouthOpen: 0.45, mouthWidth: 2.1, teeth: 1);

        /// <summary>Head back, mouth wide, eyes creased.</summary>
        public static readonly Expression Laugh = new Expression(lid: 0.3, squint: 0.5, browRaise: 0.4, mouthCurve: 1, mouthOpen: 0.85, mouthWidth: 2.2, teeth: 1);

        /// <summary>Eyes creased and a smaller, sillier laugh.</summary>
        public static readonly Expression Giggle = new Expression(lid: 0.5, squint: 0.4, mouthCurve: 0.9, mouthOpen: 0.35, mouthWidth: 1.8, teeth: 0.8);

        /// <summary>Eyes wide and mouth open with glee, mid-air.</summary>
        public static readonly Expression Excited = new Expression(browRaise: 0.6, mouthCurve: 0.8, mouthOpen: 0.6, mouthWidth: 1.9, teeth: 0.5);

        /// <summary>A wink and a smile.</summary>
        public static readonly Expression Winking = new Expression(wink: 1, mouthCurve: 0.8, mouthWidth: 1.9);

        /// <summary>One brow up, a smirk to the side.</summary>
        public static readonly Expression Smug = new Expression(lid: 0.35, browAsym: 1, mouthCurve: 0.55, mouthWidth: 1.3, mouthDx: 0.7);

        /// <summary>Chin up, lids lowered, a closed-lipped smile: admiring itself.</summary>
        public static readonly Expression Proud = new Expression(lid: 0.3, squint: 0.2, browRaise: 0.1, browAngle: 0.2, mouthCurve: 0.6, mouthWidth: 1.6);

        /// <summary>Narrowed eyes, one brow up and a lopsided grin: up to something.</summary>
        public static readonly Expression Mischievous = new Expression(lid: 0.3, squint: 0.35, browAngle: 0.5, browAsym: 0.6, mouthCurve: 0.7, mouthOpen: 0.2, mouthWidth: 1.6, mouthDx: 0.5, teeth: 0.3);

        /// <summary>Squinting, brows down and tongue out: blowing a raspberry.</summary>
        public static readonly Expression Raspberry = new Expression(squint: 0.5, browAngle: 0.6, mouthCurve: 0.3, mouthOpen: 0.4, mouthWidth: 1.3, tongue: 1);

        // ---- Thoughtful and tired ----------------------------------------------------

        /// <summary>One brow raised, lips slightly parted.</summary>
        public static readonly Expression Curious = new Expression(browAsym: 0.8, mouthCurve: 0.2, mouthOpen: 0.15, mouthWidth: 1.2);

        /// <summary>Eyes rolled up and aside, mouth pulled to one side: working something out.</summary>
        public static readonly Expression Thinking = new Expression(browRaise: 0.2, browAsym: 0.6, pupilDx: 0.3, pupilDy: 0.35, mouthCurve: -0.1, mouthWidth: 1.1, mouthDx: -0.6);

        /// <summary>Lips pursed in a small "o", eyes rolled innocently upward.</summary>
        public static readonly Expression Whistling = new Expression(lid: 0.55, browRaise: 0.35, pupilDx: 0.2, pupilDy: 0.45, mouthOpen: 0.4, mouthWidth: 0.55);

        /// <summary>Squinting in concentration before a jump.</summary>
        public static readonly Expression Focused = new Expression(lid: 0.25, squint: 0.4, browAngle: 0.4, mouthWidth: 1.4);

        /// <summary>Half-lidded, looking off to the side: nothing worth watching.</summary>
        public static readonly Expression Bored = new Expression(lid: 0.5, pupilDx: 0.25, pupilDy: 0.2, mouthCurve: -0.15, mouthWidth: 1.3);

        /// <summary>Lids drooping, gaze sinking.</summary>
        public static readonly Expression Sleepy = new Expression(lid: 0.7, squint: 0.15, pupilDy: -0.3, mouthCurve: 0.1, mouthWidth: 1.0);

        /// <summary>Eyes screwed shut, brows up, mouth as wide as it goes.</summary>
        public static readonly Expression Yawn = new Expression(lid: 0.95, browRaise: 0.5, browAngle: -0.3, mouthCurve: -0.15, mouthOpen: 1, mouthWidth: 1.3);

        /// <summary>Eyes shut, mouth slack, a hint of a smile.</summary>
        public static readonly Expression Asleep = new Expression(lid: 1, mouthCurve: 0.2, mouthOpen: 0.12, mouthWidth: 1.0);

        /// <summary>Out of breath: lids heavy, mouth open, tongue showing.</summary>
        public static readonly Expression Panting = new Expression(lid: 0.5, squint: 0.2, browAngle: -0.3, mouthCurve: 0.2, mouthOpen: 0.5, mouthWidth: 1.5, tongue: 0.5);

        // ---- Alarmed ---------------------------------------------------------------

        /// <summary>Eyes wide, brows up, mouth a small "o".</summary>
        public static readonly Expression Surprised = new Expression(browRaise: 0.9, pupilSize: 0.75, mouthOpen: 0.65, mouthWidth: 1.0);

        /// <summary>Brows as high as they go, pupils tiny, jaw dropped.</summary>
        public static readonly Expression Shocked = new Expression(browRaise: 1, pupilSize: 0.55, mouthCurve: -0.2, mouthOpen: 1, mouthWidth: 1.2);

        /// <summary>Pupils shrunk, brows up and worried, lips trembling.</summary>
        public static readonly Expression Scared = new Expression(browRaise: 0.7, browAngle: -0.6, pupilSize: 0.6, mouthCurve: -0.3, mouthOpen: 0.45, mouthWidth: 1.6, wobble: 1);

        /// <summary>Brows up, eyes closing, mouth open: the breath before a sneeze.</summary>
        public static readonly Expression Inhale = new Expression(lid: 0.6, browRaise: 0.8, mouthCurve: -0.1, mouthOpen: 0.7, mouthWidth: 1.2);

        /// <summary>Eyes shut tight, mouth thrown open: the sneeze itself.</summary>
        public static readonly Expression Sneeze = new Expression(lid: 1, browRaise: 0.3, mouthOpen: 0.9, mouthWidth: 1.6, teeth: 0.5);

        /// <summary>Cross-eyed and reeling.</summary>
        public static readonly Expression Dizzy = new Expression(lid: 0.35, pupilDy: 0.3, crossEye: 1, mouthCurve: -0.2, mouthOpen: 0.15, wobble: 1);

        // ---- Cross and sad ---------------------------------------------------------

        /// <summary>Teeth gritted, eyes narrowed: on the attack.</summary>
        public static readonly Expression Determined = new Expression(lid: 0.4, browAngle: 0.9, mouthCurve: -0.3, mouthOpen: 0.2, mouthWidth: 1.9, teeth: 1);

        /// <summary>Brows slammed down, teeth bared: a tantrum.</summary>
        public static readonly Expression Furious = new Expression(lid: 0.2, browAngle: 1, mouthCurve: -0.6, mouthOpen: 0.3, mouthWidth: 1.9, teeth: 1);

        /// <summary>Lids half down, mouth turned down: not impressed.</summary>
        public static readonly Expression Disapproving = new Expression(lid: 0.45, browAngle: 0.3, mouthCurve: -0.4, mouthWidth: 1.5);

        /// <summary>Brows up in the middle, mouth down.</summary>
        public static readonly Expression Sad = new Expression(lid: 0.35, browAngle: -0.8, mouthCurve: -0.7, mouthWidth: 1.4);

        // ---- Blending ------------------------------------------------------------

        /// <summary>Moves fraction k of the way from a to b, snapping when close.</summary>
        public static Expression Towards(Expression a, Expression b, double k)
        {
            Expression r;
            r.Lid = Step(a.Lid, b.Lid, k);
            r.Squint = Step(a.Squint, b.Squint, k);
            r.Wink = Step(a.Wink, b.Wink, k);
            r.BrowRaise = Step(a.BrowRaise, b.BrowRaise, k);
            r.BrowAngle = Step(a.BrowAngle, b.BrowAngle, k);
            r.BrowAsym = Step(a.BrowAsym, b.BrowAsym, k);
            r.PupilSize = Step(a.PupilSize, b.PupilSize, k);
            r.PupilDx = Step(a.PupilDx, b.PupilDx, k);
            r.PupilDy = Step(a.PupilDy, b.PupilDy, k);
            r.CrossEye = Step(a.CrossEye, b.CrossEye, k);
            r.MouthCurve = Step(a.MouthCurve, b.MouthCurve, k);
            r.MouthOpen = Step(a.MouthOpen, b.MouthOpen, k);
            r.MouthWidth = Step(a.MouthWidth, b.MouthWidth, k);
            r.MouthDx = Step(a.MouthDx, b.MouthDx, k);
            r.Teeth = Step(a.Teeth, b.Teeth, k);
            r.Wobble = Step(a.Wobble, b.Wobble, k);
            r.Tongue = Step(a.Tongue, b.Tongue, k);
            return r;
        }

        static double Step(double a, double b, double k)
        {
            double next = a + (b - a) * k;
            return Math.Abs(b - next) < 0.004 ? b : next;
        }

        public bool SameAs(Expression o)
        {
            return Lid == o.Lid && Squint == o.Squint && Wink == o.Wink &&
                   BrowRaise == o.BrowRaise && BrowAngle == o.BrowAngle && BrowAsym == o.BrowAsym &&
                   PupilSize == o.PupilSize && PupilDx == o.PupilDx && PupilDy == o.PupilDy && CrossEye == o.CrossEye &&
                   MouthCurve == o.MouthCurve && MouthOpen == o.MouthOpen && MouthWidth == o.MouthWidth &&
                   MouthDx == o.MouthDx && Teeth == o.Teeth && Wobble == o.Wobble && Tongue == o.Tongue;
        }
    }
}
