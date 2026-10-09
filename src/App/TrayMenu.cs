using System;
using System.Drawing;
using System.Windows.Forms;
using IdleWalker.Domain;
using IdleWalker.Platform;
using IdleWalker.Rendering;

namespace IdleWalker.App
{
    /// <summary>The notification-area icon and its right-click menu.</summary>
    sealed class TrayMenu : IDisposable
    {
        readonly NotifyIcon icon = new NotifyIcon();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly ToolStripMenuItem countMenu = new ToolStripMenuItem("Walkers");
        readonly ToolStripMenuItem idleMenu = new ToolStripMenuItem("Appear after");
        readonly ToolStripMenuItem dressMenu = new ToolStripMenuItem("Dress");
        readonly ToolStripMenuItem startupItem = new ToolStripMenuItem("Start with Windows");
        readonly ToolStripMenuItem pauseItem = new ToolStripMenuItem("Pause");
        IntPtr iconHandle;
        int count;
        int idleSeconds;
        int dress;

        public event EventHandler ExitRequested;
        public event EventHandler CountChanged;
        public event EventHandler IdleChanged;
        public event EventHandler DressChanged;
        public event EventHandler AboutRequested;

        /// <summary>True while the user has ticked Pause.</summary>
        public bool Paused
        {
            get { return pauseItem.Checked; }
        }

        /// <summary>How many walkers the user wants (1..Settings.MaxWalkers).</summary>
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

        public TrayMenu(int initialCount, int initialIdleSeconds, int initialDress)
        {
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

            // The fixed colours, then Random after a line.
            dress = initialDress;
            for (int i = 0; i < Settings.DressColors.Length; i++)
                AddDress(Settings.DressNames[i], Settings.DressColors[i]);
            dressMenu.DropDownItems.Add(new ToolStripSeparator());
            AddDress("Random", Settings.RandomDress);
            ShowDress();

            startupItem.Checked = StartupRegistration.IsEnabled();
            startupItem.Click += OnStartupClick;
            pauseItem.CheckOnClick = true;

            menu.Items.Add(countMenu);
            menu.Items.Add(idleMenu);
            menu.Items.Add(dressMenu);
            menu.Items.Add(startupItem);
            menu.Items.Add(pauseItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("About", null, OnAboutClick);
            menu.Items.Add("Exit", null, OnExitClick);

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

        void ShowCount()
        {
            countMenu.Text = "Walkers: " + count;
            foreach (ToolStripMenuItem item in countMenu.DropDownItems)
                item.Checked = (int)item.Tag == count;
        }

        void OnCountClick(object sender, EventArgs e)
        {
            int chosen = (int)((ToolStripMenuItem)sender).Tag;
            if (chosen == count) return;
            count = chosen;
            ShowCount();
            EventHandler handler = CountChanged;
            if (handler != null) handler(this, EventArgs.Empty);
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
            foreach (ToolStripItem entry in idleMenu.DropDownItems)
            {
                ToolStripMenuItem item = entry as ToolStripMenuItem;
                if (item != null) item.Checked = (int)item.Tag == idleSeconds;
            }
        }

        void OnIdleClick(object sender, EventArgs e)
        {
            int chosen = (int)((ToolStripMenuItem)sender).Tag;
            if (chosen == idleSeconds) return;
            idleSeconds = chosen;
            ShowIdle();
            EventHandler handler = IdleChanged;
            if (handler != null) handler(this, EventArgs.Empty);
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
            int i = Array.IndexOf(Settings.DressColors, rgb);
            return i >= 0 ? Settings.DressNames[i] : "Random";
        }

        void ShowDress()
        {
            dressMenu.Text = "Dress: " + DescribeDress(dress);
            foreach (ToolStripItem entry in dressMenu.DropDownItems)
            {
                ToolStripMenuItem item = entry as ToolStripMenuItem;
                if (item != null) item.Checked = (int)item.Tag == dress;
            }
        }

        void OnDressClick(object sender, EventArgs e)
        {
            int chosen = (int)((ToolStripMenuItem)sender).Tag;
            // Picking Random again re-rolls everyone.
            if (chosen == dress && chosen != Settings.RandomDress) return;
            dress = chosen;
            ShowDress();
            EventHandler handler = DressChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

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
            EventHandler handler = AboutRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        void OnExitClick(object sender, EventArgs e)
        {
            EventHandler handler = ExitRequested;
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
