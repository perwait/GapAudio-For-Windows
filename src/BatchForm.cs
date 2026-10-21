using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GapGone {
    public sealed class BatchForm : Form {
        readonly AudioEngine engine;
        readonly List<BatchItem> items = new List<BatchItem>();
        readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Palette.Surface };
        readonly FlowLayoutPanel toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8) }, settings = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8,4,8,0),Margin=Padding.Empty }, silencebar = new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(8,0,8,4),Margin=Padding.Empty};
        readonly StatusLabel status = new StatusLabel { Dock = DockStyle.Fill, Text = "批量操作对所有文件应用相同方案，不支持逐条剪切。", Margin = new Padding(10,0,0,0) };
        readonly ResourceUsageLabel resources = new ResourceUsageLabel();
        TableLayoutPanel layout;bool resizingLayout, resizingColumns;
        readonly LoadingIndicator loading = new LoadingIndicator();
        readonly StyledSelect format = Combo(new object[] {"原格式", "MP3", "FLAC"}), silence = Combo(new object[] {"去头尾静音", "去全部静音", "不去静音"}), rate = Combo(new object[] {"保持", 8000,16000,22050,24000,32000,44100,48000,96000,192000}), bits = Combo(new object[] {"保持",8,16,24,32}), bitrate = Combo(new object[] {"保持",32,64,96,128,160,192,224,256,320});
        readonly CheckBox low = new StyledCheckBox { Text = "低频消除", Checked = true, Width = 90,Height=34,Margin=new Padding(3) }, high = new StyledCheckBox { Text = "高频去除", Checked = true, Width = 90,Height=34,Margin=new Padding(3) };
        readonly StyledNumber keep = Number(0, 5, 0.3m, 2), threshold = Number(-90, -10, -36, 0), lowHz = Number(1, 192000, 50, 0), highHz = Number(1, 192000, 21000, 0), speed = Number(0.25m, 4, 1, 2), pitch = Number(-12,12,0,1);
        CancellationTokenSource operation; bool busy; int previousFormat;
        public BatchForm(AudioEngine e, params string[] paths) {
            engine = e; Icon=AppAssets.Icon(); Text = AppIdentity.Title+" · 批量编辑"; Width = 1260; Height = 760; MinimumSize = new Size(1040, 660); StartPosition = FormStartPosition.CenterParent; Font = new Font("Microsoft YaHei UI", 9); AllowDrop = true;
            layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 154)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); Controls.Add(layout);
            toolbar.Controls.Add(MainForm.Button("添加文件", (s, args) => { using (var dialog = new OpenFileDialog { Multiselect = true, Filter = MediaFormats.OpenFilter }) if (dialog.ShowDialog(this) == DialogResult.OK) Add(dialog.FileNames); }));
            toolbar.Controls.Add(MainForm.Button("添加目录", (s, args) => { using (var dialog = new FolderBrowserDialog()) if (dialog.ShowDialog(this) == DialogResult.OK) Add(new[] {dialog.SelectedPath}); }));
            toolbar.Controls.Add(MainForm.Button("清空列表", (s, args) => { items.Clear(); RefreshRows(); }));
            toolbar.Controls.Add(MainForm.Button("批量导出", async (s, args) => await ConvertFiles(false)));
            toolbar.Controls.Add(MainForm.Button("勾选项转 MP3", async (s, args) => await ConvertFiles(true)));
            AddSetting("格式", format); AddSetting("去静音模式", silence, silencebar); AddSetting("保留 s", keep, silencebar); AddSetting("阈值 dB", threshold, silencebar); AddSetting("采样率", rate); AddSetting("位深", bits); AddSetting("码率 kbps", bitrate);
            settings.Controls.Add(low); settings.Controls.Add(lowHz); settings.Controls.Add(high); settings.Controls.Add(highHz); AddSetting("速度 ×", speed); AddSetting("音调半音", pitch);
            settings.Controls.Add(new StyledLabel { Text = "默认保持源参数；MP3 最高 48 kHz / 320 kbps / 双声道，多声道可改用 FLAC。超出源值自动降低；失败保留原件。", Width = 1080, Height = 22,Margin=new Padding(3,0,3,0) });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "selected", HeaderText = "勾选", FillWeight = 8 });
            foreach (var pair in new[] {new[] {"name","文件"}, new[] {"params","时长 / 音频参数"}, new[] {"status","处理状态"}}) grid.Columns.Add(new DataGridViewTextBoxColumn { Name = pair[0], HeaderText = pair[1], ReadOnly = true, FillWeight = pair[0] == "params" ? 50 : 25 });
            grid.SizeChanged+=(s,args)=>LayoutColumns();grid.FontChanged+=(s,args)=>LayoutColumns();
            grid.CurrentCellDirtyStateChanged += (s, args) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += (s, args) => { if (args.RowIndex >= 0 && args.ColumnIndex == 0 && args.RowIndex < items.Count) items[args.RowIndex].Selected = Convert.ToBoolean(grid.Rows[args.RowIndex].Cells[0].Value); };
            layout.Controls.Add(toolbar, 0, 0); layout.Controls.Add(settings, 0, 1); layout.Controls.Add(silencebar,0,2);layout.Controls.Add(new StyledGridHost(grid), 0, 3);
            var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));footer.Controls.Add(status,0,0);footer.Controls.Add(loading,1,0);footer.Controls.Add(resources,2,0);layout.Controls.Add(footer,0,4);
            DragEnter += (s, args) => { if (!busy && args.Data.GetDataPresent(DataFormats.FileDrop)) args.Effect = DragDropEffects.Copy; }; DragDrop += (s, args) => {if(!busy)Add((string[])args.Data.GetData(DataFormats.FileDrop));};
            FormClosing += (s, args) => { if (busy) { operation.Cancel(); args.Cancel = true; status.Text = "正在取消，完成后可以关闭。"; } };
            Shown+=async(s,args)=>await ReadMetadata();
            format.SelectedIndexChanged+=(s,args)=>{if(format.SelectedIndex==1){var surround=items.FirstOrDefault(x=>x.Info!=null && x.Info.Channels>2);if(surround!=null){var answer=TrackDialog.ConfirmMp3(this,surround.Info);if(answer==DialogResult.Yes)format.SelectedIndex=2;else if(answer==DialogResult.Cancel)format.SelectedIndex=previousFormat;}}previousFormat=format.SelectedIndex;bitrate.Enabled=format.SelectedIndex!=2;bits.Enabled=format.SelectedIndex!=1;};
            Resize+=(s,args)=>LayoutRows();settings.Layout+=(s,args)=>LayoutRows();silencebar.Layout+=(s,args)=>LayoutRows();FontChanged+=(s,args)=>FitFields();
            FitFields();Add(paths); Palette.Apply(this);LayoutRows();
            LayoutColumns();
        }
        void LayoutColumns() {
            if(resizingColumns || grid.Columns.Count!=4 || grid.ClientSize.Width<100)return;resizingColumns=true;
            try {
                int total=grid.ClientSize.Width-2;
                int transfer=TextRenderer.MeasureText(new string('字',10),grid.Font,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Width;
                int selected=Math.Max(grid.Columns["selected"].MinimumWidth,(int)Math.Round(total*8.0/108));
                int statusWidth=Math.Max(grid.Columns["status"].MinimumWidth,(int)Math.Round(total*25.0/108));
                int nameWidth=Math.Max(grid.Columns["name"].MinimumWidth,Math.Min((int)Math.Round(total*25.0/108)+transfer,total-selected-statusWidth-grid.Columns["params"].MinimumWidth));
                foreach(string name in new[]{"selected","name","status"})grid.Columns[name].AutoSizeMode=DataGridViewAutoSizeColumnMode.None;
                grid.Columns["selected"].Width=selected;grid.Columns["name"].Width=nameWidth;grid.Columns["status"].Width=statusWidth;
                grid.Columns["params"].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;
            }finally{resizingColumns=false;}
        }
        static StyledSelect Combo(object[] values) { var c = new StyledSelect { Width = 80 }; c.Items.AddRange(values); c.SelectedIndex = 0; return c; }
        static StyledNumber Number(decimal min, decimal max, decimal value, int decimals) { return new StyledNumber { Minimum = min, Maximum = max, DecimalPlaces = decimals, Value = value, Width = 84, Increment = decimals > 0 ? 0.1m : 1 }; }
        void AddSetting(string label, Control control, FlowLayoutPanel target=null) {
            var group=new Panel{Height=34,Margin=new Padding(3)};
            var caption=MainForm.InlineLabel(label,TextRenderer.MeasureText(label,Font).Width+4);caption.Location=Point.Empty;
            group.Controls.Add(caption);group.Controls.Add(control);(target??settings).Controls.Add(group);
        }
        void FitFields() {
            foreach(var select in new[]{format,rate,bits,bitrate,silence})select.Width=Math.Max(select.MinimumSize.Width,select.Items.Cast<object>().Select(x=>TextRenderer.MeasureText(Convert.ToString(x),Font,Size.Empty,TextFormatFlags.NoPadding).Width).DefaultIfEmpty(24).Max()+36);
            foreach(var panel in new[]{settings,silencebar})foreach(var group in panel.Controls.OfType<Panel>()){
                var caption=group.Controls[0];var control=group.Controls[1];caption.Width=TextRenderer.MeasureText(caption.Text,Font).Width+4;control.Location=new Point(caption.Width+4,0);group.Width=control.Right;
            }
            LayoutRows();
        }
        void LayoutRows() {
            if(layout==null || resizingLayout)return;resizingLayout=true;
            try { foreach(var pair in new[]{Tuple.Create(toolbar,0),Tuple.Create(settings,1),Tuple.Create(silencebar,2)})layout.RowStyles[pair.Item2].Height=pair.Item1.Controls.Cast<Control>().Where(c=>c.Visible).Select(c=>c.Bottom+c.Margin.Bottom).DefaultIfEmpty(34).Max()+pair.Item1.Padding.Bottom; }
            finally{resizingLayout=false;}
        }
        void Add(IEnumerable<string> paths) {
            foreach (var path in paths) {
                if (Directory.Exists(path)) { try { Add(Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Where(MediaFormats.Supported)); } catch (Exception ex) { status.Text = ex.Message; } continue; }
                if (!File.Exists(path)) continue; string full = System.IO.Path.GetFullPath(path);
                if (!items.Any(x => String.Equals(x.Path, full, StringComparison.OrdinalIgnoreCase))) items.Add(new BatchItem { Path = full });
            }
            RefreshRows(); status.Text = items.Count + " 个文件；批量导出处理全部文件，勾选项可单独转为 MP3。"; if(Visible && !busy)BeginInvoke(new Action(async()=>await ReadMetadata()));
        }
        void RefreshRows() {
            grid.Rows.Clear(); foreach (var item in items) {
                int index = grid.Rows.Add(item.Selected, System.IO.Path.GetFileName(item.Path), item.Info == null ? "导出时读取" : TimelineCanvas.Time(item.Info.Duration) + " · " + item.Info.Summary, item.Status);
                grid.Rows[index].Cells["name"].ToolTipText = item.Path; grid.Rows[index].Cells["status"].ToolTipText = item.Output;
            }
        }
        async Task Work(Func<CancellationToken, Task> action) {
            if (busy || items.Count == 0) return; busy = true; status.Preparing=true; operation = new CancellationTokenSource(); toolbar.Enabled = settings.Enabled = silencebar.Enabled = grid.Enabled = false;
            loading.Begin(operation);try { await action(operation.Token); } catch (OperationCanceledException) { status.Text = "已取消；已完成的导出保留，未完成文件不修改。"; } catch (Exception ex) { status.Text = ex.Message; MessageBox.Show(this, ex.Message, "批量处理失败"); }
            finally { loading.End(); busy = false; status.Preparing=false; operation.Dispose(); operation = null; toolbar.Enabled = settings.Enabled = silencebar.Enabled = grid.Enabled = true; RefreshRows(); }
        }
        async Task<bool> Metadata(BatchItem item,CancellationToken token) {
            LoadPolicy.ValidateSize(new FileInfo(item.Path).Length);if(item.Info!=null)return true;
            var tracks=await Task.Run(()=>engine.ProbeTracks(item.Path,token),token);token.ThrowIfCancellationRequested();
            if(!item.SizeApproved){if(!LoadPolicy.Confirm(this,item.Path,tracks.Max(x=>x.Duration))){item.Status="已跳过加载";return false;}item.SizeApproved=true;}
            int selected=MediaFormats.IsVideo(item.Path)?TrackDialog.Choose(this,tracks):0;
            if(selected<0){item.Status="已取消音轨选择";return false;}
            item.Info=tracks[selected];item.Status="等待";return true;
        }
        async Task ReadMetadata() {
            if(busy)return;
            await Work(async token=>{foreach(var item in items.ToArray()){token.ThrowIfCancellationRequested();if(item.Info!=null)continue;status.Text="正在读取文件信息："+System.IO.Path.GetFileName(item.Path);try{await Metadata(item,token);}catch(OperationCanceledException){throw;}catch(Exception e){item.Status=e.Message;}RefreshRows();}status.Text=items.Count+" 个文件；音轨和音频参数已读取。";});
        }
        async Task ConvertFiles(bool selectedOnly) {
            grid.EndEdit(); var chosen = (selectedOnly ? items.Where(x => x.Selected) : items).ToArray();
            if (chosen.Length == 0) { status.Text = "请勾选要转换的文件。"; return; }
            string directory;
            using (var dialog = new FolderBrowserDialog { Description = "选择导出目录；重名会自动添加递增序号。" }) { if (dialog.ShowDialog(this) != DialogResult.OK) return; directory = dialog.SelectedPath; }
            await ConvertFilesTo(chosen, directory, selectedOnly);
        }
        async Task ConvertFilesTo(BatchItem[] chosen, string directory, bool selectedOnly) {
            bool mp3 = selectedOnly || format.SelectedIndex == 1, flac = !selectedOnly && format.SelectedIndex==2, edge = silence.SelectedIndex == 0, cut = silence.SelectedIndex != 2;
            int targetRate = rate.SelectedIndex == 0 ? 0 : (int)rate.SelectedItem, targetBits = bits.SelectedIndex == 0 ? 0 : (int)bits.SelectedItem, targetBitrate = bitrate.SelectedIndex == 0 ? 0 : (int)bitrate.SelectedItem;
            double pad = (double)keep.Value, limit = Math.Pow(10, (double)threshold.Value / 20), lowValue = (double)lowHz.Value, highValue = (double)highHz.Value, tempo = (double)speed.Value, semitones = (double)pitch.Value; bool lowCut = low.Checked, highCut = high.Checked;
            await Work(async token => {
                int done = 0, successes = 0;
                foreach (var item in chosen) {
                    token.ThrowIfCancellationRequested(); status.Text = "转换 " + (++done) + " / " + chosen.Length + "：" + item.Path;
                    item.Output = null;
                    AudioData data=null;
                    try {
                        if(!await Metadata(item,token))continue;
                        bool fileMp3=mp3,fileFlac=flac;
                        if(fileMp3 && item.Info.Channels>2){var answer=TrackDialog.ConfirmMp3(this,item.Info);if(answer==DialogResult.Cancel){item.Status="已跳过转换";continue;}if(answer==DialogResult.Yes){fileMp3=false;fileFlac=true;}}
                        loading.Begin(operation);
                        data = await Task.Run(() => engine.LoadSnapshot(item.Path, token,item.Info.AudioTrackIndex,p=>loading.Progress(p*75/100)), token); item.Info = data.Info;
                        loading.Progress(75);
                        var o = OutputOptions.For(data.Info);if(fileMp3 || fileFlac || !data.Info.VideoSource)o.SetFormat(data.Info,fileMp3,fileFlac);
                         o.LowCut = lowCut; o.HighCut = highCut; o.LowHz = lowValue; o.HighHz = highValue; o.Speed = tempo; o.Pitch = semitones;
                        if (targetRate > 0) { o.SampleRate = Math.Min(o.SampleRate, targetRate); if (fileMp3) o.SampleRate = new[] {8000,11025,12000,16000,22050,24000,32000,44100,48000}.Where(x => x <= o.SampleRate).DefaultIfEmpty(8000).Max(); }
                        if (targetBits > 0 && o.Bits > 0) o.Bits = Math.Min(o.Bits, targetBits);
                        if (targetBitrate > 0) o.Bitrate = Math.Min(o.Bitrate, targetBitrate);
                        var deleted = cut ? await Task.Run(() => Analysis.Silence(data, limit, 0.1, pad, edge, token), token) : new List<Region>();
                        loading.Progress(98);
                        string requested = System.IO.Path.Combine(directory, System.IO.Path.GetFileNameWithoutExtension(item.Path) + "_edited" + o.Extension(data.Info));
                        item.Output = await Task.Run(() => engine.Export(data.Info, deleted, o, requested, false, token), token);
                        item.Status = "转换成功"; successes++;
                    } catch (OperationCanceledException) { throw; } catch (Exception ex) { item.Status = (item.Output != null ? "导出保留；" : "原件保留；") + ex.Message; } finally {loading.End();if(data!=null){data.Dispose();AudioEngine.ReleaseSnapshot(data.Info);}}
                    RefreshRows();
                }
                status.Text = "完成：" + successes + " / " + chosen.Length + " 个文件成功。导出目录：" + directory;
            });
        }
    }
}
