using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace MonitorControl
{
    // Project-wide facts shown in the Help window. Update these before publishing.
    internal static class AppInfo
    {
        public const string Name = "Monitor Control";
        public const string Version = "1.0.0";
        public const string RepoUrl = "https://github.com/coffeerunsmycode/monitor-control";
        public const string IssuesUrl = RepoUrl + "/issues";

        // Opens https web pages only. Windows runs whatever program another kind of link (file:, http:, a custom app link) names.
        public static void Open(IWin32Window owner, string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps) { MessageBox.Show(owner, "Only https:// web links can be opened from the app.", Name); return; }
            try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(owner, ex.Message, Name); }
        }
    }

    internal static class Ui
    {
        public static readonly Color Ink = Color.FromArgb(27, 40, 58), Accent = Color.FromArgb(34, 93, 217), AccentHover = Color.FromArgb(26, 78, 190),
            Muted = Color.FromArgb(94, 111, 131), Border = Color.FromArgb(211, 219, 231), Hover = Color.FromArgb(238, 243, 252), Selected = Color.FromArgb(222, 233, 255);

        // A downward chevron drawn as a vector, so it stays crisp at every DPI and needs no image files.
        public static void DrawChevron(Graphics g, Rectangle area, Color color)
        {
            var old = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = area.Left + area.Width / 2f, cy = area.Top + area.Height / 2f;
            using (var pen = new Pen(color, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
            g.SmoothingMode = old;
        }
        // A small slanted pencil, the usual "you can edit this" sign. Vector-drawn like the chevron.
        public static void DrawPencil(Graphics g, Rectangle area, Color color)
        {
            var old = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = area.Left + area.Width / 2f, cy = area.Top + area.Height / 2f;
            var tip = new PointF(cx - 5, cy + 5); PointF a = new PointF(cx - 1.5f, cy + 4.3f), b = new PointF(cx - 4.3f, cy + 1.5f);
            using (var pen = new Pen(color, 1.4f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawPolygon(pen, new[] { tip, a, new PointF(cx + 4.9f, cy - 2.1f), new PointF(cx + 2.1f, cy - 4.9f), b });
                g.DrawLine(pen, a, b);
            }
            g.SmoothingMode = old;
        }
        // A circled "i", the usual "more details" sign. Vector-drawn like the chevron.
        public static void DrawInfo(Graphics g, Rectangle area, Color color)
        {
            var old = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = area.Left + area.Width / 2f, cy = area.Top + area.Height / 2f;
            using (var pen = new Pen(color, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawEllipse(pen, cx - 8, cy - 8, 16, 16);
                g.DrawLine(pen, cx, cy - 1, cx, cy + 4);
            }
            using (var dot = new SolidBrush(color)) g.FillEllipse(dot, cx - 1.1f, cy - 4.6f, 2.2f, 2.2f);
            g.SmoothingMode = old;
        }
        public static void DrawComboItem(ComboBox box, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool hot = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (var back = new SolidBrush(hot ? Selected : Color.White)) e.Graphics.FillRectangle(back, e.Bounds);
            var bounds = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top, e.Bounds.Width - 12, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, box.GetItemText(box.Items[e.Index]), box.Font, bounds, Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
        public static Button MakeButton(string text, bool primary)
        {
            return new RoundButton(primary) { Text = text, AutoSize = true, Height = 34, Padding = new Padding(9, 3, 9, 3), Margin = new Padding(0, 0, 8, 0) };
        }
        // Windows 11 (Fluent) uses a 4 px corner radius for buttons, text boxes and drop-downs; scaled for the screen's DPI.
        public static float CornerRadius(Graphics g) { return 4f * g.DpiX / 96f; }
        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = radius * 2; var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
    }

    // A button with rounded corners. Plain colour states for hover, press, focus and disabled; no animation.
    internal class RoundButton : Button
    {
        private readonly bool primary; private bool hot, down;
        public RoundButton(bool primary)
        {
            this.primary = primary; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false; Cursor = Cursors.Hand;
            ForeColor = primary ? Color.White : Ui.Ink;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; down = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); hot = false; down = false; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Parent == null ? Color.White : Parent.BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill, border, text;
            if (!Enabled) { fill = primary ? Color.FromArgb(158, 182, 232) : Color.FromArgb(243, 245, 249); border = primary ? fill : Color.FromArgb(225, 230, 238); text = primary ? Color.White : Color.FromArgb(150, 161, 176); }
            else if (primary) { fill = down ? Color.FromArgb(22, 66, 165) : hot ? Ui.AccentHover : Ui.Accent; border = fill; text = Color.White; }
            else { fill = down ? Ui.Selected : hot ? Ui.Hover : Color.White; border = hot || down ? Color.FromArgb(160, 178, 205) : Ui.Border; text = Ui.Ink; }
            float radius = Ui.CornerRadius(g);
            using (var shape = Ui.RoundedRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), radius))
            using (var brush = new SolidBrush(fill)) using (var pen = new Pen(border)) { g.FillPath(brush, shape); g.DrawPath(pen, shape); }
            // Keyboard focus: an inner ring, as in Windows 11.
            if (Focused && ShowFocusCues)
                using (var ring = Ui.RoundedRect(new RectangleF(2.5f, 2.5f, Width - 5.5f, Height - 5.5f), Math.Max(1f, radius - 1.5f))) using (var pen = new Pen(primary ? Color.White : Ui.Accent, 1.5f)) g.DrawPath(pen, ring);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, text, fill, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    // Round "?" help button, drawn as a vector so it needs no image file and stays sharp at any DPI.
    internal class HelpButton : Button
    {
        private bool hot, down;
        public HelpButton()
        {
            Size = new Size(36, 36); FlatStyle = FlatStyle.Flat; Cursor = Cursors.Hand; Text = ""; UseVisualStyleBackColor = false;
            FlatAppearance.BorderSize = 0; SetStyle(ControlStyles.Selectable, true);
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; down = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); down = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Parent == null ? Color.White : Parent.BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var circle = new RectangleF(3, 3, Width - 7, Height - 7);
            bool filled = hot || down;
            using (var fill = new SolidBrush(filled ? Ui.Accent : Color.White)) g.FillEllipse(fill, circle);
            using (var pen = new Pen(Ui.Accent, 1.6f)) g.DrawEllipse(pen, circle);
            using (var font = new Font("Segoe UI Semibold", 13)) using (var brush = new SolidBrush(filled ? Color.White : Ui.Accent))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString("?", font, brush, new RectangleF(circle.Left, circle.Top - 1, circle.Width, circle.Height), format);
            if (Focused && ShowFocusCues) using (var pen = new Pen(Ui.Accent) { DashStyle = DashStyle.Dot }) g.DrawEllipse(pen, 0.5f, 0.5f, Width - 2, Height - 2);
        }
    }

    // A flat, modern drop-down list: white field, thin border, vector chevron, custom item rows.
    internal class FlatCombo : ComboBox
    {
        private bool hot;
        // Grey hint shown while nothing is selected, e.g. "Choose a preset…".
        public string Placeholder = "";
        public FlatCombo()
        {
            DropDownStyle = ComboBoxStyle.DropDownList; DrawMode = DrawMode.OwnerDrawFixed; FlatStyle = FlatStyle.Flat; ItemHeight = 26; BackColor = Color.White; ForeColor = Ui.Ink;
        }
        protected override void OnDrawItem(DrawItemEventArgs e) { Ui.DrawComboItem(this, e); }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; Invalidate(); }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x000F && IsHandleCreated) // WM_PAINT: cover the system-drawn field and arrow.
                using (var g = Graphics.FromHwnd(Handle)) PaintField(g);
        }
        private void PaintField(Graphics g)
        {
            var r = ClientRectangle; if (r.Width < 20 || r.Height < 10) return;
            Color field = Enabled ? Color.White : Color.FromArgb(243, 245, 249);
            using (var outside = new SolidBrush(Parent == null ? Color.White : Parent.BackColor)) g.FillRectangle(outside, r);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = Ui.RoundedRect(new RectangleF(0.5f, 0.5f, r.Width - 1.5f, r.Height - 1.5f), Ui.CornerRadius(g)))
            using (var back = new SolidBrush(field)) using (var pen = new Pen(Focused || DroppedDown ? Ui.Accent : hot ? Color.FromArgb(160, 178, 205) : Ui.Border))
            { g.FillPath(back, shape); g.DrawPath(pen, shape); }
            string text = SelectedItem == null ? Placeholder : GetItemText(SelectedItem);
            TextRenderer.DrawText(g, text, Font, new Rectangle(8, 0, r.Width - 34, r.Height), Enabled && SelectedItem != null ? Ui.Ink : Ui.Muted, field, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            Ui.DrawChevron(g, new Rectangle(r.Width - 28, 0, 20, r.Height), Ui.Muted);
        }
    }

    // The app icon is drawn in code (two monitors on a blue tile), so there is no binary asset to license or maintain.
    internal static class AppIcon
    {
        public static Bitmap Draw(int size)
        {
            var bitmap = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
                float s = size / 64f;
                using (var tile = Round(new RectangleF(2 * s, 2 * s, 60 * s, 60 * s), 14 * s)) using (var fill = new LinearGradientBrush(new RectangleF(0, 0, size, size), Color.FromArgb(58, 110, 224), Color.FromArgb(30, 64, 150), 90f)) g.FillPath(fill, tile);
                using (var white = new SolidBrush(Color.White)) using (var soft = new SolidBrush(Color.FromArgb(190, 214, 255)))
                {
                    using (var a = Round(new RectangleF(10 * s, 16 * s, 28 * s, 20 * s), 3 * s)) g.FillPath(soft, a);
                    using (var b = Round(new RectangleF(24 * s, 26 * s, 30 * s, 22 * s), 3 * s)) g.FillPath(white, b);
                    g.FillRectangle(white, 36 * s, 48 * s, 6 * s, 4 * s); g.FillRectangle(white, 30 * s, 52 * s, 18 * s, 3 * s);
                }
            }
            return bitmap;
        }
        private static GraphicsPath Round(RectangleF r, float radius) { return Ui.RoundedRect(r, radius); }
        public static Icon Create(int size)
        {
            using (var bitmap = Draw(size)) { IntPtr handle = bitmap.GetHicon(); try { using (var icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); } finally { Native.DestroyIcon(handle); } }
        }
        // Writes assets\app.ico (PNG-compressed frames). Run once with: MonitorControl.exe --make-icon <path>
        public static void WriteIco(string path)
        {
            int[] sizes = { 16, 24, 32, 48, 64, 256 }; var frames = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++)
                using (var bitmap = Draw(sizes[i])) using (var stream = new MemoryStream()) { bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png); frames[i] = stream.ToArray(); }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using (var file = File.Create(path)) using (var w = new BinaryWriter(file))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32); w.Write(frames[i].Length); w.Write(offset); offset += frames[i].Length;
                }
                foreach (var frame in frames) w.Write(frame);
            }
        }
    }
}
