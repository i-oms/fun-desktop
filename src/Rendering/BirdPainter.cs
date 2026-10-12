using System;
using IdleWalker.Domain;

namespace IdleWalker.Rendering
{
    /// <summary>
    /// Paints an Angry Bird into a <see cref="Surface"/> the way the Minions
    /// are painted: as lit solids with anti-aliased edges, composited back to
    /// front (see <see cref="SolidPainter"/>). Faithful to the classic
    /// 2009-2013 game designs: a LIMBLESS ball of feathers - no wings, no
    /// legs, no feet - with a lighter chest on most of them, two big front
    /// eyes, a beak that opens, crest feathers and tail feathers. They hop
    /// and roll instead of walking, so all motion reads through squash,
    /// stretch, lean and bobbing.
    ///
    /// What tells them apart, kind by kind (the classic art, not the films):
    ///  Red      round, red; peach chest; two red crest feathers; three black
    ///           tail feathers; thick black brows.
    ///  Blues    small (three fifths of Red), light blue; no chest patch, no
    ///           brows; two blue crest feathers; two black tail feathers;
    ///           orange lower eyelids.
    ///  Chuck    a yellow triangle, rounded corners, face set high; white
    ///           chest; red brows; four black crest feathers swept back;
    ///           three tail feathers angled up; small long beak.
    ///  Bomb     bigger than Red, black; dark grey chest; red brows; a grey
    ///           fuse with a yellow tip instead of a crest.
    ///  Matilda  a tall white egg; large oval beak; yellow cheek spots; three
    ///           short black crest and tail feathers; lashes.
    ///  Hal      green; pure white chest; a huge orange beak; three black
    ///           crest feathers; four spiky tail feathers; medium brows.
    ///  Terence  half again Red's size, dark crimson; darker red crest; red-
    ///           violet spots about the face; two forehead wrinkles; a
    ///           smaller face for his size.
    ///  Bubbles  the smallest, orange; dark brown chest and eye rings; maroon
    ///           brows; two long black round-ended crest feathers of
    ///           different lengths.
    ///  Stella   pink; light pink chest; three pink crest feathers with light
    ///           round tips; large light-blue eyes with lashes; small wide
    ///           beak.
    ///  Silver   squarish grey (Angry Birds 2); long dark head feathers
    ///           pointing back; a red beak tip pointing down; one pointed
    ///           lash per eye; pale chest.
    ///
    /// Model space is the Minions': u across in units, positive the way it
    /// faces; v up from the soles (its round bottom). Red is
    /// <see cref="Height"/> units tall; the others are scaled from him by
    /// <see cref="SizeOf"/> when drawn.
    /// </summary>
    sealed class BirdPainter : SolidPainter
    {
        public const double Height = 14;

        enum Shape { Round, Triangle, Egg, Square }

        // Where the features sit on Red, in units. Taller kinds lift the face.
        const double BodyBottom = 1.2;
        const double BellyV = 5.3, BellyRy = 2.9, BellyOff = 1.6;
        const double EyeV = 9.7, EyeAngle = 17, IrisR = 0.42;
        const double BrowV = 10.9;
        const double BeakV = 7.6;
        const double CrestV = 11.1;

        readonly Pose pose;
        readonly Expression face;
        readonly BirdKind kind;
        readonly Shape shape;
        readonly double yaw, sinY, cosY;
        readonly double lean;
        readonly double grow;             // 1, or more when blown up (Bubbles)

        // The body for this kind.
        readonly double bodyV, bodyRx, bodyRy;          // round, square and egg: an ellipse (egg: narrower at the top)
        readonly double eggTaper;                       // egg: how much the top narrows, 0..1
        readonly double triBase, triTop, triV0, triV1, triRound;   // triangle: half-widths and heights of the base and apex, corner rounding
        readonly double faceLift;                       // the face sits this much higher than on Red

        // Colours for this kind.
        readonly double bodyR, bodyG, bodyB;
        readonly double bellyR, bellyG, bellyB;
        readonly double beakR, beakG, beakB;
        readonly double beakR2, beakG2, beakB2;
        readonly double tailR, tailG, tailB;
        readonly double crestR, crestG, crestB;
        readonly double browR, browG, browB;
        readonly double irisR, irisG, irisB;

        // Shape numbers for this kind.
        readonly bool hasBelly;
        readonly double browWeight;       // thickness of the brows; 0 for none
        readonly double eyeRx, eyeRy;
        readonly double beakScale, beakLong;
        readonly int tailCount;
        readonly double tailLift;         // how far the tail feathers angle upwards

        // Worked out once per frame.
        readonly double eyeV, browV, beakV, crestV;
        readonly double bellyU, bellyRx;
        readonly double[] eyeAlpha = new double[2], eyeU = new double[2], eyeK = new double[2], eyeVis = new double[2];
        readonly int firstEye;
        readonly bool decorated;
        readonly double topV;
        readonly double beakU, beakW;
        readonly double crestU;

        BirdPainter(Pose pose, Look look, bool faceLeft, double px)
            : base(faceLeft, px, 5.0)
        {
            this.pose = pose;
            face = pose.Face;
            kind = look.Kind;
            lean = pose.Lean;
            double puff = pose.PuffUp < 0 ? 0 : pose.PuffUp > 1 ? 1 : pose.PuffUp;
            grow = 1 + 0.35 * puff;
            yaw = Wrap(pose.Yaw) * Deg;
            sinY = Math.Sin(yaw);
            cosY = Math.Cos(yaw);

            // ---- The kind: body, colours, features --------------------------------
            shape = Shape.Round;
            bodyRx = 4.9; bodyRy = 5.1; eggTaper = 0;
            triBase = 0; triTop = 0; triV0 = 0; triV1 = 0; triRound = 0;
            faceLift = 0;
            hasBelly = true;
            browWeight = 1;
            eyeRx = 1.0; eyeRy = 0.92;
            beakScale = 1; beakLong = 1;
            tailCount = 3; tailLift = 0;
            irisR = 0.08; irisG = 0.07; irisB = 0.08;
            switch (kind)
            {
                case BirdKind.Blues:
                    Rgb(0.36, 0.64, 0.94, out bodyR, out bodyG, out bodyB);
                    Rgb(0.36, 0.64, 0.94, out bellyR, out bellyG, out bellyB);
                    hasBelly = false;
                    Rgb(0.98, 0.64, 0.12, out beakR, out beakG, out beakB);
                    Rgb(0.90, 0.50, 0.08, out beakR2, out beakG2, out beakB2);
                    Rgb(0.10, 0.10, 0.12, out tailR, out tailG, out tailB);
                    Rgb(0.22, 0.46, 0.84, out crestR, out crestG, out crestB);
                    Rgb(0, 0, 0, out browR, out browG, out browB);
                    browWeight = 0;
                    tailCount = 2;
                    eyeRx = 1.05; eyeRy = 1.0;
                    break;
                case BirdKind.Chuck:
                    shape = Shape.Triangle;
                    triBase = 4.9; triTop = 0.55; triV0 = 0.9; triV1 = 12.9; triRound = 1.1;
                    faceLift = 0.7;
                    Rgb(0.98, 0.80, 0.12, out bodyR, out bodyG, out bodyB);
                    Rgb(0.98, 0.97, 0.93, out bellyR, out bellyG, out bellyB);
                    Rgb(0.96, 0.56, 0.10, out beakR, out beakG, out beakB);
                    Rgb(0.88, 0.45, 0.07, out beakR2, out beakG2, out beakB2);
                    Rgb(0.10, 0.09, 0.09, out tailR, out tailG, out tailB);
                    Rgb(0.10, 0.09, 0.09, out crestR, out crestG, out crestB);
                    Rgb(0.80, 0.12, 0.10, out browR, out browG, out browB);
                    eyeRx = 0.88; eyeRy = 0.92;
                    beakScale = 0.9; beakLong = 1.35;
                    tailLift = 1.1;
                    break;
                case BirdKind.Bomb:
                    bodyRx = 5.0; bodyRy = 5.2;
                    Rgb(0.12, 0.12, 0.14, out bodyR, out bodyG, out bodyB);
                    Rgb(0.42, 0.42, 0.45, out bellyR, out bellyG, out bellyB);
                    Rgb(0.99, 0.78, 0.12, out beakR, out beakG, out beakB);
                    Rgb(0.93, 0.60, 0.08, out beakR2, out beakG2, out beakB2);
                    Rgb(0.26, 0.26, 0.30, out tailR, out tailG, out tailB);
                    Rgb(0.45, 0.40, 0.34, out crestR, out crestG, out crestB);
                    Rgb(0.80, 0.12, 0.10, out browR, out browG, out browB);
                    break;
                case BirdKind.Matilda:
                    shape = Shape.Egg;
                    bodyRx = 4.4; bodyRy = 5.7; eggTaper = 0.42;
                    faceLift = 0.9;
                    Rgb(0.96, 0.96, 0.95, out bodyR, out bodyG, out bodyB);
                    Rgb(0.96, 0.96, 0.95, out bellyR, out bellyG, out bellyB);
                    hasBelly = false;
                    Rgb(0.99, 0.74, 0.14, out beakR, out beakG, out beakB);
                    Rgb(0.93, 0.58, 0.10, out beakR2, out beakG2, out beakB2);
                    Rgb(0.12, 0.11, 0.11, out tailR, out tailG, out tailB);
                    Rgb(0.12, 0.11, 0.11, out crestR, out crestG, out crestB);
                    Rgb(0.12, 0.10, 0.10, out browR, out browG, out browB);
                    browWeight = 0.55;
                    eyeRx = 0.82; eyeRy = 0.84;
                    beakScale = 1.25;
                    break;
                case BirdKind.Hal:
                    bodyRx = 4.8; bodyRy = 5.0;
                    Rgb(0.30, 0.64, 0.30, out bodyR, out bodyG, out bodyB);
                    Rgb(0.97, 0.97, 0.95, out bellyR, out bellyG, out bellyB);
                    Rgb(0.97, 0.55, 0.10, out beakR, out beakG, out beakB);
                    Rgb(0.88, 0.42, 0.07, out beakR2, out beakG2, out beakB2);
                    Rgb(0.08, 0.12, 0.08, out tailR, out tailG, out tailB);
                    Rgb(0.08, 0.12, 0.08, out crestR, out crestG, out crestB);
                    Rgb(0.07, 0.07, 0.07, out browR, out browG, out browB);
                    browWeight = 0.75;
                    beakScale = 1.3; beakLong = 1.9;
                    tailCount = 4; tailLift = 0.9;
                    break;
                case BirdKind.Terence:
                    bodyRx = 5.0; bodyRy = 5.2;
                    Rgb(0.62, 0.10, 0.10, out bodyR, out bodyG, out bodyB);
                    Rgb(0.84, 0.66, 0.55, out bellyR, out bellyG, out bellyB);
                    Rgb(0.99, 0.78, 0.12, out beakR, out beakG, out beakB);
                    Rgb(0.93, 0.60, 0.08, out beakR2, out beakG2, out beakB2);
                    Rgb(0.10, 0.07, 0.07, out tailR, out tailG, out tailB);
                    Rgb(0.40, 0.05, 0.05, out crestR, out crestG, out crestB);
                    Rgb(0.07, 0.05, 0.05, out browR, out browG, out browB);
                    eyeRx = 0.9; eyeRy = 0.84;
                    beakScale = 0.9;
                    break;
                case BirdKind.Bubbles:
                    bodyRx = 4.9; bodyRy = 5.0;
                    Rgb(0.97, 0.55, 0.10, out bodyR, out bodyG, out bodyB);
                    Rgb(0.46, 0.23, 0.08, out bellyR, out bellyG, out bellyB);
                    Rgb(0.99, 0.80, 0.16, out beakR, out beakG, out beakB);
                    Rgb(0.93, 0.62, 0.10, out beakR2, out beakG2, out beakB2);
                    Rgb(0.10, 0.08, 0.07, out tailR, out tailG, out tailB);
                    Rgb(0.10, 0.08, 0.07, out crestR, out crestG, out crestB);
                    Rgb(0.50, 0.10, 0.10, out browR, out browG, out browB);
                    eyeRx = 1.05; eyeRy = 1.0;
                    beakScale = 0.85;
                    break;
                case BirdKind.Stella:
                    bodyRx = 4.8; bodyRy = 5.0;
                    Rgb(0.97, 0.52, 0.70, out bodyR, out bodyG, out bodyB);
                    Rgb(0.99, 0.84, 0.90, out bellyR, out bellyG, out bellyB);
                    Rgb(0.99, 0.80, 0.20, out beakR, out beakG, out beakB);
                    Rgb(0.93, 0.62, 0.12, out beakR2, out beakG2, out beakB2);
                    Rgb(0.12, 0.09, 0.11, out tailR, out tailG, out tailB);
                    Rgb(0.94, 0.42, 0.62, out crestR, out crestG, out crestB);
                    Rgb(0.30, 0.14, 0.20, out browR, out browG, out browB);
                    browWeight = 0.45;
                    eyeRx = 1.18; eyeRy = 1.12;
                    Rgb(0.46, 0.74, 0.96, out irisR, out irisG, out irisB);
                    beakScale = 0.8; beakLong = 1.25;
                    break;
                case BirdKind.Silver:
                    shape = Shape.Square;
                    bodyRx = 4.8; bodyRy = 5.0;
                    Rgb(0.74, 0.75, 0.78, out bodyR, out bodyG, out bodyB);
                    Rgb(0.90, 0.90, 0.92, out bellyR, out bellyG, out bellyB);
                    Rgb(0.99, 0.78, 0.12, out beakR, out beakG, out beakB);
                    Rgb(0.93, 0.60, 0.08, out beakR2, out beakG2, out beakB2);
                    Rgb(0.30, 0.32, 0.38, out tailR, out tailG, out tailB);
                    Rgb(0.30, 0.32, 0.40, out crestR, out crestG, out crestB);
                    Rgb(0.20, 0.20, 0.24, out browR, out browG, out browB);
                    browWeight = 0.6;
                    eyeRx = 1.05; eyeRy = 1.0;
                    break;
                default:    // Red
                    Rgb(0.84, 0.15, 0.12, out bodyR, out bodyG, out bodyB);
                    Rgb(0.94, 0.82, 0.68, out bellyR, out bellyG, out bellyB);
                    Rgb(0.99, 0.78, 0.12, out beakR, out beakG, out beakB);
                    Rgb(0.93, 0.60, 0.08, out beakR2, out beakG2, out beakB2);
                    Rgb(0.10, 0.08, 0.08, out tailR, out tailG, out tailB);
                    Rgb(0.60, 0.09, 0.08, out crestR, out crestG, out crestB);
                    Rgb(0.07, 0.06, 0.06, out browR, out browG, out browB);
                    break;
            }
            bodyV = shape == Shape.Triangle ? (triV0 + triV1) / 2 : BodyBottom + bodyRy;
            eyeV = EyeV + faceLift;
            browV = BrowV + faceLift;
            beakV = BeakV + faceLift;
            crestV = (shape == Shape.Triangle ? triV1 - triRound * 0.6 : shape == Shape.Egg ? bodyV + bodyRy - 0.6 : CrestV);

            // Features narrow with the local body width so nothing floats off
            // Chuck's triangle or Matilda's egg.
            double spread = Clamp01(HalfWidthAt(eyeV) / 2.6, 0.35, 1);
            PlaceEye(0, -EyeAngle * Deg, spread);
            PlaceEye(1, EyeAngle * Deg, spread);
            firstEye = Math.Cos(eyeAlpha[0]) < Math.Cos(eyeAlpha[1]) ? 0 : 1;

            double beakHalf = HalfWidthAt(beakV);
            beakU = (beakHalf + 0.4) * sinY + 0.5 * (beakScale * beakLong - 1) * sinY;
            beakW = (0.95 + face.MouthWidth * 0.12) * (0.5 + 0.5 * Math.Abs(sinY)) * beakScale * beakLong
                    * Clamp01(beakHalf / 2.8, 0.55, 1);

            double bellyHalf = HalfWidthAt(BellyV);
            bellyU = Math.Min(BellyOff, bellyHalf * 0.45) * sinY;
            bellyRx = hasBelly ? Math.Min(2.5, bellyHalf - Math.Abs(bellyU) - 0.15) : 0;
            crestU = (shape == Shape.Triangle ? 0.3 : 0.8) * sinY;

            decorated = pose.Sweat > 0 || pose.Note || pose.Zzz || pose.Bubble || pose.Heart || pose.BlastFlash;
            // Only the rows the figure can reach are shaded: the crest top
            // (the longest crest is Bubbles', the tallest body Chuck's), or the
            // floating decorations, which go no higher than the "!" overhead.
            topV = (decorated ? Height + 7 : Math.Max(Height + 1.2, crestV + 3.0)) + Math.Abs(pose.BodyDy);
        }

        static void Rgb(double r, double g, double b, out double or, out double og, out double ob)
        {
            or = r; og = g; ob = b;
        }

        /// <summary>How big a kind is, relative to Red: the Blues are small, Terence is huge.</summary>
        public static double SizeOf(BirdKind kind)
        {
            switch (kind)
            {
                case BirdKind.Blues: return 0.62;
                case BirdKind.Bubbles: return 0.72;
                case BirdKind.Stella: return 0.95;
                case BirdKind.Chuck: return 1.0;
                case BirdKind.Matilda: return 1.05;
                case BirdKind.Bomb: return 1.15;
                case BirdKind.Terence: return 1.45;
                default: return 1.0;
            }
        }

        static double Wrap(double degrees)
        {
            double turn = degrees % 360;
            if (turn > 180) turn -= 360; else if (turn <= -180) turn += 360;
            return turn;
        }

        static double Clamp01(double x, double lo, double hi)
        {
            return x < lo ? lo : x > hi ? hi : x;
        }

        // ---- The body ------------------------------------------------------------

        /// <summary>The egg's half-width scale at height v: 1 at the middle, narrower towards the top.</summary>
        double EggRx(double v)
        {
            double t = (v - bodyV) / (bodyRy * grow);
            if (t < -1) t = -1; else if (t > 1) t = 1;
            return bodyRx * grow * (1 - eggTaper * (t + 1) / 2 * 0.9 + eggTaper * 0.25);
        }

        // Half the body width at height v, puffed up.
        double HalfWidthAt(double v)
        {
            if (shape == Shape.Triangle)
            {
                double wb = triBase * grow, wt = triTop * grow;
                if (v <= triV0 + triRound) return wb - triRound + Math.Sqrt(Math.Max(0, triRound * triRound - (triV0 + triRound - v) * (triV0 + triRound - v)));
                if (v >= triV1) return 0;
                if (v >= triV1 - triRound) return Math.Max(0.1, wt * (triV1 - v) / triRound);
                return wb + (wt - wb) * (v - triV0) / (triV1 - triV0);
            }
            double ry = bodyRy * grow;
            double dy = (v - bodyV) / ry;
            double inside = 1 - dy * dy;
            if (inside <= 0) return 0;
            double rx = shape == Shape.Egg ? EggRx(v) : bodyRx * grow;
            double w = rx * Math.Sqrt(inside);
            // The square body keeps its width longer and turns the corner late.
            if (shape == Shape.Square)
            {
                double r = 2.8;
                double ady = Math.Abs(v - bodyV);
                if (ady <= ry - r) return rx;
                double k = ady - (ry - r);
                return Math.Max(w, rx - r + Math.Sqrt(Math.Max(0, r * r - k * k)));
            }
            return w;
        }

        // Signed distance to the body silhouette.
        double BodySdf(double u, double v)
        {
            double rx = bodyRx * grow, ry = bodyRy * grow;
            switch (shape)
            {
                case Shape.Triangle:
                {
                    // A trapezoid shrunk by the rounding, then grown back by it:
                    // rounded corners at the base and a rounded apex.
                    double r = triRound;
                    double wb = triBase * grow - r, wt = Math.Max(0.05, triTop * grow - r * 0.5);
                    double v0 = triV0 + r, v1 = triV1 - r;
                    double t = (v - v0) / (v1 - v0);
                    double tc = t < 0 ? 0 : t > 1 ? 1 : t;
                    double w = wb + (wt - wb) * tc;
                    double slope = (wb - wt) / (v1 - v0);
                    double side = (Math.Abs(u) - w) / Math.Sqrt(1 + slope * slope);
                    double d = Math.Max(side, Math.Max(v0 - v, v - v1));
                    if (d > 0)
                    {
                        // Outside: the corner distances are Euclidean.
                        double dx = Math.Max(0, Math.Abs(u) - w);
                        double dy = Math.Max(0, Math.Max(v0 - v, v - v1));
                        if (dy > 0) d = Math.Sqrt(dx * dx + dy * dy);
                    }
                    return d - r;
                }
                case Shape.Egg:
                    return Ellipse(u, v - bodyV, EggRx(v), ry);
                case Shape.Square:
                    return RoundRect(u, v - bodyV, rx, ry, 2.8);
                default:
                    return Ellipse(u, v - bodyV, rx, ry);
            }
        }

        static double Ellipse(double dx, double dy, double rx, double ry)
        {
            double ex = dx / rx, ey = dy / ry;
            double e = Math.Sqrt(ex * ex + ey * ey);
            if (e < 1e-6) return -Math.Min(rx, ry);
            double grad = Math.Sqrt(ex * ex / (rx * rx) + ey * ey / (ry * ry)) / e;
            return (e - 1) / grad;
        }

        void PlaceEye(int i, double phi, double spread)
        {
            double a = phi + yaw;
            eyeAlpha[i] = a;
            eyeU[i] = (4.3 * spread + 0.2 * (eyeRx - 1)) * Math.Sin(a);
            double c = Math.Cos(a);
            eyeK[i] = c > 0.25 ? c : 0.25;
            double away = Math.Abs(a) / Deg;
            eyeVis[i] = away <= 72 ? 1 : away >= 95 ? 0 : (95 - away) / 23;
        }

        // ---- Entry point ---------------------------------------------------------

        public static void Draw(Surface surface, Pose pose, Look look, bool faceLeft,
                                double centreX, double soleY, double scale)
        {
            double size = SizeOf(look.Kind);
            double sx = scale * size * pose.StretchX, sy = scale * size * pose.StretchY;
            double px = 1 / Math.Min(sx, sy);
            BirdPainter painter = new BirdPainter(pose, look, faceLeft, px);

            int left = Math.Max(0, (int)Math.Floor(centreX - 9.5 * sx));
            int right = Math.Min(surface.Width, (int)Math.Ceiling(centreX + 9.5 * sx));
            int top = Math.Max(0, (int)Math.Floor(soleY - painter.topV * sy));
            int bottom = Math.Min(surface.Height, (int)Math.Ceiling(soleY + 0.5));
            if (left >= right || top >= bottom) return;

            int[] layer = surface.Layer;
            int width = surface.Width;
            double flip = faceLeft ? -1 : 1;
            for (int y = top; y < bottom; y++)
            {
                double v = (soleY - (y + 0.5)) / sy;
                int row = y * width;
                for (int x = left; x < right; x++)
                {
                    double u = ((x + 0.5) - centreX) / sx * flip;
                    int argb = painter.Shade(u, v);
                    if (argb != 0) layer[row + x] = argb;
                }
            }
            surface.Touch(left, top, right, bottom);
        }

        int Shade(double u, double v)
        {
            Begin();

            double vb = v + pose.BodyDy;
            double ub = u - lean * 0.12 * Math.Max(0, v - 4);

            double bodyDist = BodySdf(ub, vb);
            // Depth of the body's surface here for the (rare) sorted prop.
            double rx = bodyRx * grow;
            double bulge = rx * rx - ub * ub - (vb - bodyV) * (vb - bodyV);
            bodyZ = bulge > 0 ? Math.Sqrt(bulge) : 0;

            // Back to front: tail, body, chest, crest, face. No limbs at all.
            Tail(ub, vb);
            Body(ub, vb, bodyDist);
            Belly(ub, vb, bodyDist);
            Crest(ub, vb);
            if (Math.Abs(vb - eyeV) < 3.6)
            {
                Marks(ub, vb, bodyDist);
                Eye(ub, vb, firstEye);
                Eye(ub, vb, 1 - firstEye);
                if (browWeight > 0) Brows(ub, vb);
                Beak(ub, vb);
            }
            LayFront();
            if (pose.Egg) EggProp(ub, vb);
            if (pose.BlastFlash) Blast(ub, vb);

            if (decorated)
            {
                if (pose.Sweat > 0) Sweat(ub, vb, pose.Sweat, -(bodyRx + 0.6), eyeV + 1.9);
                if (pose.Note) Note(ub, vb, pose.DecorPhase, beakU, beakV);
                if (pose.Zzz) Zzz(u, v, pose.DecorPhase, 2.4, Height + 1.2);
                if (pose.Bubble) Bubble(ub, vb, Height + 0.4, Height + 1.3, Height + 2.8, Height + 5.4);
                if (pose.Heart) Heart(ub, vb, pose.DecorPhase, beakU, beakV);
            }

            return Finish();
        }

        void Body(double u, double v, double dist)
        {
            double cov = Cov(dist);
            if (cov <= 0) return;
            double nx, ny, nz;
            double rx = bodyRx * grow, ry = bodyRy * grow;
            if (shape == Shape.Triangle)
            {
                // A rounded cone: curved across its width, leaning back towards the apex.
                double half = HalfWidthAt(v);
                double across = half > 0.2 ? u / half : 0;
                if (across > 1) across = 1; else if (across < -1) across = -1;
                Normal(across * 0.95, 0.32, 1, out nx, out ny, out nz);
            }
            else
            {
                double ex = shape == Shape.Egg ? EggRx(v) : rx;
                Normal(u / ex * 3, (v - bodyV) / ry * 3, 3, out nx, out ny, out nz);
            }
            double lr, lg, lb;
            Lit(bodyR, bodyG, bodyB, nx, ny, nz, 0.3, 16, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
            // Soft shade tucked under the chest so the round reads.
            Over(bodyR * 0.72, bodyG * 0.72, bodyB * 0.72, Cov(v - 3.2) * 0.35 * cov);
        }

        // The lighter chest patch, clipped to the silhouette.
        void Belly(double u, double v, double bodyDist)
        {
            if (bellyRx <= 0.2) return;
            double cov = Cov(Ellipse(u - bellyU, v - BellyV, bellyRx, BellyRy));
            if (cov <= 0) return;
            cov = Math.Min(cov, Cov(bodyDist));
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal((u - bellyU) / bellyRx * 2, (v - BellyV) / BellyRy * 2, 2, out nx, out ny, out nz);
            double lr, lg, lb;
            Lit(bellyR, bellyG, bellyB, nx, ny, nz, 0.25, 16, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
        }

        void Tail(double u, double v)
        {
            double bx = -HalfWidthAt(7.2) * 0.85;
            if (Math.Abs(u - bx + 1.2) > 3.6 || v < 4.0 || v > 11.2) return;
            double sway = pose.Walking ? Math.Sin(pose.WalkPhase * 2 * Math.PI) * 0.25 : 0;
            double lift = tailLift;
            if (tailCount == 2)
            {
                Tube(u, v, bx, 6.6, bx - 1.7, 6.0 + sway, 0.38, tailR, tailG, tailB, 0.1, 8);
                Tube(u, v, bx, 8.0, bx - 1.8, 8.6 + sway, 0.38, tailR, tailG, tailB, 0.1, 8);
                return;
            }
            // Three (or four) feathers fanning out behind; spiky kinds angle them upwards.
            Tube(u, v, bx, 6.3, bx - 1.7, 5.4 + lift * 0.8 + sway, 0.38, tailR, tailG, tailB, 0.1, 8);
            Tube(u, v, bx, 7.4, bx - 1.9, 7.4 + lift + sway, 0.40, tailR, tailG, tailB, 0.1, 8);
            Tube(u, v, bx, 8.5, bx - 1.7, 9.4 + lift * 1.2 + sway, 0.38, tailR, tailG, tailB, 0.1, 8);
            if (tailCount >= 4)
                Tube(u, v, bx + 0.2, 9.4, bx - 1.2, 10.6 + lift + sway, 0.34, tailR, tailG, tailB, 0.1, 8);
        }

        /// <summary>One crest feather: a tube in the crest colour, optionally with a light round tip.</summary>
        void Feather(double u, double v, double x0, double y0, double x1, double y1, double r)
        {
            Tube(u, v, x0, y0, x1, y1, r, crestR, crestG, crestB, 0.15, 10);
        }

        void Crest(double u, double v)
        {
            double cy0 = crestV;
            double cx0 = crestU;
            if (Math.Abs(u - cx0 + 0.4) > 2.6 || v < cy0 - 0.6 || v > cy0 + 3.0) return;
            double sway = pose.Walking ? Math.Sin(pose.WalkPhase * 2 * Math.PI + 1) * 0.15 : 0;
            switch (kind)
            {
                case BirdKind.Bomb:
                    // A fuse: a grey stalk with a yellow sparking tip.
                    Tube(u, v, cx0, cy0, cx0 + 0.2, cy0 + 1.3, 0.16, crestR, crestG, crestB, 0.2, 10);
                    Ball(u, v, cx0 + 0.2, cy0 + 1.45, 0.22, 1, 0.82, 0.18, 0.6, 20);
                    Ball(u, v, cx0 + 0.28, cy0 + 1.38, 0.08, 1, 1, 1, 0.8, 24);
                    return;
                case BirdKind.Blues:
                    // Two blue feathers standing tall.
                    Feather(u, v, cx0 - 0.3, cy0, cx0 - 0.7 + sway, cy0 + 1.9, 0.40);
                    Feather(u, v, cx0 + 0.3, cy0, cx0 + 0.6 + sway, cy0 + 1.7, 0.38);
                    return;
                case BirdKind.Silver:
                    // Long feathers pointing behind the head.
                    Feather(u, v, cx0, cy0, cx0 - 1.3 + sway, cy0 + 1.3, 0.30);
                    Feather(u, v, cx0 + 0.2, cy0 - 0.1, cx0 - 1.8 + sway, cy0 + 0.7, 0.28);
                    Feather(u, v, cx0 + 0.3, cy0 - 0.3, cx0 - 1.9 + sway, cy0 + 0.1, 0.24);
                    return;
                case BirdKind.Chuck:
                    // Four pointy black feathers swept back off the apex.
                    Feather(u, v, cx0 + 0.1, cy0, cx0 - 0.3 + sway, cy0 + 1.9, 0.22);
                    Feather(u, v, cx0 + 0.1, cy0 - 0.1, cx0 - 0.9 + sway, cy0 + 1.6, 0.22);
                    Feather(u, v, cx0, cy0 - 0.3, cx0 - 1.4 + sway, cy0 + 1.0, 0.22);
                    Feather(u, v, cx0 - 0.1, cy0 - 0.5, cx0 - 1.7 + sway, cy0 + 0.3, 0.20);
                    return;
                case BirdKind.Stella:
                    // Three pink feathers with light, round tips.
                    Feather(u, v, cx0 - 0.3, cy0, cx0 - 0.8 + sway, cy0 + 1.6, 0.30);
                    Feather(u, v, cx0 + 0.1, cy0, cx0 + 0.1 + sway, cy0 + 1.9, 0.30);
                    Feather(u, v, cx0 + 0.5, cy0, cx0 + 0.9 + sway, cy0 + 1.4, 0.28);
                    Ball(u, v, cx0 - 0.8 + sway, cy0 + 1.6, 0.34, 0.99, 0.78, 0.86, 0.3, 14);
                    Ball(u, v, cx0 + 0.1 + sway, cy0 + 1.9, 0.34, 0.99, 0.78, 0.86, 0.3, 14);
                    Ball(u, v, cx0 + 0.9 + sway, cy0 + 1.4, 0.32, 0.99, 0.78, 0.86, 0.3, 14);
                    return;
                case BirdKind.Matilda:
                case BirdKind.Hal:
                    // Three short black feathers.
                    Feather(u, v, cx0 - 0.35, cy0, cx0 - 0.7 + sway, cy0 + 1.4, 0.21);
                    Feather(u, v, cx0, cy0, cx0 + sway, cy0 + 1.6, 0.21);
                    Feather(u, v, cx0 + 0.35, cy0, cx0 + 0.6 + sway, cy0 + 1.3, 0.21);
                    return;
                case BirdKind.Bubbles:
                    // Two long round-ended feathers, one longer than the other.
                    Feather(u, v, cx0 - 0.2, cy0, cx0 - 0.6 + sway, cy0 + 2.6, 0.26);
                    Feather(u, v, cx0 + 0.3, cy0, cx0 + 0.7 + sway, cy0 + 1.9, 0.26);
                    return;
                default:
                    // Red and Terence: two feathers, leaning with the hop.
                    Feather(u, v, cx0 - 0.25, cy0, cx0 - 0.55 + sway, cy0 + 1.5, 0.32);
                    Feather(u, v, cx0 + 0.25, cy0, cx0 + 0.45 + sway, cy0 + 1.3, 0.30);
                    return;
            }
        }

        // What sits on the face besides eyes, brows and beak: Terence's
        // spots and wrinkles, Matilda's cheek spots, Bubbles' eye rings.
        void Marks(double u, double v, double bodyDist)
        {
            double clip = Cov(bodyDist);
            if (clip <= 0) return;
            switch (kind)
            {
                case BirdKind.Terence:
                {
                    // Five red-violet spots about the face, two wrinkles on the forehead.
                    double fu = bellyU * 0.8;
                    Spot(u, v, fu - 2.6, eyeV - 1.3, 0.24, 0.46, 0.10, 0.26, clip);
                    Spot(u, v, fu + 2.7, eyeV - 1.1, 0.22, 0.46, 0.10, 0.26, clip);
                    Spot(u, v, fu - 2.2, eyeV + 1.1, 0.20, 0.46, 0.10, 0.26, clip);
                    Spot(u, v, fu + 2.4, eyeV + 1.3, 0.20, 0.46, 0.10, 0.26, clip);
                    Spot(u, v, fu + 0.1, eyeV - 2.2, 0.20, 0.46, 0.10, 0.26, clip);
                    double qx, qy, t;
                    double w1 = Seg(u, v, fu - 0.9, browV + 1.1, fu - 0.1, browV + 1.35, out qx, out qy, out t) - 0.08;
                    double w2 = Seg(u, v, fu + 0.1, browV + 1.35, fu + 0.9, browV + 1.1, out qx, out qy, out t) - 0.08;
                    Over(0.34, 0.05, 0.05, Cov(Math.Min(w1, w2)) * 0.8 * clip);
                    break;
                }
                case BirdKind.Matilda:
                {
                    // Yellow spots on the cheeks (her make-up).
                    double off = 2.0;
                    Spot(u, v, bellyU - off, beakV - 0.9, 0.42, 0.98, 0.78, 0.20, clip * 0.85);
                    Spot(u, v, bellyU + off, beakV - 0.9, 0.42, 0.98, 0.78, 0.20, clip * 0.85);
                    break;
                }
                case BirdKind.Bubbles:
                {
                    // Dark brown rings round the eyes, behind the whites.
                    for (int i = 0; i < 2; i++)
                    {
                        if (eyeVis[i] <= 0) continue;
                        double dx = (u - eyeU[i]) / eyeK[i] / (eyeRx + 0.38), dy = (v - eyeV) / (eyeRy + 0.38);
                        Over(bellyR, bellyG, bellyB, Cov((Math.Sqrt(dx * dx + dy * dy) - 1) * (eyeRy + 0.38)) * eyeVis[i] * clip);
                    }
                    break;
                }
            }
        }

        void Spot(double u, double v, double cx, double cy, double radius, double r, double g, double b, double clip)
        {
            double dx = u - cx, dy = v - cy;
            if (Math.Abs(dx) > radius + 0.6 || Math.Abs(dy) > radius + 0.6) return;
            Over(r, g, b, Cov(Math.Sqrt(dx * dx + dy * dy) - radius) * 0.8 * clip);
        }

        void Eye(double u, double v, int i)
        {
            double vis = eyeVis[i];
            if (vis <= 0) return;
            double k = eyeK[i], alpha = eyeAlpha[i];
            bool near = i != firstEye;

            double fu = eyeU[i], fv = eyeV;
            double dx = (u - fu) / k, dy = v - fv;
            if (Math.Abs(dx) > eyeRx + 0.6 || Math.Abs(dy) > eyeRy + 0.6) return;

            double ex = dx / eyeRx, ey = dy / eyeRy;
            double white = (Math.Sqrt(ex * ex + ey * ey) - 1) * eyeRy;
            double covWhite = Cov(white) * vis;
            if (covWhite > 0)
            {
                double nx, ny, nz;
                Normal(dx, dy, eyeRx * 1.6, out nx, out ny, out nz);
                Turn(ref nx, ref nz, alpha);
                double r, g, b;
                Lit(1, 1, 1, nx, ny, nz, 0.35, 22, out r, out g, out b);

                double reach = eyeRx - IrisR - 0.05;
                double ox = pose.GazeX * reach * 0.9 + face.PupilDx * 0.4;
                double oy = pose.GazeY * (eyeRy - IrisR * 0.6) * 0.8 + face.PupilDy * 0.3;
                ox -= (i == 1 ? 1 : -1) * face.CrossEye * 0.3;
                double om = Math.Sqrt(ox * ox + oy * oy);
                if (om > reach) { ox *= reach / om; oy *= reach / om; }
                double ix = dx - ox, iy = dy - oy;
                double covIris = Cov(Squeezed(ix, iy, k, IrisR));
                if (covIris > 0)
                {
                    r += (irisR - r) * covIris;
                    g += (irisG - g) * covIris;
                    b += (irisB - b) * covIris;
                    double covPupil = Cov(Squeezed(ix, iy, k, IrisR * 0.55 * face.PupilSize));
                    r += (0.02 - r) * covPupil;
                    g += (0.02 - g) * covPupil;
                    b += (0.02 - b) * covPupil;
                    double lx = ix + flip * 0.3 * IrisR, ly = iy - 0.3 * IrisR;
                    double covLight = Cov(Squeezed(lx, ly, k, IrisR * 0.28)) * 0.95;
                    r += (1 - r) * covLight;
                    g += (1 - g) * covLight;
                    b += (1 - b) * covLight;
                }

                // Feather lids slide down over the eye.
                double upper = Math.Max(face.Lid, near ? face.Wink : 0);
                double lower = face.Squint;
                double s = ex * ex;
                double upperEdge = fv + eyeRy * (1 - 2 * upper) - 0.25 * upper * s;
                double lowerEdge = fv - eyeRy * (1 - 2 * lower) + 0.25 * lower * s;
                if (upper > 0.01 || lower > 0.01)
                {
                    double sr, sg, sb;
                    Lit(bodyR, bodyG, bodyB, nx, ny, nz, 0.3, 16, out sr, out sg, out sb);
                    double covLid = Math.Max(upper > 0.01 ? Cov(upperEdge - v) : 0, lower > 0.01 ? Cov(v - lowerEdge) : 0);
                    r += (sr - r) * covLid;
                    g += (sg - g) * covLid;
                    b += (sb - b) * covLid;
                }
                Over(r, g, b, covWhite);

                // The Blues' orange lower eyelids: a rim over the bottom of each eye.
                if (kind == BirdKind.Blues)
                {
                    double rim = Cov(white) * Cov(Math.Abs(white) - 0.26) * Cov(dy + 0.1) * vis;
                    Over(0.96, 0.52, 0.14, rim * 0.9);
                }
            }

            // Eyelashes for the ladies; Silver has a single pointed outer lash per eye.
            if (kind == BirdKind.Matilda || kind == BirdKind.Stella || kind == BirdKind.Silver)
            {
                double qx, qy, t;
                double outer = i == 0 ? -1 : 1;
                double lash = Seg(u, v, fu + outer * 0.5 * eyeRx, fv + eyeRy + 0.2, fu + outer * 0.85 * eyeRx, fv + eyeRy + 0.55, out qx, out qy, out t) - 0.07;
                Over(0.1, 0.08, 0.08, Cov(lash) * vis);
                if (kind != BirdKind.Silver)
                {
                    lash = Seg(u, v, fu - outer * 0.5 * eyeRx, fv + eyeRy + 0.2, fu - outer * 0.85 * eyeRx, fv + eyeRy + 0.55, out qx, out qy, out t) - 0.07;
                    Over(0.1, 0.08, 0.08, Cov(lash) * vis);
                }
            }
        }

        // Thick angry brows slanting down towards the middle, in the kind's colour.
        void Brows(double u, double v)
        {
            double lift = face.BrowRaise * 0.4 - 0.1;
            double slant = 0.55 + face.BrowAngle * 0.35;
            if (Math.Abs(v - (browV + lift)) > 1.4) return;
            Brow(u, v, eyeAlpha[0], -1, browV + lift, slant, eyeVis[0]);
            Brow(u, v, eyeAlpha[1], 1, browV + lift + face.BrowAsym * 0.3, slant, eyeVis[1]);
        }

        void Brow(double u, double v, double alpha, double outward, double cy, double slant, double show)
        {
            if (show <= 0) return;
            double k = Math.Cos(alpha);
            if (k < 0.2) return;
            double ex = eyeU[0] + (eyeU[1] - eyeU[0]) * (outward > 0 ? 1 : 0);
            // Inner end low (angry), outer end high.
            double ix = ex - outward * 0.55 * k, iy = cy - slant;
            double ox = ex + outward * 1.0 * k, oy = cy + slant * 0.4;
            double qx, qy, t;
            double d = Seg(u, v, ix, iy, ox, oy, out qx, out qy, out t) - (0.34 - 0.10 * t) * browWeight;
            Over(browR, browG, browB, Cov(d) * show);
        }

        void Beak(double u, double v)
        {
            double vis = cosY > 0.1 ? 1 : cosY < -0.2 ? 0 : (cosY + 0.2) / 0.3;
            if (vis <= 0) return;
            double mu = beakU;
            double mv = beakV;
            double w = beakW;
            if (w <= 0.15) return;
            if (Math.Abs(u - mu) > w + 1.2 || Math.Abs(v - mv) > 2.2 * beakScale) return;
            double open = face.MouthOpen * 1.15;
            if (open < 0.02) open = 0.02;

            // Upper beak; lower beak drops with the jaw.
            double upperCy = mv + 0.25, lowerCy = mv - 0.35 - open;
            double ux = (u - mu) / w, uy = (v - upperCy) / (0.62 * beakScale);
            double upper = (Math.Sqrt(ux * ux + uy * uy) - 1) * 0.62;
            double covU = Cov(upper) * vis;
            if (covU > 0)
            {
                double nx, ny, nz;
                Normal(ux, uy, 1, out nx, out ny, out nz);
                double lr, lg, lb;
                Lit(beakR, beakG, beakB, nx, ny, nz, 0.45, 20, out lr, out lg, out lb);
                Over(lr, lg, lb, covU);
            }
            double lx = (u - mu) / (w * 0.75), ly = (v - lowerCy) / (0.45 * beakScale);
            double lower = (Math.Sqrt(lx * lx + ly * ly) - 1) * 0.45;
            double covL = Cov(lower) * Cov(upperCy - 0.1 - v) * vis;
            if (covL > 0)
            {
                double nx, ny, nz;
                Normal(lx, ly, 1, out nx, out ny, out nz);
                double lr, lg, lb;
                Lit(beakR2, beakG2, beakB2, nx, ny, nz, 0.4, 18, out lr, out lg, out lb);
                Over(lr, lg, lb, covL);
            }
            // Dark mouth inside when open, with a tongue.
            if (face.MouthOpen > 0.12)
            {
                double mx = (u - mu) / (w * 0.6), my = (v - (mv - open * 0.5)) / (open * 0.6 + 0.05);
                double inside = (Math.Sqrt(mx * mx + my * my) - 1) * 0.5;
                Over(0.30, 0.08, 0.08, Cov(inside) * Cov(v - lowerCy) * Cov(upperCy - v) * vis);
                if (face.MouthOpen > 0.3)
                {
                    double tx = (u - mu) / 0.4, ty = (v - (lowerCy + 0.15)) / 0.25;
                    Over(0.75, 0.25, 0.25, Cov(Math.Sqrt(tx * tx + ty * ty) - 0.35) * 0.9 * vis);
                }
            }
            // The mouth line when shut.
            Over(0.25, 0.12, 0.05, Cov(Math.Abs(v - (mv - 0.05)) - 0.06) * Cov(Math.Abs(u - mu) - w) * vis);
            // Silver's red beak tip, pointing down at the end of the beak.
            if (kind == BirdKind.Silver)
            {
                double tx = u - (mu + w * 0.45), ty = v - (mv - 0.5);
                Over(0.80, 0.12, 0.12, Cov(Math.Sqrt(tx * tx + ty * ty) - 0.19) * vis);
            }
        }

        void EggProp(double u, double v)
        {
            double ex0 = 2.9, ey0 = 0.95;
            if (Math.Abs(u - ex0) > 1.6 || Math.Abs(v - ey0) > 1.8) return;
            double dx = (u - ex0) / 0.8, dy = (v - ey0) / 1.0;
            double egg = (Math.Sqrt(dx * dx + dy * dy) - 1) * 0.9;
            double cov = Cov(egg);
            if (cov > 0)
            {
                double nx, ny, nz;
                Normal(dx, dy, 1, out nx, out ny, out nz);
                double lr, lg, lb;
                Lit(0.96, 0.93, 0.88, nx, ny, nz, 0.35, 20, out lr, out lg, out lb);
                Over(lr, lg, lb, cov);
                // Speckles.
                double sx = u - (ex0 - 0.25), sy = v - (ey0 + 0.3);
                Over(0.55, 0.45, 0.35, Cov(Math.Sqrt(sx * sx + sy * sy) - 0.09));
                sx = u - (ex0 + 0.3); sy = v - ey0;
                Over(0.55, 0.45, 0.35, Cov(Math.Sqrt(sx * sx + sy * sy) - 0.07));
            }
        }

        void Blast(double u, double v)
        {
            double cx0 = 0, cy0 = bodyV;
            double d = Math.Sqrt((u - cx0) * (u - cx0) + (v - cy0) * (v - cy0));
            Over(1, 0.98, 0.92, Cov(d - 3.2));
            Over(1, 0.75, 0.25, Cov(Math.Abs(d - 3.6) - 0.35) * 0.95);
            Over(0.35, 0.30, 0.28, Cov(Math.Abs(d - 4.3) - 0.5) * 0.7);
        }

        void Ball(double u, double v, double cx, double cy, double radius, double r, double g, double b, double spec, double shine)
        {
            double dx = u - cx, dy = v - cy;
            double cov = Cov(Math.Sqrt(dx * dx + dy * dy) - radius);
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(dx, dy, radius, out nx, out ny, out nz);
            double lr, lg, lb;
            Lit(r, g, b, nx, ny, nz, spec, shine, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
        }
    }
}
