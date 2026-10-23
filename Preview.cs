using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace MonitorControl
{
    internal class ArrangementEventArgs : EventArgs
    {
        public List<DisplaySetting> Before, After;
    }
    internal class Preview : Control
    {
        private List<DisplaySetting> displays = new List<DisplaySetting>();
        private List<DisplaySetting> before;
        private string selectedId, hoverId, dragId;
        private Point mouseStart, originalPosition;
        private float scale = 1, offsetX, offsetY;
        private bool moved, snapped;
        private string hint = "Drag a display to arrange your desk. Edges snap together.";
        public event EventHandler<ArrangementEventArgs> ArrangementChanged;
        public event EventHandler SelectionChanged;
        public List<DisplaySetting> Displays
        {
            get { return displays; }
            set { CancelDrag(); displays = value.Select(d => d.Copy()).ToList(); if (!displays.Any(d => d.Id == selectedId)) selectedId = null; Fit(); Invalidate(); }
        }
        public string SelectedId { get { return selectedId; } set { selectedId = value; Invalidate(); } }
        // Header text, set by the main window: what the table holds, whether it was edited, and how many monitors Windows sees.
        private string heading = "Current Windows layout"; private bool edited; private int connected;
        public string Heading { get { return heading; } set { heading = value; Invalidate(); } }
        public bool Edited { get { return edited; } set { edited = value; Invalidate(); } }
        public int Connected { get { return connected; } set { connected = value; Invalidate(); } }
        public Preview()
        {
            SetStyle(ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true; TabStop = true; BackColor = Color.FromArgb(19, 28, 44); Font = new Font("Segoe UI", 10);
        }
        private List<DisplaySetting> EnabledDisplays() { return displays.Where(d => d.Enabled && d.Width > 0 && d.Height > 0).ToList(); }
        private void Fit()
        {
            if (dragId != null) return; // Freeze the transform until release, so tiles don't chase the pointer.
            var enabled = EnabledDisplays(); if (enabled.Count == 0) return;
            float left = enabled.Min(d => (float)d.X), top = enabled.Min(d => (float)d.Y);
            float width = enabled.Max(d => (float)d.X + d.Width) - left, height = enabled.Max(d => (float)d.Y + d.Height) - top;
            scale = Math.Max(0.001f, Math.Min(Math.Max(30, Width - 160) / width, Math.Max(30, Height - 160) / height));
            offsetX = (Width - width * scale) / 2 - left * scale;
            offsetY = 62 + (Math.Max(30, Height - 160) - height * scale) / 2 - top * scale;
        }
        private RectangleF BoundsFor(DisplaySetting d) { return new RectangleF(offsetX + d.X * scale, offsetY + d.Y * scale, d.Width * scale, d.Height * scale); }
        private string Hit(Point point)
        {
            return EnabledDisplays().OrderBy(d => d.Id == selectedId ? 1 : 0).Reverse().Where(d => BoundsFor(d).Contains(point)).Select(d => d.Id).FirstOrDefault();
        }
        protected override void OnResize(EventArgs e) { CancelDrag(); base.OnResize(e); Fit(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return;
            Focus(); string hit = Hit(e.Location); if (hit == null) return;
            selectedId = hit;
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            if (EnabledDisplays().Count < 2) { Invalidate(); return; }
            before = displays.Select(v => v.Copy()).ToList(); dragId = hit; mouseStart = e.Location;
            var d = displays.Single(v => v.Id == hit); originalPosition = new Point(d.X, d.Y);
            moved = false; snapped = false; Capture = true; Cursor = Cursors.SizeAll; Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragId == null) { hoverId = Hit(e.Location); Cursor = hoverId == null ? Cursors.Default : Cursors.Hand; Invalidate(); return; }
            if (!moved && Math.Abs(e.X - mouseStart.X) < 3 && Math.Abs(e.Y - mouseStart.Y) < 3) return;
            moved = true;
            var desired = new Point(originalPosition.X + (int)Math.Round((e.X - mouseStart.X) / scale), originalPosition.Y + (int)Math.Round((e.Y - mouseStart.Y) / scale));
            desired.X = Math.Max(-100000, Math.Min(100000, desired.X)); desired.Y = Math.Max(-100000, Math.Min(100000, desired.Y));
            Point position; snapped = LayoutPlacement.TrySnap(before, dragId, desired, (int)Math.Ceiling(12 / scale), false, out position);
            var d = displays.Single(v => v.Id == dragId); d.X = position.X; d.Y = position.Y;
            hint = snapped ? "Edges aligned · release to place · Esc to cancel" : "Release to snap to the nearest valid edge · Esc to cancel";
            Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e); if (e.Button != MouseButtons.Left || dragId == null) return;
            var d = displays.Single(v => v.Id == dragId); Point position = new Point(d.X, d.Y);
            var original = before;
            bool accepted = moved && LayoutPlacement.TrySnap(before, dragId, new Point(d.X, d.Y), (int)Math.Ceiling(12 / scale), true, out position);
            if (accepted) { d.X = position.X; d.Y = position.Y; }
            else displays = original.Select(v => v.Copy()).ToList();
            dragId = null; before = null; Capture = false; Cursor = Cursors.Hand;
            bool changed = accepted && displays.Any(v => { var old = original.Single(o => o.Id == v.Id); return old.X != v.X || old.Y != v.Y; });
            hint = "Drag a display to arrange your desk. Edges snap together.";
            Fit(); Invalidate();
            if (changed && ArrangementChanged != null) ArrangementChanged(this, new ArrangementEventArgs { Before = original, After = displays.Select(v => v.Copy()).ToList() });
        }
        private void CancelDrag()
        {
            if (dragId == null) return;
            displays = before; dragId = null; before = null; Capture = false; Cursor = Cursors.Default;
            hint = "Move cancelled. Your previous arrangement is unchanged."; Fit(); Invalidate();
        }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) CancelDrag(); }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && dragId != null) { CancelDrag(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverId = null; Invalidate(); }
        private static GraphicsPath Rounded(RectangleF r, float radius)
        {
            float size = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath(); path.AddArc(r.Left, r.Top, size, size, 180, 90); path.AddArc(r.Right - size, r.Top, size, size, 270, 90);
            path.AddArc(r.Right - size, r.Bottom - size, size, size, 0, 90); path.AddArc(r.Left, r.Bottom - size, size, size, 90, 90); path.CloseFigure(); return path;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var dot = new SolidBrush(Color.FromArgb(39, 51, 70)))
                for (int y = 64; y < Height - 68; y += 20) for (int x = 20; x < Width; x += 20) g.FillEllipse(dot, x, y, 2, 2);
            using (var white = new SolidBrush(Color.FromArgb(232, 240, 251))) using (var muted = new SolidBrush(Color.FromArgb(151, 169, 193)))
            using (var title = new Font("Segoe UI Semibold", 12)) using (var small = new Font("Segoe UI", 9))
            {
                g.DrawString(heading, title, white, 22, 16);
                if (edited) using (var tag = new SolidBrush(Color.FromArgb(159, 204, 255))) g.DrawString("· edited", title, tag, 22 + g.MeasureString(heading, title).Width - 4, 16);
                string count = connected + " connected";
                g.DrawString(count, small, muted, Width - g.MeasureString(count, small).Width - 24, 20);
                if (EnabledDisplays().Count == 0) g.DrawString("Enable a display below to start arranging your desk.", Font, muted, 24, 90);
                foreach (var d in EnabledDisplays().OrderBy(v => v.Id == selectedId ? 1 : 0)) DrawDisplay(g, d);
                // Bottom lines: drag help, and the displays that are off (they have no tile). Results of an action go to the status bar.
                g.DrawString(hint, small, white, new RectangleF(22, Height - 57, Width - 44, 22));
                string off = String.Join(" · ", displays.Where(d => !d.Enabled).Select(d => d.Label));
                if (off.Length > 0) g.DrawString("Off: " + off, small, muted, new RectangleF(22, Height - 33, Width - 44, 22));
            }
        }
        private void DrawDisplay(Graphics g, DisplaySetting d)
        {
            var rect = BoundsFor(d); rect.Inflate(-3, -3); if (rect.Width < 4 || rect.Height < 4) return;
            bool selected = d.Id == selectedId, hover = d.Id == hoverId;
            var shadow = rect; shadow.Offset(0, 5);
            using (var shape = Rounded(shadow, 9)) using (var brush = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillPath(brush, shape);
            Color top = d.Primary ? Color.FromArgb(58, 110, 224) : Color.FromArgb(62, 80, 108);
            Color bottom = d.Primary ? Color.FromArgb(36, 72, 158) : Color.FromArgb(39, 54, 77);
            using (var shape = Rounded(rect, 9)) using (var fill = new LinearGradientBrush(rect, top, bottom, 90))
            using (var pen = new Pen(d.Id == dragId && snapped ? Color.FromArgb(94, 234, 192) : selected ? Color.FromArgb(159, 204, 255) : hover ? Color.FromArgb(124, 153, 194) : Color.FromArgb(83, 107, 142), selected ? 2 : 1))
            { g.FillPath(fill, shape); g.DrawPath(pen, shape); }
            using (var white = new SolidBrush(Color.White)) using (var muted = new SolidBrush(Color.FromArgb(197, 214, 240)))
            using (var label = new Font("Segoe UI Semibold", rect.Height < 70 ? 9 : 11)) using (var small = new Font("Segoe UI", 8))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                var text = rect; text.Inflate(-8, 0);
                if (rect.Height >= 95)
                {
                    g.DrawString(d.Primary ? "MAIN DISPLAY" : "DISPLAY", small, muted, new RectangleF(text.X, rect.Top + 10, text.Width, 18), format);
                    g.DrawString(d.Label, label, white, new RectangleF(text.X, rect.Top + rect.Height / 2 - 13, text.Width, 25), format);
                    g.DrawString(d.Width + " × " + d.Height, small, muted, new RectangleF(text.X, rect.Bottom - 30, text.Width, 18), format);
                }
                else
                {
                    g.DrawString(d.Label, label, white, new RectangleF(text.X, rect.Top + 3, text.Width, Math.Max(14, rect.Height / 2)), format);
                    if (rect.Height > 36) g.DrawString(d.Width + " × " + d.Height + (d.Primary ? " · MAIN" : ""), small, muted, new RectangleF(text.X, rect.Top + rect.Height / 2, text.Width, rect.Height / 2 - 3), format);
                }
            }
            if (d.Id == dragId && moved)
            {
                var point = new RectangleF(rect.Left, rect.Bottom + 8, 180, 23);
                using (var brush = new SolidBrush(Color.FromArgb(234, 243, 255))) g.DrawString("X " + d.X + "   Y " + d.Y, Font, brush, point);
            }
        }
        // Use the real mouse event handlers in UI smoke tests, including frozen scaling and release behavior.
        internal void SimulateDrag(string id, Point target, bool cancel)
        {
            Fit(); var d = displays.Single(v => v.Id == id); var bounds = BoundsFor(d);
            var start = new Point((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2));
            var end = new Point(start.X + (int)Math.Round((target.X - d.X) * scale), start.Y + (int)Math.Round((target.Y - d.Y) * scale));
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, start.X, start.Y, 0));
            if (dragId != id) throw new InvalidOperationException("Drag test could not select the requested display.");
            OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, end.X, end.Y, 0));
            if (cancel) { var msg = new Message(); ProcessCmdKey(ref msg, Keys.Escape); }
            else OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, end.X, end.Y, 0));
        }
    }
}
