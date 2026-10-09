using System;

namespace IdleWalker.Rendering
{
    /// <summary>
    /// What the figure painters share. A figure is a handful of solids -
    /// capsules, spheres, tubes - and every pixel gets a signed distance to
    /// each (for an anti-aliased edge) and a surface normal (for lighting),
    /// the solids being composited back to front into the pixel being
    /// shaded. Here are the compositing, the lighting, the distance
    /// functions, the sorting of limbs against the body, and the things
    /// that float about a figure (sweat, notes, Zzz, "!", hearts), which
    /// each painter places where its own head and mouth are.
    /// </summary>
    abstract class SolidPainter
    {
        // Light direction (towards the light), screen terms: upper left, in front.
        protected const double Lx = -0.45, Ly = 0.72, Lz = 0.53;
        // Half vector between the light and the viewer, for highlights.
        protected const double Hx = -0.257, Hy = 0.411, Hz = 0.874;

        protected const double Deg = Math.PI / 180;

        protected readonly double flip;   // -1 when facing left, so the light stays on the screen's left
        protected readonly double px;     // one pixel in units, for anti-aliasing

        // Depth of the body's surface at the pixel being shaded, and how far
        // in front of it a limb has to be to count as in front: a soft band
        // rather than a knife edge, so the join does not shimmer.
        protected double bodyZ;
        readonly double depthBlend;
        readonly double depthRadius;      // how far forward a limb can be: beyond this it is fully lit

        // Limb pieces that turned out to be in front of the body at this
        // pixel, held back until the face has been painted. Room for both
        // arms' every piece (sleeve, patch, cuff, finger, hand) at one pixel.
        readonly double[] frontR = new double[12], frontG = new double[12], frontB = new double[12], frontCov = new double[12];
        int frontCount;

        // The pixel being shaded, premultiplied.
        protected double ca, cr, cg, cb;

        protected SolidPainter(bool faceLeft, double px, double depthRadius)
        {
            flip = faceLeft ? -1 : 1;
            this.px = px;
            this.depthRadius = depthRadius;
            depthBlend = 2.5 * px;
        }

        // ---- The pixel ---------------------------------------------------------------

        /// <summary>Starts a pixel: nothing painted yet, no limb pieces held back.</summary>
        protected void Begin()
        {
            ca = cr = cg = cb = 0;
            frontCount = 0;
        }

        /// <summary>Lays on the limb pieces that were in front of the body.</summary>
        protected void LayFront()
        {
            for (int i = 0; i < frontCount; i++) Over(frontR[i], frontG[i], frontB[i], frontCov[i]);
        }

        /// <summary>The finished pixel as premultiplied ARGB, 0 if nothing was painted.</summary>
        protected int Finish()
        {
            if (ca <= 0.003) return 0;
            if (ca > 1) ca = 1;
            int a = (int)(ca * 255 + 0.5);
            int r = (int)(Clamp(cr) * 255 + 0.5);
            int g = (int)(Clamp(cg) * 255 + 0.5);
            int b = (int)(Clamp(cb) * 255 + 0.5);
            return (a << 24) | (r << 16) | (g << 8) | b;
        }

        // ---- Compositing and lighting ---------------------------------------------

        protected double Cov(double d)
        {
            double c = 0.5 - d / px;
            return c < 0 ? 0 : c > 1 ? 1 : c;
        }

        protected static double Clamp(double x)
        {
            return x < 0 ? 0 : x > 1 ? 1 : x;
        }

        protected void Over(double r, double g, double b, double cov)
        {
            if (cov <= 0) return;
            double keep = 1 - cov;
            cr = r * cov + cr * keep;
            cg = g * cov + cg * keep;
            cb = b * cov + cb * keep;
            ca = cov + ca * keep;
        }

        /// <summary>Lights an albedo with a normal (nx right, ny up, nz towards the viewer).</summary>
        protected void Lit(double r, double g, double b, double nx, double ny, double nz, double spec, double shine,
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
        protected static void Normal(double dx, double dy, double radius, out double nx, out double ny, out double nz)
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
        protected static void Turn(ref double nx, ref double nz, double a)
        {
            double c = Math.Cos(a), s = Math.Sin(a);
            double x = nx * c + nz * s;
            nz = -nx * s + nz * c;
            nx = x;
        }

        protected static void Mix(ref double r, ref double g, ref double b, ref double spec, ref double shine,
                                  double mr, double mg, double mb, double mspec, double mshine, double w)
        {
            if (w <= 0) return;
            r += (mr - r) * w;
            g += (mg - g) * w;
            b += (mb - b) * w;
            spec += (mspec - spec) * w;
            shine += (mshine - shine) * w;
        }

        // ---- Distance fields ----------------------------------------------------------

        protected static double Seg(double x, double y, double ax, double ay, double bx, double by,
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

        protected static double RoundRect(double x, double y, double hw, double hh, double r)
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
        protected static double Squeezed(double dx, double dy, double k, double r)
        {
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 1e-6) return -r;
            double grad = Math.Sqrt(dx * dx / (k * k) + dy * dy) / dist;
            return (dist - r) / grad;
        }

        // ---- Solids -------------------------------------------------------------------

        /// <summary>A cylinder between two points, lit as a tube.</summary>
        protected void Tube(double x, double y, double ax, double ay, double bx, double by, double radius,
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

        /// <summary>
        /// A limb segment: a tube whose ends have a depth, sorted against the
        /// body pixel by pixel, and a shade darker the further back it is.
        /// </summary>
        protected void ArmTube(double x, double y, double ax, double ay, double az, double bx, double by, double bz, double radius,
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
        protected double Depth(double z)
        {
            double k = 0.89 + 0.11 * z / depthRadius;
            return k < 0.78 ? 0.78 : k > 1 ? 1 : k;
        }

        /// <summary>
        /// Composites a limb piece at depth z against the body: the part
        /// behind the body's surface now, the part in front later. Across
        /// the surface the two blend over a band a couple of pixels deep,
        /// which reads as the arm sinking into the body.
        /// </summary>
        protected void Sort(double r, double g, double b, double cov, double z)
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

        /// <summary>
        /// A drop of sweat beside the head at x, sliding down from y0 and
        /// fading as `slid` goes 0..1.
        /// </summary>
        protected void Sweat(double u, double v, double slid, double x, double y0)
        {
            double s = slid;
            double y = y0 - 2.6 * s;
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

        /// <summary>A music note drifting up and forward from the mouth at (mouthU, mouthV).</summary>
        protected void Note(double u, double v, double ph, double mouthU, double mouthV)
        {
            double x = mouthU + 2.2 + 1.2 * ph, y = mouthV + 1.0 + 5.0 * ph;
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

        /// <summary>
        /// Two "z"s floating up and away from (x0, y0) above the head, growing
        /// as they go; the second is half a drift behind the first.
        /// </summary>
        protected void Zzz(double u, double v, double ph, double x0, double y0)
        {
            if (v < y0 - 2.3 || u < x0 - 2.4 || u > x0 + 6.1) return;
            ZGlyph(u, v, ph, x0, y0);
            ZGlyph(u, v, ph >= 0.5 ? ph - 0.5 : ph + 0.5, x0, y0);
        }

        void ZGlyph(double u, double v, double ph, double x0, double y0)
        {
            double s = 1.0 + 0.6 * ph;
            double x = x0 + 2.4 * ph, y = y0 + 3.2 * ph;
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

        /// <summary>The "!" overhead: a dot at dotV under a bar from barBottom to barTop; nothing below floorV.</summary>
        protected void Bubble(double u, double v, double floorV, double dotV, double barBottom, double barTop)
        {
            if (v < floorV || Math.Abs(u) > 1.5) return;
            double qx, qy, t;
            double bar = Seg(u, v, 0, barBottom, 0, barTop, out qx, out qy, out t) - (0.78 - 0.18 * (1 - t));
            double dot = Math.Sqrt(u * u + (v - dotV) * (v - dotV)) - 0.75;
            double d = Math.Min(bar, dot);
            Over(0.12, 0.10, 0.14, Cov(d - 0.3) * 0.85);
            Over(0.98, 0.98, 0.98, Cov(d));
        }

        /// <summary>
        /// A small pink heart drifting up and forward from the mouth at
        /// (mouthU, mouthV), like the note: two round lobes over a point.
        /// </summary>
        protected void Heart(double u, double v, double ph, double mouthU, double mouthV)
        {
            double x = mouthU + 2.4 + 1.6 * ph, y = mouthV + 1.2 + 5.0 * ph;
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
