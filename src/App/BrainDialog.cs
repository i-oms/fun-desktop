using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using IdleWalker.Domain;
using IdleWalker.Platform;

namespace IdleWalker.App
{
    /// <summary>
    /// The "Brains" dashboard: where the learnings live, how big they are,
    /// totals across the cast, and a sortable per-walker table (name,
    /// temperament, learned tastes, friendships, own walk) - with buttons to
    /// open the file and to forget everything and start a fresh cast. The
    /// table refreshes every second while open (the sim keeps slow-stepping
    /// behind it), keeping the sort order and the selected row.
    /// </summary>
    sealed class BrainDialog : Form
    {
        readonly Troupe troupe;
        readonly Label fileLabel;
        readonly Label sizeLabel;
        readonly Label totalsLabel;
        readonly Label noteLabel;
        readonly Label updatedLabel;
        readonly DataGridView grid;
        readonly Button openButton;
        readonly Button forgetButton;
        readonly Button closeButton;
        readonly Timer liveTimer;

        public BrainDialog(Troupe troupe)
        {
            this.troupe = troupe;

            Text = "Walker brains";
            TopMost = true;   // the walkers are topmost too; the card must sit above them
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(760, 470);

            fileLabel = new Label();
            fileLabel.Bounds = new Rectangle(12, 10, 560, 18);
            fileLabel.AutoEllipsis = true;
            Controls.Add(fileLabel);

            updatedLabel = new Label();
            updatedLabel.Bounds = new Rectangle(638, 10, 110, 18);
            updatedLabel.TextAlign = ContentAlignment.TopRight;
            updatedLabel.ForeColor = Color.FromArgb(0x1A, 0x7F, 0x37);
            Controls.Add(updatedLabel);

            sizeLabel = new Label();
            sizeLabel.Bounds = new Rectangle(12, 30, 736, 18);
            sizeLabel.AutoEllipsis = true;
            Controls.Add(sizeLabel);

            totalsLabel = new Label();
            totalsLabel.Font = new Font(totalsLabel.Font, FontStyle.Bold);
            totalsLabel.Bounds = new Rectangle(12, 52, 736, 18);
            totalsLabel.AutoEllipsis = true;
            Controls.Add(totalsLabel);

            noteLabel = new Label();
            noteLabel.ForeColor = SystemColors.GrayText;
            noteLabel.Text = "Live - they keep playing behind this card. Click a header to sort.";
            noteLabel.Bounds = new Rectangle(12, 72, 736, 18);
            noteLabel.AutoEllipsis = true;
            Controls.Add(noteLabel);

            grid = new DataGridView();
            grid.Bounds = new Rectangle(12, 96, 736, 326);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.BackgroundColor = SystemColors.Window;
            grid.BorderStyle = BorderStyle.Fixed3D;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(0xF2, 0xF4, 0xF7);
            AddColumn("Name", 85, null, DataGridViewContentAlignment.MiddleLeft);
            AddColumn("Temperament", 75, null, DataGridViewContentAlignment.MiddleLeft);
            AddColumn("Tastes", 50, typeof(int), DataGridViewContentAlignment.MiddleRight);
            AddColumn("Friends", 50, typeof(int), DataGridViewContentAlignment.MiddleRight);
            AddColumn("Best friend", 85, null, DataGridViewContentAlignment.MiddleLeft);
            AddColumn("Rival", 85, null, DataGridViewContentAlignment.MiddleLeft);
            AddColumn("Own walk", 55, null, DataGridViewContentAlignment.MiddleCenter);
            AddColumn("Decisions", 60, typeof(int), DataGridViewContentAlignment.MiddleRight);
            AddColumn("Avg reward", 65, null, DataGridViewContentAlignment.MiddleRight);
            AddColumn("Net", 55, null, DataGridViewContentAlignment.MiddleRight);
            Controls.Add(grid);

            openButton = new Button();
            openButton.Text = "Open file location";
            openButton.Bounds = new Rectangle(12, 430, 140, 28);
            openButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            openButton.Click += OnOpenClick;
            Controls.Add(openButton);

            forgetButton = new Button();
            forgetButton.Text = "Forget everything...";
            forgetButton.Bounds = new Rectangle(160, 430, 140, 28);
            forgetButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            forgetButton.Click += OnForgetClick;
            Controls.Add(forgetButton);

            closeButton = new Button();
            closeButton.Text = "Close";
            closeButton.DialogResult = DialogResult.OK;
            closeButton.Bounds = new Rectangle(668, 430, 80, 28);
            closeButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            Controls.Add(closeButton);
            AcceptButton = closeButton;
            CancelButton = closeButton;

            RefreshDashboard();

            liveTimer = new Timer();
            liveTimer.Interval = 1000;
            liveTimer.Tick += delegate { RefreshDashboard(); };
            liveTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && liveTimer != null)
            {
                liveTimer.Stop();
                liveTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        void AddColumn(string name, int width, Type valueType, DataGridViewContentAlignment align)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = name;
            column.Width = width;
            column.SortMode = DataGridViewColumnSortMode.Automatic;
            column.DefaultCellStyle.Alignment = align;
            if (valueType != null) column.ValueType = valueType;
            grid.Columns.Add(column);
        }

        void RefreshDashboard()
        {
            // The sort order is re-applied after the values move, so a sorted
            // table stays sorted while it ticks.
            DataGridViewColumn sorted = null;
            ListSortDirection direction = ListSortDirection.Ascending;
            try
            {
                sorted = grid.SortedColumn;
                if (grid.SortOrder == SortOrder.Descending) direction = ListSortDirection.Descending;
            }
            catch (Exception)
            {
            }

            try
            {
                troupe.EnsureCast();
            }
            catch (Exception)
            {
            }

            string path = null;
            try
            {
                path = BrainStore.StoredPath();
            }
            catch (Exception)
            {
            }
            fileLabel.Text = "File: " + (path == null ? "(unavailable)" : path);
            long size = BrainStore.FileSizeBytes();
            sizeLabel.Text = "Size: " + (size < 0 ? "nothing saved yet" : DescribeSize(size));

            int friendships = 0;
            int tastes = 0;
            int decisions = 0;
            foreach (Walker walker in troupe.Walkers)
            {
                try
                {
                    friendships += walker.Friendships();
                    tastes += walker.LearnedEntries();
                    decisions += walker.Experience();
                }
                catch (Exception)
                {
                }
            }
            totalsLabel.Text = troupe.Walkers.Count + " walkers  -  " +
                friendships + " friendships  -  " + tastes + " learned tastes  -  " +
                decisions + " decisions";
            updatedLabel.Text = "Live - " + DateTime.Now.ToString("HH:mm:ss");

            // Rows are built once and updated in place afterwards: the rows
            // (and with them the scroll position, the selection and the
            // flicker-free view) survive refreshes. Each row remembers its
            // walker in Tag.
            if (grid.Rows.Count != troupe.Walkers.Count)
            {
                grid.Rows.Clear();
                foreach (Walker walker in troupe.Walkers)
                {
                    int row = grid.Rows.Add(RowCells(walker));
                    grid.Rows[row].Tag = walker;
                }
                grid.ClearSelection();
            }
            else
            {
                foreach (DataGridViewRow row in grid.Rows)
                {
                    Walker walker = row.Tag as Walker;
                    if (walker == null) continue;
                    object[] cells = RowCells(walker);
                    for (int c = 1; c < cells.Length; c++) row.Cells[c].Value = cells[c];
                }
            }

            try
            {
                if (sorted != null) grid.Sort(sorted, direction);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>One row of table values: name, temperament, tastes, friends, best friend, rival, own walk.</summary>
        static object[] RowCells(Walker walker)
        {
            int t = 0, f = 0, exp = 0;
            bool walk = false;
            string temper = "";
            string name = "";
            string best = "-";
            string rival = "-";
            string avg = "-";
            string net = "-";
            try
            {
                name = walker.Personality.Name;
                t = walker.LearnedEntries();
                f = walker.Friendships();
                walk = walker.HasOwnWalk();
                temper = walker.Personality.Temperament.ToString();
                best = walker.BestFriendName();
                rival = walker.RivalName();
                exp = walker.Experience();
                avg = walker.AvgReward().ToString("0.00");
                net = walker.MlpWeightNorm().ToString("0.00");
            }
            catch (Exception)
            {
            }
            return new object[] { name, temper, t, f, best, rival, walk ? "Yes" : "-", exp, avg, net };
        }

        static string DescribeSize(long bytes)
        {
            if (bytes < 1024) return bytes + " bytes";
            double kb = bytes / 1024.0;
            if (kb < 1024) return kb.ToString("0.0") + " KB (" + bytes.ToString("N0") + " bytes)";
            return (kb / 1024.0).ToString("0.00") + " MB (" + bytes.ToString("N0") + " bytes)";
        }

        void OnOpenClick(object sender, EventArgs e)
        {
            try
            {
                string path = BrainStore.StoredPath();
                if (path == null)
                {
                    MessageBox.Show(this, "There is nowhere to save brains on this machine.",
                        "Walker brains", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (System.IO.File.Exists(path))
                    Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else
                    Process.Start("explorer.exe", "/select,\"" + System.IO.Path.GetDirectoryName(path) + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open the file location: " + ex.Message,
                    "Walker brains", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void OnForgetClick(object sender, EventArgs e)
        {
            DialogResult answer = MessageBox.Show(this,
                "Forget everything they have learned - taste, friendships, " +
                "hangouts and walks - and start a fresh cast?",
                "Walker brains", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;
            BrainStore.Forget(troupe);
            RefreshDashboard();
        }
    }
}
