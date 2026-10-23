using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MonitorControl
{
    // In-app help, laid out like a help centre: topics and guides in a sidebar, the chosen page on the right.
    // The guides are the docs\*.md files, built into the exe (see build.ps1), so Help works offline with nothing beside the exe.
    internal class HelpForm : Form
    {
        private readonly HelpNav nav = new HelpNav();
        private readonly HelpPage page = new HelpPage();
        private readonly List<HelpTopic> topics = HelpContent.Topics();
        public HelpForm()
        {
            Text = "Help & about"; ClientSize = new Size(980, 700); MinimumSize = new Size(720, 640); StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; MinimizeBox = false;
            Font = new Font("Segoe UI", 10); BackColor = Color.White; ForeColor = Ui.Ink; Icon = AppIcon.Create(32); KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
            nav.Dock = DockStyle.Left; nav.Width = 240;
            nav.AddHeader("Help");
            for (int i = 0; i < topics.Count; i++) nav.AddPage(topics[i].Name, i);
            nav.AddHeader("Full guides");
            for (int i = 0; i < HelpDocs.Files.Length; i++) { topics.Add(HelpDocs.Load(HelpDocs.Files[i], HelpDocs.Names[i])); nav.AddPage(HelpDocs.Names[i], topics.Count - 1); }
            nav.AddHeader("Support");
            nav.AddLink("Report a problem", delegate { AppInfo.Open(this, AppInfo.IssuesUrl); });
            nav.AddLink("GitHub", delegate { AppInfo.Open(this, AppInfo.RepoUrl); });
            nav.PageChosen += delegate(int index) { page.Topic = topics[index]; };
            page.LinkClicked += FollowLink;
            page.Dock = DockStyle.Fill; page.Topic = topics[0];
            // The last control added docks first, so the sidebar takes the left edge and the page fills the rest.
            Controls.Add(page); Controls.Add(nav);
        }
        // Links to a guide or a heading open here; web links and files outside the guides open in the browser (https only, see AppInfo.Open).
        private void FollowLink(string target)
        {
            if (HelpDocs.IsWebLink(target)) { AppInfo.Open(this, target); return; }
            int hash = target.IndexOf('#');
            string file = hash < 0 ? target : target.Substring(0, hash), anchor = hash < 0 ? "" : target.Substring(hash + 1);
            int index = file.Length == 0 ? topics.IndexOf(page.Topic) : topics.FindIndex(t => t.File == file);
            if (index < 0) { AppInfo.Open(this, AppInfo.RepoUrl + "/blob/main/" + file.Replace("../", "")); return; }
            if (topics[index] != page.Topic) nav.Choose(index);
            page.ScrollTo(anchor);
        }
        // Saves the first topic as the screenshot, then every page and a narrow window, for checking the layout.
        internal void RenderForTest(string path)
        {
            Opacity = 0; Show(); Application.DoEvents();
            Save(path);
            string stem = Path.ChangeExtension(path, null);
            for (int i = 0; i < topics.Count; i++)
            {
                nav.Choose(i); ClientSize = new Size(980, Math.Min(1600, Math.Max(700, page.ContentHeight + 4))); Application.DoEvents();
                Save(stem + "-" + (i + 1) + ".png");
            }
            // Jump to a screenshot and a table the same way a link click does.
            ClientSize = new Size(980, 900);
            FollowLink("USER-GUIDE.md#a-tour-of-the-main-window"); Application.DoEvents(); Save(stem + "-image.png");
            FollowLink("TROUBLESHOOTING.md#other-messages"); Application.DoEvents(); Save(stem + "-table.png");
            nav.Choose(0); ClientSize = new Size(MinimumSize.Width - 16, 700); Application.DoEvents();
            Save(stem + "-narrow.png");
            Close();
        }
        private void Save(string path)
        {
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
        }
    }

    // One help page: a title, an optional summary, and blocks of content in reading order.
    // Text in blocks may use **bold**, `code` and [links](target), as in the guides.
    internal class HelpTopic
    {
        internal class Block { public char Kind; public string Title, Text; public int Number, Level; public List<string[]> Rows; }
        public readonly string Name, Summary;
        public string File; // the guide's file name; null for the short topics
        public readonly List<Block> Blocks = new List<Block>();
        private int steps;
        public HelpTopic(string name, string summary) { Name = name; Summary = summary; }
        public HelpTopic Heading(string text) { steps = 0; return Add('h', text, null); }
        public HelpTopic SubHeading(string text) { steps = 0; return Add('k', text, null); }
        public HelpTopic Para(string text) { return Add('p', null, text); }
        // Steps are numbered from 1 again after each heading.
        public HelpTopic Step(string title, string text) { Add('s', title, text); Blocks[Blocks.Count - 1].Number = ++steps; return this; }
        public HelpTopic Numbered(int number, string text) { Add('n', null, text); Blocks[Blocks.Count - 1].Number = number; return this; }
        public HelpTopic Bullet(string text, int level = 0) { Add('b', null, text); Blocks[Blocks.Count - 1].Level = level; return this; }
        public HelpTopic Quote(string text) { return Add('q', null, text); }
        public HelpTopic Tip(string title, string text) { return Add('t', title, text); }
        public HelpTopic Picture(string resource) { return Add('i', null, resource); }
        public HelpTopic Table(List<string[]> rows) { Add('r', null, null); Blocks[Blocks.Count - 1].Rows = rows; return this; }
        private HelpTopic Add(char kind, string title, string text) { Blocks.Add(new Block { Kind = kind, Title = title, Text = text }); return this; }
    }

    // The short topics. Keep them brief and in step with docs/USER-GUIDE.md, which has the full detail.
    internal static class HelpContent
    {
        public static List<HelpTopic> Topics()
        {
            return new List<HelpTopic> {
                new HelpTopic("Quick start", "Set up a layout and switch to it in a few steps.")
                    .Step("Name your screens", "Click **Identify screens** to show a label on each screen that is on. Click a name in the **Name** column, type a name such as Left or TV, and click **Save monitor names**.")
                    .Step("Arrange them", "Drag the tiles in the preview to match your desk. Edges snap together. Press **Esc** to cancel a drag, or click **Undo move** afterwards.")
                    .Step("Save a preset", "Click **Save as preset…**, type a name, and optionally choose a shortcut from **Ctrl + Alt + 1** to **9**.")
                    .Step("Switch layouts", "Choose a preset from the list and click **Apply layout**. You can also use its shortcut or the tray icon.")
                    .Step("Keep or revert", "Click **Keep changes** when your screens look right. If you do nothing, your previous layout returns after about 35 seconds.")
                    .Tip("Nothing changes until you apply", "Opening the app, **Refresh**, and **Identify screens** only read your displays. Windows changes only when you apply a layout."),
                new HelpTopic("Presets", "Save a layout under a name and switch to it in one click.")
                    .Heading("Save a preset")
                    .Step("Set up the table", "Choose which screens are on, which one is main, and where they sit.")
                    .Step("Click Save as preset…", "Type a name of 1 to 80 characters. If the table holds a preset, its name and shortcut are already filled in.")
                    .Step("Choose a shortcut", "Optional. Pick **Ctrl + Alt + 1** to **9**, or **None**. Each shortcut belongs to one preset.")
                    .Heading("Edit, rename, or delete")
                    .Bullet("To edit a preset, choose it, make your changes, and save it again under the same name.")
                    .Bullet("To rename a preset, save it under the new name, then delete the old one.")
                    .Bullet("To delete a preset, choose it and click **Delete**. Your displays don't change.")
                    .Tip("What a preset saves", "A preset saves what is in the table, including the screens that are off. Monitor names are shared by all presets."),
                new HelpTopic("Arrange screens", "Choose which screens are on, where they sit, and how they look.")
                    .Heading("Turn screens on and off")
                    .Bullet("Check **On** to use a screen in the layout. Uncheck it to turn that screen off in Windows.")
                    .Bullet("Select **Main** for the screen that holds the taskbar. Exactly one screen that is on is the main screen.")
                    .Heading("Position them")
                    .Bullet("Drag a tile in the preview. It snaps to the edges and centers of the other screens.")
                    .Bullet("Screens must touch along an edge and can't overlap. **Arrange in a row** lines them all up.")
                    .Bullet("Check **Exact positions** to type pixel positions. The main screen is always at 0, 0.")
                    .Heading("Resolution and orientation")
                    .Para("The **Resolution / Hz** list shows only the modes your graphics driver reports for that screen. **Orientation** turns the screen in steps of 90 degrees.")
                    .Tip("Not changed by this app", "Monitor inputs, HDR, text scaling, brightness, and mirroring."),
                new HelpTopic("Keep or revert", "Every change asks you to confirm, so you can't get stuck with a blank screen.")
                    .Para("After you apply a layout, the **Keep this display layout?** window appears in the middle of the main screen, with a countdown.")
                    .Bullet("**Keep changes** accepts the new layout.")
                    .Bullet("**Revert**, **Esc**, or closing the window brings back your previous layout right away.")
                    .Bullet("If you do nothing, your previous layout returns when the countdown ends, after about 35 seconds.")
                    .Heading("If something goes wrong")
                    .Bullet("Can't see the window? Wait. Your previous layout returns on its own.")
                    .Bullet("If the app closes or crashes, a separate recovery process still restores your previous layout.")
                    .Bullet("If Windows restarts or signs out during the countdown, your previous layout returns when you next sign in.")
                    .Tip("There is no undo after Keep changes", "To go back, apply a preset you saved for your old setup, or use Windows **Settings > System > Display**."),
                new HelpTopic("Tray & shortcuts", "Switch layouts without opening the main window.")
                    .Bullet("**Minimize to tray** hides the window. Presets and shortcuts keep working.")
                    .Bullet("Right-click the tray icon to apply a preset, open the window, or exit. Double-click it to open the window.")
                    .Bullet("**Ctrl + Alt + 1** to **9** apply their presets while the app is running.")
                    .Bullet("If another app already uses a shortcut, a **Shortcut unavailable** notice appears. The preset still works from the tray.")
                    .Bullet("Closing the window quits the app, unless **Keep running in the tray when closed** is checked.")
                    .Tip("Not started with Windows", "Monitor Control doesn't start when you sign in. Open it when you need it."),
                new HelpTopic("Your data", "Everything stays on this PC. The app never connects to the internet.")
                    .Para("Settings and logs are in `%LOCALAPPDATA%\\MonitorControl`. To open the folder, press **Windows + R**, paste that path, and press **Enter**.")
                    .Bullet("`settings.json` holds your monitor names, presets, and options.")
                    .Bullet("`activity.log` lists changes and errors.")
                    .Bullet("`last-layout.json` and pending files are used to restore a previous layout.")
                    .Tip("Before you share a file", "These files contain monitor serial numbers. Remove them before you attach a file to a bug report.")
            };
        }
    }

    // The guides built into the exe, and a reader for the small part of Markdown they use:
    // headings, paragraphs, lists, quotes, tables and images. Inline formatting is handled by HelpPage.
    internal static class HelpDocs
    {
        public static readonly string[] Files = { "USER-GUIDE.md", "FAQ.md", "TROUBLESHOOTING.md" };
        public static readonly string[] Names = { "User guide", "FAQ", "Troubleshooting" };
        private static readonly Dictionary<string, Image> pictures = new Dictionary<string, Image>();
        private static Stream Resource(string name) { return Assembly.GetExecutingAssembly().GetManifestResourceStream(name); }
        public static Image Picture(string name)
        {
            Image image;
            if (!pictures.TryGetValue(name, out image))
            {
                // Copy the image, because an Image read from a stream needs that stream open for its whole life.
                using (var stream = Resource(name)) if (stream != null) using (var source = Image.FromStream(stream)) image = new Bitmap(source);
                pictures[name] = image;
            }
            return image;
        }
        public static HelpTopic Load(string file, string name)
        {
            string text = null;
            using (var stream = Resource(file)) if (stream != null) using (var reader = new StreamReader(stream)) text = reader.ReadToEnd();
            if (text == null) return new HelpTopic(name, "This guide is not included in this build.") { File = file };
            var topic = new HelpTopic(name, null) { File = file };
            var paragraph = new List<string>(); var rows = new List<string[]>();
            Action flush = delegate
            {
                if (paragraph.Count > 0) { topic.Para(String.Join(" ", paragraph)); paragraph.Clear(); }
                if (rows.Count > 0) { topic.Table(rows); rows = new List<string[]>(); }
            };
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                string line = raw.TrimEnd(); Match m;
                if (line.StartsWith("|"))
                {
                    if (paragraph.Count > 0) flush();
                    var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                    if (!cells.All(c => c.Trim('-', ':').Length == 0)) rows.Add(cells); // skip the | --- | line under the header
                    continue;
                }
                if (line.Length == 0) { flush(); continue; }
                // Any line that isn't plain text ends the paragraph before it.
                if (Regex.IsMatch(line, @"^(#|!\[| *[-*] |\d+\. |> )")) flush(); else if (rows.Count > 0) flush();
                if (line.StartsWith("# ")) topic = new HelpTopic(line.Substring(2), null) { File = file };
                else if (line.StartsWith("### ")) topic.SubHeading(line.Substring(4));
                else if (line.StartsWith("## ")) topic.Heading(line.Substring(3));
                else if ((m = Regex.Match(line, @"^!\[.*\]\((.+)\)$")).Success) topic.Picture(m.Groups[1].Value);
                else if ((m = Regex.Match(line, @"^( *)[-*] (.*)$")).Success) topic.Bullet(m.Groups[2].Value, m.Groups[1].Length / 2);
                else if ((m = Regex.Match(line, @"^(\d+)\. (.*)$")).Success) topic.Numbered(Int32.Parse(m.Groups[1].Value), m.Groups[2].Value);
                else if (line.StartsWith("> ")) topic.Quote(line.Substring(2));
                else paragraph.Add(line.Trim());
            }
            flush();
            return topic;
        }
        // The same heading anchors GitHub makes, so #links work both on GitHub and in the app.
        public static string Slug(string heading)
        {
            return Regex.Replace(heading.Replace("**", "").Replace("`", "").Trim().ToLowerInvariant(), @"[^\w\- ]", "").Replace(' ', '-');
        }
        // Any link with a scheme such as https: or file:, as opposed to a guide file or #heading.
        public static bool IsWebLink(string link) { return Regex.IsMatch(link, @"^[A-Za-z][A-Za-z0-9+.-]*:"); }
        public static IEnumerable<string> Anchors(HelpTopic topic) { return topic.Blocks.Where(b => b.Kind == 'h' || b.Kind == 'k').Select(b => Slug(b.Title)); }
        public static IEnumerable<string> Links(HelpTopic topic)
        {
            var texts = topic.Blocks.SelectMany(b => new[] { b.Title, b.Text }.Concat(b.Rows == null ? new string[0] : b.Rows.SelectMany(r => r)));
            return texts.Where(t => t != null).SelectMany(t => Regex.Matches(t, @"\]\(([^)\s]+)\)").Cast<Match>().Select(m => m.Groups[1].Value));
        }
    }

    // The sidebar: grouped topics and links, drawn like a help-centre menu. Up and Down move between pages.
    internal class HelpNav : Control
    {
        internal class Item { public string Text; public bool Header; public int Page = -1; public Action Open; public Rectangle Bounds; }
        public static readonly Color Back = Color.FromArgb(247, 248, 250);
        private readonly List<Item> items = new List<Item>();
        private readonly Font name = new Font("Segoe UI Semibold", 11), small = new Font("Segoe UI", 8.5f), item = new Font("Segoe UI", 10), selectedItem = new Font("Segoe UI Semibold", 10);
        private Item hot; private int selected;
        public event Action<int> PageChosen;
        public HelpNav()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Back; TabStop = true;
        }
        public void AddHeader(string text) { items.Add(new Item { Text = text, Header = true }); }
        public void AddPage(string text, int page) { items.Add(new Item { Text = text, Page = page }); }
        public void AddLink(string text, Action open) { items.Add(new Item { Text = text, Open = open }); }
        public void Choose(int page) { selected = page; Invalidate(); if (PageChosen != null) PageChosen(page); }
        private void Arrange()
        {
            int y = 76;
            foreach (var i in items)
            {
                if (i.Header) { y += 14; i.Bounds = new Rectangle(12, y, Width - 24, 24); y += 26; }
                else { i.Bounds = new Rectangle(12, y, Width - 24, 32); y += 34; }
            }
        }
        private Item ItemAt(Point p) { Arrange(); return items.FirstOrDefault(i => !i.Header && i.Bounds.Contains(p)); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Arrange(); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Ui.Border)) g.DrawLine(pen, Width - 1, 0, Width - 1, Height);
            // Header: the app icon, name and version.
            using (var icon = AppIcon.Draw(32)) g.DrawImage(icon, 22, 22, 32, 32);
            TextRenderer.DrawText(g, AppInfo.Name, name, new Point(62, 19), Ui.Ink);
            TextRenderer.DrawText(g, "Version " + AppInfo.Version, small, new Point(63, 40), Ui.Muted);
            float radius = Ui.CornerRadius(g) * 1.5f;
            foreach (var i in items)
            {
                var text = new Rectangle(i.Bounds.Left + 10, i.Bounds.Top, i.Bounds.Width - 40, i.Bounds.Height);
                if (i.Header) { TextRenderer.DrawText(g, i.Text, small, text, Ui.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix); continue; }
                bool isSelected = i.Page >= 0 && i.Page == selected;
                if (isSelected || i == hot)
                    using (var shape = Ui.RoundedRect(i.Bounds, radius)) using (var fill = new SolidBrush(isSelected ? Ui.Selected : Ui.Hover)) g.FillPath(fill, shape);
                if (isSelected && Focused && ShowFocusCues)
                    using (var shape = Ui.RoundedRect(new RectangleF(i.Bounds.Left + 0.5f, i.Bounds.Top + 0.5f, i.Bounds.Width - 1, i.Bounds.Height - 1), radius)) using (var pen = new Pen(Ui.Accent, 1.5f)) g.DrawPath(pen, shape);
                TextRenderer.DrawText(g, i.Text, isSelected ? selectedItem : item, text, isSelected ? Ui.Accent : Ui.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                if (i.Open != null) DrawExternal(g, new Rectangle(i.Bounds.Right - 30, i.Bounds.Top, 20, i.Bounds.Height), Ui.Muted);
            }
        }
        // A small arrow pointing up and right: "opens in your browser". Vector-drawn like the other icons.
        private static void DrawExternal(Graphics g, Rectangle area, Color color)
        {
            float cx = area.Left + area.Width / 2f, cy = area.Top + area.Height / 2f;
            using (var pen = new Pen(color, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.DrawLine(pen, cx - 4, cy + 4, cx + 4, cy - 4);
                g.DrawLines(pen, new[] { new PointF(cx - 1, cy - 4), new PointF(cx + 4, cy - 4), new PointF(cx + 4, cy + 1) });
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var now = ItemAt(e.Location); if (now == hot) return;
            hot = now; Cursor = hot == null ? Cursors.Default : Cursors.Hand; Invalidate();
        }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = null; Invalidate(); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            var clicked = ItemAt(e.Location); if (clicked == null || e.Button != MouseButtons.Left) return;
            if (clicked.Page >= 0) Choose(clicked.Page); else clicked.Open();
        }
        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int pages = items.Count(i => i.Page >= 0);
            if (e.KeyCode == Keys.Down && selected < pages - 1) Choose(selected + 1);
            if (e.KeyCode == Keys.Up && selected > 0) Choose(selected - 1);
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void Dispose(bool disposing) { if (disposing) { name.Dispose(); small.Dispose(); item.Dispose(); selectedItem.Dispose(); } base.Dispose(disposing); }
    }

    // One page, drawn as a readable column. It is laid out word by word rather than built from labels,
    // so text re-wraps when the window is resized, steps keep a clean hanging indent, and bold and links can sit inside a sentence.
    internal class HelpPage : Panel
    {
        private const int Gutter = 48, MaxWidth = 720;
        private const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        private static readonly Color Card = Color.FromArgb(244, 246, 250);
        private static readonly Regex Inline = new Regex(@"\*\*(?<b>.+?)\*\*|`(?<c>[^`]+)`|\[(?<l>[^\]]+)\]\((?<u>[^)\s]+)\)|[^*`\[]+|[*`\[]");
        // A laid-out word run or shape, in page coordinates (before scrolling).
        private class Piece { public Rectangle Bounds; public string Text, Link; public Font Font; public Color Color; public bool Code; public Action<Graphics, Rectangle> Paint; }
        private readonly Font title = new Font("Segoe UI Semibold", 22), summary = new Font("Segoe UI", 11.5f), heading = new Font("Segoe UI Semibold", 14), subheading = new Font("Segoe UI Semibold", 11.5f),
            body = new Font("Segoe UI", 10.5f), strong = new Font("Segoe UI Semibold", 10.5f), code = new Font("Consolas", 9.75f);
        private readonly List<Piece> pieces = new List<Piece>();
        private readonly Dictionary<string, int> anchors = new Dictionary<string, int>(), widths = new Dictionary<string, int>();
        private HelpTopic topic; private int contentHeight; private bool arranging, overLink; private Graphics measuring;
        public event Action<string> LinkClicked;
        public HelpPage() { AutoScroll = true; BackColor = Color.White; DoubleBuffered = true; SetStyle(ControlStyles.Selectable, true); TabStop = true; }
        public HelpTopic Topic
        {
            get { return topic; }
            set { topic = value; AutoScrollPosition = Point.Empty; Relayout(); }
        }
        public int ContentHeight { get { return contentHeight; } }
        public void ScrollTo(string anchor) { int y; if (anchors.TryGetValue(anchor, out y)) AutoScrollPosition = new Point(0, Math.Max(0, y - 16)); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Relayout(); }
        private void Relayout()
        {
            if (arranging || topic == null) return;
            arranging = true;
            // Measure against this window's surface: without one, MeasureText ignores NoPadding and every word comes out too wide.
            measuring = CreateGraphics();
            try
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    int width = ClientSize.Width; Arrange();
                    if (AutoScrollMinSize.Height != contentHeight) AutoScrollMinSize = new Size(0, contentHeight);
                    if (ClientSize.Width == width) break; // the scroll bar appeared or went, so lay out again for the new width
                }
            }
            finally { arranging = false; measuring.Dispose(); measuring = null; }
            Invalidate();
        }
        private void Arrange()
        {
            pieces.Clear(); anchors.Clear();
            int x = Gutter, width = Math.Max(240, Math.Min(ClientSize.Width - Gutter - 40, MaxWidth)), y = 36;
            int line = body.Height + 3;
            y += Flow(topic.Name, title, title, Ui.Ink, x, y, width, true) + 4;
            y += topic.Summary == null ? 14 : Flow(topic.Summary, summary, summary, Ui.Muted, x, y, width, true) + 26;
            for (int index = 0; index < topic.Blocks.Count; index++)
            {
                var b = topic.Blocks[index];
                var next = index + 1 < topic.Blocks.Count ? topic.Blocks[index + 1].Kind : ' ';
                switch (b.Kind)
                {
                    case 'h':
                        y += 22; anchors[HelpDocs.Slug(b.Title)] = y;
                        y += Flow(b.Title, heading, heading, Ui.Ink, x, y, width, true) + 10; break;
                    case 'k':
                        y += 10; anchors[HelpDocs.Slug(b.Title)] = y;
                        y += Flow(b.Title, subheading, subheading, Ui.Ink, x, y, width, true) + 8; break;
                    case 'p':
                        y += Flow(b.Text, body, strong, Ui.Ink, x, y, width, true) + 14; break;
                    case 's':
                    case 'n':
                    {
                        // A number badge, with the text indented beside it.
                        int number = b.Number;
                        Shape(new Rectangle(x, y + (line - 26) / 2, 26, 26), delegate(Graphics g, Rectangle r)
                        {
                            using (var fill = new SolidBrush(Ui.Selected)) g.FillEllipse(fill, r);
                            TextRenderer.DrawText(g, number.ToString(), strong, r, Ui.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                        });
                        int tx = x + 42, tw = width - 42, h = 0;
                        if (b.Title != null) h = Flow(b.Title, strong, strong, Ui.Ink, tx, y, tw, true) + 2;
                        h += Flow(b.Text, body, strong, b.Title != null ? Ui.Muted : Ui.Ink, tx, y + h, tw, true);
                        y += Math.Max(26, h) + (b.Kind == 's' ? 18 : next == 'n' ? 10 : 16); break;
                    }
                    case 'b':
                    {
                        int indent = b.Level * 22;
                        Shape(new Rectangle(x + indent + 6, y + line / 2 - 3, 5, 5), delegate(Graphics g, Rectangle r) { using (var dot = new SolidBrush(Ui.Muted)) g.FillEllipse(dot, r); });
                        y += Flow(b.Text, body, strong, Ui.Ink, x + indent + 22, y, width - indent - 22, true) + (next == 'b' ? 6 : 14); break;
                    }
                    case 'q':
                    case 't':
                    {
                        // A shaded card with an accent edge. Measure first, so the card is drawn behind its text.
                        int inner = width - 36, titleHeight = b.Title == null ? 0 : Flow(b.Title, strong, strong, Ui.Ink, 0, 0, inner, false) + 4;
                        int h = 14 + titleHeight + Flow(b.Text, body, strong, Ui.Ink, 0, 0, inner, false) + 14;
                        y += 4;
                        Shape(new Rectangle(x, y, width, h), delegate(Graphics g, Rectangle r)
                        {
                            using (var shape = Ui.RoundedRect(r, 8)) using (var fill = new SolidBrush(Card)) g.FillPath(fill, shape);
                            using (var bar = new SolidBrush(Ui.Accent)) g.FillRectangle(bar, r.X, r.Y + 12, 3, r.Height - 24);
                        });
                        if (b.Title != null) Flow(b.Title, strong, strong, Ui.Ink, x + 18, y + 14, inner, true);
                        Flow(b.Text, body, strong, Ui.Ink, x + 18, y + 14 + titleHeight, inner, true);
                        y += h + 16; break;
                    }
                    case 'i':
                    {
                        var image = HelpDocs.Picture(b.Text); if (image == null) break;
                        int w = Math.Min(width, image.Width), h = image.Height * w / image.Width;
                        y += 4;
                        Shape(new Rectangle(x, y, w, h), delegate(Graphics g, Rectangle r)
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(image, r);
                            using (var pen = new Pen(Ui.Border)) g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                        });
                        y += h + 20; break;
                    }
                    case 'r':
                        y += Table(b.Rows, x, y, width) + 18; break;
                }
            }
            contentHeight = y + 24;
        }
        // A table with a shaded header row. Columns get a share of the width by how much text they hold.
        private int Table(List<string[]> rows, int x, int y, int width)
        {
            int columns = rows.Max(r => r.Length);
            Func<int, int, string> cell = delegate(int row, int column) { return column < rows[row].Length ? rows[row][column] : ""; };
            var weights = Enumerable.Range(0, columns).Select(c => Math.Max(12.0, Enumerable.Range(0, rows.Count).Average(r => Plain(cell(r, c)).Length))).ToArray();
            var cols = weights.Select(v => (int)(width * v / weights.Sum())).ToArray(); cols[columns - 1] = width - cols.Take(columns - 1).Sum();
            var heights = Enumerable.Range(0, rows.Count).Select(r => Enumerable.Range(0, columns).Max(c => Flow(cell(r, c), r == 0 ? strong : body, strong, Ui.Ink, 0, 0, cols[c] - 20, false)) + 16).ToArray();
            Shape(new Rectangle(x, y, width, heights.Sum()), delegate(Graphics g, Rectangle r)
            {
                using (var shape = Ui.RoundedRect(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), 6))
                {
                    var clip = g.Clip; g.SetClip(shape);
                    using (var fill = new SolidBrush(Card)) g.FillRectangle(fill, r.X, r.Y, r.Width, heights[0]);
                    g.Clip = clip;
                    using (var pen = new Pen(Ui.Border))
                    {
                        int ry = r.Y; for (int i = 0; i < heights.Length - 1; i++) { ry += heights[i]; g.DrawLine(pen, r.X, ry, r.Right, ry); }
                        g.DrawPath(pen, shape);
                    }
                }
            });
            int top = y;
            for (int r = 0; r < rows.Count; r++)
            {
                int cx = x;
                for (int c = 0; c < columns; c++) { Flow(cell(r, c), r == 0 ? strong : body, strong, Ui.Ink, cx + 10, top + 8, cols[c] - 20, true); cx += cols[c]; }
                top += heights[r];
            }
            return heights.Sum();
        }
        private static string Plain(string text) { return Regex.Replace(text, @"\*\*|`|\[([^\]]+)\]\([^)]+\)", "$1"); }
        private void Shape(Rectangle bounds, Action<Graphics, Rectangle> paint) { pieces.Add(new Piece { Bounds = bounds, Paint = paint }); }
        // Lays out text with **bold**, `code` and [links](target), wrapping word by word.
        // Adds the pieces when place is true; returns the height either way.
        private int Flow(string text, Font regular, Font bold, Color color, int x, int y, int width, bool place)
        {
            int line = regular.Height + 3, cx = 0, cy = 0; bool space = false; Piece last = null;
            foreach (Match run in Inline.Matches(text))
            {
                string words = run.Value; Font font = regular; Color c = color; string link = null; bool isCode = false;
                if (run.Groups["b"].Success) { words = run.Groups["b"].Value; font = bold; }
                else if (run.Groups["c"].Success) { words = run.Groups["c"].Value; font = code; isCode = true; }
                else if (run.Groups["l"].Success) { words = Plain(run.Groups["l"].Value); link = run.Groups["u"].Value; c = Ui.Accent; }
                foreach (Match token in Regex.Matches(words, @"\s+|\S+"))
                {
                    if (Char.IsWhiteSpace(token.Value[0])) { space = true; continue; }
                    int w = Measure(token.Value, font), gap = space && cx > 0 ? Measure(" ", font) : 0;
                    if (cx > 0 && cx + gap + w > width) { cx = 0; cy += line; gap = 0; last = null; }
                    if (place)
                    {
                        // Words in the same style on the same line join into one piece, so there are few to draw.
                        if (last != null && last.Font == font && last.Color == c && last.Link == link && last.Code == isCode)
                        { last.Text += (gap > 0 ? " " : "") + token.Value; last.Bounds.Width = x + cx + gap + w - last.Bounds.X; }
                        else { last = new Piece { Text = token.Value, Font = font, Color = c, Link = link, Code = isCode, Bounds = new Rectangle(x + cx + gap, y + cy, w, line) }; pieces.Add(last); }
                    }
                    cx += gap + w; space = false;
                }
            }
            return cy + line;
        }
        private int Measure(string word, Font font)
        {
            string key = font.Name + font.Size + font.Style + "\n" + word; int width;
            if (!widths.TryGetValue(key, out width))
            {
                width = word == " " ? Measure("x x", font) - Measure("xx", font) : TextRenderer.MeasureText(measuring, word, font, new Size(Int32.MaxValue, Int32.MaxValue), Flags).Width;
                widths[key] = width;
            }
            return width;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; int dy = AutoScrollPosition.Y;
            foreach (var p in pieces)
            {
                var r = p.Bounds; r.Offset(0, dy);
                if (!r.IntersectsWith(e.ClipRectangle)) continue;
                if (p.Paint != null) { p.Paint(g, r); continue; }
                if (p.Code) using (var shape = Ui.RoundedRect(new RectangleF(r.X - 3, r.Y + 2, r.Width + 6, r.Height - 3), 3)) using (var fill = new SolidBrush(Card)) g.FillPath(fill, shape);
                TextRenderer.DrawText(g, p.Text, p.Font, new Point(r.X, r.Y + (r.Height - 3 - p.Font.Height) / 2 + 1), p.Color, Flags);
            }
        }
        private Piece LinkAt(Point p) { var at = new Point(p.X, p.Y - AutoScrollPosition.Y); return pieces.FirstOrDefault(i => i.Link != null && i.Bounds.Contains(at)); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool over = LinkAt(e.Location) != null;
            if (over != overLink) { overLink = over; Cursor = over ? Cursors.Hand : Cursors.Default; }
        }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            var link = LinkAt(e.Location);
            if (link != null && e.Button == MouseButtons.Left && LinkClicked != null) LinkClicked(link.Link);
        }
        // Keyboard scrolling once the page has focus: arrows, Page Up/Down, Home and End.
        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.PageUp || keyData == Keys.PageDown || keyData == Keys.Home || keyData == Keys.End || base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int at = -AutoScrollPosition.Y, page = ClientSize.Height - 40;
            int to = e.KeyCode == Keys.Up ? at - 40 : e.KeyCode == Keys.Down ? at + 40 : e.KeyCode == Keys.PageUp ? at - page : e.KeyCode == Keys.PageDown ? at + page : e.KeyCode == Keys.Home ? 0 : e.KeyCode == Keys.End ? contentHeight : at;
            if (to != at) AutoScrollPosition = new Point(0, Math.Max(0, to));
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { title.Dispose(); summary.Dispose(); heading.Dispose(); subheading.Dispose(); body.Dispose(); strong.Dispose(); code.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
