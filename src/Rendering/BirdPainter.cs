using System;
using IdleWalker.Domain;

namespace IdleWalker.Rendering
{
    /// <summary>
    /// Paints an Angry Bird into a <see cref="Surface"/> the way the Minions
    /// are painted: as lit solids with anti-aliased edges, composited back to
    /// front (see <see cref="SolidPainter"/>). Faithful to the classic 2009
    /// designs: a LIMBLESS ball of feathers - no wings, no legs, no feet -
    /// with a lighter belly, two big front eyes under thick angry brows, a
    /// beak that opens, crest feathers and tail feathers. They hop and roll
    /// instead of walking (their skeleton is a big round skull with a curved
    /// hopping bone), so all motion reads through squash, stretch, lean and
    /// bobbing. Chuck is triangular, Matilda egg-shaped, Hal carries a huge
    /// long beak, Bomb a sparking fuse.
    ///
    /// Model space is the Minions': u across in units, positive the way it
    /// faces; v up from the soles (its round bottom). It is
    /// <see cref="Height"/> units tall.
    /// </summary>
    sealed class BirdPainter : SolidPainter
    {
        public const double Height = 14;

        // Round body: an ellipse. Bottom sits near the ground; there are no feet.
        const double BodyV = 6.3, BodyRx = 4.9, BodyRy = 5.1;
        // Tapered bodies (Chuck's triangle, Matilda's egg): half-width lerps
        // wb->wt from vb0 up to vb1, with rounded caps past each end.
        const double ChuckVb0 = 1.6, ChuckVb1 = 11.4, ChuckWb = 4.2, ChuckWt = 1.2;
        const double MatVb0 = 1.6, MatVb1 = 11.2, MatWb = 4.6, MatWt = 2.0;

        const double BellyV = 5.3, BellyRx = 2.5, BellyRy = 2.9, BellyOff = 1.6;
        const double EyeV = 9.7, EyeAngle = 17, EyeRy = 0.92, IrisR = 0.42;
        const double BrowV = 10.9;
        const double BeakV = 7.6;

        readonly Pose pose;
        readonly Expression face;
        readonly BirdKind kind;
        readonly bool tapered;
        readonly double yaw, sinY, cosY;
        readonly double lean;
        readonly double puff;

        // Body colours for this kind.
        readonly double bodyR, bodyG, bodyB;
        readonly double bellyR, bellyG, bellyB;
        readonly double beakR, beakG, beakB;
        readonly double beakR2, beakG2, beakB2;
        readonly double darkR, darkG, darkB;

        // Per-kind shape numbers.
        readonly double eyeRx;
        readonly double beakScale;
        readonly double bellyU, bellyRx;

        // Eyes: angle from the viewer, screen centre, horizontal squeeze, visibility.
        readonly double[] eyeAlpha = new double[2], eyeU = new double[2], eyeK = new double[2], eyeVis = new double[2];
        readonly int firstEye;

        readonly bool decorated;
        readonly double topV;
        readonly double beakU, beakW;

        BirdPainter(Pose pose, Look look, bool faceLeft, double px)
            : base(faceLeft, px, BodyRx)
        {
            this.pose = pose;
            face = pose.Face;
            kind = look.Kind;
            tapered = kind == BirdKind.Chuck || kind == BirdKind.Matilda;
            lean = pose.Lean;
            puff = pose.PuffUp < 0 ? 0 : pose.PuffUp > 1 ? 1 : pose.PuffUp;
            yaw = Wrap(pose.Yaw) * Deg;
            sinY = Math.Sin(yaw);
            cosY = Math.Cos(yaw);

            Colours(kind, out bodyR, out bodyG, out bodyB, out bellyR, out bellyG, out bellyB,
                    out beakR, out beakG, out beakB, out beakR2, out beakG2, out beakB2,
                    out darkR, out darkG, out darkB);

            eyeRx = kind == BirdKind.Chuck ? 0.85 : 1.0;
            beakScale = kind == BirdKind.Hal ? 1.55 : kind == BirdKind.Matilda ? 1.15 :
                        kind == BirdKind.Chuck ? 1.1 : 1.0;

            // Features narrow with the local body width so nothing floats off
            // Chuck's triangle or Matilda's egg.
            double spread = Clamp01(HalfWidthAt(EyeV) / 2.6, 0.35, 1);
            PlaceEye(0, -EyeAngle * Deg, spread);
            PlaceEye(1, EyeAngle * Deg, spread);
            firstEye = Math.Cos(eyeAlpha[0]) < Math.Cos(eyeAlpha[1]) ? 0 : 1;

            double beakHalf = HalfWidthAt(BeakV);
            beakU = (beakHalf + 0.4) * sinY + 0.5 * (beakScale - 1) * sinY;
            beakW = (0.95 + face.MouthWidth * 0.12) * (0.5 + 0.5 * Math.Abs(sinY)) * beakScale
                    * Clamp01(beakHalf / 2.8, 0.55, 1);

            double bellyHalf = HalfWidthAt(BellyV);
            bellyU = Math.Min(BellyOff, bellyHalf * 0.45) * sinY;
            // Chuck is solid yellow with no belly patch.
            bellyRx = kind == BirdKind.Chuck ? 0 : Math.Min(BellyRx, bellyHalf - Math.Abs(bellyU) - 0.15);

            decorated = pose.Sweat > 0 || pose.Note || pose.Zzz || pose.Bubble || pose.Heart || pose.BlastFlash;
            topV = decorated ? 37 : Height + 0.8 + Math.Abs(pose.BodyDy);
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

        static void Colours(BirdKind kind,
            out double br, out double bg, out double bb,
            out double er, out double eg, out double eb,
            out double kr, out double kg, out double kb,
            out double k2r, out double k2g, out double k2b,
            out double dr, out double dg, out double db)
        {
            switch (kind)
            {
                case BirdKind.Chuck:
                    br = 0.97; bg = 0.76; bb = 0.10;
                    er = 0.99; eg = 0.90; eb = 0.72;
                    kr = 0.95; kg = 0.55; kb = 0.08;
                    k2r = 0.88; k2g = 0.45; k2b = 0.06;
                    dr = 0.10; dg = 0.08; db = 0.08;
                    break;
                case BirdKind.Bomb:
                    br = 0.13; bg = 0.13; bb = 0.15;
                    er = 0.42; eg = 0.40; eb = 0.38;
                    kr = 0.99; kg = 0.78; kb = 0.12;
                    k2r = 0.93; k2g = 0.60; k2b = 0.08;
                    dr = 0.05; dg = 0.05; db = 0.06;
                    break;
                case BirdKind.Blues:
                    br = 0.20; bg = 0.55; bb = 0.95;
                    er = 0.55; eg = 0.80; eb = 0.98;
                    kr = 0.98; kg = 0.62; kb = 0.10;
                    k2r = 0.90; k2g = 0.50; k2b = 0.07;
                    dr = 0.08; dg = 0.20; db = 0.45;
                    break;
                case BirdKind.Matilda:
                    br = 0.94; bg = 0.93; bb = 0.90;
                    er = 0.97; eg = 0.87; eb = 0.87;
                    kr = 0.99; kg = 0.78; kb = 0.15;
                    k2r = 0.93; k2g = 0.62; k2b = 0.10;
                    dr = 0.15; dg = 0.12; db = 0.12;
                    break;
                case BirdKind.Hal:
                    br = 0.28; bg = 0.66; bb = 0.28;
                    er = 0.80; eg = 0.90; eb = 0.72;
                    kr = 0.98; kg = 0.60; kb = 0.10;
                    k2r = 0.90; k2g = 0.48; k2b = 0.07;
                    dr = 0.08; dg = 0.20; db = 0.08;
                    break;
                case BirdKind.Terence:
                    br = 0.66; bg = 0.12; bb = 0.10;
                    er = 0.85; eg = 0.70; eb = 0.58;
                    kr = 0.99; kg = 0.78; kb = 0.12;
                    k2r = 0.93; k2g = 0.60; k2b = 0.08;
                    dr = 0.25; dg = 0.06; db = 0.05;
                    break;
                case BirdKind.Bubbles:
                    br = 0.97; bg = 0.52; bb = 0.10;
                    er = 0.99; eg = 0.85; eb = 0.65;
                    kr = 0.99; kg = 0.75; kb = 0.10;
                    k2r = 0.93; k2g = 0.58; k2b = 0.07;
                    dr = 0.45; dg = 0.20; db = 0.05;
                    break;
                case BirdKind.Stella:
                    br = 0.97; bg = 0.52; bb = 0.68;
                    er = 0.99; eg = 0.85; eb = 0.88;
                    kr = 0.99; kg = 0.80; kb = 0.20;
                    k2r = 0.93; k2g = 0.62; k2b = 0.12;
                    dr = 0.45; dg = 0.15; db = 0.30;
                    break;
                case BirdKind.Silver:
                    br = 0.72; bg = 0.73; bb = 0.75;
                    er = 0.88; eg = 0.88; eb = 0.90;
                    kr = 0.99; kg = 0.78; kb = 0.12;
                    k2r = 0.93; k2g = 0.60; k2b = 0.08;
                    dr = 0.30; dg = 0.30; db = 0.33;
                    break;
                default:
                    br = 0.82; bg = 0.16; bb = 0.13;
                    er = 0.93; eg = 0.82; eb = 0.70;
                    kr = 0.99; kg = 0.78; kb = 0.12;
                    k2r = 0.93; k2g = 0.60; k2b = 0.08;
                    dr = 0.10; dg = 0.08; db = 0.08;
                    break;
            }
        }

        // Half the body width at height v, puffed up.
        double HalfWidthAt(double v)
        {
            double grow = 1 + 0.35 * puff;
            if (!tapered)
            {
                double dy = (v - BodyV) / BodyRy;
                double inside = 1 - dy * dy;
                return inside <= 0 ? 0 : BodyRx * Math.Sqrt(inside) * grow;
            }
            double vb0, vb1, wb, wt;
            TaperParams(out vb0, out vb1, out wb, out wt);
            wb *= grow;
            wt *= grow;
            if (v <= vb0) return wb;
            if (v >= vb1) return wt;
            return wb + (wt - wb) * (v - vb0) / (vb1 - vb0);
        }

        void TaperParams(out double vb0, out double vb1, out double wb, out double wt)
        {
            if (kind == BirdKind.Chuck)
            {
                vb0 = ChuckVb0; vb1 = ChuckVb1; wb = ChuckWb; wt = ChuckWt;
            }
            else
            {
                vb0 = MatVb0; vb1 = MatVb1; wb = MatWb; wt = MatWt;
            }
        }

        // Signed distance to the body silhouette.
        double BodySdf(double u, double v)
        {
            if (!tapered)
            {
                double rx = BodyRx * (1 + 0.35 * puff);
                double ry = BodyRy * (1 + 0.35 * puff);
                return Ellipse(u, v - BodyV, rx, ry);
            }
            double vb0, vb1, wb, wt;
            TaperParams(out vb0, out vb1, out wb, out wt);
            double grow = 1 + 0.35 * puff;
            wb *= grow;
            wt *= grow;
            double t = (v - vb0) / (vb1 - vb0);
            double tc = t < 0 ? 0 : t > 1 ? 1 : t;
            double w = wb + (wt - wb) * tc;
            if (t >= 0 && t <= 1) return Math.Abs(u) - w;
            double cv = t < 0 ? vb0 : vb1;
            double au = Math.Abs(u);
            double dx = au > w ? au - w : 0;
            double dy = v - cv;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        void PlaceEye(int i, double phi, double spread)
        {
            double a = phi + yaw;
            eyeAlpha[i] = a;
            eyeU[i] = (BodyRx - 0.6) * spread * Math.Sin(a);
            double c = Math.Cos(a);
            eyeK[i] = c > 0.25 ? c : 0.25;
            double away = Math.Abs(a) / Deg;
            eyeVis[i] = away <= 72 ? 1 : away >= 95 ? 0 : (95 - away) / 23;
        }

        // ---- Entry point ---------------------------------------------------------

        public static void Draw(Surface surface, Pose pose, Look look, bool faceLeft,
                                double centreX, double soleY, double scale)
        {
            double sx = scale * pose.StretchX, sy = scale * pose.StretchY;
            double px = 1 / Math.Min(sx, sy);
            BirdPainter painter = new BirdPainter(pose, look, faceLeft, px);

            int left = Math.Max(0, (int)Math.Floor(centreX - 11.5 * sx));
            int right = Math.Min(surface.Width, (int)Math.Ceiling(centreX + 11.5 * sx));
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
            double rx = BodyRx * (1 + 0.35 * puff);
            double bulge = rx * rx - ub * ub - (vb - BodyV) * (vb - BodyV);
            bodyZ = bulge > 0 ? Math.Sqrt(bulge) : 0;

            // Back to front: tail, body, belly, crest, face. No limbs at all.
            Tail(ub, vb);
            Body(ub, vb, bodyDist);
            Belly(ub, vb, bodyDist);
            Crest(ub, vb);
            if (Math.Abs(vb - EyeV) < 3.4)
            {
                Eye(ub, vb, firstEye);
                Eye(ub, vb, 1 - firstEye);
                // The Blues alone have no visible eyebrows.
                if (kind != BirdKind.Blues) Brows(ub, vb);
                Beak(ub, vb);
                Cheeks(ub, vb, bodyDist);
                Freckles(ub, vb, bodyDist);
            }
            LayFront();
            if (pose.Egg) EggProp(ub, vb);
            if (pose.BlastFlash) Blast(ub, vb);

            if (decorated)
            {
                if (pose.Sweat > 0) Sweat(ub, vb, pose.Sweat, -(BodyRx + 0.6), 11.6);
                if (pose.Note) Note(ub, vb, pose.DecorPhase, beakU, BeakV);
                if (pose.Zzz) Zzz(u, v, pose.DecorPhase, 2.4, Height + 1.2);
                if (pose.Bubble) Bubble(ub, vb, Height + 0.4, Height + 1.3, Height + 2.8, Height + 5.4);
                if (pose.Heart) Heart(ub, vb, pose.DecorPhase, beakU, BeakV);
            }

            return Finish();
        }

        static double Ellipse(double dx, double dy, double rx, double ry)
        {
            double ex = dx / rx, ey = dy / ry;
            double e = Math.Sqrt(ex * ex + ey * ey);
            if (e < 1e-6) return -Math.Min(rx, ry);
            double grad = Math.Sqrt(ex * ex / (rx * rx) + ey * ey / (ry * ry)) / e;
            return (e - 1) / grad;
        }

        void Body(double u, double v, double dist)
        {
            double cov = Cov(dist);
            if (cov <= 0) return;
            double nx, ny, nz;
            if (!tapered)
            {
                double rx = BodyRx * (1 + 0.35 * puff);
                double ry = BodyRy * (1 + 0.35 * puff);
                Normal(u / rx * 3, (v - BodyV) / ry * 3, 3, out nx, out ny, out nz);
            }
            else
            {
                // Flank normal of the cone/egg slope.
                double vb0, vb1, wb, wt;
                TaperParams(out vb0, out vb1, out wb, out wt);
                double slope = -(wt - wb) / (vb1 - vb0);
                double s = u >= 0 ? 1 : -1;
                Normal(s * 1.1, slope, 1.0, out nx, out ny, out nz);
            }
            double lr, lg, lb;
            Lit(bodyR, bodyG, bodyB, nx, ny, nz, 0.3, 16, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
            // Soft shade tucked under the belly so the round reads.
            Over(bodyR * 0.72, bodyG * 0.72, bodyB * 0.72, Cov(v - 3.2) * 0.35 * cov);
            // Ruffled feather flecks.
            double fleck = 0.5 + 0.5 * Math.Sin(u * 6.1) * Math.Sin(v * 5.3);
            Over(bodyR * 0.9, bodyG * 0.9, bodyB * 0.9, Cov(0.4 - fleck) * 0.18 * cov);
        }

        // The lighter front patch, clipped to the silhouette.
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
            if (Math.Abs(u - bx + 1) > 3.4 || v < 4.2 || v > 10.6) return;
            double sway = pose.Walking ? Math.Sin(pose.WalkPhase * 2 * Math.PI) * 0.25 : 0;
            Tube(u, v, bx, 6.3, bx - 1.7, 5.4 + sway, 0.38, darkR, darkG, darkB, 0.1, 8);
            Tube(u, v, bx, 7.4, bx - 1.9, 7.4 + sway, 0.40, darkR, darkG, darkB, 0.1, 8);
            Tube(u, v, bx, 8.5, bx - 1.7, 9.4 + sway, 0.38, darkR, darkG, darkB, 0.1, 8);
        }

        void Crest(double u, double v)
        {
            double cy0 = 11.1;
            double cx0 = 0.8 * sinY;
            if (Math.Abs(u - cx0) > 2.2 || v < cy0 - 0.6 || v > cy0 + 2.8) return;
            if (kind == BirdKind.Bomb)
            {
                // A fuse: grey stalk with a sparking tip.
                Tube(u, v, cx0, cy0, cx0 + 0.2, cy0 + 1.2, 0.16, 0.45, 0.38, 0.30, 0.2, 10);
                Ball(u, v, cx0 + 0.2, cy0 + 1.35, 0.20, 1, 0.8, 0.2, 0.6, 20);
                Ball(u, v, cx0 + 0.28, cy0 + 1.28, 0.08, 1, 1, 1, 0.8, 24);
                return;
            }
            double sway = pose.Walking ? Math.Sin(pose.WalkPhase * 2 * Math.PI + 1) * 0.15 : 0;
            if (kind == BirdKind.Blues)
            {
                // Two big feathers standing tall.
                Tube(u, v, cx0 - 0.3, cy0, cx0 - 0.7 + sway, cy0 + 1.9, 0.40, darkR, darkG, darkB, 0.15, 10);
                Tube(u, v, cx0 + 0.3, cy0, cx0 + 0.6 + sway, cy0 + 1.7, 0.38, darkR, darkG, darkB, 0.15, 10);
                return;
            }
            if (kind == BirdKind.Silver)
            {
                // Slicked-back crest sweeping behind the head.
                Tube(u, v, cx0, cy0, cx0 - 0.9 + sway, cy0 + 1.1, 0.28, darkR, darkG, darkB, 0.15, 10);
                Tube(u, v, cx0 + 0.1, cy0, cx0 - 1.3 + sway, cy0 + 0.55, 0.24, darkR, darkG, darkB, 0.15, 10);
                return;
            }
            if (kind == BirdKind.Chuck)
            {
                // A mohawk swept back: four pointy feathers as two ridges.
                Tube(u, v, cx0, cy0, cx0 - 0.5 + sway, cy0 + 1.7, 0.30, darkR, darkG, darkB, 0.15, 10);
                Tube(u, v, cx0 + 0.25, cy0, cx0 - 0.1 + sway, cy0 + 1.3, 0.26, darkR, darkG, darkB, 0.15, 10);
                return;
            }
            if (kind == BirdKind.Stella)
            {
                // A feather bow: two pink loops with a blue knot.
                Ball(u, v, cx0 - 0.45, cy0 + 0.9, 0.42, 0.97, 0.45, 0.62, 0.3, 14);
                Ball(u, v, cx0 + 0.45, cy0 + 0.9, 0.42, 0.97, 0.45, 0.62, 0.3, 14);
                Ball(u, v, cx0, cy0 + 0.85, 0.24, 0.30, 0.55, 0.95, 0.4, 18);
                return;
            }
            if (kind == BirdKind.Matilda)
            {
                // Three strands of black hair with a pink tip.
                Tube(u, v, cx0 - 0.3, cy0, cx0 - 0.6 + sway, cy0 + 1.5, 0.20, darkR, darkG, darkB, 0.15, 10);
                Tube(u, v, cx0, cy0, cx0 + sway, cy0 + 1.6, 0.20, darkR, darkG, darkB, 0.15, 10);
                Tube(u, v, cx0 + 0.3, cy0, cx0 + 0.5 + sway, cy0 + 1.4, 0.20, darkR, darkG, darkB, 0.15, 10);
                Ball(u, v, cx0 + 0.55 + sway, cy0 + 1.45, 0.15, 0.95, 0.55, 0.60, 0.3, 14);
                return;
            }
            // Two head feathers, leaning with the hop.
            Tube(u, v, cx0 - 0.25, cy0, cx0 - 0.55 + sway, cy0 + 1.5, 0.32, darkR, darkG, darkB, 0.15, 10);
            Tube(u, v, cx0 + 0.25, cy0, cx0 + 0.45 + sway, cy0 + 1.3, 0.30, darkR, darkG, darkB, 0.15, 10);
        }

        void Eye(double u, double v, int i)
        {
            double vis = eyeVis[i];
            if (vis <= 0) return;
            double k = eyeK[i], alpha = eyeAlpha[i];
            bool near = i != firstEye;

            double fu = eyeU[i], fv = EyeV;
            double dx = (u - fu) / k, dy = v - fv;
            if (Math.Abs(dx) > eyeRx + 0.5 || Math.Abs(dy) > EyeRy + 0.5) return;

            double ex = dx / eyeRx, ey = dy / EyeRy;
            double white = (Math.Sqrt(ex * ex + ey * ey) - 1) * EyeRy;
            double covWhite = Cov(white) * vis;
            if (covWhite <= 0) return;

            double nx, ny, nz;
            Normal(dx, dy, eyeRx * 1.6, out nx, out ny, out nz);
            Turn(ref nx, ref nz, alpha);
            double r, g, b;
            Lit(1, 1, 1, nx, ny, nz, 0.35, 22, out r, out g, out b);

            double reach = eyeRx - IrisR - 0.05;
            double ox = pose.GazeX * reach * 0.9 + face.PupilDx * 0.4;
            double oy = pose.GazeY * (EyeRy - IrisR * 0.6) * 0.8 + face.PupilDy * 0.3;
            ox -= (i == 1 ? 1 : -1) * face.CrossEye * 0.3;
            double om = Math.Sqrt(ox * ox + oy * oy);
            if (om > reach) { ox *= reach / om; oy *= reach / om; }
            double ix = dx - ox, iy = dy - oy;
            double covIris = Cov(Squeezed(ix, iy, k, IrisR));
            if (covIris > 0)
            {
                r += (0.08 - r) * covIris;
                g += (0.07 - g) * covIris;
                b += (0.08 - b) * covIris;
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
            double upperEdge = fv + EyeRy * (1 - 2 * upper) - 0.25 * upper * s;
            double lowerEdge = fv - EyeRy * (1 - 2 * lower) + 0.25 * lower * s;
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

            // The Blues' red eyelids: a reddish rim round each eye.
            if (kind == BirdKind.Blues)
            {
                double ex2 = dx / eyeRx, ey2 = dy / EyeRy;
                double rimRed = Cov((Math.Sqrt(ex2 * ex2 + ey2 * ey2) - 1) * EyeRy) *
                                Cov(Math.Abs(Math.Sqrt(ex2 * ex2 + ey2 * ey2) - 1) * EyeRy - 0.28) * vis;
                Over(0.62, 0.16, 0.12, rimRed * 0.85);
            }

            // Eyelashes for the ladies; Silver has a single outer lash per eye.
            if (kind == BirdKind.Matilda || kind == BirdKind.Stella || kind == BirdKind.Silver)
            {
                double qx, qy, t;
                double outer = i == 0 ? -1 : 1;
                double lash = Seg(u, v, fu + outer * 0.5, fv + EyeRy + 0.25, fu + outer * 0.85, fv + EyeRy + 0.55, out qx, out qy, out t) - 0.07;
                Over(0.1, 0.08, 0.08, Cov(lash) * vis);
                if (kind != BirdKind.Silver)
                {
                    lash = Seg(u, v, fu - outer * 0.5, fv + EyeRy + 0.25, fu - outer * 0.85, fv + EyeRy + 0.55, out qx, out qy, out t) - 0.07;
                    Over(0.1, 0.08, 0.08, Cov(lash) * vis);
                }
            }
        }

        // Thick black angry brows slanting down towards the middle.
        void Brows(double u, double v)
        {
            double lift = face.BrowRaise * 0.4 - 0.1;
            double slant = 0.55 + face.BrowAngle * 0.35;
            if (Math.Abs(v - (BrowV + lift)) > 1.4) return;
            Brow(u, v, eyeAlpha[0], -1, BrowV + lift, slant, eyeVis[0]);
            Brow(u, v, eyeAlpha[1], 1, BrowV + lift + face.BrowAsym * 0.3, slant, eyeVis[1]);
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
            double d = Seg(u, v, ix, iy, ox, oy, out qx, out qy, out t) - (0.34 - 0.10 * t);
            Over(0.07, 0.06, 0.06, Cov(d) * show);
        }

        void Beak(double u, double v)
        {
            double vis = cosY > 0.1 ? 1 : cosY < -0.2 ? 0 : (cosY + 0.2) / 0.3;
            if (vis <= 0) return;
            double mu = beakU;
            double mv = BeakV;
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
                Over(0.75, 0.12, 0.12, Cov(Math.Sqrt(tx * tx + ty * ty) - 0.17) * vis);
            }
        }

        void Cheeks(double u, double v, double bodyDist)
        {
            if (kind != BirdKind.Matilda && kind != BirdKind.Stella && kind != BirdKind.Bubbles) return;
            double off = 1.9 * (bellyRx / BellyRx);
            double cy = 6.1;
            if (Math.Abs(v - cy) > 1.2) return;
            double lx = u - (bellyU - off), ly = v - cy;
            Over(0.95, 0.55, 0.60, Cov(Math.Sqrt(lx * lx + ly * ly) - 0.36) * 0.55 * Cov(bodyDist));
            lx = u - (bellyU + off);
            Over(0.95, 0.55, 0.60, Cov(Math.Sqrt(lx * lx + ly * ly) - 0.36) * 0.55 * Cov(bodyDist));
        }

        // Terence's freckles, clipped to the silhouette: dark and clear of the eyes.
        void Freckles(double u, double v, double bodyDist)
        {
            if (kind != BirdKind.Terence) return;
            if (Math.Abs(u - bellyU) > 3.2 || v < 7.2 || v > 11.2) return;
            double clip = Cov(bodyDist);
            if (clip <= 0) return;
            double fx = u - (bellyU - 2.0), fy = v - 8.6;
            Over(0.28, 0.06, 0.05, Cov(Math.Sqrt(fx * fx + fy * fy) - 0.21) * 0.75 * clip);
            fx = u - (bellyU + 2.1); fy = v - 8.2;
            Over(0.28, 0.06, 0.05, Cov(Math.Sqrt(fx * fx + fy * fy) - 0.19) * 0.75 * clip);
            fx = u - (bellyU - 0.3); fy = v - 10.7;
            Over(0.28, 0.06, 0.05, Cov(Math.Sqrt(fx * fx + fy * fy) - 0.18) * 0.75 * clip);
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
            double cx0 = 0, cy0 = BodyV;
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
