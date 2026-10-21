using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace GapGone {
    public static class Palette {
        public static bool Dark = true;
        public static Color Background { get { return Dark ? Color.FromArgb(19, 23, 29) : Color.FromArgb(244, 246, 249); } }
        public static Color Surface { get { return Dark ? Color.FromArgb(29, 35, 43) : Color.White; } }
        public static Color Text { get { return Dark ? Color.FromArgb(231, 237, 244) : Color.FromArgb(31, 42, 55); } }
        public static Color Muted { get { return Dark ? Color.FromArgb(151, 164, 182) : Color.FromArgb(99, 115, 136); } }
        public static Color DisabledText { get { return Dark ? Color.FromArgb(169, 179, 194) : Color.FromArgb(92, 103, 119); } }
        public static Color DisabledSurface { get { return Dark ? Color.FromArgb(36, 42, 51) : Color.FromArgb(238, 241, 245); } }
        public static Color Selection { get { return Dark ? Color.FromArgb(43, 79, 77) : Color.FromArgb(210, 239, 230); } }
        public static Color Hover { get { return Dark ? Color.FromArgb(43, 53, 64) : Color.FromArgb(233, 241, 246); } }
        public static Color Line { get { return Dark ? Color.FromArgb(54, 65, 80) : Color.FromArgb(220, 228, 237); } }
        public static Color Accent = Color.FromArgb(55, 189, 157), Deleted = Color.FromArgb(217, 104, 118), Pending = Color.FromArgb(222, 175, 58);
        public static Color DeletedFill { get { return Dark ? Color.FromArgb(158,49,62) : Color.FromArgb(249,174,178); } }
        public static Color DeletedBorder { get { return Dark ? Color.FromArgb(255,113,125) : Color.FromArgb(187,47,62); } }
        public static void Apply(Control root) {
            root.BackColor = root is Form ? Background : Surface; root.ForeColor = Text;
            var button = root as Button;
            if (button != null) { button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Line; button.Cursor = Cursors.Hand; }
            var grid = root as DataGridView;
            if (grid != null) {
                grid.EnableHeadersVisualStyles = false; grid.BackgroundColor = Surface; grid.GridColor = Line;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Background; grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Background; grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Text;
                grid.DefaultCellStyle.BackColor = Surface; grid.DefaultCellStyle.ForeColor = Text;
                grid.DefaultCellStyle.SelectionBackColor = Selection; grid.DefaultCellStyle.SelectionForeColor = Text;
            }
            foreach (Control child in root.Controls) Apply(child);
            root.Invalidate(true);
        }
    }
    public sealed class TimelineView : StyledScrollHost {
        readonly TimelineCanvas canvas = new TimelineCanvas();
        public TimelineCanvas Canvas { get { return canvas; } }
        public double Zoom = 1;
        public TimelineView() { SetContent(canvas); canvas.PanRequested += (dx,dy)=>Pan(dx,dy); }
        public void LayoutTrack() { RefreshLayout(); canvas.Invalidate(); }
        protected override Size ContentSize(int width,int height) {
            canvas.Width = canvas.Advanced ? width : (int)Math.Min(1000000, Math.Max(1,width * Zoom));
            canvas.SecondsPerRow = Math.Max(10 / Math.Max(0.25,Zoom), canvas.Data == null ? 0 : canvas.Data.Info.Duration / 9000);
            int rows = canvas.Advanced && canvas.Data != null ? Math.Max(1,(int)Math.Ceiling(canvas.Data.Info.Duration / canvas.SecondsPerRow)) : 1;
            canvas.Height = canvas.Advanced ? rows * 104 : Math.Max(146,height);
            return canvas.Size;
        }
    }
    public sealed class TimelineCanvas : Control {
        public AudioData Data;
        public List<Region> Deleted = new List<Region>();
        public bool Advanced;
        public double Position;
        public double SecondsPerRow = 10;
        public event Action<double> Seek;
        public event Action<Region, bool> Edit;
        public event Action<int,int> PanRequested;
        double dragStart, dragEnd; int dragRow, button; bool dragging; Point origin, current;
        float[] peaks; Point panPoint;
        public TimelineCanvas() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); Font = new Font("Microsoft YaHei UI", 9); }
        public void SetData(AudioData data) {
            Data = data; Position = 0; Deleted = new List<Region>();
            if (data == null) { peaks = null; return; }
            peaks = data.Peaks ?? SampleStorage.BuildPeaks(data, System.Threading.CancellationToken.None);
            Invalidate();
        }
        public static string Time(double time) { int m = (int)(time / 60); return m.ToString("00") + ":" + (time % 60).ToString("00.00"); }
        double RowStart(int row) { return Advanced ? row * SecondsPerRow : 0; }
        double RowEnd(int row) { return Math.Min(Data.Info.Duration, Advanced ? (row + 1) * SecondsPerRow : Data.Info.Duration); }
        double RowDuration(int row) { return Advanced ? SecondsPerRow : Data.Info.Duration; }
        double At(int x, int row) { return Math.Min(RowEnd(row),RowStart(row) + Math.Max(0, Math.Min(1, (double)x / Math.Max(1, Width - 1))) * RowDuration(row)); }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); var g = e.Graphics; g.Clear(Palette.Surface); g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Data == null) { using (var b = new SolidBrush(Palette.Muted)) g.DrawString("拖入音频文件，或点击「打开音频」\n所有编辑在导出前仅记录操作", Font, b, new PointF(28, 60)); return; }
            int first = Advanced ? Math.Max(0, e.ClipRectangle.Top / 104) : 0, last = Advanced ? Math.Min((int)Math.Ceiling(Data.Info.Duration / SecondsPerRow) - 1, e.ClipRectangle.Bottom / 104) : 0;
            for (int row = first; row <= last; row++) {
                int top = Advanced ? row * 104 : 4, center = Advanced ? top + 62 : Math.Max(78,Height / 2);
                double start = RowStart(row), end = RowEnd(row), duration = RowDuration(row);
                if (duration <= 0) continue;
                float usedWidth=(float)((end-start)/duration*(Width-1));
                using (var pen = new Pen(Palette.Line)) g.DrawLine(pen, 0, center, usedWidth, center);
                using (var b = new SolidBrush(Palette.Muted)) {
                    for (int i = 0; i <= 8; i++) { double tick=start+duration*i/8; if(tick>end+0.0001)break; float x = (float)(i * (Width - 1) / 8.0); string label = Time(tick); var sz = g.MeasureString(label, Font); g.DrawString(label, Font, b, Math.Max(0,Math.Min(x,Width-sz.Width)), top + 3); }
                }
                var segments = new List<Tuple<Region, bool>>();
                foreach (var r in Regions.Kept(Deleted, Data.Info.Duration)) segments.Add(Tuple.Create(r, false));
                foreach (var r in Deleted) segments.Add(Tuple.Create(r, true));
                foreach (var seg in segments) {
                    double a = Math.Max(start, seg.Item1.Start), b = Math.Min(end, seg.Item1.End); if (b <= a) continue;
                    float x = (float)((a - start) / duration * Width), w = (float)((b - a) / duration * Width);
                    Color color = seg.Item2 ? Palette.Deleted : Palette.Accent;
                    var area=new RectangleF(x, Advanced ? top + 28 : seg.Item2 ? center + 7 : center - 46,w,Advanced ? 67 : 39);
                    using (var brush = new SolidBrush(seg.Item2 ? Palette.DeletedFill : Color.FromArgb(Advanced ? 40 : 100, color))) g.FillRectangle(brush,area);
                    if(seg.Item2)using(var border=new Pen(Palette.DeletedBorder,1.5f))g.DrawRectangle(border,area.X,area.Y,Math.Max(0,area.Width-1),area.Height-1);
                    if (!Advanced && w > 50) using (var brush = new SolidBrush(Palette.Text)) g.DrawString(seg.Item2 ? "↓ 删除" : "↑ 保留", Font, brush, x + 8, seg.Item2 ? center + 17 : center - 35);
                }
                if (Advanced && peaks != null) using (var pen = new Pen(Palette.Accent)) {
                    int xmin = Math.Max(0, e.ClipRectangle.Left), xmax = Math.Min((int)usedWidth, e.ClipRectangle.Right);
                    for (int x = xmin; x < xmax; x++) {
                        int a = (int)(At(x, row) / Data.Info.Duration * peaks.Length), b = (int)(At(x + 1, row) / Data.Info.Duration * peaks.Length); float peak = 0;
                        for (int i = a; i <= b && i < peaks.Length; i++) peak = Math.Max(peak, peaks[i]);
                        g.DrawLine(pen, x, center - peak * 28, x, center + peak * 28);
                    }
                }
                if (dragging && button != 1 && row == dragRow) using (var brush = new SolidBrush(Color.FromArgb(90, button == 0 ? Palette.Accent : Palette.Deleted))) { float x = (float)((Math.Min(dragStart, dragEnd) - start) / duration * Width), w = (float)(Math.Abs(dragEnd - dragStart) / duration * Width); g.FillRectangle(brush, x, top + 27, w, Advanced ? 70 : Height - 40); }
                if (Position >= start && Position <= end) using (var pen = new Pen(Palette.Deleted, 2)) { float x = (float)((Position - start) / duration * (Width - 1)); g.DrawLine(pen, x, top + 24, x, Advanced ? top + 98 : Height - 20); }
            }
        }
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e); if (Data == null) return;
            if(e.Button!=MouseButtons.Left && e.Button!=MouseButtons.Right && e.Button!=MouseButtons.Middle)return;
            dragRow = Advanced ? Math.Min((int)(Data.Info.Duration / SecondsPerRow), e.Y / 104) : 0;
            if (RowStart(dragRow) >= Data.Info.Duration) return;
            origin = current = e.Location; button = e.Button == MouseButtons.Right ? 2 : e.Button == MouseButtons.Middle ? 1 : 0;
            panPoint=PointToScreen(e.Location); if(button==1)Cursor=Cursors.Hand;
            dragStart = dragEnd = At(e.X, dragRow); dragging = true; Capture = true;
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (!dragging) return; if(button==1){var point=PointToScreen(e.Location);if(PanRequested!=null)PanRequested(panPoint.X-point.X,Advanced?panPoint.Y-point.Y:0);panPoint=point;return;} current = e.Location; dragEnd = At(e.X, dragRow); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e); if (!dragging) return; dragEnd = At(e.X, dragRow); current = e.Location;
            bool moved = Math.Abs(current.X - origin.X) > 3;
            if (button == 0 && !moved) { if (Seek != null) Seek(dragEnd); }
            else if (button!=1 && moved && Edit != null) Edit(new Region(Math.Min(dragStart, dragEnd), Math.Max(dragStart, dragEnd)), button == 0);
            dragging = false; Capture = false; Cursor=Cursors.Default; Invalidate();
        }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) { dragging = false; Cursor=Cursors.Default; Invalidate(); } }
    }
}
