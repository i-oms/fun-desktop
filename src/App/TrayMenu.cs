using System;
using System.Drawing;
using System.Windows.Forms;
using IdleWalker.Domain;
using IdleWalker.Platform;
using IdleWalker.Rendering;

namespace IdleWalker.App
{
    /// <summary>
    /// The notification-area icon and its right-click menu: the walkers (a
    /// master switch, then the Minions and Mr. Bean each with their own) and
    /// their settings, the hot corners and theirs, and the program itself.
    /// </summary>
    sealed class TrayMenu : IDisposable
    {
        readonly NotifyIcon icon = new NotifyIcon();
        readonly ContextMenuStrip menu = new ContextMenuStrip();

        // The walkers.
        readonly ToolStripMenuItem walkersItem = new ToolStripMenuItem("Walkers");
        readonly ToolStripMenuItem minionsItem = new ToolStripMenuItem("Minions");
        readonly ToolStripMenuItem countMenu = new ToolStripMenuItem("Minions");
        readonly ToolStripMenuItem idleMenu = new ToolStripMenuItem("Appear after");
        readonly ToolStripMenuItem dressMenu = new ToolStripMenuItem("Dress");
        readonly ToolStripMenuItem customItem = new ToolStripMenuItem("Custom...");
        readonly ToolStripMenuItem beanItem = new ToolStripMenuItem("Mr. Bean");
        int[] customPalette;         // the colour dialog's own swatches, kept between openings

        // The hot corners.
        static readonly string[] CornerNames = { "Top-left", "Top-right", "Bottom-left", "Bottom-right" };
        readonly ToolStripMenuItem cornersItem = new ToolStripMenuItem("Hot corners");
        readonly ToolStripMenuItem[] cornerMenus = new ToolStripMenuItem[CornerNames.Length];
        readonly ToolStripMenuItem delayMenu = new ToolStripMenuItem("Corner delay");

        readonly ToolStripMenuItem startupItem = new ToolStripMenuItem("Start with Windows");
        IntPtr iconHandle;
        int count;
        int idleSeconds;
        int dress;
        readonly CornerAction[] actions = new CornerAction[CornerNames.Length];
        int delayMs;

        public event EventHandler ExitRequested;
        public event EventHandler WalkersToggled;
        public event EventHandler MinionsToggled;
        public event EventHandler CountChanged;
        public event EventHandler IdleChanged;
        public event EventHandler DressChanged;
        public event EventHandler BeanToggled;
        public event EventHandler HotCornersToggled;
        public event Action<Corner> CornerChanged;
        public event EventHandler DelayChanged;
        public event EventHandler AboutRequested;

        /// <summary>True while the master switch is on: nobody comes out while it is off.</summary>
        public bool WalkersEnabled
        {
            get { return walkersItem.Checked; }
        }

        /// <summary>True while the Minions are switched on (independently of Mr. Bean).</summary>
        public bool MinionsEnabled
        {
            get { return minionsItem.Checked; }
        }

        /// <summary>How many Minions the user wants (1..Settings.MaxWalkers).</summary>
        public int Count
        {
            get { return count; }
        }

        /// <summary>How long the cursor must be still before they come out, in seconds.</summary>
        public int IdleSeconds
        {
            get { return idleSeconds; }
        }

        /// <summary>The overalls colour (0xRRGGBB), or Settings.RandomDress.</summary>
        public int Dress
        {
            get { return dress; }
        }

        /// <summary>True while Mr. Bean is switched on (independently of the Minions).</summary>
        public bool BeanEnabled
        {
            get { return beanItem.Checked; }
        }

        /// <summary>True while the hot corners are switched on.</summary>
        public bool HotCornersEnabled
        {
            get { return cornersItem.Checked; }
        }

        /// <summary>What a corner does.</summary>
        public CornerAction CornerActionFor(Corner corner)
        {
            return actions[(int)corner];
        }

        /// <summary>How long the cursor must rest in a corner before it fires, in ms.</summary>
        public int DelayMs
        {
            get { return delayMs; }
        }

        public TrayMenu(int initialCount, int initialIdleSeconds, int initialDress, bool walkersOn, bool minionsOn, bool beanOn,
                        bool cornersOn, CornerAction[] initialActions, int initialDelayMs)
        {
            // ---- The walkers: the master switch ----
            walkersItem.Checked = walkersOn;
            walkersItem.CheckOnClick = true;
            walkersItem.CheckedChanged += OnWalkersToggled;

            // ---- The Minions ----
            minionsItem.Checked = minionsOn;
            minionsItem.CheckOnClick = true;
            minionsItem.CheckedChanged += OnMinionsToggled;

            // ---- Mr. Bean ----
            beanItem.Checked = beanOn;
            beanItem.CheckOnClick = true;
            beanItem.CheckedChanged += delegate { Raise(BeanToggled); };

            count = initialCount;
            for (int n = 1; n <= Settings.MaxWalkers; n++)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(n.ToString());
                item.Tag = n;
                item.Click += OnCountClick;
                countMenu.DropDownItems.Add(item);
            }
            ShowCount();

            // Seconds first, then minutes, with a line between the two.
            idleSeconds = initialIdleSeconds;
            bool minutes = false;
            foreach (int seconds in Settings.IdleChoices)
            {
                if (seconds >= 60 && !minutes)
                {
                    minutes = true;
                    idleMenu.DropDownItems.Add(new ToolStripSeparator());
                }
                ToolStripMenuItem item = new ToolStripMenuItem(Describe(seconds));
                item.Tag = seconds;
                item.Click += OnIdleClick;
                idleMenu.DropDownItems.Add(item);
            }
            ShowIdle();

            // The fixed colours, then Random and the picker after a line.
            dress = initialDress;
            for (int i = 0; i < Settings.DressColors.Length; i++)
                AddDress(Settings.DressNames[i], Settings.DressColors[i]);
            dressMenu.DropDownItems.Add(new ToolStripSeparator());
            AddDress("Random", Settings.RandomDress);
            customItem.Click += OnCustomDressClick;
            dressMenu.DropDownItems.Add(customItem);
            ShowDress();

            // ---- The hot corners ----
            cornersItem.Checked = cornersOn;
            cornersItem.CheckOnClick = true;
            cornersItem.CheckedChanged += OnCornersToggled;

            for (int c = 0; c < CornerNames.Length; c++)
            {
                actions[c] = initialActions[c];
                ToolStripMenuItem cornerMenu = new ToolStripMenuItem(CornerNames[c]);
                cornerMenu.Tag = c;
                for (int a = 0; a < CornerActions.Names.Length; a++)
                {
                    ToolStripMenuItem item = new ToolStripMenuItem(CornerActions.Names[a]);
                    item.Tag = a;
                    item.Click += OnCornerActionClick;
                    cornerMenu.DropDownItems.Add(item);
                }
                cornerMenus[c] = cornerMenu;
                ShowCorner(c);
            }

            delayMs = initialDelayMs;
            foreach (int ms in Settings.HotCornerDelayChoices)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(DescribeDelay(ms));
                item.Tag = ms;
                item.Click += OnDelayClick;
                delayMenu.DropDownItems.Add(item);
            }
            ShowDelay();

            // ---- The program ----
            startupItem.Checked = StartupRegistration.IsEnabled();
            startupItem.Click += OnStartupClick;

            menu.Items.Add(walkersItem);
            menu.Items.Add(idleMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(minionsItem);
            menu.Items.Add(countMenu);
            menu.Items.Add(dressMenu);
            menu.Items.Add(beanItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(cornersItem);
            foreach (ToolStripMenuItem cornerMenu in cornerMenus) menu.Items.Add(cornerMenu);
            menu.Items.Add(delayMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(startupItem);
            menu.Items.Add("About", null, OnAboutClick);
            menu.Items.Add("Exit", null, OnExitClick);
            ShowEnabled();

            iconHandle = RenderIcon();
            icon.Icon = Icon.FromHandle(iconHandle);
            icon.Text = "Idle Walker";
            icon.ContextMenuStrip = menu;
            icon.Visible = true;
        }

        // The first Minion, standing, makes the icon.
        static IntPtr RenderIcon()
        {
            const int size = 40;
            Surface surface = new Surface();
            surface.Reset(size, size);
            Personality first = Personality.Roster[0];
            FrameRenderer.DrawPose(surface, Pose.Standing(), first.Look, Settings.DressColors[0], false, size / 2.0, size - 2, 1.85);

            using (Bitmap bitmap = new Bitmap(size, size))
            {
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        bitmap.SetPixel(x, y, Color.FromArgb(surface.Pixels[y * size + x]));
                return bitmap.GetHicon();
            }
        }

        // A feature's settings are greyed out while it is off: everything
        // under the master switch while that is off, the Minions' own
        // settings while they are off. Mr. Bean's tick is his own.
        void ShowEnabled()
        {
            bool walkers = walkersItem.Checked;
            idleMenu.Enabled = walkers;
            minionsItem.Enabled = walkers;
            beanItem.Enabled = walkers;

            bool minions = walkers && minionsItem.Checked;
            countMenu.Enabled = minions;
            dressMenu.Enabled = minions;

            bool corners = cornersItem.Checked;
            foreach (ToolStripMenuItem cornerMenu in cornerMenus) cornerMenu.Enabled = corners;
            delayMenu.Enabled = corners;
        }

        void OnWalkersToggled(object sender, EventArgs e)
        {
            ShowEnabled();
            Raise(WalkersToggled);
        }

        void OnMinionsToggled(object sender, EventArgs e)
        {
            ShowEnabled();
            Raise(MinionsToggled);
        }

        void OnCornersToggled(object sender, EventArgs e)
        {
            ShowEnabled();
            Raise(HotCornersToggled);
        }

        // ---- The Minions ----

        void ShowCount()
        {
            countMenu.Text = "Minions: " + count;
            foreach (ToolStripMenuItem item in countMenu.DropDownItems)
                item.Checked = (int)item.Tag == count;
        }

        void OnCountClick(object sender, EventArgs e)
        {
            int chosen = (int)((ToolStripMenuItem)sender).Tag;
            if (chosen == count) return;
            count = chosen;
            ShowCount();
            Raise(CountChanged);
        }

        /// <summary>"1 second", "2 seconds", "1 minute", "5 minutes".</summary>
        static string Describe(int seconds)
        {
            int n = seconds >= 60 ? seconds / 60 : seconds;
            string unit = seconds >= 60 ? "minute" : "second";
            return n + " " + unit + (n == 1 ? "" : "s");
        }

        void ShowIdle()
        {
            idleMenu.Text = "Appear after: " + Describe(idleSeconds);
            Tick(idleMenu, idleSeconds);
        }

        void OnIdleClick(object sender, EventArgs e)
        {
            int chosen = (int)((ToolStripMenuItem)sender).Tag;
            if (chosen == idleSeconds) return;
            idleSeconds = chosen;
            ShowIdle();
            Raise(IdleChanged);
        }

        void AddDress(string name, int rgb)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(name);
            item.Tag = rgb;
            item.Click += OnDressClick;
            dressMenu.DropDownItems.Add(item);
        }

        static string DescribeDress(int rgb)
        {
            if (rgb == Settings.RandomDress) return "Random";
            int i = Array.IndexOf(Settings.DressColors, rgb);
            return i >= 0 ? Settings.DressNames[i] : "Custom #" + rgb.ToString("X6");
        }

        static bool IsCustom(int rgb)
        {
            return rgb != Settings.RandomDress && Array.IndexOf(Settings.DressColors, rgb) < 0;
        }

        void ShowDress()
        {
            dressMenu.Text = "Dress: " + DescribeDress(dress);
            Tick(dressMenu, dress);
            customItem.Checked = IsCustom(dress);
        }

        /// <summary>The standard colour dialog, starting from the current overalls colour.</summary>
        void OnCustomDressClick(object sender, EventArgs e)
        {
            int start = IsCustom(dress) ? dress : Settings.DressColors[0];
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.FullOpen = true;
                dialog.AnyColor = true;
                dialog.Color = Color.FromArgb(0xFF, (start >> 16) & 0xFF, (start >> 8) & 0xFF, start & 0xFF);
                if (customPalette != null) dialog.CustomColors = customPalette;
                if (dialog.ShowDialog() != DialogResult.OK) return;
                customPalette = dialog.CustomColors;
                Color c = dialog.Color;
                ChooseDress((c.R << 16) | (c.G << 8) | c.B);
            }
        }

        void OnDressClick(object sender, EventArgs e)
        {
            ChooseDress((int)((ToolStripMenuItem)sender).Tag);
        }

        void ChooseDress(int chosen)
        {
            // Picking Random again re-rolls everyone.
            if (chosen == dress && chosen != Settings.RandomDress) return;
            dress = chosen;
            ShowDress();
            Raise(DressChanged);
        }

        // ---- The hot corners ----

        void ShowCorner(int c)
        {
            cornerMenus[c].Text = CornerNames[c] + ": " + CornerActions.Names[(int)actions[c]];
            Tick(cornerMenus[c], (int)actions[c]);
        }

        void OnCornerActionClick(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            int c = (int)item.OwnerItem.Tag;
            CornerAction chosen = (CornerAction)(int)item.Tag;
            if (chosen == actions[c]) return;
            actions[c] = chosen;
            ShowCorner(c);
            Action<Corner> handler = CornerChanged;
            if (handler != null) handler((Corner)c);
        }

        /// <summary>"0.3 s", "1 s".</summary>
        static string DescribeDelay(int ms)
        {
            return (ms / 1000.0).ToString("0.#") + " s";
        }

        void ShowDelay()
        {
            delayMenu.Text = "Corner delay: " + DescribeDelay(delayMs);
            Tick(delayMenu, delayMs);
        }

        void OnDelayClick(object sender, EventArgs e)
        {
            int chosen = (int)((ToolStripMenuItem)sender).Tag;
            if (chosen == delayMs) return;
            delayMs = chosen;
            ShowDelay();
            Raise(DelayChanged);
        }

        // ---- The program ----

        void OnStartupClick(object sender, EventArgs e)
        {
            bool enable = !startupItem.Checked;
            try
            {
                StartupRegistration.SetEnabled(enable);
                startupItem.Checked = enable;
                UserSettings.MarkStartupDecided();
            }
            catch (Exception ex)
            {
                icon.ShowBalloonTip(4000, "Idle Walker",
                    "Could not change the start-up setting: " + ex.Message, ToolTipIcon.Warning);
            }
        }

        void OnAboutClick(object sender, EventArgs e)
        {
            Raise(AboutRequested);
        }

        void OnExitClick(object sender, EventArgs e)
        {
            Raise(ExitRequested);
        }

        // ---- Helpers ----

        /// <summary>Ticks the submenu entry whose Tag is `value` and unticks the rest.</summary>
        static void Tick(ToolStripMenuItem submenu, int value)
        {
            foreach (ToolStripItem entry in submenu.DropDownItems)
            {
                ToolStripMenuItem item = entry as ToolStripMenuItem;
                if (item != null && item.Tag != null) item.Checked = (int)item.Tag == value;
            }
        }

        void Raise(EventHandler handler)
        {
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            icon.Visible = false;
            icon.Dispose();
            menu.Dispose();
            if (iconHandle != IntPtr.Zero)
            {
                Native.DestroyIcon(iconHandle);
                iconHandle = IntPtr.Zero;
            }
        }
    }
}
