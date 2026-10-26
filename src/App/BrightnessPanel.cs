using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;
using IdleWalker.Platform;

namespace IdleWalker.App
{
    /// <summary>
    /// The brightness flyout that opens on a left-click of the tray icon: a
    /// small dark panel above the notification area with one slider that
    /// moves every display together, then one slider per display. Displays
    /// that cannot be dimmed (no DDC/CI, no driver support) are listed greyed
    /// so it is clear why they do not move. Clicking anywhere else, or Esc,
    /// closes it.
    /// </summary>
    sealed class BrightnessPanel : Form
    {
        static readonly Color Back = Color.FromArgb(0x16, 0x18, 0x22);
        static readonly Color Rim = Color.FromArgb(0x3A, 0x3E, 0x4C);
        static readonly Color Dim = Color.FromArgb(0x8A, 0x8F, 0x9E);
        static readonly Color Accent = Color.FromArgb(0xFA, 0xD6, 0x1C);

        readonly Screen screen;
        readonly double scale;       // 1 at 100% display scaling
        readonly Font titleFont, textFont;
        readonly Label status;
        List<BrightnessDisplay> displays;
        readonly List<TrackBar> sliders = new List<TrackBar>();     // one per display, null where unsupported
        readonly List<Label> values = new List<Label>();
        TrackBar allSlider;
        Label allValue;
        bool syncing;                // true while sliders are moved from code, not by the user

        public BrightnessPanel(Point cursor)
        {
            screen = Screen.FromPoint(cursor);
            scale = AboutToast.DpiScale(screen);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Back;
            ForeColor = Color.White;
            DoubleBuffered = true;

            titleFont = new Font("Segoe UI", (float)(15 * scale), FontStyle.Bold, GraphicsUnit.Pixel);
            textFont = new Font("Segoe UI", (float)(13 * scale), FontStyle.Regular, GraphicsUnit.Pixel);

            Controls.Add(MakeLabel("Brightness", titleFont, Accent, Px(14), Px(12), Px(200)));
            status = MakeLabel("Reading displays...", textFont, Dim, Px(14), Px(44), Width0 - Px(28));
            Controls.Add(status);
            Place(Px(80));

            Deactivate += delegate { Close(); };
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
        }

        int Width0
        {
            get { return Px(340); }
        }

        /// <summary>Shows the panel, focused, and reads the displays in the background.</summary>
        public void Present()
        {
            Show();
            Activate();
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<BrightnessDisplay> found;
                try
                {
                    found = BrightnessDisplay.Enumerate();
                }
                catch (Exception)
                {
                    found = new List<BrightnessDisplay>();
                }
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (IsDisposed) Release(found);
                        else Populate(found);
                    });
                }
                catch (Exception)
                {
                    // Closed before the displays answered.
                    Release(found);
                }
            });
        }

        void Populate(List<BrightnessDisplay> found)
        {
            displays = found;
            SuspendLayout();
            Controls.Remove(status);
            status.Dispose();

            int left = Px(14), width = Width0 - 2 * left;
            int y = Px(44);
            bool anySupported = false;
            int sum = 0, count = 0;
            foreach (BrightnessDisplay d in displays)
                if (d.Supported)
                {
                    anySupported = true;
                    sum += d.Percent;
                    count++;
                }

            // All displays at once, only worth showing when there is more than one to move.
            if (count > 1)
            {
                allValue = AddRow("All displays", true, left, width, ref y);
                allSlider = AddSlider(left, width, ref y, sum / count);
                allSlider.ValueChanged += OnAllChanged;
                ShowValue(allValue, allSlider.Value);
                y += Px(6);
            }

            for (int i = 0; i < displays.Count; i++)
            {
                BrightnessDisplay d = displays[i];
                string name = d.Name + (d.Primary ? " (main)" : "");
                Label value = AddRow(name, d.Supported, left, width, ref y);
                values.Add(value);
                if (d.Supported)
                {
                    TrackBar slider = AddSlider(left, width, ref y, d.Percent);
                    slider.Tag = i;
                    slider.ValueChanged += OnDisplayChanged;
                    sliders.Add(slider);
                    ShowValue(value, d.Percent);
                }
                else
                {
                    sliders.Add(null);
                    value.Text = "";
                    Controls.Add(MakeLabel("Not adjustable here (no DDC/CI support)", textFont, Dim, left, y, width));
                    y += Px(26);
                }
            }

            if (!anySupported)
            {
                Controls.Add(MakeLabel("Turn on DDC/CI in the monitor's own menu to control it from here.", textFont, Dim, left, y, width));
                y += Px(36);
            }

            Place(y + Px(8));
            ResumeLayout();
            if (allSlider != null) allSlider.Focus();
            else if (sliders.Count > 0 && sliders[0] != null) sliders[0].Focus();
        }

        // ---- Rows ----

        Label AddRow(string text, bool enabled, int left, int width, ref int y)
        {
            int valueWidth = Px(48);
            Controls.Add(MakeLabel(text, textFont, enabled ? Color.White : Dim, left, y, width - valueWidth));
            Label value = MakeLabel("", textFont, Accent, left + width - valueWidth, y, valueWidth);
            value.TextAlign = ContentAlignment.TopRight;
            Controls.Add(value);
            y += Px(20);
            return value;
        }

        TrackBar AddSlider(int left, int width, ref int y, int value)
        {
            TrackBar slider = new TrackBar();
            slider.AutoSize = false;
            slider.Minimum = 0;
            slider.Maximum = 100;
            slider.SmallChange = 1;
            slider.LargeChange = 10;
            slider.TickStyle = TickStyle.None;
            slider.BackColor = Back;
            slider.SetBounds(left - Px(4), y, width + Px(8), Px(30));
            slider.Value = Math.Max(0, Math.Min(100, value));
            Controls.Add(slider);
            y += Px(34);
            return slider;
        }

        Label MakeLabel(string text, Font font, Color color, int left, int top, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Back;
            label.AutoEllipsis = true;
            label.SetBounds(left, top, width, TextRenderer.MeasureText(text.Length > 0 ? text : "0", font, new Size(width, 0), TextFormatFlags.WordBreak).Height + Px(2));
            return label;
        }

        static void ShowValue(Label label, int percent)
        {
            label.Text = percent + "%";
        }

        // ---- Moving the sliders ----

        void OnAllChanged(object sender, EventArgs e)
        {
            ShowValue(allValue, allSlider.Value);
            if (syncing) return;
            syncing = true;
            for (int i = 0; i < displays.Count; i++)
            {
                if (sliders[i] == null) continue;
                displays[i].Set(allSlider.Value);
                sliders[i].Value = allSlider.Value;
                ShowValue(values[i], allSlider.Value);
            }
            syncing = false;
        }

        void OnDisplayChanged(object sender, EventArgs e)
        {
            TrackBar slider = (TrackBar)sender;
            int i = (int)slider.Tag;
            ShowValue(values[i], slider.Value);
            if (syncing) return;
            displays[i].Set(slider.Value);

            // The shared slider follows the average of the others.
            if (allSlider == null) return;
            int sum = 0, count = 0;
            foreach (TrackBar s in sliders)
                if (s != null)
                {
                    sum += s.Value;
                    count++;
                }
            syncing = true;
            allSlider.Value = sum / count;
            syncing = false;
        }

        // ---- Window ----

        // Bottom-right of the working area, just above the taskbar and tray,
        // of the monitor the cursor is on. Grows upwards.
        void Place(int height)
        {
            Rectangle area = screen.WorkingArea;
            int margin = Px(12);
            SetBounds(area.Right - Width0 - margin, area.Bottom - height - margin, Width0, height);
            Region = RoundedRegion(Width0, height, Px(10));
            Invalidate();
        }

        // A tool window: no taskbar button, no Alt-Tab entry.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            using (GraphicsPath path = RoundedPath(Width - 1, Height - 1, Px(10)))
            using (Pen rim = new Pen(Rim, 1))
                g.DrawPath(rim, path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Release(displays);
                displays = null;
                titleFont.Dispose();
                textFont.Dispose();
            }
            base.Dispose(disposing);
        }

        // ---- Helpers ----

        // Letting go of a display waits for a write still in flight (a DDC/CI
        // write can take a tenth of a second, a WMI one longer), so it is
        // done on a pool thread: closing the panel never stalls the UI.
        static void Release(List<BrightnessDisplay> list)
        {
            if (list == null) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                foreach (BrightnessDisplay d in list) d.Dispose();
            });
        }

        int Px(double at100)
        {
            return (int)Math.Round(at100 * scale);
        }

        static GraphicsPath RoundedPath(int width, int height, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(width - d, 0, d, d, 270, 90);
            path.AddArc(width - d, height - d, d, d, 0, 90);
            path.AddArc(0, height - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        static Region RoundedRegion(int width, int height, int radius)
        {
            using (GraphicsPath path = RoundedPath(width, height, radius))
                return new Region(path);
        }
    }
}
