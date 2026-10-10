using System;
using IdleWalker.Domain;

namespace IdleWalker.Rendering
{
    /// <summary>
    /// Paints the Pink Panther into a <see cref="Surface"/> the way the other
    /// figures are painted: as lit solids with anti-aliased edges, composited
    /// back to front (see <see cref="SolidPainter"/>). A tall, sleek bipedal
    /// cat in the spirit of the cartoon: long legs with paws, a narrow waist,
    /// a lighter belly and muzzle, a long curling tail, pointed ears, yellow
    /// eyes with slit pupils, a dark pink nose, whiskers and a feline smile
    /// with a fang when the mouth opens.
    ///
    /// Model space is the Minions': u across in units, positive the way he
    /// faces; v up from the soles. He is <see cref="Height"/> units tall.
    /// The body turns with <see cref="Pose.Yaw"/>; the head turns further by
    /// <see cref="Pose.HeadTurn"/> and nods by <see cref="Pose.HeadDip"/>.
    /// </summary>
    sealed class PantherPainter : SolidPainter
    {
        public const double Height = 26;

        // Legs: hips either side of the axis, thigh + shin with a knee between.
        const double HipV = 11.0, HipX = 1.05, Thigh = 5.2, Shin = 5.0, ThighR = 0.95, ShinR = 0.78;

        // Torso: a capsule from hips to shoulders, narrow at the waist.
        const double TorsoBottom = 11.2, TorsoTop = 17.8, HipR = 2.2, ChestR = 2.7;
        const double NeckBottom = 17.6, NeckTop = 20.2, NeckR = 0.9;

        // Head: a rounded cat skull, slightly taller than wide.
        const double HeadV = 22.6, HeadRx = 2.6, HeadRy = 2.9;
        const double EyeV = 22.9, EyeAngle = 22, EyeRx = 0.78, EyeRy = 0.85, IrisR = 0.44;
        const double BrowV = 24.05;
        const double MuzzleV = 21.3, MuzzleRx = 1.35, MuzzleRy = 0.95;
        const double NoseV = 21.75;
        const double MouthV = 20.6;
        const double EarBaseV = 24.9;

        // Arms: shoulders on the chest surface.
        const double ShoulderV = 17.8, ShoulderAngle = 80, ArmRadius = 2.8, UpperArm = 3.2, Forearm = 3.0;
        const double RestSpread = 6;
        const double SleeveR = 0.65, ForeR = 0.58, PawR = 0.62, ClawLen = 0.5, ClawR = 0.16;

        // Colours (linear-ish 0..1).
        const double FurR = 0.93, FurG = 0.52, FurB = 0.66;
        const double FurDarkR = 0.78, FurDarkG = 0.38, FurDarkB = 0.52;
        const double CreamR = 0.99, CreamG = 0.78, CreamB = 0.86;
        const double NoseR = 0.52, NoseG = 0.14, NoseB = 0.27;
        const double IrisYR = 0.99, IrisYG = 0.83, IrisYB = 0.28;

        readonly Pose pose;
        readonly Expression face;
        readonly double yaw, sinY, cosY;
        readonly double headYaw, sinH, cosH;
        readonly double lean, dip;

        readonly double[] eyeAlpha = new double[2], eyeU = new double[2], eyeK = new double[2], eyeVis = new double[2];
        readonly int firstEye;

        readonly double[] earU = new double[2], earZ = new double[2];

        readonly double[] hipU = new double[2], kneeU = new double[2], kneeV = new double[2],
                          ankleU = new double[2], ankleV = new double[2], legZ = new double[2];
        readonly double pawLen, toeDir;

        readonly double[] shU = new double[2], shZ = new double[2],
                          elbU = new double[2], elbV = new double[2], elbZ = new double[2],
                          wristU = new double[2], wristV = new double[2], wristZ = new double[2],
                          handU = new double[2], handV = new double[2], handZ = new double[2],
                          armLo = new double[2], armHi = new double[2], armVlo = new double[2], armVhi = new double[2];

        readonly bool decorated;
        readonly double topV;
        readonly double mouthU;
        readonly double muzzleU, muzzleV;
        readonly double tailSway;

        PantherPainter(Pose pose, bool faceLeft, double px)
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
            mouthU = HeadRx * 0.55 * sinH;
            muzzleU = HeadRx * 0.72 * sinH + dip * 0.5 * sinH;
            muzzleV = MuzzleV - dip * 0.9;

            PlaceEye(0, -EyeAngle * Deg);
            PlaceEye(1, EyeAngle * Deg);
            firstEye = Math.Cos(eyeAlpha[0]) < Math.Cos(eyeAlpha[1]) ? 0 : 1;

            for (int i = 0; i < 2; i++)
            {
                double baseX = i == 0 ? -1.65 : 1.65;
                earU[i] = baseX * cosH * 0.85 + 0.45 * sinH;
                earZ[i] = -baseX * sinH;
            }

            double p = pose.WalkPhase * 2 * Math.PI;
            double swing = pose.Walking ? 30 * Math.Sin(p) : 0;
            double bendA = pose.Walking ? 55 * Math.Max(0, Math.Cos(p)) : 4;
            double bendB = pose.Walking ? 55 * Math.Max(0, -Math.Cos(p)) : 4;
            double tuck = pose.LegTuck;
            PlaceLeg(0, -HipX, -swing + 55 * tuck, bendB + 95 * tuck);
            PlaceLeg(1, HipX, swing + 55 * tuck, bendA + 95 * tuck);
            pawLen = 1.15 + 0.75 * Math.Abs(sinY);
            toeDir = 0.25 + 0.8 * sinY;

            double armSwing = pose.Walking ? 30 * Math.Sin(p) : 0;
            double foreSwing = pose.Walking ? 14 : 0;
            if (pose.ArmNear != 0 || pose.ArmFar != 0) armSwing = 0;
            if (pose.ForeNear != 0 || pose.ForeFar != 0) foreSwing = 0;
            PlaceArm(0, -1, pose.ArmFar, pose.ForeFar, pose.ReachFar, armSwing, foreSwing);
            PlaceArm(1, 1, pose.ArmNear, pose.ForeNear, pose.ReachNear, -armSwing, foreSwing);

            if (pose.Walking) tailSway = Math.Sin(p) * 0.7;
            else tailSway = pose.BodyDy * 2.0 + pose.Lean * 0.5;

            decorated = pose.Sweat > 0 || pose.Note || pose.Zzz || pose.Bubble || pose.Heart;
            topV = decorated ? 37 : Height + 1.6 + Math.Abs(pose.BodyDy) + Math.Max(0, -dip);
        }

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
            ankleV[i] = kneeV[i] - Shin * Math.Cos(b) + 0.6 * pose.Tiptoe;
        }

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

            double f = 0.82;
            wristU[i] = elbU[i] + (handU[i] - elbU[i]) * f;
            wristV[i] = elbV[i] + (handV[i] - elbV[i]) * f;
            wristZ[i] = elbZ[i] + (handZ[i] - elbZ[i]) * f;

            double pad = 1.3;
            if (side > 0 && pose.PointNear) pad += ClawLen;
            armLo[i] = Math.Min(Math.Min(shU[i], elbU[i]), handU[i]) - pad;
            armHi[i] = Math.Max(Math.Max(shU[i], elbU[i]), handU[i]) + pad;
            armVlo[i] = Math.Min(Math.Min(ShoulderV, elbV[i]), handV[i]) - pad;
            armVhi[i] = Math.Max(Math.Max(ShoulderV, elbV[i]), handV[i]) + pad;
        }

        public static void Draw(Surface surface, Pose pose, bool faceLeft, double centreX, double soleY, double scale)
        {
            double sx = scale * pose.StretchX, sy = scale * pose.StretchY;
            double px = 1 / Math.Min(sx, sy);
            PantherPainter painter = new PantherPainter(pose, faceLeft, px);

            int left = Math.Max(0, (int)Math.Floor(centreX - 12.5 * sx));
            int right = Math.Min(surface.Width, (int)Math.Ceiling(centreX + 12.5 * sx));
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
            double ub = u - lean * 0.14 * Math.Max(0, v - 10);

            double axisV = vb < TorsoBottom ? TorsoBottom : vb > TorsoTop ? TorsoTop : vb;
            double t = (axisV - TorsoBottom) / (TorsoTop - TorsoBottom);
            double torsoR = HipR + (ChestR - HipR) * t;
            // Narrow waist: pinch the middle in a little.
            torsoR -= 0.35 * Math.Sin(t * Math.PI);
            double tdy = vb - axisV;
            double torsoDist = Math.Sqrt(ub * ub + tdy * tdy);
            double bulge = torsoR * torsoR - torsoDist * torsoDist;
            bodyZ = bulge > 0 ? Math.Sqrt(bulge) : 0;
            double hdy = vb - HeadV;
            double hky = HeadRx / HeadRy;
            double headBulge = HeadRx * HeadRx - ub * ub - hdy * hdy * hky * hky;
            if (headBulge > bodyZ * bodyZ) bodyZ = Math.Sqrt(headBulge);

            int backLeg = legZ[0] < legZ[1] ? 0 : 1;
            Tail(ub, vb);
            Leg(ub, vb, backLeg);
            Leg(ub, vb, 1 - backLeg);
            Limb(ub, vb, 0);
            Limb(ub, vb, 1);
            Torso(ub, vb, tdy, torsoDist, torsoR);
            Neck(ub, vb);
            if (earZ[0] <= 0) Ear(ub, vb, 0);
            if (earZ[1] <= 0) Ear(ub, vb, 1);
            if (Math.Abs(hdy) < HeadRy + 1.6 && Math.Abs(ub) < HeadRx + 1.6)
            {
                Head(ub, vb, hdy);
                if (earZ[0] > 0) Ear(ub, vb, 0);
                if (earZ[1] > 0) Ear(ub, vb, 1);
                Eye(ub, vb, firstEye);
                Eye(ub, vb, 1 - firstEye);
                Brows(ub, vb);
                Muzzle(ub, vb);
                Nose(ub, vb);
                Mouth(ub, vb);
                Whiskers(ub, vb);
            }
            LayFront();

            if (decorated)
            {
                if (pose.Sweat > 0) Sweat(ub, vb, pose.Sweat, -(HeadRx + 0.9), HeadV + 1.2);
                if (pose.Note) Note(ub, vb, pose.DecorPhase, mouthU, MouthV);
                if (pose.Zzz) Zzz(u, v, pose.DecorPhase, 2.6, Height + 1.2);
                if (pose.Bubble) Bubble(ub, vb, Height + 0.4, Height + 1.3, Height + 2.8, Height + 5.4);
                if (pose.Heart) Heart(ub, vb, pose.DecorPhase, mouthU, MouthV);
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

        void Leg(double u, double v, int i)
        {
            if (v > HipV + 1.5) return;
            double lo = Math.Min(Math.Min(hipU[i], kneeU[i]), ankleU[i]) - 2.4;
            double hi = Math.Max(Math.Max(hipU[i], kneeU[i]), ankleU[i]) + 2.4;
            if (u < lo || u > hi) return;
            double k = 0.82 + 0.18 * (1 + legZ[i] / HipX) / 2;
            Tube(u, v, hipU[i], HipV, kneeU[i], kneeV[i], ThighR, FurR * k, FurG * k, FurB * k, 0.28, 16);
            Tube(u, v, kneeU[i], kneeV[i], ankleU[i], ankleV[i], ShinR, FurR * k, FurG * k, FurB * k, 0.28, 16);

            // Hind paw: a sleek feline foot, toe forward.
            double tip = pose.Tiptoe;
            double sx = ankleU[i] + toeDir + 0.25 * tip, sy = ankleV[i] - 0.5 - 0.2 * tip;
            double dx = (u - sx) / pawLen, dy = (v - sy) / 0.55;
            double e = Math.Sqrt(dx * dx + dy * dy);
            double cov = Cov((e - 1) * 0.55);
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(dx, dy, 1, out nx, out ny, out nz);
            double lr, lg, lb;
            Lit(FurR * k, FurG * k, FurB * k, nx, ny, nz, 0.35, 18, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
            // Toe beans: three small darker pads along the front.
            for (int toe = -1; toe <= 1; toe++)
            {
                double tx = sx + 0.45 + toe * 0.05, ty = sy + 0.08 + Math.Abs(toe) * -0.08;
                double ox = u - tx, oy = v - ty;
                Over(FurDarkR * k, FurDarkG * k, FurDarkB * k, Cov(Math.Sqrt(ox * ox + oy * oy) - 0.10) * 0.5 * cov);
            }
            // Main pad highlight on top of the paw.
            {
                double hx = u - (sx - 0.2), hy = v - (sy + 0.22);
                Over(1, 0.85, 0.9, Cov(Math.Sqrt(hx * hx + hy * hy) - 0.14) * 0.35 * cov);
            }
        }

        void Torso(double u, double v, double dy, double dist, double r)
        {
            double cov = Cov(dist - r);
            if (cov <= 0) return;

            double nx, ny, nz;
            Normal(u, dy, r, out nx, out ny, out nz);

            double phi = Math.Atan2(nx, nz) - yaw;
            if (phi > Math.PI) phi -= 2 * Math.PI; else if (phi < -Math.PI) phi += 2 * Math.PI;
            double arc = r * phi;
            double a = Math.Abs(arc);

            // Sleek pink coat with a very subtle vertical sheen.
            double sheen = 0.03 * Math.Sin(v * 3.1) * Math.Sin(arc * 2.2);
            double cr = FurR + sheen, cg = FurG + sheen, cb = FurB + sheen * 0.7;
            double spec = 0.30, shine = 18;

            // Lighter chest and belly up the front.
            double bellyHw = 1.15 - 0.35 * ((v - TorsoBottom) / (TorsoTop - TorsoBottom));
            if (bellyHw < 0.55) bellyHw = 0.55;
            double belly = Cov(a - bellyHw) * Cov(Math.Abs(v - 14.5) - 3.6);
            Mix(ref cr, ref cg, ref cb, ref spec, ref shine, CreamR, CreamG, CreamB, 0.30, 18, belly * 0.85);

            // A slightly deeper pink saddle down the back so the round reads.
            double back = Cov(2.5 - a);
            Mix(ref cr, ref cg, ref cb, ref spec, ref shine, FurDarkR, FurDarkG, FurDarkB, 0.28, 16, back * 0.35 * cov);

            double lr, lg, lb;
            Lit(cr, cg, cb, nx, ny, nz, spec, shine, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
        }

        void Neck(double u, double v)
        {
            if (Math.Abs(u) > NeckR + 0.6 || v < NeckBottom - 0.3 || v > NeckTop + 0.4) return;
            double qx, qy, tt;
            double dist = Seg(u, v, 0, NeckBottom, 0, NeckTop, out qx, out qy, out tt);
            double cov = Cov(dist - NeckR);
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(u - qx, v - qy, NeckR, out nx, out ny, out nz);
            double lr, lg, lb;
            Lit(FurR * 0.97, FurG * 0.97, FurB * 0.97, nx, ny, nz, 0.30, 18, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
            // Cream throat up the front of the neck.
            double throat = Cov(Math.Abs(u) - 0.55) * Cov(Math.Abs(v - 19.0) - 1.1);
            Over(CreamR * 0.95, CreamG * 0.95, CreamB * 0.95, throat * 0.7 * cov);
        }

        void Ear(double u, double v, int i)
        {
            double cx = earU[i], cy = EarBaseV - dip * 0.7;
            // Pointed ear: a round base with a pointed tip leaning outward.
            double leanOut = i == 0 ? -0.35 : 0.35;
            double tx = cx + leanOut, ty = cy + 1.5;
            if (Math.Abs(u - cx) > 1.4 || v < cy - 0.8 || v > ty + 0.6) return;
            double dx = u - cx, dy = v - (cy + 0.35);
            double disc = Math.Sqrt(dx * dx + dy * dy) - 0.78;
            double tipDx = u - tx, tipDy = v - ty;
            double tipD = Math.Sqrt(tipDx * tipDx + tipDy * tipDy) - 0.42;
            double d = Math.Min(disc, tipD);
            double cov = Cov(d);
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(dx, dy, 0.9, out nx, out ny, out nz);
            double lr, lg, lb;
            Lit(FurR, FurG, FurB, nx, ny, nz, 0.30, 18, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
            // Inner ear: smaller, lighter, tufted.
            double ix = u - cx, iy = v - (cy + 0.35);
            Over(CreamR, CreamG, CreamB, Cov(Math.Sqrt(ix * ix + iy * iy) - 0.38) * 0.9 * cov);
            // Dark tip fringe.
            Over(FurDarkR, FurDarkG, FurDarkB, Cov(Math.Sqrt(tipDx * tipDx + tipDy * tipDy) - 0.30) * 0.35 * cov);
        }

        void Head(double u, double v, double dy)
        {
            double cov = Cov(Ellipse(u, dy, HeadRx, HeadRy));
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(u / HeadRx * 3, dy / HeadRy * 3, 3, out nx, out ny, out nz);
            double lr, lg, lb;
            Lit(FurR, FurG, FurB, nx, ny, nz, 0.32, 20, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
            // Soft cheek shading low on the sides so the skull reads round.
            Over(FurDarkR, FurDarkG, FurDarkB, Cov(Math.Abs(Math.Abs(u) - 1.9) - 0.5) * Cov(v - 21.0) * 0.25 * cov);
            // Forehead highlight catching the light upper-left.
            {
                double hx = u + 0.9, hy = v - (HeadV + 1.1);
                Over(1, 0.85, 0.9, Cov(Math.Sqrt(hx * hx + hy * hy) - 0.55) * 0.30 * cov);
            }
        }

        void Eye(double u, double v, int i)
        {
            double vis = eyeVis[i];
            if (vis <= 0) return;
            double k = eyeK[i], alpha = eyeAlpha[i];
            bool near = i != firstEye;

            double fu = eyeU[i] + dip * 0.5 * sinH, fv = EyeV - dip * 0.8;
            double dx = (u - fu) / k, dy = v - fv;
            if (Math.Abs(dx) > EyeRx + 0.6 || Math.Abs(dy) > EyeRy + 0.6) return;

            double ex = dx / EyeRx, ey = dy / EyeRy;
            double white = (Math.Sqrt(ex * ex + ey * ey) - 1) * EyeRy;
            double covWhite = Cov(white) * vis;
            if (covWhite <= 0) return;

            double nx, ny, nz;
            Normal(dx, dy, EyeRx * 1.6, out nx, out ny, out nz);
            Turn(ref nx, ref nz, alpha);
            double r, g, b;
            // Feline eye: warm white-yellow sclera shading into the iris below.
            Lit(0.99, 0.97, 0.90, nx, ny, nz, 0.4, 24, out r, out g, out b);

            double reach = EyeRx - IrisR - 0.05;
            double ox = pose.GazeX * reach * 0.9 + face.PupilDx * 0.4;
            double oy = pose.GazeY * (EyeRy - IrisR * 0.6) * 0.8 + face.PupilDy * 0.3;
            ox -= (i == 1 ? 1 : -1) * face.CrossEye * 0.3;
            double om = Math.Sqrt(ox * ox + oy * oy);
            if (om > reach) { ox *= reach / om; oy *= reach / om; }
            double ix = dx - ox, iy = dy - oy;
            double covIris = Cov(Squeezed(ix, iy, k, IrisR));
            if (covIris > 0)
            {
                // Yellow iris, darker rim, slit pupil.
                double tt = Math.Sqrt(ix * ix + iy * iy) / IrisR;
                double shade = 1.15 - 0.45 * tt;
                r += (IrisYR * shade - r) * covIris;
                g += (IrisYG * shade - g) * covIris;
                b += (IrisYB * shade - b) * covIris;
                // Slit pupil: narrow vertical ellipse.
                double px0 = ix / (0.28 * face.PupilSize + 0.08), py0 = iy / (IrisR * 0.85 * face.PupilSize + 0.08);
                double covPupil = Cov((Math.Sqrt(px0 * px0 + py0 * py0) - 1) * 0.22);
                r += (0.04 - r) * covPupil;
                g += (0.03 - g) * covPupil;
                b += (0.04 - b) * covPupil;
                double lx = ix + flip * 0.32 * IrisR, ly = iy - 0.32 * IrisR;
                double covLight = Cov(Squeezed(lx, ly, k, IrisR * 0.24)) * 0.95;
                r += (1 - r) * covLight;
                g += (1 - g) * covLight;
                b += (1 - b) * covLight;
            }

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
                Lit(FurR, FurG, FurB, bx, by, bz, 0.32, 20, out sr, out sg, out sb);
                double covLid = Math.Max(upper > 0.01 ? Cov(upperEdge - v) : 0, lower > 0.01 ? Cov(v - lowerEdge) : 0);
                r += (sr - r) * covLid;
                g += (sg - g) * covLid;
                b += (sb - b) * covLid;
                double lash = Math.Max(upper > 0.01 ? Cov(Math.Abs(v - upperEdge) - 0.07) : 0,
                                       lower > 0.01 ? Cov(Math.Abs(v - lowerEdge) - 0.06) : 0) * 0.7;
                r += (0.25 - r) * lash;
                g += (0.12 - g) * lash;
                b += (0.18 - b) * lash;
            }
            Over(r, g, b, covWhite);

            double rim = Cov(Math.Abs(white) - 0.07) * (dy > 0.1 ? 0.8 : 0.4) * vis;
            Over(0.30, 0.12, 0.20, rim);
        }

        void Brows(double u, double v)
        {
            double baseV = BrowV - dip * 0.8 + face.BrowRaise * 0.35;
            if (Math.Abs(v - baseV) > 1.2) return;
            Brow(u, v, eyeAlpha[0], -1, baseV, eyeVis[0]);
            Brow(u, v, eyeAlpha[1], 1, baseV + face.BrowAsym * 0.3, eyeVis[1]);
        }

        void Brow(double u, double v, double alpha, double outward, double cy, double show)
        {
            if (show <= 0) return;
            double k = Math.Cos(alpha);
            if (k < 0.2) return;
            double ex = (HeadRx + 0.1) * Math.Sin(alpha) + dip * 0.5 * sinH;
            double ax = ex - outward * 0.9 * k, ay = cy - face.BrowAngle * 0.4 - 0.06;
            double bx = ex + outward * 0.9 * k, by = cy + face.BrowAngle * 0.4 - 0.06;
            double qx, qy, tt;
            double d = Seg(u, v, ax, ay, bx, by, out qx, out qy, out tt) - 0.16;
            Over(0.35, 0.12, 0.22, Cov(d) * show * 0.9);
        }

        void Muzzle(double u, double v)
        {
            double dx = u - muzzleU, dy = v - muzzleV;
            double cov = Cov(Ellipse(dx, dy, MuzzleRx, MuzzleRy));
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(dx / MuzzleRx * 2, dy / MuzzleRy * 2, 2, out nx, out ny, out nz);
            double lr, lg, lb;
            Lit(CreamR, CreamG, CreamB, nx, ny, nz, 0.30, 18, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
        }

        void Nose(double u, double v)
        {
            double vis = cosH > -0.3 ? 1 : cosH < -0.6 ? 0 : (cosH + 0.6) / 0.3;
            if (vis <= 0) return;
            double nu = muzzleU + 0.05 * sinH, nv = NoseV - dip * 0.9;
            if (Math.Abs(u - nu) > 1.2 || Math.Abs(v - nv) > 1.0) return;
            // Triangular feline nose: wide top, point below.
            double dx = (u - nu) / 0.42, dy = (v - nv) / 0.32;
            double tri = Math.Max(Math.Abs(dx) + (dy - 0.4) * 0.9, -dy - 0.4);
            double d = tri * 0.32;
            double cov = Cov(d) * vis;
            if (cov > 0)
            {
                double nx, ny, nz;
                Normal(dx, dy, 1, out nx, out ny, out nz);
                double lr, lg, lb;
                Lit(NoseR, NoseG, NoseB, nx, ny, nz, 0.5, 22, out lr, out lg, out lb);
                Over(lr, lg, lb, cov);
                double hx = u - (nu - 0.12 * flip), hy = v - (nv + 0.10);
                Over(1, 0.8, 0.85, Cov(Math.Sqrt(hx * hx + hy * hy) - 0.09) * 0.6 * vis);
            }
            // Philtrum seam down to the mouth.
            double qx, qy, tt;
            double seam = Seg(u, v, nu, nv - 0.25, nu, MouthV + 0.25, out qx, out qy, out tt) - 0.045;
            Over(0.45, 0.18, 0.28, Cov(seam) * 0.7 * vis);
        }

        void Mouth(double u, double v)
        {
            double vis = cosH > 0.17 ? 1 : cosH < -0.08 ? 0 : (cosH + 0.08) / 0.25;
            if (vis <= 0) return;
            double k = cosH > 0.35 ? cosH : 0.35;
            double mu = muzzleU + face.MouthDx * 0.5 * k;
            double mv = MouthV - dip * 0.9;
            double w = (0.9 + face.MouthWidth * 0.9) * k;
            double hx = Math.Abs(u - mu) - w;
            if (hx > 0.7 || Math.Abs(v - mv) > 2.2) return;

            double tt = (u - mu) / w;
            if (tt < -1) tt = -1; else if (tt > 1) tt = 1;
            // Feline smile: dips at the centre (philtrum), curls up at the corners.
            double line = mv + face.MouthCurve * 0.55 * (tt * tt - 0.35) - 0.18 * (1 - tt * tt) + face.Wobble * 0.10 * Math.Sin(tt * 8.5);
            double open = face.MouthOpen * 1.2;
            const double lip = 0.17;

            if (open < 0.03)
            {
                double ox = hx > 0 ? hx : 0, oy = v - line;
                double cov = Cov(Math.Sqrt(ox * ox + oy * oy) - lip / 2);
                Over(0.48, 0.16, 0.28, cov * vis);
                return;
            }

            double gape = open * 1.4 * Math.Pow(1 - tt * tt, 0.6);
            double upper = line, lower = line - gape;
            double d = Math.Max(Math.Max(v - upper, lower - v), hx);
            double inside = Cov(d);
            if (inside > 0)
            {
                Over(0.30, 0.08, 0.14, inside * vis);
                double teethH = open * 0.55 * face.Teeth;
                if (teethH > 0.04)
                {
                    double teeth = Cov(Math.Max(upper - teethH - v, d)) * inside;
                    Over(0.98, 0.96, 0.94, teeth * vis);
                    // One fang each side.
                    double fx = Math.Abs(u - mu) - w * 0.55, fy = v - (upper - teethH * 0.6);
                    Over(0.98, 0.96, 0.94, Cov(Math.Sqrt(fx * fx + fy * fy) - 0.14) * teeth * vis);
                }
                if (face.Tongue > 0.02 || open > 0.35)
                {
                    double rx = Math.Min(w * 0.5, 0.5), ry = 0.20 + 0.30 * Math.Max(face.Tongue, open * 0.5);
                    double cy = lower + 0.20 - 0.45 * Math.Max(face.Tongue, open * 0.4);
                    double tx2 = (u - mu) / rx, ty2 = (v - cy) / ry;
                    double ee = Math.Sqrt(tx2 * tx2 + ty2 * ty2);
                    double td = Math.Max((ee - 1) * ry, v - upper + 0.1);
                    double covT = Cov(td);
                    if (covT > 0) Over(0.85, 0.35, 0.48, covT * vis);
                }
            }
            double outline = Cov(Math.Abs(d) - lip / 2);
            Over(0.48, 0.16, 0.28, outline * vis);
        }

        void Whiskers(double u, double v)
        {
            double vis = cosH > 0 ? 1 : cosH < -0.4 ? 0 : (cosH + 0.4) / 0.4;
            if (vis <= 0) return;
            if (Math.Abs(v - muzzleV) > 1.6) return;
            double qx, qy, tt;
            for (int side = -1; side <= 1; side += 2)
            {
                double rootU = muzzleU + side * 0.9 * (cosH > 0.35 ? cosH : 0.35);
                for (int w = -1; w <= 1; w++)
                {
                    double tipU = rootU + side * (1.6 + Math.Abs(w) * 0.25);
                    double tipV = muzzleV + w * 0.35 + 0.1;
                    double d = Seg(u, v, rootU, muzzleV + w * 0.18, tipU, tipV, out qx, out qy, out tt) - 0.035;
                    Over(1, 0.98, 0.99, Cov(d) * 0.65 * vis);
                }
                // Whisker root dots.
                for (int dot = 0; dot < 3; dot++)
                {
                    double dx = u - (rootU + side * dot * 0.12), dy = v - (muzzleV - 0.25 + dot * 0.25);
                    Over(0.55, 0.25, 0.35, Cov(Math.Sqrt(dx * dx + dy * dy) - 0.055) * 0.6 * vis);
                }
            }
        }

        void Limb(double u, double v, int i)
        {
            if (u < armLo[i] || u > armHi[i] || v < armVlo[i] || v > armVhi[i]) return;
            ArmTube(u, v, shU[i], ShoulderV, shZ[i], elbU[i], elbV[i], elbZ[i], SleeveR, FurR, FurG, FurB, 0.28, 16);
            ArmTube(u, v, elbU[i], elbV[i], elbZ[i], wristU[i], wristV[i], wristZ[i], ForeR, FurR, FurG, FurB, 0.28, 16);

            if (i == 1 && pose.PointNear)
            {
                double fx = handU[i] - wristU[i], fy = handV[i] - wristV[i];
                double len = Math.Sqrt(fx * fx + fy * fy);
                if (len > 1e-6)
                {
                    double reach = (PawR + ClawLen) / len;
                    ArmTube(u, v, handU[i], handV[i], handZ[i], handU[i] + fx * reach, handV[i] + fy * reach, handZ[i],
                            ClawR, CreamR, CreamG, CreamB, 0.3, 16);
                }
            }

            double dx = u - handU[i], dy = v - handV[i];
            double cov = Cov(Math.Sqrt(dx * dx + dy * dy) - PawR);
            if (cov <= 0) return;
            double hnx, hny, hnz;
            Normal(dx, dy, PawR, out hnx, out hny, out hnz);
            double hk = Depth(handZ[i]);
            double hr, hg, hb;
            Lit(FurR * hk, FurG * hk, FurB * hk, hnx, hny, hnz, 0.32, 20, out hr, out hg, out hb);
            Sort(hr, hg, hb, cov, handZ[i] + hnz * PawR);
            // Paw pads: one main + toes when the paw is held up.
            double px0 = u - handU[i], py0 = v - (handV[i] - 0.15);
            Sort(FurDarkR * hk, FurDarkG * hk, FurDarkB * hk, Cov(Math.Sqrt(px0 * px0 + py0 * py0) - 0.16) * 0.5 * cov, handZ[i] + PawR);
        }

        void Tail(double u, double v)
        {
            // Tail root behind the body; sways with the walk, curls up at the tip.
            double rootU = -2.0 * sinY;
            double rootV = 11.5;
            double midU = rootU - 1.6 * cosY - 0.4 + tailSway * 0.8;
            double midV = 9.2;
            double tipU = rootU - 0.7 * cosY + 0.9 + tailSway * 1.4;
            double tipV = 12.8;
            double curlU = tipU + 0.9 * sinY + 0.7 + tailSway * 0.5;
            double curlV = 14.2;
            if (Math.Abs(u - rootU) > 5.5 && Math.Abs(u - midU) > 4.5 && Math.Abs(u - tipU) > 4.5) return;
            if (v < 7.5 || v > 16.0) return;
            Tube(u, v, rootU, rootV, midU, midV, 0.55, FurR * 0.96, FurG * 0.96, FurB * 0.96, 0.28, 16);
            Tube(u, v, midU, midV, tipU, tipV, 0.48, FurR, FurG, FurB, 0.30, 18);
            Tube(u, v, tipU, tipV, curlU, curlV, 0.40, FurR * 1.02, FurG * 1.02, FurB * 1.02, 0.32, 20);
            // Tuft highlight on the curl.
            {
                double dx = u - curlU, dy = v - curlV;
                Over(1, 0.85, 0.9, Cov(Math.Sqrt(dx * dx + dy * dy) - 0.22) * 0.4);
            }
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
