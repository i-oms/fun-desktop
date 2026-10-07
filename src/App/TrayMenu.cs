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
        readonly ToolStripMenuItem startupItem = new ToolStripMenuItem("Start with Windows");
        readonly ToolStripMenuItem pauseItem = new ToolStripMenuItem("Pause");
        IntPtr iconHandle;
        int count;

        public event EventHandler ExitRequested;
        public event EventHandler CountChanged;

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

        public TrayMenu(int initialCount)
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

            startupItem.Checked = StartupRegistration.IsEnabled();
            startupItem.Click += OnStartupClick;
            pauseItem.CheckOnClick = true;

            menu.Items.Add(countMenu);
            menu.Items.Add(startupItem);
            menu.Items.Add(pauseItem);
            menu.Items.Add(new ToolStripSeparator());
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
            FrameRenderer.DrawPose(surface, Pose.Standing(first.Faces), first.Palette, false, size / 2, size - 2, 2);
            surface.CommitCharacter(2);

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

        void OnStartupClick(object sender, EventArgs e)
        {
            bool enable = !startupItem.Checked;
            try
            {
                StartupRegistration.SetEnabled(enable);
                startupItem.Checked = enable;
            }
            catch (Exception ex)
            {
                icon.ShowBalloonTip(4000, "Idle Walker",
                    "Could not change the start-up setting: " + ex.Message, ToolTipIcon.Warning);
            }
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
