using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace MonitorControl
{
    internal class MainForm : Form
    {
        private Preferences preferences;
        private List<MonitorInfo> monitors = new List<MonitorInfo>();
        private DataGridView grid = new DataGridView();
        private FlatCombo presets = new FlatCombo();
        private Label status = new Label();
        private Preview preview = new Preview();
        private List<DisplaySetting> undoArrangement;
        private Button undoButton, saveNamesButton, revertNamesButton, deleteButton;
        // What the table holds: "Current Windows layout", a preset's name, or "Unsaved layout"; edited once the user changes it.
        private string source = "Current Windows layout"; private bool edited;
        private NotifyIcon tray;
        private bool loading, busy, exiting;
        private List<int> hotkeys = new List<int>();
        private static readonly string[] Rotations = { "Landscape", "Portrait (90°)", "Landscape (180°)", "Portrait (270°)" };
        internal static Color Ink = Color.FromArgb(27, 40, 58), Accent = Color.FromArgb(34, 93, 217), Muted = Color.FromArgb(94, 111, 131);

        public MainForm()
        {
            preferences = Store.Load();
            Text = "Monitor Control"; Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(245, 247, 251); ForeColor = Ink;
            MinimumSize = new Size(1100, 730); Size = new Size(1240, 830); StartPosition = FormStartPosition.CenterScreen;
            Icon = AppIcon.Create(32); KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.F1) { e.Handled = true; ShowHelp(); } };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 24, 24, 16), ColumnCount = 1, RowCount = 5 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 53)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 57));
            // Top row: presets on the left; whole-app actions (Identify, Refresh, Help) on the right. The window title already names the app.
            var top = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0), ColumnCount = 3, RowCount = 1 };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var help = new HelpButton { TabStop = true, Margin = new Padding(0), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            help.Click += delegate { if (!busy) Run(delegate { ShowHelp(); }); };
            new ToolTip().SetToolTip(help, "How to use Monitor Control (F1)");
            var presetBar = Bar();
            presets.DropDownStyle = ComboBoxStyle.DropDownList; presets.Width = 300; presets.Margin = new Padding(0, 5, 12, 0);
            // Picking a preset only fills the editor; Windows changes when the user applies it.
            // SelectionChangeCommitted fires for user picks only, so ReloadPresets at startup keeps the live layout in the table.
            presets.SelectionChangeCommitted += delegate { LoadPreset(); };
            presets.SelectedIndexChanged += delegate { deleteButton.Enabled = presets.SelectedItem != null; };
            presetBar.Controls.Add(presets);
            presetBar.Controls.Add(Button("Save as preset…", delegate { SavePreset(); }, false));
            deleteButton = Button("Delete", delegate { DeletePreset(); }, false); deleteButton.Enabled = false; presetBar.Controls.Add(deleteButton);
            var appBar = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0) };
            appBar.Controls.Add(Button("Identify screens", delegate { Identify(); }, false));
            appBar.Controls.Add(Button("Refresh", delegate { RefreshMonitors(); }, false));
            top.Controls.Add(presetBar, 0, 0); top.Controls.Add(appBar, 1, 0); top.Controls.Add(help, 2, 0);
            root.Controls.Add(top, 0, 0);
            preview.Dock = DockStyle.Fill; root.Controls.Add(preview, 0, 1);
            preview.SelectionChanged += delegate {
                if (loading) return;
                foreach (DataGridViewRow row in grid.Rows)
                    if (((DisplaySetting)row.Tag).Id == preview.SelectedId) { grid.CurrentCell = row.Cells["Label"]; break; }
            };
            preview.ArrangementChanged += delegate(object sender, ArrangementEventArgs e) {
                undoArrangement = e.Before; undoButton.Enabled = true; SetPositions(e.After); MarkEdited();
                SetStatus("Arrangement updated. Apply the layout or save it as a preset.");
            };
            // Tool row: only tools that change the arrangement shown above it.
            var tools = Bar(); tools.Padding = new Padding(0, 8, 0, 0);
            tools.Controls.Add(Button("Arrange in a row", delegate { Populate(LayoutRules.Row(ReadGrid())); MarkEdited(); }, false));
            undoButton = Button("Undo move", delegate { UndoMove(); }, false); undoButton.Enabled = false; tools.Controls.Add(undoButton);
            var exact = new CheckBox { Text = "Exact positions", AutoSize = true, ForeColor = Muted, Margin = new Padding(8, 9, 0, 0) };
            exact.CheckedChanged += delegate { grid.Columns["X"].Visible = exact.Checked; grid.Columns["Y"].Visible = exact.Checked; };
            tools.Controls.Add(exact);
            root.Controls.Add(tools, 0, 2);
            BuildGrid(); root.Controls.Add(grid, 0, 3);
            var actions = Bar(); actions.Padding = new Padding(0, 10, 0, 0);
            // The one Apply button: it applies the table, which is either the current layout or a preset chosen above, with any edits.
            actions.Controls.Add(Button("Apply layout", delegate { Apply(ReadGrid()); }, true));
            // Names are only kept when the user saves them; both buttons stay disabled until a name differs from its saved value.
            saveNamesButton = Button("Save monitor names", delegate { SaveNames(); SetStatus("Monitor names saved."); }, false); saveNamesButton.Enabled = false; actions.Controls.Add(saveNamesButton);
            revertNamesButton = Button("Undo name changes", delegate { RevertNames(); }, false); revertNamesButton.Enabled = false; actions.Controls.Add(revertNamesButton);
            actions.Controls.Add(Button("Minimize to tray", delegate { HideToTray(); }, false));
            var closeToTray = new CheckBox { Text = "Keep running in the tray when closed", AutoSize = true, ForeColor = Muted, Margin = new Padding(8, 9, 0, 0), Checked = preferences.CloseToTray };
            closeToTray.CheckedChanged += delegate { Run(delegate { preferences.CloseToTray = closeToTray.Checked; Store.Write(Store.ConfigPath, preferences); }); };
            new ToolTip().SetToolTip(closeToTray, "When on, the X button hides the window and shortcuts keep working. Use Exit in the tray menu to quit.");
            actions.Controls.Add(closeToTray);
            root.Controls.Add(actions, 0, 4);
            // Status bar: a full-width strip along the bottom edge, in the same tint and border as the table header.
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Color.FromArgb(236, 240, 247) };
            statusBar.Paint += delegate(object sender, PaintEventArgs e) { using (var pen = new Pen(Ui.Border)) e.Graphics.DrawLine(pen, 0, 0, statusBar.Width, 0); };
            status.Dock = DockStyle.Fill; status.ForeColor = Muted; status.Padding = new Padding(22, 0, 24, 0); status.TextAlign = ContentAlignment.MiddleLeft; status.AutoEllipsis = true;
            statusBar.Controls.Add(status);
            Controls.Add(root); Controls.Add(statusBar);
            tray = new NotifyIcon { Icon = AppIcon.Create(32), Text = "Monitor Control", Visible = true };
            tray.DoubleClick += delegate { OpenWindow(); };
            // Never block a Windows shutdown or sign-out; a pending change is restored at the next sign-in.
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (busy && !exiting && e.CloseReason != CloseReason.WindowsShutDown) { e.Cancel = true; return; }
                // With the setting on, X hides the window; tray Exit sets exiting, and Windows shutdown is never blocked.
                if (preferences.CloseToTray && !exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Run(delegate { HideToTray(); }); }
            };
            FormClosed += delegate { foreach (int id in hotkeys) Native.UnregisterHotKey(Handle, id); tray.Dispose(); };
            Shown += delegate { Run(delegate { RefreshMonitors(); ReloadPresets(); }); };
        }
        private static FlowLayoutPanel Bar() { return new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) }; }
        private Button Button(string text, Action action, bool primary)
        {
            var button = Ui.MakeButton(text, primary);
            button.Click += delegate { if (!busy) Run(action); };
            return button;
        }
        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex) { Store.Log(ex.Message); SetStatus(ex.Message); MessageBox.Show(this, ex.Message, "Monitor Control", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        private void SetStatus(string text) { status.Text = text; }
        // The preview's heading names what the table holds, so the user knows what Apply and Save as preset will use.
        private void ShowSource(string name, bool isEdited) { source = name; edited = isEdited; preview.Heading = source; preview.Edited = edited; }
        private void MarkEdited() { if (!edited) ShowSource(source, true); }
        private void ShowHelp() { using (var dialog = new HelpForm()) dialog.ShowDialog(this); }
        private void OpenWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        // Explains the tray once, the first time the window is hidden; after that it hides quietly.
        private void HideToTray()
        {
            Hide();
            if (preferences.TrayNoticeShown) return;
            tray.ShowBalloonTip(4000, "Monitor Control is still running", "Your presets and shortcuts work from here. Right-click the tray icon to switch layouts or exit.", ToolTipIcon.Info);
            preferences.TrayNoticeShown = true; Store.Write(Store.ConfigPath, preferences);
        }
        private void BuildGrid()
        {
            grid.Dock = DockStyle.Fill; grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None;
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false; grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.RowTemplate.Height = 43;
            grid.ColumnHeadersHeight = 38; grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(236, 240, 247); grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9);
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 251, 254);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(222, 233, 255); grid.DefaultCellStyle.SelectionForeColor = Ink;
            grid.GridColor = Color.FromArgb(231, 236, 243);
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "On", FillWeight = 35 });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Primary", HeaderText = "Main", FillWeight = 40 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Label", HeaderText = "Name", FillWeight = 110, CellTemplate = new NameCell() });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Model", HeaderText = "Monitor", ReadOnly = true, FillWeight = 100 });
            grid.Columns["Model"].DefaultCellStyle.Padding = new Padding(0, 0, InfoWidth, 0); // room for the info icon
            grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Mode", HeaderText = "Resolution / Hz", FillWeight = 145, FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing });
            var rotation = new DataGridViewComboBoxColumn { Name = "Rotation", HeaderText = "Orientation", FillWeight = 110, FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing };
            rotation.Items.AddRange(Rotations); grid.Columns.Add(rotation);
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "X", HeaderText = "Left (px)", FillWeight = 58 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Y", HeaderText = "Top (px)", FillWeight = 58 });
            grid.Columns["X"].Visible = false; grid.Columns["Y"].Visible = false;
            grid.SelectionChanged += delegate { if (!loading && grid.CurrentRow != null && grid.CurrentRow.Tag != null) preview.SelectedId = ((DisplaySetting)grid.CurrentRow.Tag).Id; };
            foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
            // Check boxes are centred in their cells, so centre their headings to match.
            grid.Columns["Enabled"].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.Columns["Primary"].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.CurrentCellDirtyStateChanged += delegate
            {
                if (grid.IsCurrentCellDirty && (grid.CurrentCell is DataGridViewCheckBoxCell || grid.CurrentCell is DataGridViewComboBoxCell)) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                // Enable the name buttons as soon as typing starts, so the first click on Save or Undo is not lost on a disabled button.
                if (grid.IsCurrentCellDirty && grid.CurrentCell.OwningColumn.Name == "Label") { saveNamesButton.Enabled = true; revertNamesButton.Enabled = true; }
            };
            // Name cells: drawn as a text field with a pencil, an I-beam cursor, and a single click to start typing, so they read as editable.
            grid.CellPainting += delegate(object sender, DataGridViewCellPaintingEventArgs e)
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != "Label") return;
                e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
                bool changed = NameChanged(grid.Rows[e.RowIndex]);
                // While typing, the border turns blue (the Windows focus cue); the edit box itself sits inside it (see NameCell).
                bool editing = grid.IsCurrentCellInEditMode && grid.CurrentCell.RowIndex == e.RowIndex && grid.CurrentCell.ColumnIndex == e.ColumnIndex;
                var g = e.Graphics; var oldMode = g.SmoothingMode; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var field = NameCell.Field(e.CellBounds);
                using (var shape = Ui.RoundedRect(field, Ui.CornerRadius(g))) using (var back = new SolidBrush(Color.White)) using (var pen = new Pen(changed || editing ? Accent : Ui.Border, changed || editing ? 1.5f : 1f))
                { g.FillPath(back, shape); g.DrawPath(pen, shape); }
                g.SmoothingMode = oldMode;
                var text = new Rectangle((int)field.Left + 8, (int)field.Top, (int)field.Width - 36, (int)field.Height);
                TextRenderer.DrawText(g, Convert.ToString(e.FormattedValue), e.CellStyle.Font, text, Ink, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                Ui.DrawPencil(g, new Rectangle((int)field.Right - 26, (int)field.Top, 20, (int)field.Height), changed || editing ? Accent : Muted);
                e.Handled = true;
            };
            // Main cells: drawn as radio buttons, because exactly one screen can be main.
            grid.CellPainting += delegate(object sender, DataGridViewCellPaintingEventArgs e)
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != "Primary") return;
                e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
                var state = Convert.ToBoolean(e.Value) ? System.Windows.Forms.VisualStyles.RadioButtonState.CheckedNormal : System.Windows.Forms.VisualStyles.RadioButtonState.UncheckedNormal;
                var size = RadioButtonRenderer.GetGlyphSize(e.Graphics, state);
                RadioButtonRenderer.DrawRadioButton(e.Graphics, new Point(e.CellBounds.Left + (e.CellBounds.Width - size.Width) / 2, e.CellBounds.Top + (e.CellBounds.Height - size.Height) / 2), state);
                e.Handled = true;
            };
            // Monitor cells: an info icon at the right opens the hardware details for that row.
            grid.CellPainting += delegate(object sender, DataGridViewCellPaintingEventArgs e)
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != "Model") return;
                e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.Focus);
                Ui.DrawInfo(e.Graphics, new Rectangle(e.CellBounds.Right - InfoWidth, e.CellBounds.Top, InfoWidth - 6, e.CellBounds.Height), Muted);
                e.Handled = true;
            };
            grid.CellMouseMove += delegate(object sender, DataGridViewCellMouseEventArgs e) { if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "Model") grid.Cursor = OnInfo(e) ? Cursors.Hand : Cursors.Default; };
            grid.CellMouseClick += delegate(object sender, DataGridViewCellMouseEventArgs e) { if (e.Button == MouseButtons.Left && e.RowIndex >= 0 && e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "Model" && OnInfo(e) && !busy) Run(delegate { HardwareDetails(grid.Rows[e.RowIndex]); }); };
            grid.CellBeginEdit += delegate(object sender, DataGridViewCellCancelEventArgs e) { grid.InvalidateCell(e.ColumnIndex, e.RowIndex); };
            grid.CellEndEdit += delegate { UpdateNameButtons(); };
            grid.CellMouseEnter += delegate(object sender, DataGridViewCellEventArgs e) { grid.Cursor = e.RowIndex >= 0 && e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "Label" ? Cursors.IBeam : Cursors.Default; };
            grid.CellClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != "Label") return;
                grid.CurrentCell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex]; grid.BeginEdit(false);
            };
            // Drop-down cells: draw our own chevron, and open the list on a single click.
            grid.CellPainting += delegate(object sender, DataGridViewCellPaintingEventArgs e)
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || !(grid.Columns[e.ColumnIndex] is DataGridViewComboBoxColumn)) return;
                e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.Focus);
                Ui.DrawChevron(e.Graphics, new Rectangle(e.CellBounds.Right - 32, e.CellBounds.Top, 22, e.CellBounds.Height), Muted);
                e.Handled = true;
            };
            grid.CellClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || !(grid.Columns[e.ColumnIndex] is DataGridViewComboBoxColumn)) return;
                grid.CurrentCell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex]; grid.BeginEdit(true);
                var box = grid.EditingControl as ComboBox; if (box != null) box.DroppedDown = true;
            };
            grid.EditingControlShowing += delegate(object sender, DataGridViewEditingControlShowingEventArgs e)
            {
                if (e.Control is TextBox) e.CellStyle.BackColor = Color.White; // match the drawn name field, not the row stripe
                var box = e.Control as ComboBox; if (box == null || "styled".Equals(box.Tag)) return;
                box.Tag = "styled"; box.FlatStyle = FlatStyle.Flat; box.DrawMode = DrawMode.OwnerDrawFixed; box.ItemHeight = 26; box.BackColor = Color.White;
                box.DrawItem += delegate(object s2, DrawItemEventArgs a) { Ui.DrawComboItem((ComboBox)s2, a); };
            };
            grid.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (loading || e.RowIndex < 0) return;
                loading = true;
                try
                {
                    var row = grid.Rows[e.RowIndex];
                    // Like a radio button, clicking the current main again leaves it selected.
                    if (e.ColumnIndex == 1 && !grid.Rows.Cast<DataGridViewRow>().Any(r => Convert.ToBoolean(r.Cells["Primary"].Value))) row.Cells["Primary"].Value = true;
                    if (e.ColumnIndex == 1 && Convert.ToBoolean(row.Cells["Primary"].Value))
                    {
                        row.Cells["Enabled"].Value = true;
                        foreach (DataGridViewRow other in grid.Rows) if (other != row) other.Cells["Primary"].Value = false;
                    }
                    if (e.ColumnIndex == 0)
                    {
                        if (!Convert.ToBoolean(row.Cells["Enabled"].Value)) row.Cells["Primary"].Value = false;
                        // Keep the current main if it is still on; otherwise the first enabled row in the table becomes main.
                        var on = grid.Rows.Cast<DataGridViewRow>().Where(r => Convert.ToBoolean(r.Cells["Enabled"].Value)).ToList();
                        if (on.Count > 0 && !on.Any(r => Convert.ToBoolean(r.Cells["Primary"].Value))) on[0].Cells["Primary"].Value = true;
                    }
                    if (e.ColumnIndex == 5)
                    {
                        var previous = (DisplaySetting)row.Tag;
                        int next = Array.IndexOf(Rotations, Convert.ToString(row.Cells["Rotation"].Value));
                        var d = ParseRow(row);
                        if (previous.Rotation % 2 != next % 2) { int w = d.Width; d.Width = d.Height; d.Height = w; SetMode(row, d); }
                        previous.Rotation = next;
                    }
                }
                finally { loading = false; }
                UpdatePreview();
                if (grid.Columns[e.ColumnIndex].Name != "Label") { MarkEdited(); SetStatus("Edited layout — apply to change Windows, or save it as a preset."); }
                else
                {
                    UpdateNameButtons();
                    if (NameChanged(grid.Rows[e.RowIndex])) SetStatus("Name changed. Click Save monitor names to keep it, or Undo name changes.");
                }
            };
            grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; SetStatus("Choose a listed value or enter whole-number positions."); };
        }
        private void RefreshMonitors()
        {
            monitors = DisplayService.Scan();
            foreach (var m in monitors) { string label; if (preferences.Labels.TryGetValue(m.Id, out label)) m.Current.Label = label; }
            Populate(monitors.Select(m => m.Current));
            presets.SelectedIndex = -1; ShowSource("Current Windows layout", false); preview.Connected = monitors.Count;
            SetStatus(monitors.Count == 0 ? "No monitors are visible to this session. Run the app directly on your Windows desktop." : "");
        }
        private static string ModeText(DisplaySetting d) { return d.Width + " × " + d.Height + " @ " + d.Hertz + " Hz"; }
        private void SetMode(DataGridViewRow row, DisplaySetting d)
        {
            var cell = (DataGridViewComboBoxCell)row.Cells["Mode"];
            string text = ModeText(d); if (!cell.Items.Contains(text)) cell.Items.Add(text); cell.Value = text;
        }
        private void Populate(IEnumerable<DisplaySetting> values)
        {
            undoArrangement = null; if (undoButton != null) undoButton.Enabled = false;
            // Unsaved names survive a refresh, a preset load and an apply; only Save monitor names or Undo name changes clears them.
            var pendingNames = new Dictionary<string, string>();
            foreach (DataGridViewRow row in grid.Rows) if (NameChanged(row)) pendingNames[((DisplaySetting)row.Tag).Id] = CellName(row);
            loading = true;
            try
            {
                grid.Rows.Clear();
                foreach (var original in values)
                {
                    var d = original.Copy();
                    var m = monitors.FirstOrDefault(v => v.Id == d.Id);
                    string label; if (preferences.Labels.TryGetValue(d.Id, out label)) d.Label = label;
                    int index = grid.Rows.Add(); var row = grid.Rows[index]; row.Tag = d;
                    row.Cells["Enabled"].Value = d.Enabled; row.Cells["Primary"].Value = d.Primary;
                    string pending; row.Cells["Label"].Value = pendingNames.TryGetValue(d.Id, out pending) ? pending : d.Label;
                    row.Cells["Label"].ToolTipText = "Click to rename. Esc cancels typing.";
                    row.Cells["Model"].Value = m == null ? "Unavailable" : m.Model;
                    var modeCell = (DataGridViewComboBoxCell)row.Cells["Mode"];
                    if (m != null) foreach (var item in m.Modes.OrderByDescending(v => v.Width).ThenByDescending(v => v.Height).ThenByDescending(v => v.Hertz).Select(ModeText).Distinct()) modeCell.Items.Add(item);
                    SetMode(row, d); row.Cells["Rotation"].Value = Rotations[Math.Max(0, Math.Min(3, d.Rotation))];
                    row.Cells["X"].Value = d.X; row.Cells["Y"].Value = d.Y;
                    row.Cells["Model"].ToolTipText = m == null ? "This display is currently unavailable to Windows." : m.IdentityKind + "\n" + m.Id + (m.Ambiguous ? "\nAmbiguous identity: applying is blocked." : "");
                    if (m == null) row.DefaultCellStyle.ForeColor = Muted;
                }
            }
            finally { loading = false; }
            UpdatePreview(); UpdateNameButtons();
        }
        // The row's Tag holds the saved name; the cell holds what is typed. They differ while a name is unsaved.
        private static string CellName(DataGridViewRow row) { string name = Convert.ToString(row.Cells["Label"].Value).Trim(); return name.Length == 0 ? "Monitor" : name; }
        private static bool NameChanged(DataGridViewRow row) { return row.Tag != null && CellName(row) != ((DisplaySetting)row.Tag).Label; }
        private void UpdateNameButtons()
        {
            bool changed = grid.Rows.Cast<DataGridViewRow>().Any(NameChanged);
            if (saveNamesButton != null) { saveNamesButton.Enabled = changed; revertNamesButton.Enabled = changed; }
            grid.InvalidateColumn(grid.Columns["Label"].Index);
        }
        private DisplaySetting ParseRow(DataGridViewRow row)
        {
            var d = ((DisplaySetting)row.Tag).Copy();
            d.Label = CellName(row);
            d.Enabled = Convert.ToBoolean(row.Cells["Enabled"].Value); d.Primary = Convert.ToBoolean(row.Cells["Primary"].Value);
            var parts = Convert.ToString(row.Cells["Mode"].Value).Split(new[] { ' ', '×', '@' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !Int32.TryParse(parts[0], out d.Width) || !Int32.TryParse(parts[1], out d.Height) || !Int32.TryParse(parts[2], out d.Hertz)) throw new InvalidOperationException("Choose a valid display mode for " + d.Label);
            if (d.Hertz != ((DisplaySetting)row.Tag).Hertz) { d.RefreshNumerator = 0; d.RefreshDenominator = 0; }
            d.Rotation = Array.IndexOf(Rotations, Convert.ToString(row.Cells["Rotation"].Value));
            if (!Int32.TryParse(Convert.ToString(row.Cells["X"].Value), out d.X) || !Int32.TryParse(Convert.ToString(row.Cells["Y"].Value), out d.Y)) throw new InvalidOperationException("Display positions must be whole numbers.");
            return d;
        }
        private List<DisplaySetting> ReadGrid() { grid.EndEdit(); return grid.Rows.Cast<DataGridViewRow>().Select(ParseRow).ToList(); }
        private void UpdatePreview() { try { preview.Displays = ReadGrid(); preview.Invalidate(); } catch { } }
        private void SetPositions(List<DisplaySetting> values)
        {
            loading = true;
            try
            {
                foreach (DataGridViewRow row in grid.Rows)
                {
                    var position = values.FirstOrDefault(d => d.Id == ((DisplaySetting)row.Tag).Id);
                    if (position != null) { row.Cells["X"].Value = position.X; row.Cells["Y"].Value = position.Y; }
                }
            }
            finally { loading = false; }
            UpdatePreview();
        }
        private void UndoMove()
        {
            if (undoArrangement == null) return;
            SetPositions(undoArrangement); undoArrangement = null; undoButton.Enabled = false;
            SetStatus("Previous arrangement restored in the editor.");
        }
        private void SaveNames()
        {
            grid.EndEdit();
            foreach (DataGridViewRow row in grid.Rows) { var d = (DisplaySetting)row.Tag; d.Label = CellName(row); preferences.Labels[d.Id] = d.Label; }
            Store.Write(Store.ConfigPath, preferences); UpdatePreview(); UpdateNameButtons();
        }
        private void RevertNames()
        {
            grid.CancelEdit(); grid.EndEdit();
            loading = true;
            try { foreach (DataGridViewRow row in grid.Rows) row.Cells["Label"].Value = ((DisplaySetting)row.Tag).Label; }
            finally { loading = false; }
            UpdatePreview(); UpdateNameButtons(); SetStatus("Names restored to the last saved names.");
        }
        private void LoadPreset()
        {
            var p = presets.SelectedItem as Preset;
            if (p == null) return;
            Populate(p.Displays); ShowSource(p.Name, false); SetStatus("Loaded “" + p.Name + "”. Click Apply layout to switch to it. Windows has not changed yet.");
        }
        private void SavePreset()
        {
            var values = LayoutRules.Normalize(ReadGrid());
            // When editing a preset, start from its name and shortcut so updating it is one click.
            var current = preferences.Presets.FirstOrDefault(p => p.Name == source);
            using (var dialog = new SavePresetForm(current))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string name = dialog.PresetName;
                var existing = preferences.Presets.FirstOrDefault(p => String.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (existing != null && MessageBox.Show(this, "Replace the saved preset “" + existing.Name + "”?", "Replace preset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                if (dialog.Shortcut > 0 && preferences.Presets.Any(p => p != existing && p.Shortcut == dialog.Shortcut)) throw new InvalidOperationException("That shortcut is already assigned to another preset.");
                var pnew = new Preset { Name = name, Shortcut = dialog.Shortcut, Displays = values };
                if (existing != null) preferences.Presets[preferences.Presets.IndexOf(existing)] = pnew; else preferences.Presets.Add(pnew);
                Store.Write(Store.ConfigPath, preferences); ReloadPresets(); presets.SelectedItem = pnew; ShowSource(pnew.Name, false); SetStatus("Saved “" + name + "”. Available in the tray while Monitor Control is running.");
            }
        }
        private void DeletePreset()
        {
            var p = presets.SelectedItem as Preset; if (p == null) return;
            if (MessageBox.Show(this, "Delete “" + p.Name + "”?", "Delete preset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            preferences.Presets.Remove(p); Store.Write(Store.ConfigPath, preferences); ReloadPresets();
            // The table still holds the deleted preset's values; they now belong to no saved preset.
            if (p.Name == source) ShowSource("Unsaved layout", edited);
            SetStatus("Deleted “" + p.Name + "”.");
        }
        private void ReloadPresets()
        {
            // No preset is selected until the user picks one, because at startup and after a refresh the table shows the live layout.
            presets.Items.Clear(); presets.Items.AddRange(preferences.Presets.Cast<object>().ToArray()); presets.SelectedIndex = -1;
            presets.Placeholder = presets.Items.Count == 0 ? "No presets yet" : "Choose a preset…"; presets.Invalidate();
            foreach (int id in hotkeys) Native.UnregisterHotKey(Handle, id); hotkeys.Clear();
            var menu = new ContextMenuStrip(); menu.Items.Add("Open Monitor Control", null, delegate { OpenWindow(); }); menu.Items.Add(new ToolStripSeparator());
            for (int i = 0; i < preferences.Presets.Count; i++)
            {
                var preset = preferences.Presets[i];
                menu.Items.Add(preset.ToString(), null, delegate { if (!busy) Run(delegate { Apply(preset.Displays); }); });
                if (preset.Shortcut >= 1 && preset.Shortcut <= 9)
                {
                    int id = i + 100;
                    if (Native.RegisterHotKey(Handle, id, 0x4003, (uint)(0x30 + preset.Shortcut))) hotkeys.Add(id);
                    else { Store.Log("Shortcut unavailable: " + preset); tray.ShowBalloonTip(3000, "Shortcut unavailable", preset.ToString() + " is in use by another application. The tray action still works.", ToolTipIcon.Warning); }
                }
            }
            menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Help && about", null, delegate { OpenWindow(); ShowHelp(); }); menu.Items.Add("Exit", null, delegate { if (!busy) { exiting = true; Close(); } });
            var old = tray.ContextMenuStrip; tray.ContextMenuStrip = menu; if (old != null) old.Dispose();
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && !busy)
            {
                int index = m.WParam.ToInt32() - 100;
                if (index >= 0 && index < preferences.Presets.Count) Run(delegate { Apply(preferences.Presets[index].Displays); });
            }
            if (m.Msg == 0x007e && !busy) SetStatus("Windows display configuration changed. Refresh to load its current state; your edits are preserved.");
            base.WndProc(ref m);
        }
        private void Apply(List<DisplaySetting> values)
        {
            if (busy) return;
            var desired = LayoutRules.Normalize(values);
            var live = DisplayService.Scan(); LayoutRules.CheckTargets(desired, live);
            // A valid snapshot is mandatory before starting a transaction.
            var before = LayoutRules.Normalize(live.Select(m => m.Current));
            if (Directory.Exists(Store.Root) && Directory.GetFiles(Store.Root, "pending-*.json*").Any(p => p.EndsWith(".json") || p.EndsWith(".restoring")))
                throw new InvalidOperationException("A previous change is awaiting recovery. Wait a few seconds and try again.");
            busy = true; string recoveryPath = null;
            try
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(35);
                recoveryPath = RecoveryService.Arm(new Recovery { DeadlineUtc = deadline, Displays = before });
                using (var mutex = new Mutex(false, @"Local\MonitorControl.DisplayChange"))
                {
                    RecoveryService.Acquire(mutex);
                    try { DisplayService.Apply(desired); }
                    finally { mutex.ReleaseMutex(); }
                }
                Store.Write(Path.Combine(Store.Root, "last-layout.json"), before);
                // ConfirmForm centers itself on the resulting primary display.
                using (var confirm = new ConfirmForm(deadline))
                {
                    var answer = confirm.ShowDialog();
                    if (confirm.WindowsShuttingDown)
                    {
                        // Windows refuses display changes while it shuts down or signs out, so leave the change pending.
                        // The recovery process restores it if the shutdown is cancelled; otherwise the next sign-in does.
                        Store.Log("Windows is shutting down or signing out during the countdown. The previous layout will be restored automatically.");
                        return;
                    }
                    bool keep = answer == DialogResult.OK && RecoveryService.Keep(recoveryPath);
                    // Names are not saved here: only Save monitor names keeps them, so an accidental rename never sticks.
                    if (!keep) RecoveryService.Restore(recoveryPath);
                    RefreshMonitors();
                    SetStatus(keep ? "Layout applied and confirmed." : "Reverting to your previous layout. Refresh if recovery is still finishing.");
                }
            }
            catch (Exception ex)
            {
                if (recoveryPath != null)
                {
                    try { RecoveryService.Restore(recoveryPath); }
                    catch (DesktopUnavailableException) { throw new InvalidOperationException(ex.Message + "\n\nWindows is not accepting display changes right now. Your previous layout will be restored as soon as it does."); }
                    catch (Exception rollback) { throw new InvalidOperationException(ex.Message + "\n\n" + rollback.Message); }
                }
                throw;
            }
            finally { busy = false; }
        }
        private void Identify()
        {
            foreach (var m in DisplayService.Scan().Where(m => m.Current.Enabled))
            {
                string label; if (!preferences.Labels.TryGetValue(m.Id, out label)) label = m.Model;
                var popup = new Form { FormBorderStyle = FormBorderStyle.None, BackColor = Accent, ForeColor = Color.White, Size = new Size(380, 150), TopMost = true, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual };
                popup.Location = new Point(m.Current.X + 40, m.Current.Y + 40);
                popup.Controls.Add(new Label { Text = label + "\n" + m.Model, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 22) });
                var timer = new System.Windows.Forms.Timer { Interval = 3500 }; timer.Tick += delegate { timer.Stop(); popup.Close(); timer.Dispose(); }; popup.Show(); timer.Start();
            }
        }
        private const int InfoWidth = 30;
        private bool OnInfo(DataGridViewCellMouseEventArgs e) { return e.X >= grid.Columns[e.ColumnIndex].Width - InfoWidth; }
        private void HardwareDetails(DataGridViewRow row)
        {
            var id = ((DisplaySetting)row.Tag).Id;
            var monitor = monitors.FirstOrDefault(m => m.Id == id);
            string text = monitor == null ? "Saved identity: " + id + "\r\nCurrently unavailable." : monitor.Model + "\r\n\r\n" + monitor.IdentityKind + "\r\nSerial: " + (monitor.Serial.Length == 0 ? "Not reported" : monitor.Serial) + "\r\n\r\nSaved identity:\r\n" + monitor.Id + "\r\n\r\nCurrent Windows address (not saved in presets):\r\n" + monitor.Device + "\r\n\r\nInterface:\r\n" + monitor.InterfacePath + (monitor.Ambiguous ? "\r\n\r\nThis identity/source is ambiguous. Applying is blocked to avoid targeting the wrong display." : "");
            using (var dialog = new Form { Text = "Monitor hardware details", Size = new Size(660, 390), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false })
            {
                dialog.Controls.Add(new TextBox { Text = text, Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 11), BackColor = Color.White }); dialog.ShowDialog(this);
            }
        }
        internal void RenderForTest(string path)
        {
            ShowInTaskbar = false; Opacity = 0; Show(); Application.DoEvents();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
            // Exercise the same editable grid and preview used by people, without applying a change.
            foreach (DataGridViewRow row in grid.Rows) row.Cells["Enabled"].Value = true;
            if (grid.Rows.Count > 0) grid.Rows[grid.Rows.Count - 1].Cells["Primary"].Value = true;
            Populate(LayoutRules.Row(ReadGrid()));
            var draft = LayoutRules.Normalize(ReadGrid());
            if (draft.Count(d => d.Primary) != 1 || draft.Any(d => !d.Enabled)) throw new InvalidOperationException("UI smoke test failed.");
            if (draft.Count > 1)
            {
                var last = ReadGrid().Last(); var first = ReadGrid().First();
                var target = new Point(first.X, first.Y - last.Height);
                preview.SimulateDrag(last.Id, target, false);
                var placed = ReadGrid().Single(d => d.Id == last.Id);
                if (placed.X != target.X || placed.Y != target.Y) throw new InvalidOperationException("Drag-to-grid integration failed: expected " + target + ", got " + placed.X + ", " + placed.Y);
                LayoutRules.Normalize(ReadGrid());
                preview.SimulateDrag(last.Id, new Point(first.X - last.Width, first.Y), true);
                placed = ReadGrid().Single(d => d.Id == last.Id);
                if (placed.X != target.X || placed.Y != target.Y) throw new InvalidOperationException("Escape did not cancel the drag.");
                UndoMove();
                placed = ReadGrid().Single(d => d.Id == last.Id);
                if (placed.X != last.X || placed.Y != last.Y) throw new InvalidOperationException("Undo move failed.");
                preview.SimulateDrag(last.Id, target, false);
            }
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.ChangeExtension(path, ".edited.png"), System.Drawing.Imaging.ImageFormat.Png); }
            Close();
        }
    }

    // A monitor-name cell. The grid normally stretches its edit box over the whole cell, which hid the drawn field.
    // Here the edit box is placed inside the field's border, so the field keeps its shape while the user types.
    internal class NameCell : DataGridViewTextBoxCell
    {
        public static RectangleF Field(Rectangle cell) { return new RectangleF(cell.Left + 6.5f, cell.Top + 6.5f, cell.Width - 14, cell.Height - 14); }
        public override void PositionEditingControl(bool setLocation, bool setSize, Rectangle cellBounds, Rectangle cellClip, DataGridViewCellStyle cellStyle, bool singleVerticalBorderAdded, bool singleHorizontalBorderAdded, bool isFirstDisplayedColumn, bool isFirstDisplayedRow)
        {
            var field = Field(cellBounds);
            // Inset past the border; leave room on the right so the pencil stays visible.
            var inner = new Rectangle((int)field.Left + 6, (int)field.Top + 3, (int)field.Width - 36, (int)field.Height - 5);
            base.PositionEditingControl(setLocation, setSize, inner, Rectangle.Intersect(inner, cellClip), cellStyle, singleVerticalBorderAdded, singleHorizontalBorderAdded, isFirstDisplayedColumn, isFirstDisplayedRow);
        }
    }
    internal class SavePresetForm : Form
    {
        private TextBox name = new TextBox(); private FlatCombo shortcut = new FlatCombo();
        public string PresetName { get { return name.Text.Trim(); } }
        public int Shortcut { get { return shortcut.SelectedIndex; } }
        public SavePresetForm(Preset current)
        {
            Text = "Save a custom preset"; ClientSize = new Size(430, 220); FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false; Font = new Font("Segoe UI", 10);
            Controls.Add(new Label { Text = "Preset name", Location = new Point(24, 20), AutoSize = true }); name.SetBounds(24, 48, 382, 28); Controls.Add(name);
            Controls.Add(new Label { Text = "Global shortcut (while the app is running)", Location = new Point(24, 92), AutoSize = true });
            shortcut.SetBounds(24, 120, 240, 30); shortcut.DropDownStyle = ComboBoxStyle.DropDownList; shortcut.Items.Add("None"); for (int i = 1; i <= 9; i++) shortcut.Items.Add("Ctrl + Alt + " + i); shortcut.SelectedIndex = 0; Controls.Add(shortcut);
            if (current != null) { name.Text = current.Name; shortcut.SelectedIndex = current.Shortcut; }
            var save = Ui.MakeButton("Save preset", true); save.AutoSize = false; save.Location = new Point(286, 170); save.Size = new Size(120, 34);
            save.Click += delegate { if (PresetName.Length == 0 || PresetName.Length > 80) { MessageBox.Show(this, "Enter a name between 1 and 80 characters."); return; } DialogResult = DialogResult.OK; };
            Controls.Add(save); AcceptButton = save;
        }
    }
    internal class ConfirmForm : Form
    {
        private Label countdown = new Label(); private System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        // WinForms closes a modal dialog as Cancel when Windows asks to shut down; that must not count as Revert.
        public bool WindowsShuttingDown { get; private set; }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0011) WindowsShuttingDown = true; // WM_QUERYENDSESSION
            base.WndProc(ref m);
        }
        public ConfirmForm(DateTime deadline)
        {
            Text = "Keep this display layout?"; ClientSize = new Size(490, 195); FormBorderStyle = FormBorderStyle.FixedDialog; TopMost = true; MaximizeBox = false; MinimizeBox = false; Font = new Font("Segoe UI", 11); BackColor = Color.White;
            Controls.Add(new Label { Text = "Can you see and use your displays?", Font = new Font("Segoe UI Semibold", 16), Location = new Point(24, 22), AutoSize = true });
            countdown.SetBounds(24, 69, 440, 52); Controls.Add(countdown);
            var revert = Ui.MakeButton("Revert", false); revert.AutoSize = false; revert.Location = new Point(202, 133); revert.Size = new Size(115, 38); revert.DialogResult = DialogResult.Cancel;
            var keep = Ui.MakeButton("Keep changes", true); keep.AutoSize = false; keep.Location = new Point(329, 133); keep.Size = new Size(137, 38); keep.DialogResult = DialogResult.OK;
            Controls.Add(revert); Controls.Add(keep); CancelButton = revert; ActiveControl = revert;
            Action tick = delegate { int left = Math.Max(0, (int)Math.Ceiling((deadline - DateTime.UtcNow).TotalSeconds)); countdown.Text = "Your previous layout will return automatically in " + left + " seconds."; if (left <= 0) { DialogResult = DialogResult.Cancel; Close(); } };
            // The primary display always contains (0, 0). Query it fresh: Screen.PrimaryScreen and the work area can still describe the old layout right after a change.
            StartPosition = FormStartPosition.Manual;
            Action center = delegate { var area = Screen.FromPoint(Point.Empty).Bounds; Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2); };
            Load += delegate { center(); };
            timer.Interval = 200; timer.Tick += delegate { tick(); }; Shown += delegate { center(); tick(); timer.Start(); }; FormClosed += delegate { timer.Dispose(); };
        }
    }
}
