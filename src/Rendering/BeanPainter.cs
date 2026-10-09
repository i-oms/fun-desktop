using System;
using IdleWalker.Domain;

namespace IdleWalker.Rendering
{
    /// <summary>
    /// Paints Mr. Bean into a <see cref="Surface"/> the way the Minions are
    /// painted: as lit solids with anti-aliased edges, composited back to
    /// front (see <see cref="SolidPainter"/>). A tall thin man: long legs
    /// with knees, a tapered torso in a brown tweed jacket over a white
    /// shirt and a thin red tie, a neck, and a round head with heavy
    /// eyebrows, a long nose, a wide mouth, ears, and dark hair swept back
    /// from a high forehead. Teddy appears in his hand when the pose says so.
    ///
    /// Model space is the Minions': u across in units, positive the way he
    /// faces; v up from the soles. He is <see cref="Height"/> units tall.
    /// The body turns with <see cref="Pose.Yaw"/>; the head turns further
    /// by <see cref="Pose.HeadTurn"/> and nods by <see cref="Pose.HeadDip"/>.
    /// The arms can come forward out of the picture plane
    /// (<see cref="Pose.ReachNear"/>), which is how a hand reaches the chin,
    /// the collar or the middle of the chest.
    /// </summary>
    sealed class BeanPainter : SolidPainter
    {
        public const double Height = 28;

        // Legs: hips either side of the axis, a thigh and a shin with a knee between.
        const double HipV = 12.0, HipX = 1.15, Thigh = 5.6, Shin = 5.4, ThighR = 1.0, ShinR = 0.86;

        // Torso: a capsule from the hips to the shoulders, wider at the top.
        const double TorsoBottom = 12.2, TorsoTop = 19.4, HipR = 2.4, ChestR = 2.95;
        const double HemV = 13.1;                                // the jacket's hem; trousers below
        const double VBottom = 14.6;                             // where the lapels meet over the tie's tip
        const double CollarV = 19.9;                             // where the collar spreads
        const double NeckBottom = 19.3, NeckTop = 22.0, NeckR = 0.8, CollarTop = 20.4;

        // Head: big, as a cartoon's, so the face reads at a few pixels per
        // unit, and taller than it is wide - a long face with a long chin.
        // The hair is a second ellipsoid sitting a little proud of the skull.
        const double HeadV = 24.4, HeadRx = 3.0, HeadRy = 3.5;
        const double HairV = 24.85, HairRx = 3.2, HairRy = 3.55;
        const double EyeV = 24.9, EyeAngle = 19, EyeRx = 0.9, EyeRy = 0.66, IrisR = 0.46;
        const double BrowV = 25.95;
        const double NoseTopV = 24.7, NoseTipV = 23.15;
        const double MouthV = 22.25;
        const double EarV = 24.3, EarR = 0.7;

        // Arms: shoulders on the chest's surface, ShoulderAngle round from the front.
        const double ShoulderV = 19.4, ShoulderAngle = 82, ArmRadius = ChestR + 0.1, UpperArm = 3.4, Forearm = 3.2;
        const double RestSpread = 4;
        const double SleeveR = 0.78, ForeR = 0.7, CuffLen = 0.5, HandR = 0.72, FingerLen = 0.55, FingerR = 0.2;
        const double PatchR = 0.6;                               // the elbow patches

        // Colours.
        const double SkinR = 0.96, SkinG = 0.81, SkinB = 0.70;
        const double HairR = 0.13, HairG = 0.085, HairB = 0.06;
        const double CoatR = 0.50, CoatG = 0.37, CoatB = 0.24;
        const double TrouserR = 0.32, TrouserG = 0.24, TrouserB = 0.17;
        const double ShirtR = 0.95, ShirtG = 0.95, ShirtB = 0.91;
        const double TieR = 0.66, TieG = 0.11, TieB = 0.14;
        const double TeddyR = 0.76, TeddyG = 0.56, TeddyB = 0.33;

        readonly Pose pose;
        readonly Expression face;
        readonly double yaw, sinY, cosY;             // the body
        readonly double headYaw, sinH, cosH;         // the head, turned further
        readonly double lean, dip;

        // Eyes: angle from the viewer, screen centre, horizontal squeeze, visibility.
        readonly double[] eyeAlpha = new double[2], eyeU = new double[2], eyeK = new double[2], eyeVis = new double[2];
        readonly int firstEye;

        // Ears: screen centre and depth.
        readonly double[] earU = new double[2], earZ = new double[2];

        // Legs: hip, knee and ankle of each, and which is behind.
        readonly double[] hipU = new double[2], kneeU = new double[2], kneeV = new double[2],
                          ankleU = new double[2], ankleV = new double[2], legZ = new double[2];
        readonly double shoeLen, toe;

        // Arms: shoulder, elbow, cuff and hand of each, with depths, and the screen box it all fits in.
        readonly double[] shU = new double[2], shZ = new double[2],
                          elbU = new double[2], elbV = new double[2], elbZ = new double[2],
                          cuffU = new double[2], cuffV = new double[2], cuffZ = new double[2],
                          handU = new double[2], handV = new double[2], handZ = new double[2],
                          armLo = new double[2], armHi = new double[2], armVlo = new double[2], armVhi = new double[2];

        readonly bool decorated;
        readonly double topV;
        readonly double mouthU;                      // where the mouth is across the screen, for the note and the heart

        BeanPainter(Pose pose, bool faceLeft, double px)
            : base(faceLeft, px, ArmRadius)
        {
            this.pose = pose;
            face = pose.Face;
            lean = pose.Lean;
            dip = pose.HeadDip;
            yaw = Wrap(pose.Yaw) * Deg;
            sinY = Math.Sin(yaw);
            cosY = Math.Cos(yaw);
            headYaw = Wrap(pose.Yaw + pose.HeadTurn) * Deg;
            sinH = Math.Sin(headYaw);
            cosH = Math.Cos(headYaw);
            mouthU = HeadRx * 0.95 * sinH;

            PlaceEye(0, -EyeAngle * Deg);
            PlaceEye(1, EyeAngle * Deg);
            firstEye = Math.Cos(eyeAlpha[0]) < Math.Cos(eyeAlpha[1]) ? 0 : 1;

            for (int i = 0; i < 2; i++)
            {
                double a = (i == 0 ? -90 : 90) * Deg + headYaw;
                earU[i] = (HeadRx + 0.15) * Math.Sin(a);
                earZ[i] = HeadRx * Math.Cos(a);
            }

            // Legs. The thigh swings fore and aft with the stride; the knee
            // bends as the leg comes forward and straightens for the heel
            // strike and the push-off. Knees up for a jump.
            double p = pose.WalkPhase * 2 * Math.PI;
            double swing = pose.Walking ? 28 * Math.Sin(p) : 0;
            double bendA = pose.Walking ? 55 * Math.Max(0, Math.Cos(p)) : 3;
            double bendB = pose.Walking ? 55 * Math.Max(0, -Math.Cos(p)) : 3;
            double tuck = pose.LegTuck;
            PlaceLeg(0, -HipX, -swing + 55 * tuck, bendB + 95 * tuck);
            PlaceLeg(1, HipX, swing + 55 * tuck, bendA + 95 * tuck);
            shoeLen = 1.25 + 0.85 * Math.Abs(sinY);
            toe = 0.25 + 0.8 * sinY;

            // Arms, as for a Minion, plus the reach out of the picture plane.
            double armSwing = pose.Walking ? 28 * Math.Sin(p) : 0;
            double foreSwing = pose.Walking ? 15 : 0;
            if (pose.ArmNear != 0 || pose.ArmFar != 0) armSwing = 0;
            if (pose.ForeNear != 0 || pose.ForeFar != 0) foreSwing = 0;
            PlaceArm(0, -1, pose.ArmFar, pose.ForeFar, pose.ReachFar, armSwing, foreSwing);
            PlaceArm(1, 1, pose.ArmNear, pose.ForeNear, pose.ReachNear, -armSwing, foreSwing);

            decorated = pose.Sweat > 0 || pose.Note || pose.Zzz || pose.Bubble || pose.Heart;
            topV = decorated ? 37 : Height + 0.8 + Math.Abs(pose.BodyDy) + Math.Max(0, -dip);
        }

        // A spin can carry the yaw past a full turn; bring it back to (-180, 180].
        static double Wrap(double degrees)
        {
            double turn = degrees % 360;
            if (turn > 180) turn -= 360; else if (turn <= -180) turn += 360;
            return turn;
        }

        void PlaceEye(int i, double phi)
        {
            double a = phi + headYaw;
            eyeAlpha[i] = a;
            eyeU[i] = (HeadRx + 0.05) * Math.Sin(a);
            double c = Math.Cos(a);
            eyeK[i] = c > 0.25 ? c : 0.25;
            double away = Math.Abs(a) / Deg;
            eyeVis[i] = away <= 72 ? 1 : away >= 95 ? 0 : (95 - away) / 23;
        }

        void PlaceLeg(int i, double x, double swing, double bend)
        {
            hipU[i] = x * cosY;
            legZ[i] = -x * sinY;
            double a = swing * Deg;
            kneeU[i] = hipU[i] + Thigh * Math.Sin(a);
            kneeV[i] = HipV - Thigh * Math.Cos(a);
            double b = (swing - bend) * Deg;
            ankleU[i] = kneeU[i] + Shin * Math.Sin(b);
            ankleV[i] = kneeV[i] - Shin * Math.Cos(b) + 0.65 * pose.Tiptoe;
        }

        /// <summary>
        /// Lays out one arm like a Minion's (`raise` and `bend` in the picture
        /// plane, `swing` fore and aft for the walk), with `reach` bringing
        /// the upper arm forward out of the picture plane - towards where he
        /// faces whether the arm hangs or is raised - and the forearm back
        /// into it, so a bent arm can put a hand in front of the body.
        /// </summary>
        void PlaceArm(int i, double side, double raise, double bend, double reach, double swing, double foreSwing)
        {
            double at = side * ShoulderAngle * Deg + yaw;
            shU[i] = ArmRadius * Math.Sin(at);
            shZ[i] = ArmRadius * Math.Cos(at);

            if (raise == 0) raise = side * RestSpread;
            if (reach != 0)
            {
                swing = raise > 90 || raise < -90 ? -reach : reach;
                foreSwing = -swing;
            }

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

            // The shirt cuff: the last bit of the forearm.
            double f = 1 - CuffLen / Forearm;
            cuffU[i] = elbU[i] + (handU[i] - elbU[i]) * f;
            cuffV[i] = elbV[i] + (handV[i] - elbV[i]) * f;
            cuffZ[i] = elbZ[i] + (handZ[i] - elbZ[i]) * f;

            double pad = 1.3;
            if (side > 0 && pose.PointNear) pad += FingerLen;
            armLo[i] = Math.Min(Math.Min(shU[i], elbU[i]), handU[i]) - pad;
            armHi[i] = Math.Max(Math.Max(shU[i], elbU[i]), handU[i]) + pad;
            armVlo[i] = Math.Min(Math.Min(ShoulderV, elbV[i]), handV[i]) - pad;
            armVhi[i] = Math.Max(Math.Max(ShoulderV, elbV[i]), handV[i]) + pad;
        }

        // ---- Entry point ---------------------------------------------------------

        /// <summary>Paints Mr. Bean with his soles on soleY, centred on centreX, at `scale` pixels per unit.</summary>
        public static void Draw(Surface surface, Pose pose, bool faceLeft, double centreX, double soleY, double scale)
        {
            double sx = scale * pose.StretchX, sy = scale * pose.StretchY;
            double px = 1 / Math.Min(sx, sy);
            BeanPainter painter = new BeanPainter(pose, faceLeft, px);

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

            // The upper body bobs and leans from the hips; the legs stay planted.
            double vb = v + pose.BodyDy;
            double ub = u - lean * 0.14 * Math.Max(0, v - 10);

            // How far forward the body's surface is here: the torso's or the
            // head's, whichever bulges further. Limbs are sorted against it.
            double axisV = vb < TorsoBottom ? TorsoBottom : vb > TorsoTop ? TorsoTop : vb;
            double t = (axisV - TorsoBottom) / (TorsoTop - TorsoBottom);
            double torsoR = HipR + (ChestR - HipR) * t;
            double tdy = vb - axisV;
            double torsoDist = Math.Sqrt(ub * ub + tdy * tdy);
            double bulge = torsoR * torsoR - torsoDist * torsoDist;
            bodyZ = bulge > 0 ? Math.Sqrt(bulge) : 0;
            double hdy = vb - HeadV;
            double hky = HeadRx / HeadRy;
            double headBulge = HeadRx * HeadRx - ub * ub - hdy * hdy * hky * hky;
            if (headBulge > bodyZ * bodyZ) bodyZ = Math.Sqrt(headBulge);

            // Back to front.
            int backLeg = legZ[0] < legZ[1] ? 0 : 1;
            Leg(u, v, backLeg);
            Leg(u, v, 1 - backLeg);
            Limb(ub, vb, 0);
            Limb(ub, vb, 1);
            Torso(ub, vb, tdy, torsoDist, torsoR);
            Neck(ub, vb);
            if (earZ[0] <= 0) Ear(ub, vb, 0);
            if (earZ[1] <= 0) Ear(ub, vb, 1);
            if (Math.Abs(hdy) < HeadRy + 1.5 && Math.Abs(ub) < HeadRx + 1.5)
            {
                Head(ub, vb, hdy);
                if (earZ[0] > 0) Ear(ub, vb, 0);
                if (earZ[1] > 0) Ear(ub, vb, 1);
                Eye(ub, vb, firstEye);
                Eye(ub, vb, 1 - firstEye);
                Brows(ub, vb);
                Nose(ub, vb);
                Mouth(ub, vb);
            }
            LayFront();
            if (pose.Teddy) Teddy(ub, vb);

            // Things floating about him, in front of everything.
            if (decorated)
            {
                if (pose.Sweat > 0) Sweat(ub, vb, pose.Sweat, -(HeadRx + 0.9), HeadV + 1.4);
                if (pose.Note) Note(ub, vb, pose.DecorPhase, mouthU, MouthV);
                if (pose.Zzz) Zzz(u, v, pose.DecorPhase, 2.6, Height + 1.6);
                if (pose.Bubble) Bubble(ub, vb, Height + 0.4, Height + 1.3, Height + 2.8, Height + 5.4);
                if (pose.Heart) Heart(ub, vb, pose.DecorPhase, mouthU, MouthV);
            }

            return Finish();
        }

        // ---- The parts, back to front ---------------------------------------------------

        void Leg(double u, double v, int i)
        {
            if (v > HipV + 1.5) return;
            double lo = Math.Min(Math.Min(hipU[i], kneeU[i]), ankleU[i]) - 2.6;
            double hi = Math.Max(Math.Max(hipU[i], kneeU[i]), ankleU[i]) + 2.6;
            if (u < lo || u > hi) return;
            double k = 0.8 + 0.2 * (1 + legZ[i] / HipX) / 2;   // the far leg a shade darker
            Tube(u, v, hipU[i], HipV, kneeU[i], kneeV[i], ThighR, TrouserR * k, TrouserG * k, TrouserB * k, 0.06, 8);
            Tube(u, v, kneeU[i], kneeV[i], ankleU[i], ankleV[i], ShinR, TrouserR * k, TrouserG * k, TrouserB * k, 0.06, 8);

            // The shoe: a glossy black ellipsoid under the ankle, toe forward;
            // on tiptoe it tips so the toe stays down.
            double tip = pose.Tiptoe;
            double sx = ankleU[i] + toe + 0.3 * tip, sy = ankleV[i] - 0.55 - 0.25 * tip, rx = shoeLen, ry = 0.58;
            double dx = (u - sx) / rx, dy = (v - sy) / ry;
            double e = Math.Sqrt(dx * dx + dy * dy);
            double cov = Cov((e - 1) * ry);
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(dx, dy, 1, out nx, out ny, out nz);
            double lr, lg, lb;
            Lit(0.09 * k, 0.08 * k, 0.09 * k, nx, ny, nz, 0.6, 22, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
        }

        /// <summary>The torso at (u, v): dy is the height above the nearest point of the capsule's axis, dist the distance from it, r its radius there.</summary>
        void Torso(double u, double v, double dy, double dist, double r)
        {
            double cov = Cov(dist - r);
            if (cov <= 0) return;

            double nx, ny, nz;
            Normal(u, dy, r, out nx, out ny, out nz);

            // Where on the surface this is, round from the front, as an arc length.
            double phi = Math.Atan2(nx, nz) - yaw;
            if (phi > Math.PI) phi -= 2 * Math.PI; else if (phi < -Math.PI) phi += 2 * Math.PI;
            double arc = r * phi;
            double a = Math.Abs(arc);

            double cr, cg, cb, spec, shine;
            if (v < HemV)
            {
                // Trousers below the jacket's hem.
                cr = TrouserR; cg = TrouserG; cb = TrouserB; spec = 0.06; shine = 8;
                double hem = Cov(Math.Abs(v - HemV) - 0.08);
                Mix(ref cr, ref cg, ref cb, ref spec, ref shine, CoatR * 0.7, CoatG * 0.7, CoatB * 0.7, 0.05, 6, hem);
            }
            else
            {
                // The tweed: brown with a faint fleck.
                double fleck = 0.035 * Math.Sin(arc * 7.5) * Math.Sin(v * 7.5);
                cr = CoatR + fleck; cg = CoatG + fleck; cb = CoatB + fleck * 0.6; spec = 0.05; shine = 6;

                // A breast pocket: a short dark slit on the far side of the chest.
                double pocket = Cov(Math.Max(Math.Abs(v - 17.6) - 0.09, Math.Max(-2.0 - arc, arc + 1.1)));
                Mix(ref cr, ref cg, ref cb, ref spec, ref shine, CoatR * 0.7, CoatG * 0.7, CoatB * 0.7, 0.05, 6, pocket);

                if (v > VBottom - 0.4)
                {
                    // The jacket open in a V over the shirt, the lapels either
                    // side of it, the collar spreading at the top.
                    double hw = v < CollarV ? 0.3 + 0.95 * Math.Min(1, (v - VBottom) / (CollarV - VBottom - 0.2))
                                            : 1.25 + (v - CollarV) * 1.5;
                    double lapel = Cov(Math.Max(hw - a, a - hw - 0.6));
                    Mix(ref cr, ref cg, ref cb, ref spec, ref shine, CoatR * 0.82, CoatG * 0.82, CoatB * 0.82, 0.05, 6, lapel);
                    double shirt = Cov(a - hw);
                    Mix(ref cr, ref cg, ref cb, ref spec, ref shine, ShirtR, ShirtG, ShirtB, 0.1, 10, shirt);

                    // The tie: thin, pointed at the bottom, a knot at the top.
                    double tieHw = v < VBottom + 0.6 ? 0.38 * (v - VBottom) / 0.6 : v > CollarV - 0.5 ? 0.52 : 0.38;
                    double tie = Cov(Math.Max(a - tieHw, Math.Max(VBottom - v, v - CollarV - 0.5)));
                    double knot = v > CollarV - 0.5 ? 0.85 : 1;
                    Mix(ref cr, ref cg, ref cb, ref spec, ref shine, TieR * knot, TieG * knot, TieB * knot, 0.3, 14, tie);

                    // A button where the lapels meet.
                    double bx = a - 0.75, by = v - VBottom + 0.1;
                    double button = Cov(Math.Sqrt(bx * bx + by * by) - 0.17);
                    Mix(ref cr, ref cg, ref cb, ref spec, ref shine, 0.12, 0.09, 0.07, 0.5, 20, button);
                }
            }

            double lr, lg, lb;
            Lit(cr, cg, cb, nx, ny, nz, spec, shine, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
        }

        // The neck, with the shirt collar round its base.
        void Neck(double u, double v)
        {
            if (Math.Abs(u) > NeckR + 0.5 || v < NeckBottom - 0.3 || v > NeckTop + 0.3) return;
            Tube(u, v, 0, NeckBottom, 0, CollarTop, NeckR + 0.12, ShirtR, ShirtG, ShirtB, 0.1, 10);
            double qx, qy, t;
            double dist = Seg(u, v, 0, CollarTop, 0, NeckTop, out qx, out qy, out t);
            double cov = Math.Min(Cov(dist - NeckR), Cov(CollarTop - v));   // cut flush on the collar
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(u - qx, v - qy, NeckR, out nx, out ny, out nz);
            double lr, lg, lb;
            Lit(SkinR * 0.92, SkinG * 0.9, SkinB * 0.9, nx, ny, nz, 0.2, 16, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
        }

        void Ear(double u, double v, int i)
        {
            double dx = u - earU[i], dy = v - EarV;
            double d = Math.Sqrt(dx * dx + dy * dy);
            double cov = Cov(d - EarR);
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(dx, dy, EarR, out nx, out ny, out nz);
            double lr, lg, lb;
            Lit(SkinR * 0.96, SkinG * 0.9, SkinB * 0.88, nx, ny, nz, 0.25, 18, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
            // The hollow, a little towards the outside.
            double ox = dx - (earU[i] > 0 ? 0.1 : -0.1), oy = dy - 0.02;
            Over(SkinR * 0.6, SkinG * 0.5, SkinB * 0.45, Cov(Math.Sqrt(ox * ox + oy * oy) - 0.2) * 0.55 * cov);
        }

        /// <summary>Signed distance to an ellipse of radii rx, ry, first-order exact.</summary>
        static double Ellipse(double dx, double dy, double rx, double ry)
        {
            double ex = dx / rx, ey = dy / ry;
            double e = Math.Sqrt(ex * ex + ey * ey);
            if (e < 1e-6) return -Math.Min(rx, ry);
            double grad = Math.Sqrt(ex * ex / (rx * rx) + ey * ey / (ry * ry)) / e;
            return (e - 1) / grad;
        }

        // The head: a long skin-coloured egg, and over it the hair - a
        // second ellipsoid standing a little proud of the skull, kept only
        // where its surface faces up and back: a high hairline across the
        // forehead, lower at the temples, sideburns in front of the ears,
        // swept across to one side from a parting, down to the nape behind.
        void Head(double u, double v, double dy)
        {
            double cov = Cov(Ellipse(u, dy, HeadRx, HeadRy));
            if (cov > 0)
            {
                double nx, ny, nz;
                Normal(u / HeadRx * 3, dy / HeadRy * 3, 3, out nx, out ny, out nz);
                double lr, lg, lb;
                Lit(SkinR, SkinG, SkinB, nx, ny, nz, 0.25, 18, out lr, out lg, out lb);
                Over(lr, lg, lb, cov);
            }

            double hdy = v - HairV;
            double hairCov = Cov(Ellipse(u, hdy, HairRx, HairRy));
            if (hairCov <= 0) return;
            double mx, my, mz;
            Normal(u / HairRx * 3, hdy / HairRy * 3, 3, out mx, out my, out mz);

            // The normal in the head's own frame: hz towards the face.
            double hx = mx * cosH - mz * sinH;
            double hz = mx * sinH + mz * cosH;
            double hy = my;
            double line = hz > 0 ? 0.04 + 0.5 * hz : 0.04 + 0.5 * hz;
            line -= 0.18 * Math.Max(0, -hx) * Math.Max(0, hz);         // swept down over one side of the forehead
            if (Math.Abs(hx) > 0.75 && hz > 0 && hz < 0.5) line = Math.Min(line, -0.15);   // sideburns
            double keep = Cov((line - hy) * HairRy * 0.8);
            hairCov *= keep;
            if (hairCov <= 0) return;

            double hr = HairR, hg = HairG, hb = HairB;
            // The parting: a thin lighter line running back from the hairline.
            double part = Cov(Math.Abs(hx - 0.32) - 0.03) * Cov(line + 0.05 - hy) * (hz > -0.2 ? 0.5 : 0);
            hr += (0.4 - hr) * part;
            hg += (0.3 - hg) * part;
            hb += (0.22 - hb) * part;
            double r, g, b;
            Lit(hr, hg, hb, mx, my, mz, 0.35, 12, out r, out g, out b);
            Over(r, g, b, hairCov);
        }

        void Eye(double u, double v, int i)
        {
            double vis = eyeVis[i];
            if (vis <= 0) return;
            double k = eyeK[i], alpha = eyeAlpha[i];
            bool near = i != firstEye;

            // The eye's own frame: squeezed as it turns away, shifted down
            // and forward as the head nods.
            double fu = eyeU[i] + dip * 0.5 * sinH, fv = EyeV - dip * 0.8;
            double dx = (u - fu) / k, dy = v - fv;
            if (Math.Abs(dx) > EyeRx + 0.5 || Math.Abs(dy) > EyeRy + 0.5) return;

            double ex = dx / EyeRx, ey = dy / EyeRy;
            double white = (Math.Sqrt(ex * ex + ey * ey) - 1) * EyeRy;
            double covWhite = Cov(white) * vis;
            if (covWhite <= 0) return;

            double nx, ny, nz;
            Normal(dx, dy, EyeRx * 1.6, out nx, out ny, out nz);
            Turn(ref nx, ref nz, alpha);
            double r, g, b;
            Lit(0.96, 0.95, 0.92, nx, ny, nz, 0.4, 24, out r, out g, out b);

            // The iris follows the gaze, kept inside the white.
            double reach = EyeRx - IrisR - 0.05;
            double ox = pose.GazeX * reach * 0.9 + face.PupilDx * 0.4;
            double oy = pose.GazeY * (EyeRy - IrisR * 0.6) * 0.8 + face.PupilDy * 0.3;
            ox -= (i == 1 ? 1 : -1) * face.CrossEye * 0.3;
            double om = Math.Sqrt(ox * ox + oy * oy);
            if (om > reach) { ox *= reach / om; oy *= reach / om; }
            double ix = dx - ox, iy = dy - oy;
            double id = Math.Sqrt(ix * ix + iy * iy);
            double covIris = Cov(Squeezed(ix, iy, k, IrisR));
            if (covIris > 0)
            {
                double tt = id / IrisR;
                double shade = 1.25 - 0.55 * tt;
                if (tt > 0.82) shade *= 1 - 0.55 * (tt - 0.82) / 0.18;
                r += (0.30 * shade - r) * covIris;
                g += (0.17 * shade - g) * covIris;
                b += (0.08 * shade - b) * covIris;
                double covPupil = Cov(Squeezed(ix, iy, k, IrisR * 0.5 * face.PupilSize));
                r += (0.05 - r) * covPupil;
                g += (0.04 - g) * covPupil;
                b += (0.04 - b) * covPupil;
                double lx = ix + flip * 0.35 * IrisR, ly = iy - 0.35 * IrisR;
                double covLight = Cov(Squeezed(lx, ly, k, IrisR * 0.25)) * 0.9;
                r += (1 - r) * covLight;
                g += (1 - g) * covLight;
                b += (1 - b) * covLight;
            }

            // Lids: skin sliding down (and up) over the eye.
            double upper = Math.Max(face.Lid, near ? face.Wink : 0);
            double lower = face.Squint;
            double s = ex * ex;
            double upperEdge = fv + EyeRy * (1 - 2 * upper) - 0.25 * upper * s;
            double lowerEdge = fv - EyeRy * (1 - 2 * lower) + 0.25 * lower * s;
            if (upper > 0.01 || lower > 0.01)
            {
                double bx, by, bz;
                Normal(u / HeadRx * 3, (v - HeadV) / HeadRy * 3, 3, out bx, out by, out bz);
                double sr, sg, sb;
                Lit(SkinR, SkinG, SkinB, bx, by, bz, 0.25, 18, out sr, out sg, out sb);
                double covLid = Math.Max(upper > 0.01 ? Cov(upperEdge - v) : 0, lower > 0.01 ? Cov(v - lowerEdge) : 0);
                r += (sr - r) * covLid;
                g += (sg - g) * covLid;
                b += (sb - b) * covLid;
                double lash = Math.Max(upper > 0.01 ? Cov(Math.Abs(v - upperEdge) - 0.07) : 0,
                                       lower > 0.01 ? Cov(Math.Abs(v - lowerEdge) - 0.06) : 0) * 0.7;
                r += (0.2 - r) * lash;
                g += (0.12 - g) * lash;
                b += (0.09 - b) * lash;
            }
            Over(r, g, b, covWhite);

            // The rim of the eye: a fine dark line, heavier along the top;
            // a crease above it where the lid folds, and a shadow under it.
            double rim = Cov(Math.Abs(white) - 0.07) * (dy > 0.1 ? 0.85 : 0.4) * vis;
            Over(0.22, 0.13, 0.1, rim);
            double crease = Ellipse(dx, dy - 0.2, EyeRx + 0.12, EyeRy + 0.12);
            Over(0.62, 0.44, 0.34, Cov(Math.Abs(crease) - 0.05) * (dy > 0.3 ? 0.6 : 0) * vis);
            double bag = Ellipse(dx, dy + 0.12, EyeRx + 0.05, EyeRy + 0.1);
            Over(0.72, 0.54, 0.44, Cov(Math.Abs(bag) - 0.05) * (dy < -0.35 ? 0.45 : 0) * vis);
        }

        // Heavy dark brows above each eye, raised, angled and lifted one at
        // a time as the face says.
        void Brows(double u, double v)
        {
            double baseV = BrowV - dip * 0.8 + face.BrowRaise * 0.35;
            if (Math.Abs(v - baseV) > 1.3) return;
            Brow(u, v, eyeAlpha[0], -1, baseV, eyeVis[0]);
            Brow(u, v, eyeAlpha[1], 1, baseV + face.BrowAsym * 0.3, eyeVis[1]);
        }

        void Brow(double u, double v, double alpha, double outward, double cy, double show)
        {
            if (show <= 0) return;
            double k = Math.Cos(alpha);
            if (k < 0.2) return;
            // Thick and dark, a little arched: heaviest over the inner half,
            // tapering to a point at the temple.
            double ex = (HeadRx + 0.15) * Math.Sin(alpha) + dip * 0.5 * sinH;
            double ax = ex - outward * 1.0 * k, ay = cy - face.BrowAngle * 0.4 - 0.08;
            double mx = ex + outward * 0.15 * k, my = cy + 0.12;
            double bx = ex + outward * 1.05 * k, by = cy + face.BrowAngle * 0.4 - 0.1;
            double qx, qy, t;
            double d1 = Seg(u, v, ax, ay, mx, my, out qx, out qy, out t) - (0.3 + 0.06 * t);
            double d2 = Seg(u, v, mx, my, bx, by, out qx, out qy, out t) - (0.36 - 0.26 * t);
            double cov = Cov(Math.Min(d1, d2)) * show;
            Over(0.12, 0.08, 0.06, cov);
        }

        // A long nose: a tube from the bridge to the tip, standing out from
        // the face, so it is a bump from the front and a profile from the side.
        void Nose(double u, double v)
        {
            double vis = cosH > -0.3 ? 1 : cosH < -0.6 ? 0 : (cosH + 0.6) / 0.3;
            if (vis <= 0) return;
            double shift = dip * 0.5 * sinH;
            double bu = (HeadRx + 0.1) * sinH + shift, bv = NoseTopV - dip * 0.7;
            double tu = (HeadRx + 0.9) * sinH + shift, tv = NoseTipV - dip * 0.9;
            if (Math.Abs(u - tu) > 1.4 || v > bv + 0.6 || v < tv - 0.9) return;
            double qx, qy, t;
            double dist = Seg(u, v, bu, bv, tu, tv, out qx, out qy, out t);
            double radius = 0.28 + 0.2 * t * t;   // narrow at the bridge, bulbous at the tip
            double cov = Cov(dist - radius) * vis;
            if (cov > 0)
            {
                double nx, ny, nz;
                Normal(u - qx, v - qy, radius, out nx, out ny, out nz);
                double lr, lg, lb;
                Lit(SkinR * 0.98, SkinG * 0.88, SkinB * 0.84, nx, ny, nz, 0.4, 20, out lr, out lg, out lb);
                Over(lr, lg, lb, cov);
                // The shaded side of the bridge, so it stands off the face from the front.
                Over(0.6, 0.4, 0.3, cov * 0.35 * Math.Max(0, (u - qx) * flip / radius) * (1 - t));
            }
            // The nostrils' shadows under the tip.
            double sx = u - tu - 0.3 * cosH + 0.1 * sinH, sy = v - tv + 0.36;
            Over(0.5, 0.28, 0.2, Cov(Math.Sqrt(sx * sx + sy * sy) - 0.14) * 0.5 * vis);
            sx = u - tu + 0.3 * cosH + 0.1 * sinH;
            Over(0.5, 0.28, 0.2, Cov(Math.Sqrt(sx * sx + sy * sy) - 0.14) * 0.5 * vis);
        }

        void Mouth(double u, double v)
        {
            double vis = cosH > 0.17 ? 1 : cosH < -0.08 ? 0 : (cosH + 0.08) / 0.25;
            if (vis <= 0) return;
            double k = cosH > 0.35 ? cosH : 0.35;
            double mu = mouthU + face.MouthDx * 0.5 * k + dip * 0.5 * sinH;
            double mv = MouthV - dip * 0.9;
            double w = face.MouthWidth * 0.88 * k;
            double hx = Math.Abs(u - mu) - w;
            if (hx > 0.6 || Math.Abs(v - mv) > 2.5) return;

            double t = (u - mu) / w;
            if (t < -1) t = -1; else if (t > 1) t = 1;
            double line = mv + face.MouthCurve * 0.55 * (t * t - 0.45) + face.Wobble * 0.12 * Math.Sin(t * 8.5);
            double open = face.MouthOpen;
            const double lip = 0.19;

            if (open < 0.03)
            {
                double ox = hx > 0 ? hx : 0, oy = v - line;
                double cov = Cov(Math.Sqrt(ox * ox + oy * oy) - lip / 2);
                Over(0.45, 0.2, 0.18, cov * vis);
                // A thin lower lip catching the light under the line.
                Over(0.9, 0.62, 0.55, Cov(Math.Sqrt(ox * ox + (oy + 0.22) * (oy + 0.22)) - 0.1) * 0.5 * vis);
                return;
            }

            double gape = open * 1.5 * Math.Pow(1 - t * t, 0.6);
            double upper = line, lower = line - gape;
            double d = Math.Max(Math.Max(v - upper, lower - v), hx);
            double inside = Cov(d);
            if (inside > 0)
            {
                Over(0.28, 0.08, 0.09, inside * vis);
                double teethH = open * 0.5 * face.Teeth;
                if (teethH > 0.04)
                {
                    double teeth = Cov(Math.Max(upper - teethH - v, d)) * inside;
                    double gap = Math.Abs(((u - mu) % 0.42 + 0.42) % 0.42 - 0.21);
                    double seam = Cov(gap - 0.025) * 0.35;
                    Over(0.95 - seam * 0.5, 0.94 - seam * 0.5, 0.90 - seam * 0.5, teeth * vis);
                }
            }
            double outline = Cov(Math.Abs(d) - lip / 2);
            Over(0.45, 0.2, 0.18, outline * vis);

            if (face.Tongue > 0.02)
            {
                double tongue = face.Tongue;
                double rx = Math.Min(w * 0.55, 0.55), ry = 0.22 + 0.35 * tongue;
                double cy = lower + 0.22 - 0.55 * tongue;
                double tx = (u - mu) / rx, ty = (v - cy) / ry;
                double e = Math.Sqrt(tx * tx + ty * ty);
                double rim = (e - 1) * ry;
                double td = Math.Max(rim, v - upper + 0.1);
                double cov = Cov(td);
                if (cov <= 0) return;
                double shade = 1 - 0.25 * e;
                Over(0.12 + 0.86 * shade, 0.42 * shade, 0.52 * shade, cov * vis);
                Over(0.45, 0.2, 0.18, Cov(Math.Abs(rim) - 0.07) * Cov(v - upper + 0.1) * 0.7 * vis);
            }
        }

        /// <summary>
        /// One arm: the sleeve in two tubes with an elbow patch, the shirt
        /// cuff, and the hand, each sorted against the body pixel by pixel.
        /// </summary>
        void Limb(double u, double v, int i)
        {
            if (u < armLo[i] || u > armHi[i] || v < armVlo[i] || v > armVhi[i]) return;
            ArmTube(u, v, shU[i], ShoulderV, shZ[i], elbU[i], elbV[i], elbZ[i], SleeveR, CoatR, CoatG, CoatB, 0.05, 6);
            ArmTube(u, v, elbU[i], elbV[i], elbZ[i], cuffU[i], cuffV[i], cuffZ[i], ForeR, CoatR, CoatG, CoatB, 0.05, 6);

            // The elbow patch: a darker disc of suede on the sleeve.
            double ex = u - elbU[i], ey = v - elbV[i];
            double patch = Cov(Math.Sqrt(ex * ex + ey * ey) - PatchR);
            if (patch > 0)
            {
                double nx, ny, nz;
                Normal(ex, ey, SleeveR * 1.2, out nx, out ny, out nz);
                double k = Depth(elbZ[i]);
                double lr, lg, lb;
                Lit(0.38 * k, 0.27 * k, 0.17 * k, nx, ny, nz, 0.04, 5, out lr, out lg, out lb);
                Sort(lr, lg, lb, patch, elbZ[i] + nz * SleeveR);
            }

            ArmTube(u, v, cuffU[i], cuffV[i], cuffZ[i], handU[i], handV[i], handZ[i], ForeR + 0.04, ShirtR, ShirtG, ShirtB, 0.1, 10);

            // Pointing: the index finger carries on past the hand.
            if (i == 1 && pose.PointNear)
            {
                double fx = handU[i] - cuffU[i], fy = handV[i] - cuffV[i];
                double len = Math.Sqrt(fx * fx + fy * fy);
                if (len > 1e-6)
                {
                    double reach = (HandR + FingerLen) / len;
                    ArmTube(u, v, handU[i], handV[i], handZ[i], handU[i] + fx * reach, handV[i] + fy * reach, handZ[i],
                            FingerR, SkinR, SkinG, SkinB, 0.25, 18);
                }
            }

            double dx = u - handU[i], dy = v - handV[i];
            double cov = Cov(Math.Sqrt(dx * dx + dy * dy) - HandR);
            if (cov <= 0) return;
            double hnx, hny, hnz;
            Normal(dx, dy, HandR, out hnx, out hny, out hnz);
            double hk = Depth(handZ[i]);
            double hr, hg, hb;
            Lit(SkinR * hk, SkinG * hk, SkinB * hk, hnx, hny, hnz, 0.25, 18, out hr, out hg, out hb);
            Sort(hr, hg, hb, cov, handZ[i] + hnz * HandR);
        }

        // Teddy, sitting in the near hand: a round body, a round head with
        // two ears, a lighter muzzle, button eyes, and one paw that waves.
        void Teddy(double u, double v)
        {
            double tx = handU[1] + 0.2, ty = handV[1] + 0.55;
            if (Math.Abs(u - tx) > 1.8 || v < ty - 1.2 || v > ty + 2.4) return;

            // The paws: one held out, one waving.
            double ang = (25 + 85 * pose.TeddyWave) * Deg;
            double px0 = tx + 0.55, py0 = ty + 0.3;
            Tube(u, v, px0, py0, px0 + 0.85 * Math.Cos(ang), py0 + 0.85 * Math.Sin(ang), 0.2, TeddyR, TeddyG, TeddyB, 0.1, 8);
            Tube(u, v, tx - 0.55, ty + 0.1, tx - 1.1, ty - 0.35, 0.2, TeddyR, TeddyG, TeddyB, 0.1, 8);

            Ball(u, v, tx, ty, 0.85, TeddyR, TeddyG, TeddyB, 0.1, 8);
            double hx = tx + 0.05, hy = ty + 1.3;
            Ball(u, v, hx - 0.52, hy + 0.45, 0.23, TeddyR * 0.9, TeddyG * 0.9, TeddyB * 0.9, 0.1, 8);
            Ball(u, v, hx + 0.52, hy + 0.45, 0.23, TeddyR * 0.9, TeddyG * 0.9, TeddyB * 0.9, 0.1, 8);
            Ball(u, v, hx, hy, 0.65, TeddyR, TeddyG, TeddyB, 0.1, 8);
            Ball(u, v, hx + 0.08, hy - 0.15, 0.28, 0.88, 0.74, 0.52, 0.1, 8);

            // Button eyes and a nose.
            double ex = u - hx + 0.22, ey = v - hy - 0.15;
            Over(0.1, 0.07, 0.06, Cov(Math.Sqrt(ex * ex + ey * ey) - 0.08));
            ex = u - hx - 0.26;
            Over(0.1, 0.07, 0.06, Cov(Math.Sqrt(ex * ex + ey * ey) - 0.08));
            ex = u - hx - 0.08; ey = v - hy + 0.05;
            Over(0.15, 0.09, 0.07, Cov(Math.Sqrt(ex * ex + ey * ey) - 0.09));
        }

        /// <summary>A lit sphere, in front of everything.</summary>
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
