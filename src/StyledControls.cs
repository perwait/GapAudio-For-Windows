using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace GapGone {
    public class StyledButton : Button {
        bool hovered, pressed;
        public StyledButton() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            UseVisualStyleBackColor = false; FlatStyle = FlatStyle.Flat;
        }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); hovered = pressed = false; Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = e.Button == MouseButtons.Left; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) { pressed = false; Invalidate(); } }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(!Enabled ? Palette.DisabledSurface : pressed ? Palette.Selection : hovered ? Palette.Hover : Palette.Surface);
            using (var border = new Pen(Enabled && Focused ? Palette.Accent : Palette.Line)) e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? Palette.Text : Palette.DisabledText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (Enabled && Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), Palette.Text, Palette.Surface);
        }
    }
    public class StyledLabel : Label {
        public StyledLabel() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(BackColor);
            var bounds = new Rectangle(Padding.Left, Padding.Top, Math.Max(0, Width - Padding.Horizontal), Math.Max(0, Height - Padding.Vertical));
            var flags = TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak;
            if (AutoEllipsis) flags |= TextFormatFlags.EndEllipsis;
            if (TextAlign == ContentAlignment.MiddleLeft || TextAlign == ContentAlignment.MiddleCenter || TextAlign == ContentAlignment.MiddleRight) flags |= TextFormatFlags.VerticalCenter;
            if (TextAlign == ContentAlignment.BottomLeft || TextAlign == ContentAlignment.BottomCenter || TextAlign == ContentAlignment.BottomRight) flags |= TextFormatFlags.Bottom;
            if (TextAlign == ContentAlignment.TopCenter || TextAlign == ContentAlignment.MiddleCenter || TextAlign == ContentAlignment.BottomCenter) flags |= TextFormatFlags.HorizontalCenter;
            if (TextAlign == ContentAlignment.TopRight || TextAlign == ContentAlignment.MiddleRight || TextAlign == ContentAlignment.BottomRight) flags |= TextFormatFlags.Right;
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, Palette.Text, flags);
        }
    }
    public sealed class StyledCheckBox : CheckBox {
        public StyledCheckBox() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(BackColor);
            var box = new Rectangle(2, Math.Max(0, (Height - 16) / 2), 16, 16);
            Color color = Enabled ? Palette.Text : Palette.DisabledText;
            using (var fill = new SolidBrush(Enabled ? Palette.Surface : Palette.DisabledSurface)) e.Graphics.FillRectangle(fill, box);
            using (var border = new Pen(Enabled && (Checked || Focused) ? Palette.Accent : Palette.Muted)) e.Graphics.DrawRectangle(border, box);
            if (Checked) using (var pen = new Pen(Enabled ? Palette.Accent : Palette.DisabledText, 2)) e.Graphics.DrawLines(pen, new[] { new Point(box.Left + 3, box.Top + 8), new Point(box.Left + 7, box.Top + 12), new Point(box.Left + 13, box.Top + 4) });
            var textBounds = new Rectangle(24, 0, Math.Max(0, Width - 24), Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, color, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Enabled && Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, textBounds, Palette.Text, BackColor);
        }
    }
    public sealed class ApplyButton : StyledButton {
        readonly Timer animation = new Timer { Interval = 40 };
        readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
        bool pending;
        public bool Pending {
            get { return pending; }
            set {
                if (pending == value) return;
                pending = value;
                if (pending) { clock.Restart(); animation.Start(); }
                else { animation.Stop(); clock.Reset(); }
                Invalidate();
            }
        }
        public ApplyButton() {
            Text = "应用处理"; AutoSize = false; Size = new Size(90, 34); Margin = new Padding(3); Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            animation.Tick += (s,e) => Invalidate();
        }
        public double Glow { get { return pending ? 0.5 - 0.5 * Math.Cos(clock.Elapsed.TotalSeconds * Math.PI) : 0; } }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(Enabled ? Palette.Surface : Palette.DisabledSurface);
            if (pending && Enabled) {
                using (var fill = new SolidBrush(Color.FromArgb((int)(60 + 90 * Glow), Palette.Pending))) e.Graphics.FillRectangle(fill, ClientRectangle);
                using (var border = new Pen(Palette.Pending, 2)) e.Graphics.DrawRectangle(border, 1,1,Width-3,Height-3);
            } else using (var border = new Pen(Focused ? Palette.Accent : Palette.Line)) e.Graphics.DrawRectangle(border,0,0,Width-1,Height-1);
            Color text = !Enabled ? Palette.DisabledText : pending ? Palette.Dark ? Color.FromArgb(255,230,158) : Color.FromArgb(98,61,0) : Palette.Text;
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,text,TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        protected override void Dispose(bool disposing) { if(disposing) animation.Dispose(); base.Dispose(disposing); }
    }
    public sealed class StatusLabel : Label {
        public bool Preparing { get; set; }
        public StatusLabel() { Font = new Font("Microsoft YaHei UI", 12, FontStyle.Bold); TextAlign = ContentAlignment.MiddleLeft; AutoEllipsis = true; }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(BackColor);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Preparing ? Palette.Pending : Palette.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
    public sealed class StyledNumber : UserControl {
        readonly TextBox editor = new TextBox { BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Left };
        decimal minimum, maximum = 100, value, increment = 1; int decimals; bool updating;
        public event EventHandler ValueChanged;
        public decimal Minimum { get { return minimum; } set { minimum = value; if (maximum < minimum) maximum = minimum; Value = this.value; } }
        public decimal Maximum { get { return maximum; } set { maximum = value; if (minimum > maximum) minimum = maximum; Value = this.value; } }
        public decimal Increment { get { return increment; } set { increment = value; } }
        public int DecimalPlaces { get { return decimals; } set { decimals = value; FormatValue(); } }
        public decimal Value { get { return value; } set { decimal next = Math.Round(Math.Min(maximum, Math.Max(minimum, value)), decimals); bool changed = next != this.value; this.value = next; FormatValue(); if (changed && ValueChanged != null) ValueChanged(this, EventArgs.Empty); } }
        public StyledNumber() {
            DoubleBuffered = true; Height = 34; Width = 100; MinimumSize = new Size(58,34); Margin = new Padding(3); Controls.Add(editor);
            editor.TextChanged += (s,e) => { if (updating) return; decimal parsed; if (Decimal.TryParse(editor.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed) && parsed >= minimum && parsed <= maximum) { decimal next = Math.Round(parsed,decimals); if (next != value) { value = next; if (ValueChanged != null) ValueChanged(this,EventArgs.Empty); } } };
            editor.Leave += (s,e) => { FormatValue(); Invalidate(); }; editor.Enter += (s,e) => Invalidate();
            editor.KeyDown += (s,e) => { if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) { Value += e.KeyCode == Keys.Up ? increment : -increment; e.SuppressKeyPress = true; } if (e.KeyCode == Keys.Enter) { FormatValue(); e.SuppressKeyPress = true; } };
            Resize += (s,e) => LayoutEditor(); LayoutEditor(); FormatValue();
        }
        void FormatValue() { updating = true; editor.Text = value.ToString("F" + decimals, CultureInfo.CurrentCulture); updating = false; Invalidate(); }
        void LayoutEditor() { editor.SetBounds(10, Math.Max(5,(Height-editor.PreferredHeight)/2), Math.Max(20,Width-42), editor.PreferredHeight); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (editor != null) { editor.Font = Font; LayoutEditor(); } }
        protected override void OnBackColorChanged(EventArgs e) { base.OnBackColorChanged(e); if (editor != null) editor.BackColor = BackColor; }
        protected override void OnForeColorChanged(EventArgs e) { base.OnForeColorChanged(e); if (editor != null) editor.ForeColor = ForeColor; }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (editor != null) editor.Visible = Enabled; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(Enabled ? Palette.Surface : Palette.DisabledSurface);
            using (var p = new Pen(ContainsFocus ? Palette.Accent : Palette.Line)) e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1);
            if (!Enabled) TextRenderer.DrawText(e.Graphics, value.ToString("F" + decimals, CultureInfo.CurrentCulture), Font, new Rectangle(10,0,Math.Max(1,Width-42),Height), Palette.DisabledText, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var p = new Pen(Enabled ? Palette.Muted : Palette.DisabledText)) { int x = Width-16; e.Graphics.DrawLines(p,new[] {new Point(x-4,12),new Point(x,8),new Point(x+4,12)}); e.Graphics.DrawLines(p,new[] {new Point(x-4,Height-12),new Point(x,Height-8),new Point(x+4,Height-12)}); }
        }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.X >= Width-30) Value += e.Y < Height/2 ? increment : -increment; else editor.Focus(); }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); if (ContainsFocus) Value += e.Delta > 0 ? increment : -increment; }
    }
    public sealed class StyledSelect : Control {
        readonly List<object> items = new List<object>(); int index = -1; Form popup;
        public List<object> Items { get { return items; } }
        public event EventHandler SelectedIndexChanged;
        public int SelectedIndex { get { return index; } set { int next = value < 0 || value >= items.Count ? -1 : value; if (index == next) { Invalidate(); return; } index = next; Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this,EventArgs.Empty); } }
        public object SelectedItem { get { return index >= 0 && index < items.Count ? items[index] : null; } set { SelectedIndex = items.IndexOf(value); } }
        public StyledSelect() { DoubleBuffered = true; SetStyle(ControlStyles.Selectable,true); TabStop = true; Height = 34; Width = 130; MinimumSize = new Size(64,34); Margin = new Padding(3); Cursor = Cursors.Hand; }
        protected override bool IsInputKey(Keys keyData){var key=keyData&Keys.KeyCode;return key==Keys.Up || key==Keys.Down || base.IsInputKey(keyData);}
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(Enabled ? Palette.Surface : Palette.DisabledSurface); using (var p = new Pen(Focused ? Palette.Accent : Palette.Line)) e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1);
            TextRenderer.DrawText(e.Graphics, Convert.ToString(SelectedItem), Font, new Rectangle(10,1,Width-35,Height-2), Enabled ? Palette.Text : Palette.DisabledText, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var p = new Pen(Enabled ? Palette.Muted : Palette.DisabledText,1.5f)) e.Graphics.DrawLines(p,new[] {new Point(Width-22,Height/2-2),new Point(Width-17,Height/2+3),new Point(Width-12,Height/2-2)});
        }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (!Enabled && popup != null) popup.Close(); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { Focus(); OpenPopup(); } }
        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e); if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter || e.KeyCode == Keys.F4) { OpenPopup(); e.Handled = true; }
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up) { SelectedIndex = Math.Max(0,Math.Min(items.Count-1,index + (e.KeyCode == Keys.Down ? 1 : -1))); e.Handled = true; }
        }
        public void OpenPopup() {
            if (!Enabled || items.Count == 0 || popup != null) return;
            var list = new ChoiceList(this); var host = new StyledScrollHost { Dock = DockStyle.Fill }; host.SetContent(list);
            popup = new Form { FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, KeyPreview = true, BackColor = Palette.Line, Padding = new Padding(1), Font = Font };
            popup.Controls.Add(host); popup.ClientSize = new Size(Math.Max(Width,120),Math.Min(288,items.Count*34)+2);
            var point = PointToScreen(new Point(0,Height+3)); var area = Screen.FromControl(this).WorkingArea;
            popup.Location = new Point(Math.Max(area.Left,Math.Min(point.X,area.Right-popup.Width)),point.Y+popup.Height > area.Bottom ? PointToScreen(Point.Empty).Y-popup.Height-3 : point.Y);
            popup.Deactivate += (s,e) => { if (popup != null) popup.Close(); }; popup.FormClosed += (s,e) => { popup = null; Invalidate(); };
            popup.KeyDown += (s,e) => { if (e.KeyCode == Keys.Escape) popup.Close(); if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) { list.Active = Math.Max(0,Math.Min(items.Count-1,list.Active+(e.KeyCode==Keys.Down?1:-1))); host.ScrollTo(0,list.Active*34); list.Invalidate(); e.Handled=true; } if (e.KeyCode == Keys.Enter) { SelectedIndex = list.Active; popup.Close(); } };
            popup.Show(FindForm()); host.RefreshLayout(); host.ScrollTo(0,Math.Max(0,index*34-100));
        }
        protected override void Dispose(bool disposing) { if (disposing && popup != null) popup.Close(); base.Dispose(disposing); }
        sealed class ChoiceList : Control {
            readonly StyledSelect owner; public int Active;
            public ChoiceList(StyledSelect select) { owner = select; Active = Math.Max(0,select.index); Height = select.items.Count*34; DoubleBuffered = true; Font = select.Font; }
            protected override void OnPaint(PaintEventArgs e) { e.Graphics.Clear(Palette.Surface); int first = Math.Max(0,e.ClipRectangle.Top/34), last = Math.Min(owner.items.Count-1,e.ClipRectangle.Bottom/34); for (int i=first;i<=last;i++) { var rect=new Rectangle(0,i*34,Width,34); if(i==Active) using(var b=new SolidBrush(Color.FromArgb(60,Palette.Accent)))e.Graphics.FillRectangle(b,rect); TextRenderer.DrawText(e.Graphics,Convert.ToString(owner.items[i]),Font,new Rectangle(10,i*34,Width-18,34),Palette.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis); } }
            protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); Active = Math.Max(0,Math.Min(owner.items.Count-1,e.Y/34)); Invalidate(); }
            protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if(e.Button!=MouseButtons.Left)return; owner.SelectedIndex = Math.Max(0,Math.Min(owner.items.Count-1,e.Y/34)); if(owner.popup!=null)owner.popup.Close(); }
        }
    }
    public sealed class StyledScrollBar : Control {
        public bool Vertical; int maximum, value, page=1, grab; bool dragging;
        public event EventHandler ValueChanged;
        public int Maximum { get { return maximum; } set { maximum=Math.Max(0,value); Value=this.value; Invalidate(); } }
        public int PageSize { get { return page; } set { page=Math.Max(1,value); Invalidate(); } }
        public int Value { get { return value; } set { int next=Math.Max(0,Math.Min(maximum,value)); if(next==this.value)return; this.value=next; Invalidate(); if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty); } }
        public StyledScrollBar() { DoubleBuffered=true; Height=14; Width=14; Cursor=Cursors.Hand; }
        Rectangle Thumb {
            get { int length=Vertical?Height:Width; int size=Math.Min(length,Math.Max(28,(int)((long)length*page/(maximum+page)))); int pos=maximum>0?(int)((long)(length-size)*value/maximum):0; return Vertical?new Rectangle(3,pos,Math.Max(1,Width-6),size):new Rectangle(pos,3,size,Math.Max(1,Height-6)); }
        }
        protected override void OnPaint(PaintEventArgs e) { e.Graphics.Clear(Palette.Background); using(var b=new SolidBrush(dragging?Palette.Accent:Palette.Muted)) e.Graphics.FillRectangle(b,Thumb); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if(e.Button!=MouseButtons.Left)return; var thumb=Thumb; int p=Vertical?e.Y:e.X,start=Vertical?thumb.Top:thumb.Left; if(!thumb.Contains(e.Location)) Value += p<start?-page:page; grab=p-(Vertical?Thumb.Top:Thumb.Left); dragging=true; Capture=true; Invalidate(); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if(!dragging)return; int length=(Vertical?Height-Thumb.Height:Width-Thumb.Width); if(length>0)Value=(int)((long)((Vertical?e.Y:e.X)-grab)*maximum/length); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging=false; Capture=false; Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if(!Capture){dragging=false;Invalidate();} }
    }
    public class StyledScrollHost : UserControl {
        protected readonly Panel viewport = new Panel();
        readonly StyledScrollBar horizontal=new StyledScrollBar(), vertical=new StyledScrollBar {Vertical=true};
        Control content; bool layingOut;
        public Size ViewSize { get { return viewport.ClientSize; } }
        public int ScrollX { get { return horizontal.Value; } }
        public int ScrollY { get { return vertical.Value; } }
        public StyledScrollHost() {
            DoubleBuffered=true; Controls.Add(viewport); Controls.Add(horizontal); Controls.Add(vertical);
            horizontal.ValueChanged += (s,e)=>MoveContent(); vertical.ValueChanged += (s,e)=>MoveContent(); Resize += (s,e)=>RefreshLayout();
        }
        public void SetContent(Control control) { if(content!=null)viewport.Controls.Remove(content); content=control; viewport.Controls.Add(control); control.MouseWheel += Wheel; RefreshLayout(); }
        protected virtual Size ContentSize(int availableWidth,int availableHeight) { if(content==null)return Size.Empty; content.Width=availableWidth; return content.Size; }
        public void RefreshLayout() {
            if(layingOut || IsDisposed)return; layingOut=true;
            try {
                int w=Math.Max(1,ClientSize.Width),h=Math.Max(1,ClientSize.Height); bool x=false,y=false; Size size=Size.Empty;
                for(int i=0;i<3;i++){size=ContentSize(Math.Max(1,w-(y?14:0)),Math.Max(1,h-(x?14:0))); x=size.Width>w-(y?14:0); y=size.Height>h-(x?14:0);}
                int vw=Math.Max(1,w-(y?14:0)),vh=Math.Max(1,h-(x?14:0)); size=ContentSize(vw,vh); viewport.SetBounds(0,0,vw,vh);
                horizontal.Visible=x; vertical.Visible=y; horizontal.SetBounds(0,vh,vw,14); vertical.SetBounds(vw,0,14,vh);
                horizontal.PageSize=vw; horizontal.Maximum=Math.Max(0,size.Width-vw); vertical.PageSize=vh; vertical.Maximum=Math.Max(0,size.Height-vh); MoveContent();
            }finally{layingOut=false;}
        }
        public void ScrollTo(int x,int y){horizontal.Value=x;vertical.Value=y;}
        public void Pan(int dx,int dy){ScrollTo(ScrollX+dx,ScrollY+dy);}
        void MoveContent(){if(content!=null)content.Location=new Point(-horizontal.Value,-vertical.Value);}
        void Wheel(object sender,MouseEventArgs e){if((ModifierKeys&Keys.Shift)!=0 || vertical.Maximum==0)Pan(-e.Delta/3,0);else Pan(0,-e.Delta/3); var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;}
    }
    public sealed class StyledGridHost : UserControl {
        readonly DataGridView grid; readonly StyledScrollBar vertical=new StyledScrollBar {Vertical=true};
        public StyledGridHost(DataGridView table) { grid=table; Dock=DockStyle.Fill; Controls.Add(grid); Controls.Add(vertical); grid.ScrollBars=ScrollBars.None; vertical.ValueChanged+=(s,e)=>{if(grid.Rows.Count>0)try{grid.FirstDisplayedScrollingRowIndex=vertical.Value;}catch(InvalidOperationException){}}; grid.RowsAdded+=(s,e)=>RefreshGrid();grid.RowsRemoved+=(s,e)=>RefreshGrid();Resize+=(s,e)=>RefreshGrid();grid.MouseWheel+=(s,e)=>{vertical.Value-=e.Delta/120*3;};grid.Scroll+=(s,e)=>{if(grid.FirstDisplayedScrollingRowIndex>=0)vertical.Value=grid.FirstDisplayedScrollingRowIndex;}; }
        void RefreshGrid(){int visible=Math.Max(1,(ClientSize.Height-grid.ColumnHeadersHeight)/Math.Max(1,grid.RowTemplate.Height));vertical.PageSize=visible;vertical.Maximum=Math.Max(0,grid.Rows.Count-visible);vertical.Visible=vertical.Maximum>0;grid.SetBounds(0,0,Math.Max(1,ClientSize.Width-(vertical.Visible?14:0)),ClientSize.Height);vertical.SetBounds(ClientSize.Width-14,0,14,ClientSize.Height);}
    }
    public sealed class ResultDialog : Form {
        readonly string result; readonly Button copy;
        public ResultDialog(string title,string text) {
            result=text; Text=title; Font=new Font("Microsoft YaHei UI",10); Width=780;Height=520;MinimumSize=new Size(540,340);StartPosition=FormStartPosition.CenterParent;
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Padding=new Padding(18)};layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,50));Controls.Add(layout);
            var host=new StyledScrollHost {Dock=DockStyle.Fill};host.SetContent(new ResultText(text){Font=Font});layout.Controls.Add(host,0,0);
            var actions=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var close=MainForm.Button("关闭",(s,e)=>Close());copy=MainForm.Button("复制说明",(s,e)=>CopyResult());actions.Controls.Add(close);actions.Controls.Add(copy);layout.Controls.Add(actions,0,1);AcceptButton=close;CancelButton=close;Palette.Apply(this);
        }
        public bool CopyResult(){try{Clipboard.SetText(result);copy.Text="已复制";return true;}catch(System.Runtime.InteropServices.ExternalException){copy.Text="复制失败，请重试";return false;}}
        sealed class ResultText : Control {
            readonly string text;readonly List<string> lines=new List<string>();
            public ResultText(string value){text=value;DoubleBuffered=true;Resize+=(s,e)=>Wrap();}
            void Wrap(){lines.Clear();foreach(string paragraph in text.Replace("\r","").Split('\n')){string line="";foreach(char c in paragraph){if(line.Length>0 && TextRenderer.MeasureText(line+c,Font,Size.Empty,TextFormatFlags.NoPadding).Width>Math.Max(1,Width-20)){lines.Add(line);line="";}line+=c;}lines.Add(line);}Height=Math.Max(30,lines.Count*(Font.Height+9)+16);Invalidate();}
            protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Palette.Surface);int lh=Font.Height+9;for(int i=Math.Max(0,e.ClipRectangle.Top/lh);i<lines.Count && i*lh<e.ClipRectangle.Bottom;i++)TextRenderer.DrawText(e.Graphics,lines[i],Font,new Point(10,8+i*lh),Palette.Text,TextFormatFlags.NoPadding);}
        }
    }
}
