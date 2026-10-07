// IdleWalker - when the mouse cursor rests for a couple of seconds, a little
// pixel-art character peeks over the bottom edge of the screen, hops out and
// strolls along the bottom of every monitor. Moving the mouse startles it and
// it dives back down.
//
// Build with build.cmd (uses the C# compiler that ships with Windows).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace IdleWalker
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "IdleWalker.SingleInstance", out createdNew))
            {
                if (!createdNew) return;

                // Must happen before any window exists so that monitor
                // coordinates are real pixels on every display.
                Native.EnablePerMonitorDpi();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (WalkerContext context = new WalkerContext())
                    Application.Run(context);
            }
        }
    }

    /// <summary>
    /// The character's pixel art, split into parts so that the head, arms,
    /// legs and scarf can be posed independently.
    /// </summary>
    static class Art
    {
        public const int Columns = 16;
        public const int Rows = 18; // the soles of the boots are the bottom of row 17

        /// <summary>A block of art rows; Top is the row its first line sits on.</summary>
        public sealed class Part
        {
            public readonly int Top;
            public readonly string[] Lines;

            public Part(int top, params string[] lines)
            {
                Top = top;
                Lines = lines;
            }
        }

        // Everything is drawn facing right. '.' is transparent.

        static Part Head(string eyes, string lids, string cheeks, string chin)
        {
            return new Part(0,
                "......YY........",
                ".....YYYY.......",
                "....TTTTTTT.....",
                "...TTTTTTTTT....",
                "..TTTTTTTTTTT...",
                "..tttttttttttt..",
                eyes, lids, cheeks, chin);
        }

        public static readonly Part HeadNormal = Head(
            "..SSSSSSKSSKS...",
            "..SSSSSSKSSKS...",
            "..SSSSPPSSSSSS..",
            "...SSSSSSSSSS...");

        public static readonly Part HeadBlink = Head(
            "..SSSSSSSSSSS...",
            "..SSSSSSKSSKS...",
            "..SSSSPPSSSSSS..",
            "...SSSSSSSSSS...");

        public static readonly Part HeadSurprised = Head(
            "..SSSSSSKSSKS...",
            "..SSSSSSKSSKS...",
            "..SSSSPPSSSSSS..",
            "...SSSSSSSKSS...");

        // Scarf plus jacket, with the near arm in different positions.
        static Part Torso(string a, string b, string c)
        {
            return new Part(10, "..OOOOOOOOOOO...", a, b, c);
        }

        public static readonly Part ArmMid = Torso(
            "...NNNNnnNNN....",
            "...NNNNnnNNN....",
            "...NNNNSSNNN....");

        public static readonly Part ArmForward = Torso(
            "...NNNNnnNNN....",
            "...NNNNNnnNN....",
            "...NNNNNNSSN....");

        public static readonly Part ArmBack = Torso(
            "...NNNNnnNNN....",
            "...NNNnnNNNN....",
            "...NNSSNNNNN....");

        public static readonly Part TorsoPlain = Torso(
            "...NNNNNNNNN....",
            "...NNNNNNNNN....",
            "...NNNNNNNNN....");

        // A raised arm, drawn over TorsoPlain and the head.
        public static readonly Part WaveA = new Part(7,
            "..............SS",
            "...............S",
            ".............nn.",
            "............nn..",
            "..........nnn...");

        public static readonly Part WaveB = new Part(6,
            "..............SS",
            "..............SS",
            "..............n.",
            ".............nn.",
            "............nn..",
            "..........nnn...");

        // The first hip row normally hides under the jacket; it fills the gap
        // when the upper body bobs up.
        static Part Legs(string a, string b, string c)
        {
            return new Part(13, "....GGGGGGG.....", "....GGGGGGG.....", a, b, c);
        }

        public static readonly Part LegsStand = Legs(
            "....GGG.ggg.....",
            "....GGG.ggg.....",
            "....BBBBbbbb....");

        public static readonly Part LegsJump = Legs(
            "...GGGG.gggg....",
            "..BBBB...ggg....",
            ".........bbbb...");

        // Six-frame walk: contact, recoil, passing - then the same again with
        // the other leg leading. G/B is the near leg, g/b the far one.
        public static readonly Part[] WalkLegs =
        {
            Legs("...GGGG.gggg....",
                 "..GGG.....ggg...",
                 "..BBBB....bbbb.."),
            Legs("...GGG.ggg......",
                 "..BBB..ggg......",
                 ".......bbbb....."),
            Legs(".....ggGGG......",
                 ".....ggBBBB.....",
                 ".....bbbb......."),
            Legs("...gggg.GGGG....",
                 "..ggg.....GGG...",
                 "..bbbb....BBBB.."),
            Legs("...ggg.GGG......",
                 "..bbb..GGG......",
                 ".......BBBB....."),
            Legs(".....GGggg......",
                 ".....GGbbbb.....",
                 ".....BBBB......."),
        };

        // The near arm swings opposite to the near leg.
        public static readonly Part[] WalkArms = { ArmForward, ArmForward, ArmMid, ArmBack, ArmBack, ArmMid };

        // Upper-body bob per walk frame, in small steps (positive is down).
        public static readonly int[] WalkBob = { 0, 1, -1, 0, 1, -1 };

        // The loose end of the scarf.
        public static readonly Part TailDown = new Part(11, "..O.............", "..O.............");
        public static readonly Part TailA = new Part(11, ".OO.............");
        public static readonly Part TailB = new Part(10, "OO..............");
        public static readonly Part TailUp = new Part(9, "OO..............", ".O..............");

        // "!" shown above the head when startled.
        public static readonly Part Bubble = new Part(-7,
            ".......WW.......",
            ".......WW.......",
            ".......WW.......",
            "................",
            ".......WW.......");

        public static readonly int Outline = Rgb(0x1E1826);

        static int Rgb(int rgb)
        {
            return unchecked((int)0xFF000000) | rgb;
        }

        public static int ColorOf(char c)
        {
            switch (c)
            {
                case 'Y': return Rgb(0xFFD23F); // pom-pom
                case 'T': return Rgb(0x1FA39A); // beanie
                case 't': return Rgb(0x157A74); // beanie brim
                case 'S': return Rgb(0xF6C9A0); // skin
                case 'P': return Rgb(0xEFA08A); // cheek
                case 'K': return Rgb(0x2B2233); // eyes
                case 'O': return Rgb(0xF2762E); // scarf
                case 'N': return Rgb(0x34508F); // jacket
                case 'n': return Rgb(0x263A6B); // sleeve
                case 'G': return Rgb(0x6B6F82); // near leg
                case 'g': return Rgb(0x4E5163); // far leg
                case 'B': return Rgb(0x6B4226); // near boot
                case 'b': return Rgb(0x4F301B); // far boot
                case 'W': return Rgb(0xFFFFFF); // "!"
                default: return 0;
            }
        }
    }

    /// <summary>One frame's worth of body positions.</summary>
    struct Pose
    {
        public Art.Part Legs, Torso, Tail, Head, Overlay;
        public bool HeadTurned; // head looks the opposite way to the body
        public bool Bubble;
        public int BodyDy;      // upper-body offset in pixels (positive is down)
        public double StretchX, StretchY;

        public static Pose Standing()
        {
            Pose pose = new Pose();
            pose.Legs = Art.LegsStand;
            pose.Torso = Art.ArmMid;
            pose.Tail = Art.TailDown;
            pose.Head = Art.HeadNormal;
            pose.StretchX = 1;
            pose.StretchY = 1;
            return pose;
        }

        /// <summary>
        /// Draws the pose with its soles resting on soleY, centred on centreX.
        /// </summary>
        public void Draw(Surface surface, bool faceLeft, double centreX, double soleY, int scale)
        {
            double w = scale * StretchX, h = scale * StretchY;
            surface.DrawPart(Tail, faceLeft, centreX, soleY, w, h, BodyDy);
            surface.DrawPart(Legs, faceLeft, centreX, soleY, w, h, 0);
            surface.DrawPart(Torso, faceLeft, centreX, soleY, w, h, BodyDy);
            surface.DrawPart(Head, faceLeft != HeadTurned, centreX, soleY, w, h, BodyDy);
            if (Overlay != null) surface.DrawPart(Overlay, faceLeft, centreX, soleY, w, h, BodyDy);
            if (Bubble) surface.DrawPart(Art.Bubble, false, centreX, soleY, w, h, BodyDy);
        }
    }

    /// <summary>
    /// A small software canvas. The character is drawn into its own layer so
    /// it can be given an outline, then laid over whatever is behind it.
    /// Pixels are premultiplied ARGB, ready for a layered window.
    /// </summary>
    sealed class Surface
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int[] Pixels = new int[0];

        int[] layer = new int[0];
        bool[] rim = new bool[0];

        public void Reset(int width, int height)
        {
            if (width != Width || height != Height)
            {
                Width = width;
                Height = height;
                Pixels = new int[width * height];
                layer = new int[width * height];
                rim = new bool[width * height];
            }
            else
            {
                Array.Clear(Pixels, 0, Pixels.Length);
                Array.Clear(layer, 0, layer.Length);
            }
        }

        void Fill(int[] target, int x0, int y0, int x1, int y1, int color)
        {
            if (x0 < 0) x0 = 0;
            if (y0 < 0) y0 = 0;
            if (x1 > Width) x1 = Width;
            if (y1 > Height) y1 = Height;
            for (int y = y0; y < y1; y++)
            {
                int row = y * Width;
                for (int x = x0; x < x1; x++) target[row + x] = color;
            }
        }

        /// <summary>A translucent square of dust resting on (x, y).</summary>
        public void Puff(int x, int y, int size, int alpha)
        {
            if (alpha <= 0) return;
            int color = (alpha << 24) | ((214 * alpha / 255) << 16) | ((204 * alpha / 255) << 8) | (190 * alpha / 255);
            Fill(Pixels, x - size / 2, y - size, x - size / 2 + size, y, color);
        }

        static int Round(double value)
        {
            return (int)Math.Floor(value + 0.5);
        }

        public void DrawPart(Art.Part part, bool flip, double centreX, double soleY,
                             double cellWidth, double cellHeight, int dy)
        {
            for (int i = 0; i < part.Lines.Length; i++)
            {
                string line = part.Lines[i];
                int row = part.Top + i;
                int y0 = Round(soleY - (Art.Rows - row) * cellHeight) + dy;
                int y1 = Round(soleY - (Art.Rows - row - 1) * cellHeight) + dy;
                for (int col = 0; col < Art.Columns; col++)
                {
                    int color = Art.ColorOf(line[col]);
                    if (color == 0) continue;
                    int c = flip ? Art.Columns - 1 - col : col;
                    int x0 = Round(centreX + (c - Art.Columns / 2) * cellWidth);
                    int x1 = Round(centreX + (c + 1 - Art.Columns / 2) * cellWidth);
                    Fill(layer, x0, y0, x1, y1, color);
                }
            }
        }

        /// <summary>
        /// Outlines the character layer (so it reads on any background) and
        /// lays it over the canvas.
        /// </summary>
        public void CommitCharacter(int thickness)
        {
            Array.Clear(rim, 0, rim.Length);

            // A transparent pixel is rim if a character pixel lies within
            // `thickness` pixels of it along its row or its column.
            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                int gap = thickness;
                for (int x = 0; x < Width; x++)
                {
                    if (layer[row + x] != 0) gap = 0;
                    else if (gap < thickness) { gap++; rim[row + x] = true; }
                }
                gap = thickness;
                for (int x = Width - 1; x >= 0; x--)
                {
                    if (layer[row + x] != 0) gap = 0;
                    else if (gap < thickness) { gap++; rim[row + x] = true; }
                }
            }
            for (int x = 0; x < Width; x++)
            {
                int gap = thickness;
                for (int y = 0; y < Height; y++)
                {
                    int i = y * Width + x;
                    if (layer[i] != 0) gap = 0;
                    else if (gap < thickness) { gap++; rim[i] = true; }
                }
                gap = thickness;
                for (int y = Height - 1; y >= 0; y--)
                {
                    int i = y * Width + x;
                    if (layer[i] != 0) gap = 0;
                    else if (gap < thickness) { gap++; rim[i] = true; }
                }
            }

            for (int i = 0; i < layer.Length; i++)
            {
                if (layer[i] != 0) Pixels[i] = layer[i];
                else if (rim[i]) Pixels[i] = Art.Outline;
            }
        }
    }

    /// <summary>
    /// A borderless, always-on-top, click-through window that shows a Surface
    /// with per-pixel transparency.
    /// </summary>
    sealed class SpriteWindow : NativeWindow, IDisposable
    {
        IntPtr memoryDc;
        IntPtr dib;
        IntPtr dibBits;
        IntPtr previousBitmap;
        int dibWidth, dibHeight;
        bool visible;

        public SpriteWindow()
        {
            CreateParams cp = new CreateParams();
            cp.Caption = "IdleWalker";
            cp.Style = unchecked((int)Native.WS_POPUP);
            cp.ExStyle = Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW |
                         Native.WS_EX_TOPMOST | Native.WS_EX_NOACTIVATE;
            CreateHandle(cp);
        }

        public void Present(Surface surface, int x, int y)
        {
            EnsureBitmap(surface.Width, surface.Height);
            if (dib == IntPtr.Zero) return;
            Marshal.Copy(surface.Pixels, 0, dibBits, surface.Pixels.Length);

            Native.POINT destination = new Native.POINT(x, y);
            Native.POINT source = new Native.POINT(0, 0);
            Native.SIZE size = new Native.SIZE(surface.Width, surface.Height);
            Native.BLENDFUNCTION blend = new Native.BLENDFUNCTION();
            blend.SourceConstantAlpha = 255;
            blend.AlphaFormat = Native.AC_SRC_ALPHA;
            Native.UpdateLayeredWindow(Handle, IntPtr.Zero, ref destination, ref size, memoryDc,
                                       ref source, 0, ref blend, Native.ULW_ALPHA);

            if (!visible)
            {
                Native.ShowWindow(Handle, Native.SW_SHOWNOACTIVATE);
                visible = true;
                StayOnTop();
            }
        }

        void EnsureBitmap(int width, int height)
        {
            if (width == dibWidth && height == dibHeight && dib != IntPtr.Zero) return;
            ReleaseBitmap();
            if (memoryDc == IntPtr.Zero) memoryDc = Native.CreateCompatibleDC(IntPtr.Zero);

            Native.BITMAPINFOHEADER header = new Native.BITMAPINFOHEADER();
            header.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            header.biWidth = width;
            header.biHeight = -height; // top-down
            header.biPlanes = 1;
            header.biBitCount = 32;
            dib = Native.CreateDIBSection(IntPtr.Zero, ref header, 0, out dibBits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero) return;
            previousBitmap = Native.SelectObject(memoryDc, dib);
            dibWidth = width;
            dibHeight = height;
        }

        void ReleaseBitmap()
        {
            if (dib == IntPtr.Zero) return;
            Native.SelectObject(memoryDc, previousBitmap);
            Native.DeleteObject(dib);
            dib = IntPtr.Zero;
        }

        public void StayOnTop()
        {
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        public void Hide()
        {
            if (!visible) return;
            Native.ShowWindow(Handle, Native.SW_HIDE);
            visible = false;
        }

        public void Dispose()
        {
            ReleaseBitmap();
            if (memoryDc != IntPtr.Zero)
            {
                Native.DeleteDC(memoryDc);
                memoryDc = IntPtr.Zero;
            }
            DestroyHandle();
        }
    }

    /// <summary>Watches the cursor and drives the character.</summary>
    sealed class WalkerContext : ApplicationContext
    {
        // ---- Tweak these to taste -------------------------------------------
        // Speeds are in pixels per second at 100% display scaling.
        const double IdleSeconds = 2.0;   // cursor rest time before the walker appears
        const int MoveTolerance = 2;      // pixels of sensor jitter that don't count as movement
        const int BasePixelSize = 3;      // size of one sprite cell at 100% display scaling
        const double WalkSpeed = 90;
        const double Acceleration = 420;  // easing into a walk
        const double Braking = 520;       // easing to a stop
        const double Gravity = 1100;
        const double HopSpeed = 320;      // take-off speed of the occasional hop
        const double StartleSpeed = 210;  // the little jump when the mouse moves
        const double ExitGravity = 2200;  // how briskly it dives away afterwards
        // ----------------------------------------------------------------------

        const double StrideCells = 16;    // distance covered by one full walk cycle
        const int CanvasColumns = 40;
        const int CanvasRows = 48;
        const int RowsBelowFeet = 18;     // canvas kept under the feet during high jumps
        const int HalfBodyCells = 9;
        const int HiddenDepthCells = 20;  // feet this far underground hides everything
        const int PeekDepthCells = 10;    // feet this far underground shows the eyes
        const int WatchIntervalMs = 100;

        enum Mode { Hidden, PeekRise, PeekLook, PeekDuck, Air, Walk, Crouch, Turn, Look, Wave, Rest, Exit }

        sealed class Puff
        {
            public double X, Y, Vx, Vy, Life, MaxLife;
        }

        readonly SpriteWindow window = new SpriteWindow();
        readonly Surface surface = new Surface();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Random rng = new Random();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly ToolStripMenuItem pauseItem = new ToolStripMenuItem("Pause");
        readonly List<Puff> puffs = new List<Puff>();
        readonly SynchronizationContext sync;
        readonly SendOrPostCallback tickCallback;
        readonly Thread pump;
        IntPtr trayIconHandle;
        volatile bool animating;
        volatile bool stopping;
        int tickPending;

        Native.POINT anchor;
        double lastMoveTime;
        double lastTick;
        double nextTopmost;

        Mode mode = Mode.Hidden;
        double modeTime;
        double time;

        int scale = BasePixelSize;
        double sizeFactor = 1;      // scale relative to 100% display scaling
        int worldLeft, worldRight;  // outer edges of the whole desktop

        double cx;                  // centre of the character, screen pixels
        double feetY;               // where its feet are
        double vx, vy;
        double clipY;               // ground line; nothing is drawn below it
        int facing = 1;
        bool headTurned;

        double walkPhase;           // in walk cycles; advanced by distance so feet don't slide
        double squash;              // 1 right after a landing, easing back to 0
        bool braking;
        Mode afterBrake = Mode.Walk;
        Mode afterTurn = Mode.Walk;
        double pendingJump, pendingAirSpeed;
        double landPause;
        double restDuration;
        double idleTimer, hopTimer;
        double blinkTimer, blinkLeft;
        bool startled;

        public WalkerContext()
        {
            Native.GetCursorPos(out anchor);

            pauseItem.CheckOnClick = true;
            menu.Items.Add(pauseItem);
            menu.Items.Add("Exit", null, delegate { ExitThread(); });

            using (Bitmap icon = new Bitmap(40, 40))
            {
                Surface small = new Surface();
                small.Reset(40, 40);
                Pose.Standing().Draw(small, false, 20, 38, 2);
                small.CommitCharacter(2);
                for (int y = 0; y < 40; y++)
                    for (int x = 0; x < 40; x++)
                        icon.SetPixel(x, y, Color.FromArgb(small.Pixels[y * 40 + x]));
                trayIconHandle = icon.GetHicon();
            }
            tray.Icon = Icon.FromHandle(trayIconHandle);
            tray.Text = "Idle Walker";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;

            if (SynchronizationContext.Current == null)
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            sync = SynchronizationContext.Current;
            tickCallback = OnTick;

            pump = new Thread(PumpLoop);
            pump.IsBackground = true;
            pump.Name = "IdleWalker frame pump";
            pump.Start();
        }

        // Paces the animation: one tick per screen refresh while the walker is
        // out, a lazy poll of the cursor while it is hidden.
        void PumpLoop()
        {
            Stopwatch frame = Stopwatch.StartNew();
            while (!stopping)
            {
                if (animating)
                {
                    frame.Restart();
                    if (Native.DwmFlush() != 0 || frame.ElapsedMilliseconds < 3) Thread.Sleep(12);
                }
                else
                {
                    Thread.Sleep(WatchIntervalMs);
                }

                if (Interlocked.Exchange(ref tickPending, 1) != 0) continue;
                try
                {
                    sync.Post(tickCallback, null);
                }
                catch (InvalidOperationException)
                {
                    return; // the UI thread has gone away
                }
            }
        }

        void OnTick(object unused)
        {
            Interlocked.Exchange(ref tickPending, 0);
            if (stopping) return;

            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(now - lastTick, 0.05);
            lastTick = now;

            bool moved = false;
            Native.POINT cursor;
            if (Native.GetCursorPos(out cursor))
            {
                moved = Math.Abs(cursor.X - anchor.X) > MoveTolerance ||
                        Math.Abs(cursor.Y - anchor.Y) > MoveTolerance;
                if (moved)
                {
                    anchor = cursor;
                    lastMoveTime = now;
                }
            }

            bool paused = pauseItem.Checked;
            if (mode == Mode.Hidden)
            {
                if (paused || now - lastMoveTime < IdleSeconds) return;
                Spawn();
                animating = true;
            }
            else if ((moved || paused) && mode != Mode.Exit)
            {
                BeginExit();
            }

            Step(dt);
            if (mode == Mode.Hidden)
            {
                window.Hide();
                animating = false;
                return;
            }

            Render();
            if (now >= nextTopmost)
            {
                window.StayOnTop();
                nextTopmost = now + 1;
            }
        }

        // ---- World ------------------------------------------------------------

        // The monitor whose horizontal span contains x. When monitors are
        // stacked vertically the lowest one wins; in a gap, the nearest one.
        static Screen ScreenUnder(int x)
        {
            Screen best = null;
            int bestDistance = int.MaxValue;
            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle b = screen.Bounds;
                int distance = x < b.Left ? b.Left - x : (x >= b.Right ? x - b.Right + 1 : 0);
                if (distance < bestDistance ||
                    (distance == bestDistance && b.Bottom > best.Bounds.Bottom))
                {
                    best = screen;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // The surface to stand on at x: the top of the taskbar, or the bottom
        // of the screen where there is none.
        static double GroundAt(double x)
        {
            return ScreenUnder((int)Math.Floor(x)).WorkingArea.Bottom;
        }

        // Sprite cell size for a monitor, following its display scaling.
        static int ScaleFor(Screen screen)
        {
            Rectangle b = screen.Bounds;
            Native.POINT centre = new Native.POINT(b.Left + b.Width / 2, b.Top + b.Height / 2);
            IntPtr monitor = Native.MonitorFromPoint(centre, Native.MONITOR_DEFAULTTONEAREST);
            uint dpiX, dpiY;
            if (Native.GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) != 0) dpiX = 96;
            return Math.Max(2, (int)Math.Round(BasePixelSize * dpiX / 96.0));
        }

        void UpdateWorld()
        {
            worldLeft = int.MaxValue;
            worldRight = int.MinValue;
            foreach (Screen screen in Screen.AllScreens)
            {
                worldLeft = Math.Min(worldLeft, screen.Bounds.Left);
                worldRight = Math.Max(worldRight, screen.Bounds.Right);
            }

            int newScale = ScaleFor(ScreenUnder((int)Math.Floor(cx)));
            if (newScale != scale)
            {
                double ratio = (double)newScale / scale;
                vx *= ratio;
                vy *= ratio;
                scale = newScale;
            }
            sizeFactor = scale / (double)BasePixelSize;
        }

        // Moves horizontally unless that would mean walking into the side of
        // higher ground (a neighbouring monitor whose bottom edge is higher).
        bool MoveX(double dx)
        {
            double half = HalfBodyCells * scale;
            double next = Math.Max(worldLeft + half, Math.Min(worldRight - half, cx + dx));
            if (GroundAt(next) < feetY - scale) return false;
            cx = next;
            return true;
        }

        // Looks for a step up ahead, e.g. the edge of a monitor that sits higher.
        bool WallAhead(out double height, out double distance)
        {
            height = 0;
            distance = double.MaxValue;
            double reach = 400 * sizeFactor;
            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle b = screen.Bounds;
                for (int i = 0; i < 2; i++)
                {
                    int edge = i == 0 ? b.Left : b.Right;
                    double d = (edge - cx) * facing;
                    if (d <= 0 || d > reach || d >= distance) continue;
                    double beyond = GroundAt(facing > 0 ? edge : edge - 1);
                    if (beyond < feetY - scale)
                    {
                        distance = d;
                        height = feetY - beyond;
                    }
                }
            }
            return distance != double.MaxValue;
        }

        // Keeps the feet on the ground; returns false if there is none (it
        // has walked off the edge of a higher monitor) and a fall has begun.
        bool StayOnGround()
        {
            double ground = GroundAt(cx);
            clipY = ground;
            if (ground > feetY + 1.5)
            {
                vy = 0;
                Enter(Mode.Air);
                return false;
            }
            feetY = ground;
            return true;
        }

        // ---- Behaviour --------------------------------------------------------

        double Between(double low, double high)
        {
            return low + rng.NextDouble() * (high - low);
        }

        static double Approach(double value, double target, double step)
        {
            return value < target ? Math.Min(target, value + step) : Math.Max(target, value - step);
        }

        void Enter(Mode next)
        {
            mode = next;
            modeTime = 0;
            headTurned = false;
            braking = false;
        }

        void Brake(Mode next)
        {
            braking = true;
            afterBrake = next;
        }

        // Come up on the monitor the cursor is resting on, at a random spot.
        void Spawn()
        {
            Rectangle bounds = Screen.FromPoint(new Point(anchor.X, anchor.Y)).Bounds;
            double margin = Math.Min(bounds.Width / 4.0, 120);
            cx = bounds.Left + margin + rng.NextDouble() * (bounds.Width - 2 * margin);
            UpdateWorld();

            facing = rng.Next(2) == 0 ? -1 : 1;
            clipY = GroundAt(cx);
            feetY = clipY + HiddenDepthCells * scale;
            vx = vy = 0;
            squash = 0;
            walkPhase = 0;
            landPause = 0;
            startled = false;
            afterTurn = Mode.Walk;
            idleTimer = Between(4, 9);
            hopTimer = Between(3, 8);
            blinkTimer = Between(1, 3);
            puffs.Clear();
            Enter(Mode.PeekRise);
        }

        // The mouse moved: jump in fright, then drop out of sight.
        void BeginExit()
        {
            bool underground = mode == Mode.PeekRise || mode == Mode.PeekLook || mode == Mode.PeekDuck;
            startled = !underground;
            if (underground) vy = 0;
            else if (mode != Mode.Air) vy = -StartleSpeed * sizeFactor;
            vx = 0;
            Enter(Mode.Exit);
        }

        void Step(double dt)
        {
            time += dt;
            modeTime += dt;
            squash = Math.Max(0, squash - dt / 0.18);
            UpdateWorld();

            blinkTimer -= dt;
            blinkLeft -= dt;
            if (blinkTimer <= 0)
            {
                blinkLeft = 0.12;
                blinkTimer = Between(1.8, 4.5);
            }

            switch (mode)
            {
                case Mode.PeekRise:
                {
                    // Ease the top of the head up over the edge.
                    double t = Math.Min(1, modeTime / 0.4);
                    double eased = 1 - Math.Pow(1 - t, 3);
                    feetY = clipY + (HiddenDepthCells + (PeekDepthCells - HiddenDepthCells) * eased) * scale;
                    if (t >= 1) Enter(Mode.PeekLook);
                    break;
                }

                case Mode.PeekLook:
                    // Check that the coast is clear.
                    headTurned = modeTime > 0.3 && modeTime < 0.8;
                    if (modeTime >= 1.1) Enter(Mode.PeekDuck);
                    break;

                case Mode.PeekDuck:
                {
                    // Dip down, then spring out.
                    double t = Math.Min(1, modeTime / 0.12);
                    feetY = clipY + (PeekDepthCells + 3 * t) * scale;
                    if (t >= 1)
                    {
                        double rise = feetY - clipY + 10 * scale;
                        vy = -Math.Sqrt(2 * Gravity * sizeFactor * rise);
                        vx = facing * WalkSpeed * sizeFactor * 0.6;
                        landPause = 0.5;
                        EmitDust(cx, clipY, 6, 1);
                        Enter(Mode.Air);
                    }
                    break;
                }

                case Mode.Air:
                {
                    vy += Gravity * sizeFactor * dt;
                    feetY += vy * dt;
                    MoveX(vx * dt);
                    double ground = GroundAt(cx);
                    clipY = ground;
                    if (vy > 0 && feetY >= ground) Land(ground);
                    break;
                }

                case Mode.Walk:
                    StepWalk(dt);
                    break;

                case Mode.Crouch:
                    // A beat of anticipation before a jump.
                    if (!StayOnGround()) break;
                    MoveX(vx * dt);
                    if (modeTime >= 0.08)
                    {
                        vy = -pendingJump;
                        vx = pendingAirSpeed;
                        Enter(Mode.Air);
                    }
                    break;

                case Mode.Turn:
                    // The head turns first, the body follows.
                    if (!StayOnGround()) break;
                    if (modeTime < 0.16)
                    {
                        headTurned = true;
                    }
                    else if (headTurned)
                    {
                        headTurned = false;
                        facing = -facing;
                    }
                    if (modeTime >= 0.28)
                    {
                        Mode next = afterTurn;
                        afterTurn = Mode.Walk;
                        Enter(next);
                    }
                    break;

                case Mode.Look:
                    if (!StayOnGround()) break;
                    headTurned = modeTime > 0.35 && modeTime < 1.1;
                    if (modeTime >= 1.5) Enter(rng.NextDouble() < 0.3 ? Mode.Turn : Mode.Walk);
                    break;

                case Mode.Wave:
                    if (!StayOnGround()) break;
                    if (modeTime >= 1.35) Enter(Mode.Walk);
                    break;

                case Mode.Rest:
                    if (!StayOnGround()) break;
                    if (modeTime >= restDuration) Enter(Mode.Walk);
                    break;

                case Mode.Exit:
                {
                    vy += ExitGravity * sizeFactor * dt;
                    feetY += vy * dt;
                    double tall = (HiddenDepthCells + (startled ? 8 : 0)) * scale * 1.2;
                    if (feetY - clipY >= tall)
                    {
                        puffs.Clear();
                        Enter(Mode.Hidden);
                    }
                    break;
                }
            }

            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                Puff puff = puffs[i];
                puff.Life -= dt;
                if (puff.Life <= 0)
                {
                    puffs.RemoveAt(i);
                    continue;
                }
                puff.X += puff.Vx * dt;
                puff.Y += puff.Vy * dt;
                double drag = Math.Max(0, 1 - 3 * dt);
                puff.Vx *= drag;
                puff.Vy *= drag;
            }
        }

        void StepWalk(double dt)
        {
            if (!StayOnGround()) return;

            double full = WalkSpeed * sizeFactor;
            double target = braking ? 0 : facing * full;
            vx = Approach(vx, target, (braking ? Braking : Acceleration) * sizeFactor * dt);
            bool blocked = !MoveX(vx * dt);
            walkPhase += Math.Abs(vx) * dt / (StrideCells * scale);

            if (braking)
            {
                if (Math.Abs(vx) < 1)
                {
                    vx = 0;
                    Enter(afterBrake);
                }
                return;
            }

            // Turn around at the outer edge of the first and last monitor,
            // slowing down in time to stop right at it.
            double half = HalfBodyCells * scale;
            double room = facing > 0 ? worldRight - half - cx : cx - (worldLeft + half);
            if (room <= vx * vx / (2 * Braking * sizeFactor) + 1)
            {
                if (Math.Abs(vx) > 0.7 * full) EmitDust(cx + facing * 4 * scale, feetY, 2, 0.6);
                afterTurn = Mode.Walk;
                Brake(Mode.Turn);
                return;
            }

            // A monitor with a higher bottom edge ahead: jump up onto it,
            // timing the take-off so the top of the arc is over the edge.
            double wallHeight, wallDistance;
            if (WallAhead(out wallHeight, out wallDistance))
            {
                double gravity = Gravity * sizeFactor;
                double launch = Math.Sqrt(2 * gravity * (wallHeight + 6 * scale));
                if (blocked || wallDistance <= full * launch / gravity)
                {
                    StartJump(launch, facing * full);
                    return;
                }
            }
            else if (blocked)
            {
                afterTurn = Mode.Walk;
                Brake(Mode.Turn);
                return;
            }

            idleTimer -= dt;
            hopTimer -= dt;
            if (idleTimer <= 0)
            {
                idleTimer = Between(5, 11);
                PickIdle();
            }
            else if (hopTimer <= 0)
            {
                hopTimer = Between(4, 9);
                if (Math.Abs(vx) > 0.8 * full) StartJump(HopSpeed * sizeFactor, vx);
            }
        }

        void StartJump(double speed, double airSpeed)
        {
            pendingJump = speed;
            pendingAirSpeed = airSpeed;
            Enter(Mode.Crouch);
        }

        void Land(double ground)
        {
            double impact = vy;
            feetY = ground;
            vy = 0;
            squash = Math.Max(0.35, Math.Min(1, impact / (HopSpeed * sizeFactor)));
            if (impact > 0.5 * HopSpeed * sizeFactor) EmitDust(cx, ground, 4, squash);

            if (landPause > 0)
            {
                vx = 0;
                restDuration = landPause;
                landPause = 0;
                Enter(Mode.Rest);
            }
            else
            {
                Enter(Mode.Walk);
            }
        }

        // Every so often: stop and look around, wave at the cursor, or rest.
        void PickIdle()
        {
            double roll = rng.NextDouble();
            if (roll < 0.4)
            {
                Brake(Mode.Look);
            }
            else if (roll < 0.75)
            {
                bool cursorBehind = (anchor.X - cx) * facing < -2 * HalfBodyCells * scale;
                if (cursorBehind)
                {
                    afterTurn = Mode.Wave;
                    Brake(Mode.Turn);
                }
                else
                {
                    Brake(Mode.Wave);
                }
            }
            else
            {
                restDuration = Between(0.8, 2.0);
                Brake(Mode.Rest);
            }
        }

        void EmitDust(double x, double y, int count, double strength)
        {
            for (int i = 0; i < count; i++)
            {
                double side = i % 2 == 0 ? -1 : 1;
                Puff puff = new Puff();
                puff.X = x + side * Between(2, 6) * scale;
                puff.Y = y - Between(0, 1) * scale;
                puff.Vx = side * Between(25, 70) * sizeFactor * strength;
                puff.Vy = -Between(8, 35) * sizeFactor;
                puff.Life = puff.MaxLife = Between(0.28, 0.5);
                puffs.Add(puff);
            }
        }

        // ---- Drawing ----------------------------------------------------------

        Pose CurrentPose()
        {
            Pose pose = Pose.Standing();
            int step = Math.Max(1, scale / BasePixelSize);
            bool breathe = false;

            switch (mode)
            {
                case Mode.Walk:
                    if (Math.Abs(vx) > 6 * sizeFactor)
                    {
                        int frame = (int)(walkPhase * 6) % 6;
                        pose.Legs = Art.WalkLegs[frame];
                        pose.Torso = Art.WalkArms[frame];
                        pose.Tail = frame < 3 ? Art.TailA : Art.TailB;
                        pose.BodyDy = Art.WalkBob[frame] * step;
                    }
                    break;

                case Mode.Crouch:
                    pose.StretchX = 1.12;
                    pose.StretchY = 0.84;
                    break;

                case Mode.Air:
                case Mode.Exit:
                {
                    bool rising = vy < 0;
                    pose.Legs = rising ? Art.LegsJump : Art.LegsStand;
                    pose.Torso = rising ? Art.ArmBack : Art.ArmForward;
                    pose.Tail = rising ? Art.TailDown : Art.TailUp;
                    double stretch = Math.Min(1, Math.Abs(vy) / (600 * sizeFactor)) * 0.1;
                    pose.StretchY = 1 + stretch;
                    pose.StretchX = 1 - stretch * 0.7;
                    if (mode == Mode.Exit)
                    {
                        pose.Head = Art.HeadSurprised;
                        pose.Bubble = startled;
                    }
                    break;
                }

                case Mode.Wave:
                    pose.Torso = Art.TorsoPlain;
                    pose.Overlay = (int)(modeTime / 0.15) % 2 == 0 ? Art.WaveA : Art.WaveB;
                    break;

                case Mode.Turn:
                case Mode.Look:
                case Mode.Rest:
                    breathe = true;
                    break;
            }

            if (breathe && Math.Sin(time * 2.6) > 0.4) pose.BodyDy = step;
            pose.HeadTurned = headTurned;
            pose.StretchY *= 1 - 0.22 * squash;
            pose.StretchX *= 1 + 0.16 * squash;
            if (blinkLeft > 0 && pose.Head == Art.HeadNormal) pose.Head = Art.HeadBlink;
            return pose;
        }

        static int Round(double value)
        {
            return (int)Math.Floor(value + 0.5);
        }

        void Render()
        {
            // The canvas rests on the ground line, so anything underground is
            // simply cut off. In a high jump it follows the character up.
            int width = CanvasColumns * scale;
            int height = CanvasRows * scale;
            int top = Round(Math.Min(clipY, feetY + RowsBelowFeet * scale)) - height;
            int left = Round(cx) - width / 2;

            surface.Reset(width, height);
            foreach (Puff puff in puffs)
            {
                double age = 1 - puff.Life / puff.MaxLife;
                int size = Math.Max(1, Round(scale * (0.7 + 0.7 * age)));
                surface.Puff(Round(puff.X) - left, Round(puff.Y) - top, size, (int)(190 * (1 - age)));
            }

            // The soles sit one outline-width above the ground.
            CurrentPose().Draw(surface, facing < 0, width / 2, Round(feetY) - top - scale, scale);
            surface.CommitCharacter(scale);
            window.Present(surface, left, top);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                stopping = true;
                tray.Visible = false;
                tray.Dispose();
                menu.Dispose();
                window.Dispose();
                if (trayIconHandle != IntPtr.Zero)
                {
                    Native.DestroyIcon(trayIconHandle);
                    trayIconHandle = IntPtr.Zero;
                }
            }
            base.Dispose(disposing);
        }
    }

    static class Native
    {
        public const uint WS_POPUP = 0x80000000;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const int SW_HIDE = 0;
        public const int SW_SHOWNOACTIVATE = 4;

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        public const int ULW_ALPHA = 2;
        public const byte AC_SRC_ALPHA = 1;
        public const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
            public POINT(int x, int y) { X = x; Y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int Width;
            public int Height;
            public SIZE(int width, int height) { Width = width; Height = height; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst,
            ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter,
            int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT point, uint flags);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hicon);

        [DllImport("user32.dll")]
        static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("dwmapi.dll")]
        public static extern int DwmFlush();

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER header, uint usage,
            out IntPtr bits, IntPtr section, uint offset);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr obj);

        public static void EnablePerMonitorDpi()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
                if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware();
            }
            catch (EntryPointNotFoundException)
            {
                SetProcessDPIAware();
            }
        }
    }
}
