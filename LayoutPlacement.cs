using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MonitorControl
{
    // Geometry only: dragging never calls Windows or changes saved presets.
    internal static class LayoutPlacement
    {
        public static bool TrySnap(List<DisplaySetting> displays, string id, Point requested, int tolerance, bool dock, out Point result)
        {
            result = requested;
            var moving = displays.Single(d => d.Id == id);
            var others = displays.Where(d => d.Enabled && d.Id != id).ToList();
            if (others.Count == 0) { result = new Point(moving.X, moving.Y); return true; }
            // Align across the whole group, so another overlapping edge cannot win by a 1px offset.
            requested.X = Align(requested.X, others.SelectMany(d => new[] { d.X, d.X + d.Width - moving.Width, d.X + (d.Width - moving.Width) / 2, d.X - moving.Width, d.X + d.Width }).ToArray(), tolerance);
            requested.Y = Align(requested.Y, others.SelectMany(d => new[] { d.Y, d.Y + d.Height - moving.Height, d.Y + (d.Height - moving.Height) / 2, d.Y - moving.Height, d.Y + d.Height }).ToArray(), tolerance);
            var candidates = new List<Point>();
            foreach (var other in others)
            {
                int y = Clamp(requested.Y, other.Y - moving.Height + 1, other.Y + other.Height - 1);
                int x = Clamp(requested.X, other.X - moving.Width + 1, other.X + other.Width - 1);
                int[] ys = { other.Y, other.Y + other.Height - moving.Height, other.Y + (other.Height - moving.Height) / 2 };
                int[] xs = { other.X, other.X + other.Width - moving.Width, other.X + (other.Width - moving.Width) / 2 };
                // Prefer exact edge/center alignment when the pointer is close to it.
                y = Align(y, ys, tolerance); x = Align(x, xs, tolerance);
                candidates.Add(new Point(other.X - moving.Width, y));
                candidates.Add(new Point(other.X + other.Width, y));
                candidates.Add(new Point(x, other.Y - moving.Height));
                candidates.Add(new Point(x, other.Y + other.Height));
                // Exact alignments can be needed to avoid a third monitor or keep the group connected.
                foreach (int a in ys) { candidates.Add(new Point(other.X - moving.Width, a)); candidates.Add(new Point(other.X + other.Width, a)); }
                foreach (int a in xs) { candidates.Add(new Point(a, other.Y - moving.Height)); candidates.Add(new Point(a, other.Y + other.Height)); }
            }
            foreach (var candidate in candidates.Distinct().OrderBy(p => Distance(p, requested)))
            {
                if (!dock && Distance(candidate, requested) > (double)tolerance * tolerance) continue;
                if (Math.Abs((long)candidate.X) > 100000 || Math.Abs((long)candidate.Y) > 100000) continue;
                var copy = displays.Where(d => d.Enabled).Select(d => d.Copy()).ToList();
                var target = copy.Single(d => d.Id == id); target.X = candidate.X; target.Y = candidate.Y;
                if (ConnectedWithoutOverlap(copy)) { result = candidate; return true; }
            }
            return false;
        }
        private static int Align(int value, int[] anchors, int tolerance)
        {
            int nearest = anchors.OrderBy(a => Math.Abs((long)a - value)).First();
            return Math.Abs((long)nearest - value) <= tolerance ? nearest : value;
        }
        private static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(max, value)); }
        private static double Distance(Point a, Point b) { return (double)(a.X - b.X) * (a.X - b.X) + (double)(a.Y - b.Y) * (a.Y - b.Y); }
        private static bool ConnectedWithoutOverlap(List<DisplaySetting> displays)
        {
            foreach (var a in displays)
                foreach (var b in displays.Where(d => d.Id != a.Id))
                    if (Math.Max(a.X, b.X) < Math.Min(a.X + a.Width, b.X + b.Width) && Math.Max(a.Y, b.Y) < Math.Min(a.Y + a.Height, b.Y + b.Height)) return false;
            var reached = new HashSet<string> { displays[0].Id };
            bool changed;
            do
            {
                changed = false;
                foreach (var b in displays.Where(d => !reached.Contains(d.Id)))
                    if (displays.Any(a => reached.Contains(a.Id) && Touches(a, b))) { reached.Add(b.Id); changed = true; }
            } while (changed);
            return reached.Count == displays.Count;
        }
        internal static bool Touches(DisplaySetting a, DisplaySetting b)
        {
            return ((a.X + a.Width == b.X || b.X + b.Width == a.X) && Math.Max(a.Y, b.Y) < Math.Min(a.Y + a.Height, b.Y + b.Height)) ||
                   ((a.Y + a.Height == b.Y || b.Y + b.Height == a.Y) && Math.Max(a.X, b.X) < Math.Min(a.X + a.Width, b.X + b.Width));
        }
    }
}
