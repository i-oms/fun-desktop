using System;
using IdleWalker.Domain;

namespace IdleWalker.Rendering
{
    /// <summary>
    /// Paints a Minion into a <see cref="Surface"/>. There is no sprite
    /// sheet: the figure is a handful of solids - a capsule for the body,
    /// cylinders for the limbs, a sphere behind glass for each eye, a torus
    /// for each goggle rim. Every pixel gets a signed distance to each solid
    /// (for an anti-aliased edge) and a surface normal (for lighting), and
    /// the solids are composited back to front. Because poses and faces are
    /// numbers rather than pictures, everything blends smoothly.
    ///
    /// Model space: u runs across in "units", 0 at the centre, positive
    /// towards where the Minion faces; v runs up from the soles. The figure
    /// is <see cref="Height"/> units tall, the same as the old pixel art, so
    /// one unit is one old art cell on screen.
    ///
    /// The figure can turn about its vertical axis (<see cref="Pose.Yaw"/>).
    /// The body is a capsule so its outline does not change, but everything
    /// attached to it - goggles, mouth, overalls, hair, limbs - sits at an
    /// angle round the axis and is swung round, foreshortened and hidden
    /// behind the body as it turns. The arms carry a depth as well as a
    /// position, and every pixel of them is sorted against the body's
    /// surface, so a hand slides behind the body or comes out in front of
    /// it continuously instead of the whole arm flipping over at once.
    /// </summary>
    sealed class MinionPainter
    {
        public const double Height = 18;

        // Light direction (towards the light), screen terms: upper left, in front.
        const double Lx = -0.45, Ly = 0.72, Lz = 0.53;
        // Half vector between the light and the viewer, for highlights.
        const double Hx = -0.257, Hy = 0.411, Hz = 0.874;

        // Body: a capsule of radius BodyR with its axis from BodyBottom to BodyTop.
        const double BodyR = 5.4, BodyBottom = 7.6, BodyTop = 12.6;
        const double WaistV = 4.6;                        // overalls below this
        const double BandBottom = 11.85, BandTop = 13.25; // the goggle strap
        const double EyeV = 12.55, MouthV = 9.0;
        const double EyeAngle = 26;                       // each eye this far round from the front, degrees
        const double GoggleOut = 0.4;                     // goggles stand this far off the body

        const double HipV = 4.3, HipX = 1.7, LegLen = 3.0, LegR = 0.95;
        // Shoulders sit on the body surface, ShoulderAngle round from the
        // front on each side: just forward of the widest point, so a hanging
        // arm shows beside the body when the Minion faces you and swings
        // round in front of it as the body turns away.
        const double ShoulderAngle = 78, ShoulderOut = 0.15, ShoulderV = 10.2, UpperArm = 1.75, Forearm = 1.85;
        const double RestSpread = 7;                      // a hanging arm angles out this far, degrees
        const double ArmR = 0.72, ForeR = 0.66, GloveR = 0.95;
        const double FingerLen = 0.55, FingerR = 0.3;    // an index finger past the glove when pointing

        const double Deg = Math.PI / 180;

        readonly Pose pose;
        readonly Look look;
        readonly Expression face;
        readonly double flip;   // -1 when facing left, so the light stays on the screen's left
        readonly double px;     // one pixel in units, for anti-aliasing
        readonly double yaw, sinY, cosY;
        readonly double lean;

        // Eyes: angle from the viewer, screen centre, horizontal squeeze, visibility.
        readonly int eyes;
        readonly double lensR, ringR, tubeR;
        readonly double[] eyeAlpha = new double[2], eyeU = new double[2], eyeK = new double[2], eyeVis = new double[2];
        readonly int firstEye;  // the one further back is painted first

        // Legs: hip and ankle of each, and which is behind.
        readonly double[] hipU = new double[2], ankleU = new double[2], ankleV = new double[2], legZ = new double[2];
        readonly double shoeLen, toe;

        // Arms: shoulder, elbow and hand of each, with the depth of each
        // joint (positive towards the viewer) and the screen box it all fits in.
        readonly double[] shU = new double[2], shZ = new double[2],
                          elbU = new double[2], elbV = new double[2], elbZ = new double[2],
                          handU = new double[2], handV = new double[2], handZ = new double[2],
                          armLo = new double[2], armHi = new double[2], armVlo = new double[2], armVhi = new double[2];

        readonly double[] hair;              // segments: u0, v0, u1, v1, r0, r1
        readonly bool decorated;             // anything floating about the figure this frame

        // Depth of the body's surface at the pixel being shaded, and how far
        // in front of it a limb has to be to count as in front: a soft band
        // rather than a knife edge, so the join does not shimmer.
        double bodyZ;
        readonly double depthBlend;
        const double ArmRadius = BodyR + ShoulderOut;

        // Limb pieces that turned out to be in front of the body at this
        // pixel, held back until the face has been painted.
        readonly double[] frontR = new double[6], frontG = new double[6], frontB = new double[6], frontCov = new double[6];
        int frontCount;

        // The pixel being shaded, premultiplied.
        double ca, cr, cg, cb;

        MinionPainter(Pose pose, Look look, bool faceLeft, double px)
        {
            this.pose = pose;
            this.look = look;
            face = pose.Face;
            flip = faceLeft ? -1 : 1;
            this.px = px;
            lean = pose.Lean;
            // A spin can carry the yaw past a full turn; bring it back to
            // (-180, 180] so the body's arc lengths stay in range.
            double turn = pose.Yaw % 360;
            if (turn > 180) turn -= 360; else if (turn <= -180) turn += 360;
            yaw = turn * Deg;
            sinY = Math.Sin(yaw);
            cosY = Math.Cos(yaw);
            depthBlend = 2.5 * px;

            // Eyes sit on the surface at +-EyeAngle from the front (one eye: dead ahead).
            if (look.OneEye)
            {
                eyes = 1;
                lensR = 2.35; ringR = 2.65; tubeR = 0.36;
                PlaceEye(0, 0);
                firstEye = 0;
            }
            else
            {
                eyes = 2;
                lensR = 1.85; ringR = 2.12; tubeR = 0.3;
                PlaceEye(0, -EyeAngle * Deg);
                PlaceEye(1, EyeAngle * Deg);
                firstEye = Math.Cos(eyeAlpha[0]) < Math.Cos(eyeAlpha[1]) ? 0 : 1;
            }

            // Legs: hips either side of the axis, swung round by the yaw. The
            // stride is along the direction of travel, i.e. across the screen.
            double p = pose.WalkPhase * 2 * Math.PI;
            double swing = pose.Walking ? 38 * Math.Sin(p) : 0;
            double liftA = pose.Walking ? 0.65 * Math.Max(0, Math.Cos(p)) : 0;
            double liftB = pose.Walking ? 0.65 * Math.Max(0, -Math.Cos(p)) : 0;
            PlaceLeg(0, -HipX, -swing, liftB);
            PlaceLeg(1, HipX, swing, liftA);
            shoeLen = 1.2 + 0.75 * Math.Abs(sinY);       // seen from the side a shoe is long
            toe = 0.3 + 0.7 * sinY;                      // and its toe points the way it is going

            // Arms: shoulders on the sides of the body, swung round likewise.
            // A hanging arm swings fore and aft - the way the Minion is going,
            // which brings the hand towards and away from the viewer as the
            // body turns - opposite to the leg on the same side. An explicitly
            // raised arm (waving, flailing overhead, hands on hips, ...) stays
            // where the pose puts it.
            double armSwing = pose.Walking ? 32 * Math.Sin(p) : 0;
            double foreSwing = pose.Walking ? 14 : 0;
            if (pose.ArmNear != 0 || pose.ArmFar != 0) armSwing = 0;
            if (pose.ForeNear != 0 || pose.ForeFar != 0) foreSwing = 0;
            PlaceArm(0, -1, pose.ArmFar, pose.ForeFar, armSwing, foreSwing);
            PlaceArm(1, 1, pose.ArmNear, pose.ForeNear, -armSwing, foreSwing);

            hair = HairFor(look.Hair);
            decorated = pose.Sweat > 0 || pose.Note || pose.Zzz || pose.Bubble || pose.Heart;
        }

        // Where an eye at `phi` round the body ends up once the body has turned.
        void PlaceEye(int i, double phi)
        {
            double a = phi + yaw;
            eyeAlpha[i] = a;
            eyeU[i] = (BodyR + GoggleOut) * Math.Sin(a);
            double c = Math.Cos(a);
            eyeK[i] = c > 0.22 ? c : 0.22;             // the goggle is a barrel, never quite edge-on
            double away = Math.Abs(a) / Deg;
            eyeVis[i] = away <= 78 ? 1 : away >= 100 ? 0 : (100 - away) / 22;
        }

        void PlaceLeg(int i, double x, double angle, double lift)
        {
            hipU[i] = x * cosY;
            legZ[i] = -x * sinY;
            double a = angle * Deg;
            double u = hipU[i] + LegLen * Math.Sin(a);
            double v = HipV - LegLen * Math.Cos(a) + lift;
            // Knees up: the foot comes back and up under the body.
            u += (hipU[i] - 0.9 - u) * pose.LegTuck;
            v += (HipV - 1.5 - v) * pose.LegTuck;
            ankleU[i] = u;
            ankleV[i] = v;
        }

        /// <summary>
        /// Lays out one arm. `side` is -1 or 1. `raise` and `bend` are the
        /// pose's angles, measured in the picture plane through the shoulder
        /// (0 hanging, positive out to that arm's own side, 180 straight up),
        /// so a raised hand reads the same from any angle. `swing` and
        /// `foreSwing` are the walk, measured fore and aft of the body: a
        /// forward swing carries the hand the way the Minion faces, which is
        /// partly towards the viewer when it is turned.
        /// </summary>
        void PlaceArm(int i, double side, double raise, double bend, double swing, double foreSwing)
        {
            double at = side * ShoulderAngle * Deg + yaw;
            shU[i] = ArmRadius * Math.Sin(at);
            shZ[i] = ArmRadius * Math.Cos(at);

            // A hanging arm angles out a little so the hand clears the hip.
            if (raise == 0) raise = side * RestSpread;

            double a = raise * Deg, b = swing * Deg;
            double sa = Math.Sin(a), ca = Math.Cos(a), sb = Math.Sin(b), cb = Math.Cos(b);
            elbU[i] = shU[i] + UpperArm * (sa + ca * sb * sinY);
            elbV[i] = ShoulderV - UpperArm * ca * cb;
            elbZ[i] = shZ[i] + UpperArm * ca * sb * cosY;

            a = (raise + bend) * Deg;
            b = (swing + foreSwing) * Deg;
            sa = Math.Sin(a); ca = Math.Cos(a); sb = Math.Sin(b); cb = Math.Cos(b);
            handU[i] = elbU[i] + Forearm * (sa + ca * sb * sinY);
            handV[i] = elbV[i] - Forearm * ca * cb;
            handZ[i] = elbZ[i] + Forearm * ca * sb * cosY;

            // The box everything fits in, with room for a pointing finger.
            double pad = side > 0 && pose.PointNear ? 1.2 + FingerLen : 1.2;
            armLo[i] = Math.Min(Math.Min(shU[i], elbU[i]), handU[i]) - pad;
            armHi[i] = Math.Max(Math.Max(shU[i], elbU[i]), handU[i]) + pad;
            armVlo[i] = Math.Min(Math.Min(ShoulderV, elbV[i]), handV[i]) - pad;
            armVhi[i] = Math.Max(Math.Max(ShoulderV, elbV[i]), handV[i]) + pad;
        }

        static double DomeTop(double x)
        {
            double d = BodyR * BodyR - x * x;
            return BodyTop + (d > 0 ? Math.Sqrt(d) : 0);
        }

        // Hair strands are laid out as if seen from the front, then each is
        // swung round the dome to where the yaw puts its root.
        double[] HairFor(HairStyle style)
        {
            double[] s;
            switch (style)
            {
                case HairStyle.Sprouts:
                {
                    double[] at = { -1.1, 0.1, 1.2 };
                    double[] tilt = { -0.5, 0.1, 0.6 };
                    s = new double[at.Length * 12];
                    for (int i = 0; i < at.Length; i++)
                    {
                        double u0 = at[i], v0 = DomeTop(at[i]) - 0.15;
                        double u1 = u0 + tilt[i] * 0.6, v1 = v0 + 1.25;
                        double u2 = u1 + tilt[i] * 1.2, v2 = v1 + 1.1;
                        Strand(s, i * 12, u0, v0, u1, v1, 0.26, 0.17);
                        Strand(s, i * 12 + 6, u1, v1, u2, v2, 0.17, 0.06);
                    }
                    break;
                }
                case HairStyle.Combed:
                {
                    double[] at = { -2.6, -1.9, -1.2, -0.5, 0.2, 0.9 };
                    s = new double[at.Length * 12];
                    for (int i = 0; i < at.Length; i++)
                    {
                        double u0 = at[i], u1 = at[i] + 1.4, u2 = at[i] + 2.5;
                        Strand(s, i * 12, u0, DomeTop(u0) - 0.1, u1, DomeTop(u1) + 0.28, 0.24, 0.15);
                        Strand(s, i * 12 + 6, u1, DomeTop(u1) + 0.28, u2, DomeTop(u2) + 0.22, 0.15, 0.05);
                    }
                    break;
                }
                case HairStyle.Parted:
                {
                    double[] at = { -0.4, -1.1, -1.8, 0.5, 1.2, 1.9 };
                    s = new double[at.Length * 12];
                    for (int i = 0; i < at.Length; i++)
                    {
                        double side = at[i] < 0 ? -1 : 1;
                        double u0 = at[i], v0 = DomeTop(at[i]) - 0.15;
                        double u1 = u0 + side * 0.5, v1 = v0 + 0.95;
                        double u2 = u1 + side * 0.95, v2 = v1 + 0.45;
                        Strand(s, i * 12, u0, v0, u1, v1, 0.25, 0.16);
                        Strand(s, i * 12 + 6, u1, v1, u2, v2, 0.16, 0.05);
                    }
                    break;
                }
                case HairStyle.Crew:
                {
                    const int n = 9;
                    s = new double[n * 6];
                    for (int i = 0; i < n; i++)
                    {
                        double u0 = -3.0 + 0.75 * i;
                        double nx = u0 / BodyR, ny = Math.Sqrt(Math.Max(0, 1 - nx * nx));
                        Strand(s, i * 6, u0, DomeTop(u0) - 0.15, u0 + nx * 1.0, DomeTop(u0) - 0.15 + ny * 1.0, 0.24, 0.09);
                    }
                    break;
                }
                case HairStyle.Spiky:
                {
                    // Short, stiff spikes radiating straight out of the dome.
                    double[] at = { -2.4, -1.3, -0.2, 0.9, 2.0 };
                    s = new double[at.Length * 6];
                    for (int i = 0; i < at.Length; i++)
                    {
                        double u0 = at[i];
                        double nx = u0 / BodyR, ny = Math.Sqrt(Math.Max(0, 1 - nx * nx));
                        Strand(s, i * 6, u0, DomeTop(u0) - 0.15, u0 + nx * 1.9, DomeTop(u0) - 0.15 + ny * 1.9, 0.3, 0.04);
                    }
                    break;
                }
                case HairStyle.Quiff:
                {
                    // A tall wave rising from the front and curling back.
                    double[] at = { 1.8, 1.1, 0.4, -0.3, -1.0 };
                    s = new double[at.Length * 12];
                    for (int i = 0; i < at.Length; i++)
                    {
                        double u0 = at[i], v0 = DomeTop(at[i]) - 0.15;
                        double u1 = u0 + 0.35, v1 = v0 + 1.6 - 0.12 * i;
                        double u2 = u1 - 0.9, v2 = v1 + 0.9 - 0.1 * i;
                        Strand(s, i * 12, u0, v0, u1, v1, 0.3, 0.22);
                        Strand(s, i * 12 + 6, u1, v1, u2, v2, 0.22, 0.06);
                    }
                    break;
                }
                case HairStyle.Tuft:
                {
                    // One long curl: up from the crown, over, and back down.
                    s = new double[3 * 6];
                    double t0 = 0.1, tv0 = DomeTop(0.1) - 0.15;
                    Strand(s, 0, t0, tv0, t0 + 0.3, tv0 + 1.8, 0.34, 0.26);
                    Strand(s, 6, t0 + 0.3, tv0 + 1.8, t0 + 1.4, tv0 + 2.5, 0.26, 0.18);
                    Strand(s, 12, t0 + 1.4, tv0 + 2.5, t0 + 2.2, tv0 + 1.7, 0.18, 0.05);
                    break;
                }
                case HairStyle.Wild:
                {
                    // Long strands sticking out every which way, as if it had a fright.
                    double[] at = { -2.6, -1.6, -0.7, 0.3, 1.2, 2.3 };
                    double[] tilt = { -1.1, -0.4, 0.5, -0.6, 0.9, 1.2 };
                    double[] len = { 1.5, 2.1, 1.7, 2.3, 1.6, 1.9 };
                    s = new double[at.Length * 12];
                    for (int i = 0; i < at.Length; i++)
                    {
                        double u0 = at[i], v0 = DomeTop(at[i]) - 0.15;
                        double u1 = u0 + tilt[i] * 0.5, v1 = v0 + len[i] * 0.55;
                        double u2 = u1 + tilt[i] * 0.9 * (i % 2 == 0 ? -0.5 : 1), v2 = v1 + len[i] * 0.45;
                        Strand(s, i * 12, u0, v0, u1, v1, 0.24, 0.15);
                        Strand(s, i * 12 + 6, u1, v1, u2, v2, 0.15, 0.04);
                    }
                    break;
                }
                default:
                    return new double[0];
            }

            // Swing each strand round: its root is at some angle round the
            // dome's circle of latitude; move it to that angle plus the yaw.
            for (int i = 0; i < s.Length; i += 6)
            {
                double root = s[i], rootV = s[i + 1];
                double dv = rootV - BodyTop;
                double ring = Math.Sqrt(Math.Max(0.3, BodyR * BodyR - dv * dv));
                double sinPhi = root / ring;
                if (sinPhi > 1) sinPhi = 1; else if (sinPhi < -1) sinPhi = -1;
                double shift = ring * Math.Sin(Math.Asin(sinPhi) + yaw) - root;
                s[i] += shift;
                s[i + 2] += shift;
            }
            return s;
        }

        static void Strand(double[] s, int i, double u0, double v0, double u1, double v1, double r0, double r1)
        {
            s[i] = u0; s[i + 1] = v0; s[i + 2] = u1; s[i + 3] = v1; s[i + 4] = r0; s[i + 5] = r1;
        }

        // ---- Entry point ---------------------------------------------------------

        /// <summary>
        /// Paints the Minion with its soles on soleY, centred on centreX, at
        /// `scale` pixels per unit (before squash and stretch).
        /// </summary>
        public static void Draw(Surface surface, Pose pose, Look look, bool faceLeft,
                                double centreX, double soleY, double scale)
        {
            double sx = scale * pose.StretchX, sy = scale * pose.StretchY;
            double px = 1 / Math.Min(sx, sy);
            MinionPainter painter = new MinionPainter(pose, look, faceLeft, px);

            int left = Math.Max(0, (int)Math.Floor(centreX - 10.5 * sx));
            int right = Math.Min(surface.Width, (int)Math.Ceiling(centreX + 10.5 * sx));
            int top = Math.Max(0, (int)Math.Floor(soleY - 26 * sy));
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
            ca = cr = cg = cb = 0;

            // The upper body bobs and leans; the legs stay planted.
            double vb = v + pose.BodyDy;
            double ub = u - lean * 0.11 * Math.Max(0, v - 2);

            // The body's distance field: how far this pixel is from the
            // capsule's axis, and how far forward its surface bulges here.
            // Everything that dips into the body is sorted against this.
            double axisV = vb < BodyBottom ? BodyBottom : vb > BodyTop ? BodyTop : vb;
            double bdy = vb - axisV;
            double bodyDist = Math.Sqrt(ub * ub + bdy * bdy);
            double bulge = BodyR * BodyR - ub * ub - bdy * bdy;
            bodyZ = bulge > 0 ? Math.Sqrt(bulge) : 0;

            // Back to front: whatever the turn has put behind the body first.
            // Each arm is painted once; the parts of it that are in front of
            // the body are held back and laid on after the face.
            int backLeg = legZ[0] < legZ[1] ? 0 : 1;
            Leg(u, v, backLeg);
            Leg(u, v, 1 - backLeg);
            frontCount = 0;
            Limb(ub, vb, 0);
            Limb(ub, vb, 1);
            Body(ub, vb, bdy, bodyDist);
            if (eyes == 2) Eye(ub, vb, firstEye);
            Eye(ub, vb, eyes == 2 ? 1 - firstEye : 0);
            Brows(ub, vb);
            Mouth(ub, vb);
            Hair(ub, vb);
            for (int i = 0; i < frontCount; i++) Over(frontR[i], frontG[i], frontB[i], frontCov[i]);

            // Things floating about the figure, in front of everything.
            if (decorated)
            {
                if (pose.Sweat > 0) Sweat(ub, vb);
                if (pose.Note) Note(ub, vb);
                if (pose.Zzz) Zzz(u, v);
                if (pose.Bubble) Bubble(ub, vb);
                if (pose.Heart) Heart(ub, vb);
            }

            if (ca <= 0.003) return 0;
            if (ca > 1) ca = 1;
            int a = (int)(ca * 255 + 0.5);
            int r = (int)(Clamp(cr) * 255 + 0.5);
            int g = (int)(Clamp(cg) * 255 + 0.5);
            int b = (int)(Clamp(cb) * 255 + 0.5);
            return (a << 24) | (r << 16) | (g << 8) | b;
        }

        // ---- Compositing and lighting ---------------------------------------------

        double Cov(double d)
        {
            double c = 0.5 - d / px;
            return c < 0 ? 0 : c > 1 ? 1 : c;
        }

        static double Clamp(double x)
        {
            return x < 0 ? 0 : x > 1 ? 1 : x;
        }

        void Over(double r, double g, double b, double cov)
        {
            if (cov <= 0) return;
            double keep = 1 - cov;
            cr = r * cov + cr * keep;
            cg = g * cov + cg * keep;
            cb = b * cov + cb * keep;
            ca = cov + ca * keep;
        }

        /// <summary>Lights an albedo with a normal (nx right, ny up, nz towards the viewer).</summary>
        void Lit(double r, double g, double b, double nx, double ny, double nz, double spec, double shine,
                 out double lr, out double lg, out double lb)
        {
            nx *= flip;
            double diffuse = nx * Lx + ny * Ly + nz * Lz;
            if (diffuse < 0) diffuse = 0;
            double k = (0.40 + 0.62 * diffuse) * (0.6 + 0.4 * Math.Sqrt(nz < 0 ? 0 : nz));
            double h = nx * Hx + ny * Hy + nz * Hz;
            double s = h > 0 ? spec * Math.Pow(h, shine) : 0;
            lr = r * k + s;
            lg = g * k + s;
            lb = b * k + s;
        }

        /// <summary>Normal of a sphere (or a tube seen side on) from the offset to its axis.</summary>
        static void Normal(double dx, double dy, double radius, out double nx, out double ny, out double nz)
        {
            nx = dx / radius;
            ny = dy / radius;
            double m = nx * nx + ny * ny;
            if (m > 0.995)
            {
                double f = Math.Sqrt(0.995 / m);
                nx *= f;
                ny *= f;
                m = 0.995;
            }
            nz = Math.Sqrt(1 - m);
        }

        /// <summary>Turns a normal about the vertical axis by angle a.</summary>
        static void Turn(ref double nx, ref double nz, double a)
        {
            double c = Math.Cos(a), s = Math.Sin(a);
            double x = nx * c + nz * s;
            nz = -nx * s + nz * c;
            nx = x;
        }

        // ---- Distance fields ----------------------------------------------------------

        static double Seg(double x, double y, double ax, double ay, double bx, double by,
                          out double qx, out double qy, out double t)
        {
            double abx = bx - ax, aby = by - ay;
            double len2 = abx * abx + aby * aby;
            t = len2 > 0 ? ((x - ax) * abx + (y - ay) * aby) / len2 : 0;
            if (t < 0) t = 0; else if (t > 1) t = 1;
            qx = ax + abx * t;
            qy = ay + aby * t;
            double dx = x - qx, dy = y - qy;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        static double RoundRect(double x, double y, double hw, double hh, double r)
        {
            double qx = Math.Abs(x) - (hw - r), qy = Math.Abs(y) - (hh - r);
            double ox = qx > 0 ? qx : 0, oy = qy > 0 ? qy : 0;
            double inside = Math.Min(Math.Max(qx, qy), 0);
            return Math.Sqrt(ox * ox + oy * oy) + inside - r;
        }

        /// <summary>
        /// Distance to a circle of radius r that has been squeezed to k of
        /// its width: dx is already divided by k. First-order exact.
        /// </summary>
        static double Squeezed(double dx, double dy, double k, double r)
        {
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 1e-6) return -r;
            double grad = Math.Sqrt(dx * dx / (k * k) + dy * dy) / dist;
            return (dist - r) / grad;
        }

        // ---- The parts, back to front ---------------------------------------------------

        /// <summary>A cylinder between two points, lit as a tube.</summary>
        void Tube(double x, double y, double ax, double ay, double bx, double by, double radius,
                  double r, double g, double b, double spec, double shine)
        {
            double qx, qy, t;
            double dist = Seg(x, y, ax, ay, bx, by, out qx, out qy, out t);
            double cov = Cov(dist - radius);
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(x - qx, y - qy, radius, out nx, out ny, out nz);
            double lr, lg, lb;
            Lit(r, g, b, nx, ny, nz, spec, shine, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
        }


        void Leg(double u, double v, int i)
        {
            double k = 0.78 + 0.22 * (1 + legZ[i] / HipX) / 2;
            if (v > HipV + 1.5) return;
            double au = ankleU[i], av = ankleV[i];
            if (u < Math.Min(hipU[i], au) - 2.5 || u > Math.Max(hipU[i], au) + 2.5) return;
            Tube(u, v, hipU[i], HipV, au, av, LegR, 0.17 * k, 0.35 * k, 0.72 * k, 0.06, 8);

            // The shoe: a glossy black ellipsoid under the ankle, toe forward.
            double sx = au + toe, sy = av - 0.5, rx = shoeLen, ry = 0.72;
            double dx = (u - sx) / rx, dy = (v - sy) / ry;
            double e = Math.Sqrt(dx * dx + dy * dy);
            double cov = Cov((e - 1) * ry);
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(dx, dy, 1, out nx, out ny, out nz);
            double lr, lg, lb;
            Lit(0.10 * k, 0.09 * k, 0.12 * k, nx, ny, nz, 0.6, 22, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
        }

        /// <summary>The body at (u, v): dy is the height above the nearest point of the capsule's axis, dist the distance from it.</summary>
        void Body(double u, double v, double dy, double dist)
        {
            double cov = Cov(dist - BodyR);
            if (cov <= 0) return;

            double nx, ny, nz;
            Normal(u, dy, BodyR, out nx, out ny, out nz);

            // Where on the surface this is, measured round the body from the
            // front of the Minion (not the front as seen), as an arc length.
            double phi = Math.Atan2(nx, nz) - yaw;
            if (phi > Math.PI) phi -= 2 * Math.PI; else if (phi < -Math.PI) phi += 2 * Math.PI;
            double arc = BodyR * phi;

            // Yellow skin, with the overalls and the goggle strap painted on.
            double r = 0.98, g = 0.84, b = 0.11, spec = 0.35, shine = 26;
            double denim = Math.Max(Cov(v - WaistV), Cov(RoundRect(arc, v - 6.1, 2.5, 1.5, 0.45)));
            Mix(ref r, ref g, ref b, ref spec, ref shine, 0.17, 0.35, 0.72, 0.06, 8, denim);

            double qx, qy, t;
            double straps = Math.Max(Cov(Seg(arc, v, 2.2, 7.4, 4.4, 9.6, out qx, out qy, out t) - 0.42),
                                     Cov(Seg(arc, v, -2.2, 7.4, -4.4, 9.6, out qx, out qy, out t) - 0.42));
            double pocket = Cov(RoundRect(arc, v - 6.0, 0.85, 0.6, 0.2));
            Mix(ref r, ref g, ref b, ref spec, ref shine, 0.13, 0.27, 0.58, 0.06, 8, Math.Max(straps, pocket));

            double band = Cov(Math.Max(BandBottom - v, v - BandTop));
            Mix(ref r, ref g, ref b, ref spec, ref shine, 0.30, 0.31, 0.34, 0.2, 10, band);

            double lr, lg, lb;
            Lit(r, g, b, nx, ny, nz, spec, shine, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
        }

        static void Mix(ref double r, ref double g, ref double b, ref double spec, ref double shine,
                        double mr, double mg, double mb, double mspec, double mshine, double w)
        {
            if (w <= 0) return;
            r += (mr - r) * w;
            g += (mg - g) * w;
            b += (mb - b) * w;
            spec += (mspec - spec) * w;
            shine += (mshine - shine) * w;
        }

        void Eye(double u, double v, int i)
        {
            double vis = eyeVis[i];
            if (vis <= 0) return;
            double k = eyeK[i], alpha = eyeAlpha[i];
            bool near = i == eyes - 1;

            // Work in the eye's own frame: squeezed horizontally as it turns away.
            double dx = (u - eyeU[i]) / k, dy = v - EyeV;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist > ringR + tubeR + 0.6) return;

            double covLens = Cov(Squeezed(dx, dy, k, lensR)) * vis;
            if (covLens > 0)
            {
                // The eyeball: a white sphere bulging out through the lens.
                double nx, ny, nz;
                Normal(dx, dy, lensR * 1.3, out nx, out ny, out nz);
                Turn(ref nx, ref nz, alpha);
                double r, g, b;
                Lit(0.96, 0.96, 0.94, nx, ny, nz, 0.5, 30, out r, out g, out b);

                // Where the iris sits: following the gaze, kept inside the lens.
                double irisR = lensR * 0.47;
                double reach = lensR - irisR - 0.12;
                double ox = pose.GazeX * reach * 0.9 + face.PupilDx;
                double oy = pose.GazeY * reach * 0.7 + face.PupilDy;
                if (eyes == 2) ox -= (i == 1 ? 1 : -1) * face.CrossEye * 0.75;
                double om = Math.Sqrt(ox * ox + oy * oy);
                if (om > reach) { ox *= reach / om; oy *= reach / om; }
                double ix = dx - ox, iy = dy - oy;
                double id = Math.Sqrt(ix * ix + iy * iy);

                double covIris = Cov(Squeezed(ix, iy, k, irisR));
                if (covIris > 0)
                {
                    int rgb = near ? look.NearIris : look.FarIris;
                    double t = id / irisR;
                    double shade = 1.25 - 0.55 * t;
                    if (t > 0.82) shade *= 1 - 0.55 * (t - 0.82) / 0.18;
                    double ir = ((rgb >> 16) & 255) / 255.0 * shade;
                    double ig = ((rgb >> 8) & 255) / 255.0 * shade;
                    double ib = (rgb & 255) / 255.0 * shade;
                    r += (ir - r) * covIris;
                    g += (ig - g) * covIris;
                    b += (ib - b) * covIris;

                    double covPupil = Cov(Squeezed(ix, iy, k, irisR * 0.5 * face.PupilSize));
                    r += (0.05 - r) * covPupil;
                    g += (0.04 - g) * covPupil;
                    b += (0.06 - b) * covPupil;

                    // A catchlight from the window.
                    double lx = ix + flip * 0.36 * irisR, ly = iy - 0.36 * irisR;
                    double covLight = Cov(Squeezed(lx, ly, k, irisR * 0.24)) * 0.9;
                    r += (1 - r) * covLight;
                    g += (1 - g) * covLight;
                    b += (1 - b) * covLight;
                }

                // Lids: yellow skin sliding down (and up) over the glass.
                double upper = Math.Max(face.Lid, near ? face.Wink : 0);
                double lower = face.Squint;
                double s = (dx / lensR) * (dx / lensR);
                double upperEdge = EyeV + lensR * (1 - 2 * upper) - 0.4 * upper * s;
                double lowerEdge = EyeV - lensR * (1 - 2 * lower) + 0.4 * lower * s;
                if (upper > 0.01 || lower > 0.01)
                {
                    double bx, by, bz;
                    Normal(u, v - (v < BodyBottom ? BodyBottom : v > BodyTop ? BodyTop : v), BodyR, out bx, out by, out bz);
                    double sr, sg, sb;
                    Lit(0.93, 0.79, 0.10, bx, by, bz, 0.3, 24, out sr, out sg, out sb);
                    double covLid = Math.Max(upper > 0.01 ? Cov(upperEdge - v) : 0, lower > 0.01 ? Cov(v - lowerEdge) : 0);
                    r += (sr - r) * covLid;
                    g += (sg - g) * covLid;
                    b += (sb - b) * covLid;
                    double lash = Math.Max(upper > 0.01 ? Cov(Math.Abs(v - upperEdge) - 0.12) : 0,
                                           lower > 0.01 ? Cov(Math.Abs(v - lowerEdge) - 0.1) : 0) * 0.75;
                    r += (0.16 - r) * lash;
                    g += (0.10 - g) * lash;
                    b += (0.08 - b) * lash;
                }

                // A soft reflection on the glass.
                double gx = (dx + flip * 0.45 * lensR) / (0.5 * lensR), gy = (dy - 0.45 * lensR) / (0.32 * lensR);
                double gt = gx * gx + gy * gy;
                if (gt < 1)
                {
                    double w = 0.28 * (1 - gt);
                    r += (1 - r) * w;
                    g += (1 - g) * w;
                    b += (1 - b) * w;
                }

                Over(r, g, b, covLens);
            }

            // The rim: a steel ring, lit as a torus.
            double ringD = Squeezed(dx, dy, k, ringR);
            double covRing = Cov(Math.Abs(ringD) - tubeR) * vis;
            if (covRing > 0)
            {
                double t = (dist - ringR) / tubeR;
                if (t > 0.995) t = 0.995; else if (t < -0.995) t = -0.995;
                double ux = dist > 0 ? dx / dist : 0, uy = dist > 0 ? dy / dist : 1;
                double nx = ux * t, ny = uy * t, nz = Math.Sqrt(1 - t * t);
                Turn(ref nx, ref nz, alpha);
                double tone = 0.86 + 0.26 * uy * t; // a hint of sky above, ground below
                double lr, lg, lb;
                Lit(0.60 * tone, 0.62 * tone, 0.66 * tone, nx, ny, nz, 0.9, 14, out lr, out lg, out lb);
                Over(lr, lg, lb, covRing);
            }
        }

        void Brows(double u, double v)
        {
            double show = Math.Abs(face.BrowAngle) * 1.2 + Math.Max(0, face.BrowRaise) + face.BrowAsym;
            if (show <= 0.02) return;
            if (show > 1) show = 1;
            double baseV = EyeV + ringR + tubeR + 0.45 + face.BrowRaise * 0.5;
            if (Math.Abs(v - baseV) > 2.5) return;

            if (eyes == 2)
            {
                Brow(u, v, eyeAlpha[0], -1, baseV, show * eyeVis[0]);
                Brow(u, v, eyeAlpha[1], 1, baseV + face.BrowAsym * 0.6, show * eyeVis[1]);
            }
            else
            {
                Brow(u, v, eyeAlpha[0] - 14 * Deg, -1, baseV, show);
                Brow(u, v, eyeAlpha[0] + 14 * Deg, 1, baseV + face.BrowAsym * 0.6, show);
            }
        }

        void Brow(double u, double v, double alpha, double outward, double cy, double show)
        {
            if (show <= 0) return;
            double k = Math.Cos(alpha);
            if (k < 0.2) return;
            double ex = (BodyR + 0.3) * Math.Sin(alpha);
            double ax = ex - outward * 1.15 * k, ay = cy - face.BrowAngle * 0.55;
            double bx = ex + outward * 1.15 * k, by = cy + face.BrowAngle * 0.55;
            double qx, qy, t;
            double dist = Seg(u, v, ax, ay, bx, by, out qx, out qy, out t);
            double cov = Cov(dist - (0.27 - 0.09 * t)) * show;
            Over(0.12, 0.09, 0.10, cov);
        }

        void Mouth(double u, double v)
        {
            // The mouth is on the front of the body, so it swings round and
            // narrows with the yaw like everything else, and goes out of
            // sight once the body has turned its back.
            double vis = cosY > 0.17 ? 1 : cosY < -0.08 ? 0 : (cosY + 0.08) / 0.25;
            if (vis <= 0) return;
            double k = cosY > 0.35 ? cosY : 0.35;
            double mu = BodyR * 0.93 * sinY + face.MouthDx * k;
            double w = face.MouthWidth * k;
            double hx = Math.Abs(u - mu) - w;
            if (hx > 0.8 || Math.Abs(v - MouthV) > 4) return;

            double t = (u - mu) / w;
            if (t < -1) t = -1; else if (t > 1) t = 1;
            double line = MouthV + face.MouthCurve * 0.8 * (t * t - 0.45) + face.Wobble * 0.18 * Math.Sin(t * 8.5);
            double open = face.MouthOpen;
            const double lip = 0.26;

            if (open < 0.03)
            {
                double ox = hx > 0 ? hx : 0, oy = v - line;
                double cov = Cov(Math.Sqrt(ox * ox + oy * oy) - lip / 2);
                Over(0.16, 0.10, 0.09, cov * vis);
                return;
            }

            double gape = open * 2.3 * Math.Pow(1 - t * t, 0.6);
            double upper = line, lower = line - gape;
            double d = Math.Max(Math.Max(v - upper, lower - v), hx);
            double inside = Cov(d);
            if (inside > 0)
            {
                Over(0.30, 0.07, 0.09, inside * vis);
                double teethH = open * 0.7 * face.Teeth;
                if (teethH > 0.05)
                {
                    double teeth = Cov(Math.Max(upper - teethH - v, d)) * inside;
                    double gap = Math.Abs(((u - mu) % 0.6 + 0.6) % 0.6 - 0.3);
                    double seam = Cov(gap - 0.03) * 0.35;
                    Over(0.95 - seam * 0.5, 0.94 - seam * 0.5, 0.90 - seam * 0.5, teeth * vis);
                }
            }
            double outline = Cov(Math.Abs(d) - lip / 2);
            Over(0.16, 0.10, 0.09, outline * vis);

            if (face.Tongue > 0.02) Tongue(u, v, mu, w, upper, lower, vis);
        }

        // A pink tongue: lying in the bottom of an open mouth, or, the more
        // of it there is, hanging out over the lower lip.
        void Tongue(double u, double v, double mu, double w, double upper, double lower, double vis)
        {
            double tongue = face.Tongue;
            double rx = Math.Min(w * 0.55, 0.8), ry = 0.35 + 0.5 * tongue;
            double cy = lower + 0.35 - 0.8 * tongue;
            double dx = (u - mu) / rx, dy = (v - cy) / ry;
            double e = Math.Sqrt(dx * dx + dy * dy);
            double rim = (e - 1) * ry;
            double d = Math.Max(rim, v - upper + 0.15);   // never over the upper lip
            double cov = Cov(d);
            if (cov <= 0) return;
            double shade = 1 - 0.25 * e;
            Over(0.12 + 0.86 * shade, 0.42 * shade, 0.52 * shade, cov * vis);
            double edge = Cov(Math.Abs(rim) - 0.09) * Cov(v - upper + 0.15) * 0.7;
            Over(0.16, 0.10, 0.09, edge * vis);
        }

        void Hair(double u, double v)
        {
            if (v < BodyTop + BodyR - 2.5) return;
            for (int i = 0; i < hair.Length; i += 6)
            {
                double qx, qy, t;
                double dist = Seg(u, v, hair[i], hair[i + 1], hair[i + 2], hair[i + 3], out qx, out qy, out t);
                double radius = hair[i + 4] + (hair[i + 5] - hair[i + 4]) * t;
                double cov = Cov(dist - radius);
                if (cov > 0) Over(0.10 * (0.8 + 0.4 * t), 0.08 * (0.8 + 0.4 * t), 0.10 * (0.8 + 0.4 * t), cov);
            }
        }

        /// <summary>
        /// One arm: two tubes and a glove, each sorted against the body
        /// pixel by pixel. Where a piece is behind the body's surface it is
        /// painted now and the body will cover it; where it is in front it
        /// is kept for after the face. The arm's own depth matters too: a
        /// far arm is a shade darker.
        /// </summary>
        void Limb(double u, double v, int i)
        {
            if (u < armLo[i] || u > armHi[i] || v < armVlo[i] || v > armVhi[i]) return;
            ArmTube(u, v, shU[i], ShoulderV, shZ[i], elbU[i], elbV[i], elbZ[i], ArmR, 0.98, 0.84, 0.11, 0.3, 24);
            ArmTube(u, v, elbU[i], elbV[i], elbZ[i], handU[i], handV[i], handZ[i], ForeR, 0.98, 0.84, 0.11, 0.3, 24);

            // Pointing: the index finger carries on past the glove in the
            // direction of the forearm.
            if (i == 1 && pose.PointNear)
            {
                double fx = handU[i] - elbU[i], fy = handV[i] - elbV[i];
                double len = Math.Sqrt(fx * fx + fy * fy);
                if (len > 1e-6)
                {
                    double reach = (GloveR + FingerLen) / len;
                    ArmTube(u, v, handU[i], handV[i], handZ[i], handU[i] + fx * reach, handV[i] + fy * reach, handZ[i],
                            FingerR, 0.11, 0.10, 0.13, 0.7, 18);
                }
            }

            double dx = u - handU[i], dy = v - handV[i];
            double cov = Cov(Math.Sqrt(dx * dx + dy * dy) - GloveR);
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(dx, dy, GloveR, out nx, out ny, out nz);
            double k = Depth(handZ[i]);
            double lr, lg, lb;
            Lit(0.11 * k, 0.10 * k, 0.13 * k, nx, ny, nz, 0.7, 18, out lr, out lg, out lb);
            Sort(lr, lg, lb, cov, handZ[i] + nz * GloveR);
        }

        void ArmTube(double x, double y, double ax, double ay, double az, double bx, double by, double bz, double radius,
                     double r, double g, double b, double spec, double shine)
        {
            double qx, qy, t;
            double dist = Seg(x, y, ax, ay, bx, by, out qx, out qy, out t);
            double cov = Cov(dist - radius);
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(x - qx, y - qy, radius, out nx, out ny, out nz);
            double z = az + (bz - az) * t;
            double k = Depth(z);
            double lr, lg, lb;
            Lit(r * k, g * k, b * k, nx, ny, nz, spec, shine, out lr, out lg, out lb);
            Sort(lr, lg, lb, cov, z + nz * radius);
        }

        /// <summary>How much darker something gets with distance from the viewer.</summary>
        static double Depth(double z)
        {
            double k = 0.89 + 0.11 * z / ArmRadius;
            return k < 0.78 ? 0.78 : k > 1 ? 1 : k;
        }

        /// <summary>
        /// Composites a limb piece at depth z against the body: the part
        /// behind the body's surface now, the part in front later. Across
        /// the surface the two blend over a band a couple of pixels deep,
        /// which reads as the arm sinking into the body.
        /// </summary>
        void Sort(double r, double g, double b, double cov, double z)
        {
            double f = 0.5 + (z - bodyZ) / depthBlend;
            if (f <= 0)
            {
                Over(r, g, b, cov);
                return;
            }
            if (f < 1) Over(r, g, b, cov * (1 - f)); else f = 1;
            int n = frontCount++;
            frontR[n] = r;
            frontG[n] = g;
            frontB[n] = b;
            frontCov[n] = cov * f;
        }

        // ---- Things floating about the figure ---------------------------------------------

        // A drop of sweat beside the head on the side away from where it
        // faces, sliding down and fading as Pose.Sweat goes 0..1.
        void Sweat(double u, double v)
        {
            double s = pose.Sweat;
            double x = -(BodyR + 1.1), y = 15.0 - 2.6 * s;
            if (Math.Abs(u - x) > 1.0 || Math.Abs(v - y - 0.3) > 1.3) return;
            double qx, qy, t;
            double dx = u - x, dy = v - y;
            double d = Math.Min(Math.Sqrt(dx * dx + dy * dy) - 0.42,
                                Seg(u, v, x, y + 1.0, x, y, out qx, out qy, out t) - (0.04 + 0.36 * t));
            double a = s < 0.75 ? 1 : (1 - s) / 0.25;
            Over(0.10, 0.22, 0.42, Cov(Math.Abs(d) - 0.07) * 0.5 * a);
            Over(0.55, 0.78, 0.98, Cov(d) * a);
            double hx = dx + 0.15, hy = dy - 0.12;
            Over(1, 1, 1, Cov(Math.Sqrt(hx * hx + hy * hy) - 0.12) * 0.9 * a);
        }

        // A music note drifting up and forward from the mouth.
        void Note(double u, double v)
        {
            double ph = pose.DecorPhase;
            double x = BodyR * 0.93 * sinY + 2.2 + 1.2 * ph, y = MouthV + 1.0 + 5.0 * ph;
            if (Math.Abs(u - x - 0.3) > 1.4 || Math.Abs(v - y - 0.8) > 1.6) return;
            const double s = 0.85;
            double dx = (u - x) / (0.5 * s), dy = (v - y) / (0.38 * s);
            double d = (Math.Sqrt(dx * dx + dy * dy) - 1) * 0.38 * s;          // the head
            double qx, qy, t;
            double sx = x + 0.42 * s;
            d = Math.Min(d, Seg(u, v, sx, y, sx, y + 1.7 * s, out qx, out qy, out t) - 0.1 * s);                    // the stem
            d = Math.Min(d, Seg(u, v, sx, y + 1.7 * s, sx + 0.6 * s, y + 1.15 * s, out qx, out qy, out t) - 0.12 * s); // the flag
            double a = ph < 0.75 ? 1 : (1 - ph) / 0.25;
            Over(0.98, 0.98, 1.0, Cov(d - 0.22) * 0.85 * a);
            Over(0.12, 0.10, 0.18, Cov(d) * a);
        }

        // Two "z"s floating up and away from the top of the head, growing
        // as they go; the second is half a drift behind the first.
        void Zzz(double u, double v)
        {
            if (v < 17.5 || u < 1.0 || u > 9.5) return;
            double ph = pose.DecorPhase;
            ZGlyph(u, v, ph);
            ZGlyph(u, v, ph >= 0.5 ? ph - 0.5 : ph + 0.5);
        }

        void ZGlyph(double u, double v, double ph)
        {
            double s = 1.0 + 0.6 * ph;
            double x = 3.4 + 2.4 * ph, y = 19.8 + 3.2 * ph;
            double h = s * 0.5;
            if (Math.Abs(u - x) > h + 0.6 || Math.Abs(v - y) > h + 0.6) return;
            double qx, qy, t;
            double d = Seg(u, v, x - h, y + h, x + h, y + h, out qx, out qy, out t);
            d = Math.Min(d, Seg(u, v, x + h, y + h, x - h, y - h, out qx, out qy, out t));
            d = Math.Min(d, Seg(u, v, x - h, y - h, x + h, y - h, out qx, out qy, out t));
            double r = 0.08 + 0.1 * s;
            double a = ph < 0.8 ? 1 : (1 - ph) / 0.2;
            Over(0.12, 0.10, 0.14, Cov(d - r - 0.25) * 0.8 * a);
            Over(0.95, 0.96, 1.0, Cov(d - r) * a);
        }

        void Bubble(double u, double v)
        {
            if (v < 18.5 || Math.Abs(u) > 1.5) return;
            double qx, qy, t;
            double bar = Seg(u, v, 0, 20.8, 0, 23.4, out qx, out qy, out t) - (0.78 - 0.18 * (1 - t));
            double dot = Math.Sqrt(u * u + (v - 19.3) * (v - 19.3)) - 0.75;
            double d = Math.Min(bar, dot);
            Over(0.12, 0.10, 0.14, Cov(d - 0.3) * 0.85);
            Over(0.98, 0.98, 0.98, Cov(d));
        }

        // A small pink heart drifting up and forward from the mouth, like
        // the note: two round lobes over a point.
        void Heart(double u, double v)
        {
            double ph = pose.DecorPhase;
            double x = BodyR * 0.93 * sinY + 2.4 + 1.6 * ph, y = MouthV + 1.2 + 5.0 * ph;
            if (Math.Abs(u - x) > 1.4 || Math.Abs(v - y) > 1.4) return;
            const double s = 0.8;
            double hx = Math.Abs(u - x) / s, hy = (v - y) / s;
            double lx = hx - 0.45, ly = hy - 0.3;
            double lobe = Math.Sqrt(lx * lx + ly * ly) - 0.5;
            // The point: inside the triangle between the lobes' centres and the tip.
            double edge = (1.2 * hx - 0.95 * (hy + 0.85)) / 1.53;
            double tri = Math.Max(edge, hy - 0.3);
            double d = Math.Min(lobe, tri) * s;
            double a = ph < 0.75 ? 1 : (1 - ph) / 0.25;
            Over(0.14, 0.08, 0.12, Cov(d - 0.22) * 0.85 * a);
            Over(0.96, 0.28, 0.42, Cov(d) * a);
            double gx = (u - x) / s + 0.35, gy = hy - 0.5;
            Over(1, 0.85, 0.9, Cov(Math.Sqrt(gx * gx + gy * gy) - 0.14) * 0.8 * a);
        }
    }
}
