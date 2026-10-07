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
    /// behind the body as it turns.
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
        const double ShoulderX = 4.55, ShoulderV = 10.2, UpperArm = 1.75, Forearm = 1.85;
        const double ArmR = 0.72, ForeR = 0.66, GloveR = 0.95;

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

        // Arms: shoulder, elbow, hand, depth and brightness of each.
        readonly double[] shU = new double[2], elbU = new double[2], elbV = new double[2],
                          handU = new double[2], handV = new double[2], armZ = new double[2], armK = new double[2];
        readonly bool[] armFront = new bool[2];

        readonly double[] hair;              // segments: u0, v0, u1, v1, r0, r1

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
            yaw = pose.Yaw * Deg;
            sinY = Math.Sin(yaw);
            cosY = Math.Cos(yaw);

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

            // Arms: shoulders at the sides of the body, swung round likewise.
            // They swing opposite to the leg on the same side.
            double armSwing = pose.Walking ? 32 * Math.Sin(p) : 0;
            double foreSwing = pose.Walking ? 14 : 0;
            PlaceArm(0, -ShoulderX, pose.ArmFar + armSwing, pose.ForeFar + foreSwing);
            PlaceArm(1, ShoulderX, pose.ArmNear - armSwing, pose.ForeNear + foreSwing);

            hair = HairFor(look.Hair);
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

        void PlaceArm(int i, double x, double upper, double fore)
        {
            shU[i] = x * cosY;
            armZ[i] = -x * sinY;
            armK[i] = 0.78 + 0.22 * (1 + armZ[i] / ShoulderX) / 2;
            // An arm the turn has put behind the body stays there unless it
            // is raised or thrown forward, which brings it round the front.
            armFront[i] = armZ[i] >= -0.05 || Math.Abs(upper) > 60;
            double a = upper * Deg;
            elbU[i] = shU[i] + UpperArm * Math.Sin(a);
            elbV[i] = ShoulderV - UpperArm * Math.Cos(a);
            double b = (upper + fore) * Deg;
            handU[i] = elbU[i] + Forearm * Math.Sin(b);
            handV[i] = elbV[i] - Forearm * Math.Cos(b);
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

            // Back to front: whatever the turn has put behind the body first.
            int backLeg = legZ[0] < legZ[1] ? 0 : 1;
            Leg(u, v, backLeg);
            Leg(u, v, 1 - backLeg);
            for (int i = 0; i < 2; i++) if (!armFront[i]) Limb(ub, vb, i);
            Body(ub, vb);
            if (eyes == 2) Eye(ub, vb, firstEye);
            Eye(ub, vb, eyes == 2 ? 1 - firstEye : 0);
            Brows(ub, vb);
            Mouth(ub, vb);
            Hair(ub, vb);
            for (int i = 0; i < 2; i++) if (armFront[i]) Limb(ub, vb, i);
            if (pose.Bubble) Bubble(ub, vb);

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

        /// <summary>A ball, lit as a sphere.</summary>
        void Ball(double x, double y, double cx, double cy, double radius,
                  double r, double g, double b, double spec, double shine)
        {
            double dx = x - cx, dy = y - cy;
            double cov = Cov(Math.Sqrt(dx * dx + dy * dy) - radius);
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(dx, dy, radius, out nx, out ny, out nz);
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

        void Body(double u, double v)
        {
            double axisV = v < BodyBottom ? BodyBottom : v > BodyTop ? BodyTop : v;
            double dx = u, dy = v - axisV;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double cov = Cov(dist - BodyR);
            if (cov <= 0) return;

            double nx, ny, nz;
            Normal(dx, dy, BodyR, out nx, out ny, out nz);

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
            // narrows with the yaw like everything else.
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
                Over(0.16, 0.10, 0.09, cov);
                return;
            }

            double gape = open * 2.3 * Math.Pow(1 - t * t, 0.6);
            double upper = line, lower = line - gape;
            double d = Math.Max(Math.Max(v - upper, lower - v), hx);
            double inside = Cov(d);
            if (inside > 0)
            {
                Over(0.30, 0.07, 0.09, inside);
                double teethH = open * 0.7 * face.Teeth;
                if (teethH > 0.05)
                {
                    double teeth = Cov(Math.Max(upper - teethH - v, d)) * inside;
                    double gap = Math.Abs(((u - mu) % 0.6 + 0.6) % 0.6 - 0.3);
                    double seam = Cov(gap - 0.03) * 0.35;
                    Over(0.95 - seam * 0.5, 0.94 - seam * 0.5, 0.90 - seam * 0.5, teeth);
                }
            }
            double outline = Cov(Math.Abs(d) - lip / 2);
            Over(0.16, 0.10, 0.09, outline);
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

        void Limb(double u, double v, int i)
        {
            double su = shU[i], eu = elbU[i], ev = elbV[i], hu = handU[i], hv = handV[i], k = armK[i];
            double lo = Math.Min(Math.Min(su, eu), hu) - 1.2, hi = Math.Max(Math.Max(su, eu), hu) + 1.2;
            if (u < lo || u > hi) return;
            double vlo = Math.Min(Math.Min(ShoulderV, ev), hv) - 1.2, vhi = Math.Max(Math.Max(ShoulderV, ev), hv) + 1.2;
            if (v < vlo || v > vhi) return;
            Tube(u, v, su, ShoulderV, eu, ev, ArmR, 0.98 * k, 0.84 * k, 0.11 * k, 0.3, 24);
            Tube(u, v, eu, ev, hu, hv, ForeR, 0.98 * k, 0.84 * k, 0.11 * k, 0.3, 24);
            Ball(u, v, hu, hv, GloveR, 0.11 * k, 0.10 * k, 0.13 * k, 0.7, 18);
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
    }
}
