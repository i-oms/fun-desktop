using System;
using IdleWalker.Domain;

namespace IdleWalker.Rendering
{
    /// <summary>
    /// Paints the Pink Panther into a <see cref="Surface"/> the way the other
    /// figures are painted: as lit solids with anti-aliased edges, composited
    /// back to front (see <see cref="SolidPainter"/>). The DePatie-Freleng
    /// cartoon cat: tall, thin and all pink - long legs with paws, a slim
    /// waist, a long neck, a small head with a long tapering snout ending
    /// in a big black nose, small pointed ears set back, large half-lidded
    /// yellow eyes with round black pupils, thin whiskers, a feline smile
    /// with a fang when the mouth opens, and a long thin tail curling at
    /// the tip. No lighter markings anywhere: the cartoon has none.
    ///
    /// Model space is the Minions': u across in units, positive the way he
    /// faces; v up from the soles. He is <see cref="Height"/> units tall.
    /// The body turns with <see cref="Pose.Yaw"/>; the head turns further by
    /// <see cref="Pose.HeadTurn"/> and nods by <see cref="Pose.HeadDip"/>.
    /// </summary>
    sealed class PantherPainter : SolidPainter
    {
        // One painter per thread, set up afresh for every frame.
        [ThreadStatic] static PantherPainter cached;

        public const double Height = 26;

        // The DePatie-Freleng panther: all pink, nothing else - no lighter
        // chest, muzzle or belly - built long and thin: a slender torso on
        // long legs, a long neck, a small head with a long tapering snout
        // ending in a big black nose, small pointed ears set back, large
        // half-lidded yellow eyes with round black pupils, thin whiskers,
        // and a long thin tail curling at the tip.

        // Legs: hips either side of the axis, thigh + shin with a knee between.
        const double HipV = 11.0, HipX = 0.95, Thigh = 5.2, Shin = 5.0, ThighR = 0.78, ShinR = 0.62;

        // Torso: a slim capsule from hips to shoulders, narrow at the waist.
        const double TorsoBottom = 11.2, TorsoTop = 17.6, HipR = 1.85, ChestR = 2.25;
        // Shoulders rise this far above TorsoTop before rounding off: a low
        // cap so the neck stands clear instead of lying along the chest.
        const double ShoulderCap = 0.8;
        const double NeckBottom = 17.4, NeckTop = 21.0, NeckR = 0.68;

        // Head: a small rounded skull; the snout reaches forward from it.
        const double HeadV = 23.0, HeadRx = 2.25, HeadRy = 2.45;
        const double EyeV = 23.3, EyeAngle = 20, EyeRx = 0.72, EyeRy = 0.92, IrisR = 0.44;
        const double BrowV = 24.4;
        const double SnoutLen = 2.6, SnoutRBase = 1.05, SnoutRTip = 0.55;   // the muzzle: a tapering tube out of the face
        const double MuzzleV = 21.9;                                        // where the snout leaves the face
        const double SnoutDrop = 0.45;                                      // the tip sits this much lower than the base
        const double NoseR0 = 0.46;                                         // the black nose at the tip
        const double MouthV = 21.1;
        const double EarBaseV = 24.9;

        // Arms: shoulders on the chest surface.
        const double ShoulderV = 17.6, ShoulderAngle = 80, ArmRadius = 2.35, UpperArm = 3.3, Forearm = 3.1;
        const double RestSpread = 6;
        const double SleeveR = 0.55, ForeR = 0.48, PawR = 0.58, ClawLen = 0.5, ClawR = 0.16;

        // Colours (linear-ish 0..1): the cartoon's pastel pink and a deeper pink for shade.
        const double FurR = 0.96, FurG = 0.63, FurB = 0.77;
        const double FurDarkR = 0.84, FurDarkG = 0.46, FurDarkB = 0.63;
        const double CreamR = 0.98, CreamG = 0.74, CreamB = 0.85;   // the inner ear only
        const double NoseR = 0.08, NoseG = 0.06, NoseB = 0.08;
        const double IrisYR = 0.99, IrisYG = 0.86, IrisYB = 0.30;

        Pose pose;
        Expression face;
        double yaw, sinY, cosY;
        double headYaw, sinH, cosH;
        double lean, dip;

        readonly double[] eyeAlpha = new double[2], eyeU = new double[2], eyeK = new double[2], eyeVis = new double[2];
        int firstEye;

        readonly double[] earU = new double[2], earZ = new double[2];

        readonly double[] hipU = new double[2], kneeU = new double[2], kneeV = new double[2],
                          ankleU = new double[2], ankleV = new double[2], legZ = new double[2];
        double pawLen, toeDir;

        readonly double[] shU = new double[2], shZ = new double[2],
                          elbU = new double[2], elbV = new double[2], elbZ = new double[2],
                          wristU = new double[2], wristV = new double[2], wristZ = new double[2],
                          handU = new double[2], handV = new double[2], handZ = new double[2],
                          armLo = new double[2], armHi = new double[2], armVlo = new double[2], armVhi = new double[2];

        bool decorated;
        double topV;
        double mouthU;
        double snoutU0, snoutV0, snoutU1, snoutV1;   // the snout's base on the face and its tip, on screen
        double tailSway;

        // Tail centreline resampled along a smooth spline: one flowing curve,
        // never straight rods. Filled in BuildTail once per pose.
        const int TailSpans = 5, TailPer = 4, TailSamples = TailSpans * TailPer;
        readonly double[] tailU = new double[TailSamples + 1], tailV = new double[TailSamples + 1];
        double tailLo, tailHi, tailVlo, tailVhi;
        readonly double[] tailDu = new double[TailSamples], tailDv = new double[TailSamples], tailLen2 = new double[TailSamples];
        readonly double[] knotU = new double[6], knotV = new double[6];   // the spline's control points

        // Fixed for the frame: the eyes' cosines and sines, where each pupil
        // sits, and the box the whiskers fit in.
        readonly double[] eyeCos = new double[2], eyeSin = new double[2];
        readonly double[] gazeU = new double[2], gazeV = new double[2];
        double pupilR;
        double whiskLo, whiskHi;

        // The torso's cross-section depends on the row only: the last row's,
        // and the cosines of the angles round the body where the darker back
        // starts and ends (Torso only needs the exact angle in between).
        const double BackArc = 2.2;
        double rowV, rowTorsoR, rowDyEff, rowCosFront, rowCosBack;

        void Init(Pose pose, bool faceLeft, double px)
        {
            Setup(faceLeft, px, ArmRadius);
            rowV = double.NaN;
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
            // The snout leaves the face just below the eyes and reaches
            // forward the way the head faces: a long profile from the side,
            // a short bump from the front.
            double shift = dip * 0.5 * sinH;
            snoutU0 = (HeadRx - 0.5) * sinH + shift;
            snoutV0 = MuzzleV - dip * 0.8;
            snoutU1 = (HeadRx - 0.5 + SnoutLen) * sinH + shift;
            snoutV1 = MuzzleV - SnoutDrop - dip * 1.1;
            mouthU = (snoutU0 + snoutU1) / 2;

            PlaceEye(0, -EyeAngle * Deg);
            PlaceEye(1, EyeAngle * Deg);
            firstEye = Math.Cos(eyeAlpha[0]) < Math.Cos(eyeAlpha[1]) ? 0 : 1;
            pupilR = IrisR * 0.62 * face.PupilSize + 0.04;
            double reach = EyeRx - pupilR - 0.08;
            for (int i = 0; i < 2; i++)
            {
                eyeCos[i] = Math.Cos(eyeAlpha[i]);
                eyeSin[i] = Math.Sin(eyeAlpha[i]);
                double ox = pose.GazeX * reach * 0.9 + face.PupilDx * 0.4;
                double oy = pose.GazeY * (EyeRy - pupilR) * 0.8 + face.PupilDy * 0.3;
                ox -= (i == 1 ? 1 : -1) * face.CrossEye * 0.3;
                double om = Math.Sqrt(ox * ox + oy * oy);
                if (om > reach) { ox *= reach / om; oy *= reach / om; }
                gazeU[i] = ox;
                gazeV[i] = oy;
            }

            // The whiskers' box: every root and tip, padded by their
            // thickness and a pixel for the soft edge.
            {
                double rootU = (snoutU0 + snoutU1) / 2;
                double k = cosH > 0.35 ? cosH : 0.35;
                whiskLo = double.MaxValue;
                whiskHi = double.MinValue;
                for (int side = -1; side <= 1; side += 2)
                {
                    double ru = rootU + side * 0.75 * k;
                    for (int w = -1; w <= 1; w++)
                    {
                        double tipU = ru + side * (2.0 + Math.Abs(w) * 0.2) * (0.4 + 0.6 * k) - 0.6 * sinH;
                        whiskLo = Math.Min(whiskLo, Math.Min(ru, tipU));
                        whiskHi = Math.Max(whiskHi, Math.Max(ru, tipU));
                    }
                }
                whiskLo -= 0.03 + px;
                whiskHi += 0.03 + px;
            }

            for (int i = 0; i < 2; i++)
            {
                double baseX = i == 0 ? -1.4 : 1.4;
                earU[i] = baseX * cosH * 0.85 + 0.2 * sinH;
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

            BuildTail();

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

        /// <summary>
        /// Lays out the tail as a long hanging banana curve through six
        /// control points: out from the rear, drooping down, then flicking up
        /// at the thin tip. Every bend along the thick part turns 45 degrees
        /// or less, so the tube's inner wall can never pinch into a crease;
        /// only the thin tip hooks harder. Resampled with Catmull-Rom into
        /// short links so no straight-rod joints ever show. Radius tapers
        /// thin and even towards the tip. Drawn in profile at any yaw, the
        /// way the cartoon always draws the tail.
        /// </summary>
        void BuildTail()
        {
            double[] cu = knotU, cv = knotV;
            double rootU = -2.0 * sinY;
            cu[0] = rootU;                        cv[0] = 11.5;
            cu[1] = rootU - 3.0 + tailSway * 0.4; cv[1] = 9.8;
            cu[2] = rootU - 4.8 + tailSway * 0.7; cv[2] = 7.6;
            cu[3] = rootU - 5.4 + tailSway;       cv[3] = 5.0;
            cu[4] = rootU - 4.4 + tailSway * 1.3; cv[4] = 3.2;
            cu[5] = rootU - 3.2 + tailSway * 1.5; cv[5] = 3.4;

            tailLo = tailHi = cu[0];
            tailVlo = tailVhi = cv[0];
            for (int j = 0; j < TailSamples; j++)
            {
                int span = j / TailPer;
                double t = (j % TailPer) / (double)TailPer;
                int p0 = span - 1 < 0 ? 0 : span - 1, p3 = span + 2 > 5 ? 5 : span + 2;
                tailU[j] = Catmull(cu[p0], cu[span], cu[span + 1], cu[p3], t);
                tailV[j] = Catmull(cv[p0], cv[span], cv[span + 1], cv[p3], t);
                if (tailU[j] < tailLo) tailLo = tailU[j];
                if (tailU[j] > tailHi) tailHi = tailU[j];
                if (tailV[j] < tailVlo) tailVlo = tailV[j];
                if (tailV[j] > tailVhi) tailVhi = tailV[j];
            }
            tailU[TailSamples] = cu[5];
            tailV[TailSamples] = cv[5];
            for (int j = 0; j < TailSamples; j++)
            {
                tailDu[j] = tailU[j + 1] - tailU[j];
                tailDv[j] = tailV[j + 1] - tailV[j];
                tailLen2[j] = tailDu[j] * tailDu[j] + tailDv[j] * tailDv[j];
            }
        }

        static double Catmull(double p0, double p1, double p2, double p3, double t)
        {
            double t2 = t * t, t3 = t2 * t;
            return 0.5 * ((2 * p1) + (-p0 + p2) * t +
                          (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 +
                          (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
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
            PantherPainter painter = cached ?? (cached = new PantherPainter());
            painter.Init(pose, faceLeft, px);

            int left = Math.Max(0, (int)Math.Floor(centreX - 12.5 * sx));
            int right = Math.Min(surface.Width, (int)Math.Ceiling(centreX + 12.5 * sx));
            int top = Math.Max(0, (int)Math.Floor(soleY - painter.topV * sy));
            int bottom = Math.Min(surface.Height, (int)Math.Ceiling(soleY + 0.5));
            if (left >= right || top >= bottom) return;

            painter.Raster(surface, left, top, right, bottom, centreX, soleY, sx, sy);
        }

        protected override int Shade(double u, double v)
        {
            Begin();

            double vb = v + pose.BodyDy;
            double ub = u - lean * 0.14 * Math.Max(0, v - 10);

            if (vb != rowV)
            {
                double axisV = vb < TorsoBottom ? TorsoBottom : vb > TorsoTop ? TorsoTop : vb;
                double t = (axisV - TorsoBottom) / (TorsoTop - TorsoBottom);
                double r = HipR + (ChestR - HipR) * t;
                // Narrow waist: pinch the middle in a little.
                r -= 0.35 * Math.Sin(t * Math.PI);
                // Above the shoulders the dome is squashed to ShoulderCap so the
                // neck stands clear of the chest instead of lying along it.
                double tdy = vb - axisV;
                rowDyEff = vb > TorsoTop ? tdy * r / ShoulderCap : tdy;
                rowTorsoR = r;
                rowCosFront = Math.Cos((BackArc - 0.5 * px) / r);
                rowCosBack = Math.Cos((BackArc + 0.5 * px) / r);
                rowV = vb;
            }
            double torsoR = rowTorsoR;
            double dyEff = rowDyEff;
            double torsoDist = Math.Sqrt(ub * ub + dyEff * dyEff);
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
            // The neck behind the torso: its base stays buried in the
            // shoulders, so no tube shows through the chest - only the part
            // standing clear above them is ever seen.
            Neck(ub, vb);
            Torso(ub, vb, dyEff, torsoDist, torsoR);
            if (earZ[0] <= 0) Ear(ub, vb, 0);
            if (earZ[1] <= 0) Ear(ub, vb, 1);
            if (Math.Abs(hdy) < HeadRy + 1.6 && Math.Abs(ub) < HeadRx + SnoutLen + 0.8)
            {
                Head(ub, vb, hdy);
                if (earZ[0] > 0) Ear(ub, vb, 0);
                if (earZ[1] > 0) Ear(ub, vb, 1);
                Snout(ub, vb);
                Eye(ub, vb, firstEye);
                Eye(ub, vb, 1 - firstEye);
                Brows(ub, vb);
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

            // Sleek pink coat, plain as the cartoon's: no chest patch, no
            // sheen; a slightly deeper pink down the back so the round reads.
            // The back starts BackArc round from the front. The cosine of
            // the angle round the body says at once whether a pixel is well
            // in front of that (none of it) or well behind (all of it); only
            // across the soft edge between is the angle itself worked out.
            double back;
            double n = Math.Sqrt(nx * nx + nz * nz);
            double c = nx * sinY + nz * cosY;
            if (c > n * rowCosFront + 1e-9) back = 0;
            else if (c < n * rowCosBack - 1e-9) back = 1;
            else
            {
                double phi = Math.Atan2(nx, nz) - yaw;
                if (phi > Math.PI) phi -= 2 * Math.PI; else if (phi < -Math.PI) phi += 2 * Math.PI;
                double arc = r * phi;
                back = Cov(BackArc - Math.Abs(arc));
            }
            double cr = FurR, cg = FurG, cb = FurB;
            double spec = 0.30, shine = 18;
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
        }

        void Ear(double u, double v, int i)
        {
            double cx = earU[i], cy = EarBaseV - dip * 0.7;
            // A small pointed ear: a tapering tube from a round base to a
            // tip leaning outward, set back on the skull.
            double leanOut = i == 0 ? -0.4 : 0.4;
            double tx = cx + leanOut, ty = cy + 1.25;
            if (Math.Abs(u - cx) > 1.2 || v < cy - 0.7 || v > ty + 0.5) return;
            double qx, qy, t;
            double d = Seg(u, v, cx, cy + 0.2, tx, ty, out qx, out qy, out t) - (0.62 - 0.42 * t);
            double cov = Cov(d);
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(u - qx, v - qy, 0.62 - 0.42 * t, out nx, out ny, out nz);
            double lr, lg, lb;
            Lit(FurR, FurG, FurB, nx, ny, nz, 0.30, 18, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
            // Inner ear: a lighter pink hollow, only when the ear faces the viewer.
            if (earZ[i] > 0)
            {
                double ix = u - (cx + leanOut * 0.3), iy = v - (cy + 0.5);
                Over(CreamR, CreamG, CreamB, Cov(Math.Sqrt(ix * ix + iy * iy * 2.2) - 0.3) * 0.85 * cov);
            }
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
            double k = eyeK[i];
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
            Turn(ref nx, ref nz, eyeCos[i], eyeSin[i]);
            double r, g, b;
            // The cartoon's eye: the whole eye is yellow, with a round black
            // pupil, under a heavy half-closed lid - the cool look.
            Lit(IrisYR, IrisYG, IrisYB, nx, ny, nz, 0.4, 24, out r, out g, out b);

            double ix = dx - gazeU[i], iy = dy - gazeV[i];
            double covPupil = Cov(Squeezed(ix, iy, k, pupilR));
            if (covPupil > 0)
            {
                r += (0.04 - r) * covPupil;
                g += (0.03 - g) * covPupil;
                b += (0.04 - b) * covPupil;
                double lx = ix + flip * 0.35 * pupilR, ly = iy - 0.35 * pupilR;
                double covLight = Cov(Squeezed(lx, ly, k, pupilR * 0.3)) * 0.95;
                r += (1 - r) * covLight;
                g += (1 - g) * covLight;
                b += (1 - b) * covLight;
            }

            // The lids sit a third of the way down even when the face is at
            // rest; a wide-eyed face lifts them, a sleepy one drops them.
            double upper = Math.Max(Math.Min(0.95, face.Lid + 0.3 - 0.35 * Math.Max(0, face.BrowRaise)), near ? face.Wink : 0);
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
            Brow(u, v, 0, -1, baseV, eyeVis[0]);
            Brow(u, v, 1, 1, baseV + face.BrowAsym * 0.3, eyeVis[1]);
        }

        void Brow(double u, double v, int i, double outward, double cy, double show)
        {
            if (show <= 0) return;
            double k = eyeCos[i];
            if (k < 0.2) return;
            double ex = (HeadRx + 0.1) * eyeSin[i] + dip * 0.5 * sinH;
            double ax = ex - outward * 0.9 * k, ay = cy - face.BrowAngle * 0.4 - 0.06;
            double bx = ex + outward * 0.9 * k, by = cy + face.BrowAngle * 0.4 - 0.06;
            double qx, qy, tt;
            double d = Seg(u, v, ax, ay, bx, by, out qx, out qy, out tt) - 0.16;
            Over(0.35, 0.12, 0.22, Cov(d) * show * 0.9);
        }

        // The snout: a tapering tube out of the face, the full length in
        // profile and a short bump head-on, in the same pink as the rest.
        void Snout(double u, double v)
        {
            double vis = cosH > -0.5 ? 1 : cosH < -0.8 ? 0 : (cosH + 0.8) / 0.3;
            if (vis <= 0) return;
            if (v > snoutV0 + SnoutRBase + 0.4 || v < snoutV1 - SnoutRTip - 0.5) return;
            if (u < Math.Min(snoutU0, snoutU1) - SnoutRBase - 0.4 || u > Math.Max(snoutU0, snoutU1) + SnoutRBase + 0.4) return;
            double qx, qy, t;
            double dist = Seg(u, v, snoutU0, snoutV0, snoutU1, snoutV1, out qx, out qy, out t);
            double radius = SnoutRBase + (SnoutRTip - SnoutRBase) * t;
            double cov = Cov(dist - radius) * vis;
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(u - qx, v - qy, radius, out nx, out ny, out nz);
            double lr, lg, lb;
            Lit(FurR, FurG, FurB, nx, ny, nz, 0.30, 18, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
            // A touch of shade under the snout where it meets the face.
            Over(FurDarkR, FurDarkG, FurDarkB, Cov(v - (snoutV0 - radius * 0.3)) * 0.25 * (1 - t) * cov);
        }

        // The big black nose on the tip of the snout, with a glint.
        void Nose(double u, double v)
        {
            double vis = cosH > -0.5 ? 1 : cosH < -0.8 ? 0 : (cosH + 0.8) / 0.3;
            if (vis <= 0) return;
            double nu = snoutU1 + 0.15 * sinH, nv = snoutV1 + 0.1;
            double dx = u - nu, dy = v - nv;
            if (Math.Abs(dx) > NoseR0 + 0.5 || Math.Abs(dy) > NoseR0 + 0.5) return;
            double cov = Cov(Math.Sqrt(dx * dx + dy * dy * 1.3) - NoseR0) * vis;
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(dx, dy, NoseR0, out nx, out ny, out nz);
            double lr, lg, lb;
            Lit(NoseR, NoseG, NoseB, nx, ny, nz, 0.6, 24, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
            double hx = u - (nu - 0.14 * flip), hy = v - (nv + 0.14);
            Over(0.9, 0.9, 0.95, Cov(Math.Sqrt(hx * hx + hy * hy) - 0.09) * 0.7 * cov);
        }

        void Mouth(double u, double v)
        {
            double vis = cosH > 0.17 ? 1 : cosH < -0.08 ? 0 : (cosH + 0.08) / 0.25;
            if (vis <= 0) return;
            double k = cosH > 0.35 ? cosH : 0.35;
            // The mouth runs along the underside of the snout, from the
            // cheek towards the nose.
            double mu = (snoutU0 + snoutU1) / 2 + face.MouthDx * 0.5 * k;
            double mv = MouthV - dip * 0.9 - 0.25 * Math.Abs(sinH);
            double w = (0.8 + face.MouthWidth * 0.8) * k + 0.6 * Math.Abs(sinH);
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
            // Three long thin whiskers a side, from the snout's cheeks; from
            // the side only the near ones show, sweeping back past the face.
            double rootV = (snoutV0 + snoutV1) / 2 - 0.1;
            if (Math.Abs(v - rootV) > 1.8 || u < whiskLo || u > whiskHi) return;
            double qx, qy, tt;
            double rootU = (snoutU0 + snoutU1) / 2;
            double k = cosH > 0.35 ? cosH : 0.35;
            for (int side = -1; side <= 1; side += 2)
            {
                double ru = rootU + side * 0.75 * k;
                for (int w = -1; w <= 1; w++)
                {
                    double tipU = ru + side * (2.0 + Math.Abs(w) * 0.2) * (0.4 + 0.6 * k) - 0.6 * sinH;
                    double tipV = rootV + w * 0.5 + 0.15;
                    double d = Seg(u, v, ru, rootV + w * 0.16, tipU, tipV, out qx, out qy, out tt) - 0.03;
                    Over(0.25, 0.10, 0.16, Cov(d) * 0.7 * vis);
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
            // One flowing ribbon, shaded once: distance to the whole sampled
            // centreline, then a single lighting pass - so neither straight
            // joints nor caterpillar bands can show.
            if (u < tailLo - 1.2 || u > tailHi + 1.2 || v < tailVlo - 1.2 || v > tailVhi + 1.2) return;
            // Squared distances to each link (the spans worked out per
            // frame), one square root for the nearest.
            double best = double.MaxValue, qx = 0, qy = 0, qt = 0;
            int hit = 0;
            for (int i = 0; i < TailSamples; i++)
            {
                double ax = tailU[i], ay = tailV[i], abx = tailDu[i], aby = tailDv[i], len2 = tailLen2[i];
                double st = len2 > 0 ? ((u - ax) * abx + (v - ay) * aby) / len2 : 0;
                if (st < 0) st = 0; else if (st > 1) st = 1;
                double sx = ax + abx * st, sy = ay + aby * st;
                double dx = u - sx, dy = v - sy;
                double d2 = dx * dx + dy * dy;
                if (d2 < best) { best = d2; qx = sx; qy = sy; qt = st; hit = i; }
            }
            best = Math.Sqrt(best);
            double g = (hit + qt) / TailSamples;
            double radius = 0.42 - 0.22 * g;
            double cov = Cov(best - radius);
            if (cov <= 0) return;
            double nx, ny, nz;
            Normal(u - qx, v - qy, radius, out nx, out ny, out nz);
            double k = 0.96 + 0.06 * g;
            double lr, lg, lb;
            Lit(FurR * k, FurG * k, FurB * k, nx, ny, nz, 0.28 + 0.04 * g, 16.0 + 4.0 * g, out lr, out lg, out lb);
            Over(lr, lg, lb, cov);
            // Tuft highlight on the curl.
            {
                double dx = u - tailU[TailSamples], dy = v - tailV[TailSamples];
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
