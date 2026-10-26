using System;
using System.Drawing;
using System.Windows.Forms;
using IdleWalker.Domain;
using IdleWalker.Platform;
using IdleWalker.Rendering;

namespace IdleWalker.App
{
    /// <summary>
    /// The notification-area icon and its right-click menu, grouped by feature:
    /// Walkers (the master switch plus the shared "Appear after" delay), then
    /// Minions / Mr. Bean / Angry Birds (each a group with its own Enabled
    /// switch and settings), then Hot corners (its own group), then the program.
    /// A left-click on the icon opens the brightness panel instead.
    /// </summary>
    sealed class TrayMenu : IDisposable
    {
        readonly NotifyIcon icon = new NotifyIcon();
        readonly ContextMenuStrip menu = new ContextMenuStrip();

        // The walkers.
        readonly ToolStripMenuItem walkersItem = new ToolStripMenuItem("Walkers");
        readonly ToolStripMenuItem minionsMenu = new ToolStripMenuItem("Minions");
        readonly ToolStripMenuItem minionsItem = new ToolStripMenuItem("Enabled");
        readonly ToolStripMenuItem minionsAllItem = new ToolStripMenuItem("All");
        readonly ToolStripMenuItem[] minionItems = new ToolStripMenuItem[Settings.MaxWalkers];
        readonly ToolStripMenuItem idleMenu = new ToolStripMenuItem("Appear after");
        readonly ToolStripMenuItem hideOnMoveItem = new ToolStripMenuItem("Hide on mouse move");
        readonly ToolStripMenuItem dressMenu = new ToolStripMenuItem("Dress");
        readonly ToolStripMenuItem customItem = new ToolStripMenuItem("Custom...");
        readonly ToolStripMenuItem beanItem = new ToolStripMenuItem("Mr. Bean");
        readonly ToolStripMenuItem birdsMenu = new ToolStripMenuItem("Angry Birds");
        readonly ToolStripMenuItem birdsItem = new ToolStripMenuItem("Enabled");
        readonly ToolStripMenuItem birdsAllItem = new ToolStripMenuItem("All");
        readonly ToolStripMenuItem[] birdItems = new ToolStripMenuItem[Settings.MaxBirds];
        readonly ToolStripMenuItem pantherItem = new ToolStripMenuItem("Pink Panther");
        int[] customPalette;         // the colour dialog's own swatches, kept between openings

        // The hot corners.
        static readonly string[] CornerNames = { "Top-left", "Top-right", "Bottom-left", "Bottom-right" };
        readonly ToolStripMenuItem hotCornersMenu = new ToolStripMenuItem("Hot corners");
        readonly ToolStripMenuItem cornersItem = new ToolStripMenuItem("Enabled");
        readonly ToolStripMenuItem[] cornerMenus = new ToolStripMenuItem[CornerNames.Length];
        readonly ToolStripMenuItem delayMenu = new ToolStripMenuItem("Delay");

        readonly ToolStripMenuItem keepAwakeItem = new ToolStripMenuItem("Keep system awake");
        readonly ToolStripMenuItem startupItem = new ToolStripMenuItem("Start with Windows");
        IntPtr iconHandle;
        int minionMask;
        int birdMask;
        int idleSeconds;
        int dress;
        readonly CornerAction[] actions = new CornerAction[CornerNames.Length];
        int delayMs;
        bool closeAllowed;      // true only for the click that should really dismiss the menu (About / Exit / picker)

        public event EventHandler ExitRequested;
        public event EventHandler WalkersToggled;
        public event EventHandler MinionsToggled;
        public event EventHandler MinionSelectionChanged;
        public event EventHandler IdleChanged;
        public event EventHandler DressChanged;
        public event EventHandler BeanToggled;
        public event EventHandler BirdsToggled;
        public event EventHandler BirdSelectionChanged;
        public event EventHandler PantherToggled;
        public event EventHandler HideOnMoveToggled;
        public event EventHandler HotCornersToggled;
        public event EventHandler KeepAwakeToggled;
        public event Action<Corner> CornerChanged;
        public event EventHandler DelayChanged;
        public event EventHandler AboutRequested;
        public event EventHandler BrainsRequested;
        public event EventHandler BrightnessRequested;

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

        /// <summary>Bit mask over Personality.Roster: bit i set means that Minion may come out.</summary>
        public int MinionMask
        {
            get { return minionMask; }
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

        /// <summary>True while the Angry Birds are switched on (independently of the rest).</summary>
        public bool BirdsEnabled
        {
            get { return birdsItem.Checked; }
        }

        /// <summary>Bit mask over Personality.BirdRoster: bit i set means that bird may come out.</summary>
        public int BirdMask
        {
            get { return birdMask; }
        }

        /// <summary>True while the Pink Panther is switched on (independently of the rest).</summary>
        public bool PantherEnabled
        {
            get { return pantherItem.Checked; }
        }

        /// <summary>True while a mouse move sends everyone back underground. Unticked, they stay out and carry on.</summary>
        public bool HideOnMove
        {
            get { return hideOnMoveItem.Checked; }
        }

        /// <summary>True while the hot corners are switched on.</summary>
        public bool HotCornersEnabled
        {
            get { return cornersItem.Checked; }
        }

        /// <summary>True while the machine is kept awake and the session kept active: no sleep, screen saver, auto-lock, or "Away" presence while the user is away.</summary>
        public bool KeepAwake
        {
            get { return keepAwakeItem.Checked; }
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

        public TrayMenu(int initialMinionMask, int initialBirdMask, int initialIdleSeconds, int initialDress,
                        bool walkersOn, bool minionsOn, bool beanOn, bool birdsOn, bool pantherOn, bool hideOnMove,
                        bool cornersOn, CornerAction[] initialActions, int initialDelayMs, bool keepAwakeOn)
        {
            // ---- The walkers: the master switch ----
            walkersItem.Checked = walkersOn;
            walkersItem.CheckOnClick = true;
            walkersItem.CheckedChanged += OnWalkersToggled;

            // ---- Staying out: untick and a mouse move no longer sends them away ----
            hideOnMoveItem.Checked = hideOnMove;
            hideOnMoveItem.CheckOnClick = true;
            hideOnMoveItem.CheckedChanged += delegate { Raise(HideOnMoveToggled); };

            // ---- The Minions ----
            minionsItem.Checked = minionsOn;
            minionsItem.CheckOnClick = true;
            minionsItem.CheckedChanged += OnMinionsToggled;

            // ---- Mr. Bean ----
            beanItem.Checked = beanOn;
            beanItem.CheckOnClick = true;
            beanItem.CheckedChanged += delegate { Raise(BeanToggled); };

            // ---- The Pink Panther ----
            pantherItem.Checked = pantherOn;
            pantherItem.CheckOnClick = true;
            pantherItem.CheckedChanged += delegate { Raise(PantherToggled); };

            // ---- The Angry Birds ----
            birdsItem.Checked = birdsOn;
            birdsItem.CheckOnClick = true;
            birdsItem.CheckedChanged += OnBirdsToggled;

            birdMask = initialBirdMask & AllBirds;
            birdsAllItem.Click += OnBirdsAllClick;
            for (int b = 0; b < Settings.MaxBirds; b++)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(Personality.BirdRoster[b].Name);
                item.Tag = b;
                item.Click += OnBirdClick;
                birdItems[b] = item;
            }
            ShowBirds();

            minionMask = initialMinionMask & AllMinions;
            minionsAllItem.Click += OnMinionsAllClick;
            for (int n = 0; n < Settings.MaxWalkers; n++)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(Personality.Roster[n].Name);
                item.Tag = n;
                item.Click += OnMinionClick;
                minionItems[n] = item;
            }
            ShowMinions();

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
            keepAwakeItem.Checked = keepAwakeOn;
            keepAwakeItem.CheckOnClick = true;
            keepAwakeItem.CheckedChanged += delegate { Raise(KeepAwakeToggled); };

            startupItem.Checked = StartupRegistration.IsEnabled();
            startupItem.Click += OnStartupClick;

            // ---- Grouping: each feature is a sub-menu with an "Enabled"
            // switch first, then its own settings after a line. ----
            minionsMenu.DropDownItems.Add(minionsItem);
            minionsMenu.DropDownItems.Add(minionsAllItem);
            minionsMenu.DropDownItems.Add(new ToolStripSeparator());
            foreach (ToolStripMenuItem minion in minionItems) minionsMenu.DropDownItems.Add(minion);
            minionsMenu.DropDownItems.Add(new ToolStripSeparator());
            minionsMenu.DropDownItems.Add(dressMenu);

            birdsMenu.DropDownItems.Add(birdsItem);
            birdsMenu.DropDownItems.Add(birdsAllItem);
            birdsMenu.DropDownItems.Add(new ToolStripSeparator());
            foreach (ToolStripMenuItem bird in birdItems) birdsMenu.DropDownItems.Add(bird);

            hotCornersMenu.DropDownItems.Add(cornersItem);
            hotCornersMenu.DropDownItems.Add(new ToolStripSeparator());
            foreach (ToolStripMenuItem cornerMenu in cornerMenus) hotCornersMenu.DropDownItems.Add(cornerMenu);
            hotCornersMenu.DropDownItems.Add(new ToolStripSeparator());
            hotCornersMenu.DropDownItems.Add(delayMenu);

            menu.Items.Add(walkersItem);
            menu.Items.Add(idleMenu);
            menu.Items.Add(hideOnMoveItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(minionsMenu);
            menu.Items.Add(beanItem);
            menu.Items.Add(pantherItem);
            menu.Items.Add(birdsMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(hotCornersMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Brightness...", null, OnBrightnessClick);
            menu.Items.Add(keepAwakeItem);
            menu.Items.Add(startupItem);
            menu.Items.Add("Brains...", null, OnBrainsClick);
            menu.Items.Add("About", null, OnAboutClick);
            menu.Items.Add("Exit", null, OnExitClick);
            ShowEnabled();

            // Sticky menu: clicks on settings keep it open, only a click
            // outside (or Esc, About, Exit, the colour picker) dismisses it.
            menu.Closing += OnDropDownClosing;
            menu.Closed += delegate { closeAllowed = false; };
            KeepOpen(menu.Items);

            iconHandle = RenderIcon();
            icon.Icon = Icon.FromHandle(iconHandle);
            icon.Text = "Idle Walker";
            icon.ContextMenuStrip = menu;
            // A left-click opens the brightness panel; the right-click menu is unchanged.
            icon.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) Raise(BrightnessRequested);
            };
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
        // settings while they are off. Mr. Bean's tick is his own. Group
        // headers stay enabled so the group can always be opened; only the
        // settings inside are dimmed.
        void ShowEnabled()
        {
            bool walkers = walkersItem.Checked;
            idleMenu.Enabled = walkers;
            hideOnMoveItem.Enabled = walkers;
            minionsMenu.Enabled = walkers;
            beanItem.Enabled = walkers;
            pantherItem.Enabled = walkers;
            birdsMenu.Enabled = walkers;

            bool minions = walkers && minionsItem.Checked;
            minionsAllItem.Enabled = minions;
            foreach (ToolStripMenuItem minion in minionItems) minion.Enabled = minions;
            dressMenu.Enabled = minions;

            bool birds = walkers && birdsItem.Checked;
            birdsAllItem.Enabled = birds;
            foreach (ToolStripMenuItem bird in birdItems) bird.Enabled = birds;

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

        void OnBirdsToggled(object sender, EventArgs e)
        {
            ShowEnabled();
            Raise(BirdsToggled);
        }

        void OnCornersToggled(object sender, EventArgs e)
        {
            ShowEnabled();
            Raise(HotCornersToggled);
        }

        // ---- The Minions: each one ticked separately, plus All ----

        static int AllMinions
        {
            get { return (1 << Settings.MaxWalkers) - 1; }
        }

        static int AllBirds
        {
            get { return (1 << Settings.MaxBirds) - 1; }
        }

        void ShowMinions()
        {
            minionsAllItem.Checked = minionMask == AllMinions;
            for (int n = 0; n < minionItems.Length; n++)
                minionItems[n].Checked = ((minionMask >> n) & 1) != 0;
        }

        void OnMinionClick(object sender, EventArgs e)
        {
            int chosen = (int)((ToolStripMenuItem)sender).Tag;
            if (chosen < 0 || chosen >= Settings.MaxWalkers) return;
            minionMask ^= (1 << chosen);
            ShowMinions();
            Raise(MinionSelectionChanged);
        }

        void OnMinionsAllClick(object sender, EventArgs e)
        {
            minionMask = minionMask == AllMinions ? 0 : AllMinions;
            ShowMinions();
            Raise(MinionSelectionChanged);
        }

        void ShowBirds()
        {
            birdsAllItem.Checked = birdMask == AllBirds;
            for (int b = 0; b < birdItems.Length; b++)
                birdItems[b].Checked = ((birdMask >> b) & 1) != 0;
        }

        void OnBirdClick(object sender, EventArgs e)
        {
            int chosen = (int)((ToolStripMenuItem)sender).Tag;
            if (chosen < 0 || chosen >= Settings.MaxBirds) return;
            birdMask ^= (1 << chosen);
            ShowBirds();
            Raise(BirdSelectionChanged);
        }

        void OnBirdsAllClick(object sender, EventArgs e)
        {
            birdMask = birdMask == AllBirds ? 0 : AllBirds;
            ShowBirds();
            Raise(BirdSelectionChanged);
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
            // A modal dialog cannot share mouse capture with the open menu,
            // so this one click is allowed to dismiss it first.
            AllowCloseAndHide();
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
            delayMenu.Text = "Delay: " + DescribeDelay(delayMs);
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
            AllowCloseAndHide();
            Raise(AboutRequested);
        }

        void OnBrightnessClick(object sender, EventArgs e)
        {
            AllowCloseAndHide();
            Raise(BrightnessRequested);
        }

        void OnBrainsClick(object sender, EventArgs e)
        {
            AllowCloseAndHide();
            Raise(BrainsRequested);
        }

        void OnExitClick(object sender, EventArgs e)
        {
            AllowCloseAndHide();
            Raise(ExitRequested);
        }

        // ---- Helpers ----

        /// <summary>
        /// Cancels the automatic dismissal when a settings item is clicked,
        /// so the menu stays open. Clicks outside the menu, Esc, and the
        /// clicks explicitly allowed via <see cref="AllowCloseAndHide"/>
        /// still close it.
        /// </summary>
        void OnDropDownClosing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked && !closeAllowed)
                e.Cancel = true;
        }

        /// <summary>Hooks every drop-down (at any nesting depth) so settings clicks keep the menu open.</summary>
        void KeepOpen(ToolStripItemCollection items)
        {
            foreach (ToolStripItem entry in items)
            {
                ToolStripMenuItem holder = entry as ToolStripMenuItem;
                if (holder == null || !holder.HasDropDownItems) continue;
                holder.DropDown.Closing += OnDropDownClosing;
                KeepOpen(holder.DropDownItems);
            }
        }

        /// <summary>Let the in-progress click dismiss the menu (About, Exit, colour picker).</summary>
        void AllowCloseAndHide()
        {
            closeAllowed = true;
            if (menu.Visible) menu.Close();
        }

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
