using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GapGone {
    public sealed class MainForm : Form {
        // 未保存窗口宽度时的默认宽度（像素）；修改后运行 reProject/build.ps1。
        public const int InitialWindowWidth = 1280;
        readonly WindowPreferences windowPreferences;
        int normalWindowWidth;
        readonly AudioEngine engine;
        readonly TimelineView timeline = new TimelineView { Dock = DockStyle.Fill };
        readonly OptionsPanel options = new OptionsPanel();
        readonly Label file = new StyledLabel { Dock = DockStyle.Fill, AutoEllipsis = true, Text = "尚未打开音频", Padding = new Padding(12, 6, 0, 0) };
        readonly Label info = new StyledLabel { Dock = DockStyle.Fill, Height = 60, Padding = new Padding(12, 4, 0, 0), Text = "本地处理 · 非破坏性编辑 · 支持多开" };
        readonly Label stats = new StyledLabel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 0, 0) };
        readonly StatusLabel status = new StatusLabel { Dock = DockStyle.Fill, Text = "就绪", Margin=new Padding(12,0,0,0) };
        readonly Button switchTrack = Button("切换音轨");
        readonly ResourceUsageLabel resources = new ResourceUsageLabel();
        readonly Panel fileRow = new Panel {Dock=DockStyle.Fill,Margin=Padding.Empty};
        readonly LoadingIndicator loading = new LoadingIndicator();
        string videoPath; List<AudioInfo> videoTracks; int videoTrack;
        readonly Button direct = Button("直接保存"), play = Button("▶ 播放"), undo = Button("撤销上一步");
        readonly CheckBox advanced = new StyledCheckBox { Text = "高级波形编辑", Width = 136, Height = 34 };
        readonly StyledNumber zoom = new StyledNumber { Minimum = 0.25m, Maximum = 20, DecimalPlaces = 2, Increment = 0.25m, Value = 1, Width = 84 };
        readonly StyledNumber threshold = new StyledNumber { Minimum = -90, Maximum = -10, Value = -36, Width = 84 };
        readonly StyledNumber padding = new StyledNumber { Minimum = 0, Maximum = 5, DecimalPlaces = 2, Increment = 0.05m, Value = 0.3m, Width = 84 };
        readonly Playback player = new Playback();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 30 };
        readonly Stack<EditState> history = new Stack<EditState>();
        readonly FlowLayoutPanel toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(8) };
        readonly FlowLayoutPanel editbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(8, 4, 0, 0) };
        TableLayoutPanel layout; bool resizingLayout; bool pendingOptions;
        AudioData audio; EditState state; FrequencyRange frequencyRange; AudioInfo measuredInfo;
        string savedSignature;
        bool dirty, busy; int version, previewVersion = -1; string previewPath;
        CancellationTokenSource operation; readonly string[] startup;
        public MainForm(AudioEngine audioEngine, string[] args) : this(audioEngine,args,WindowPreferences.DefaultPath) {}
        public MainForm(AudioEngine audioEngine, string[] args,string widthSettingsPath) {
            windowPreferences=new WindowPreferences(widthSettingsPath);
            engine = audioEngine; startup = args;
            Icon=AppAssets.Icon(); Text = AppIdentity.Title+" · 音频工作台"; Width = InitialWindowWidth; Height = 620; MinimumSize = new Size(960, 590); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Microsoft YaHei UI", 9); AllowDrop = true; KeyPreview = true;
            string themeFile = ThemePath;
            if (File.Exists(themeFile)) Palette.Dark = File.ReadAllText(themeFile).Trim() != "light";
            layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] {52,48,170,30,58,48}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            Controls.Add(layout);
            var title = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 0, 0) };
            title.Controls.Add(new PictureBox {Image=AppAssets.Logo(),SizeMode=PictureBoxSizeMode.Zoom,Width=34,Height=34,Margin=new Padding(0,0,8,0)});
            title.Controls.Add(new StyledLabel { Text = AppIdentity.Name, Width = 156, Height = 34, Font = new Font(Font.FontFamily, 17, FontStyle.Bold) });
            title.Controls.Add(new StyledLabel { Text = "音频工作台 · "+AppIdentity.Version, Width = 180, Height = 32, Padding = new Padding(0, 7, 0, 0) });
            title.Controls.Add(Button("白天 / 黑夜", (s, e) => ToggleTheme()));
            title.Controls.Add(Button("资源管理器菜单", (s, e) => MenuSettings()));
            title.Controls.Add(Button("使用说明", (s, e) => Help()));
            layout.Controls.Add(title, 0, 0);
            toolbar.Controls.Add(Button("打开音频", async (s, e) => await OpenDialog()));
            play.Click += async (s, e) => await TogglePlayback(); toolbar.Controls.Add(play);
            undo.Click += (s, e) => Undo(); toolbar.Controls.Add(undo);
            toolbar.Controls.Add(Button("重置", (s, e) => Reset()));
            direct.Click += async (s, e) => await Save(true); toolbar.Controls.Add(direct);
            toolbar.Controls.Add(Button("另存为导出", async (s, e) => await Save(false)));
            toolbar.Controls.Add(Button("批量工具", (s, e) => { StopPlayback(); using (var form = new BatchForm(engine)) form.ShowDialog(this); }));
            layout.Controls.Add(toolbar, 0, 1);
            layout.Controls.Add(options,0,2);
            file.Dock=DockStyle.None;file.TextAlign=ContentAlignment.MiddleLeft;file.Padding=Padding.Empty;file.Margin=Padding.Empty;
            fileRow.Controls.Add(file);fileRow.Controls.Add(switchTrack);switchTrack.Visible=false;switchTrack.Click+=async(s,e)=>await SwitchTrack();
            fileRow.Resize+=(s,e)=>LayoutFilename();file.TextChanged+=(s,e)=>LayoutFilename();switchTrack.VisibleChanged+=(s,e)=>LayoutFilename();
            layout.Controls.Add(fileRow,0,3);layout.RowStyles[3].Height=42;
            layout.Controls.Add(info, 0, 4);
            advanced.Margin=new Padding(3); editbar.Controls.Add(advanced); editbar.Controls.Add(InlineLabel("缩放 ×",60)); editbar.Controls.Add(zoom);
            editbar.Controls.Add(Button("RMSE 去全部静音", async (s, e) => await MarkSilence(false)));
            editbar.Controls.Add(Button("一键去头尾静音", async (s, e) => await MarkSilence(true)));
            editbar.Controls.Add(InlineLabel("阈值 dB",65)); editbar.Controls.Add(threshold);
            editbar.Controls.Add(InlineLabel("保留 s",53)); editbar.Controls.Add(padding);
            layout.Controls.Add(editbar, 0, 5);
            var editor = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            var instructions = new StyledLabel {Dock=DockStyle.Fill,Text="左键拖拽保留 · 右键拖拽删除 · 中键拖拽平移",TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(12,0,0,0)};
            editor.Controls.Add(instructions, 0, 0); editor.Controls.Add(timeline, 0, 1); editor.Controls.Add(stats, 0, 2);
            layout.Controls.Add(editor, 0, 6);
            var statusRow=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1};statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));statusRow.Controls.Add(status,0,0);statusRow.Controls.Add(loading,1,0);statusRow.Controls.Add(resources,2,0);layout.Controls.Add(statusRow,0,7);
            advanced.CheckedChanged += (s, e) => { timeline.Canvas.Advanced = advanced.Checked; if(WindowState==FormWindowState.Normal)Height=advanced.Checked?Math.Max(880,Height):Math.Max(MinimumSize.Height,620); timeline.LayoutTrack(); };
            zoom.ValueChanged += (s, e) => { timeline.Zoom = (double)zoom.Value; timeline.LayoutTrack(); };
            options.Changed += () => { if (audio == null || busy) return; UpdateDraftPending(); status.Text=pendingOptions?"输出设置待应用；播放继续使用已生效设置。":"输出设置与已生效设置一致。"; };
            options.ApplyButton.Click += async (s,e)=>await ApplyProcessing();
            options.ResetButton.Click += (s,e)=>ResetOutputSettings();
            timeline.Canvas.Edit += (r, restore) => { if (busy || audio == null) return; Push(); state.Deleted = restore ? Regions.Subtract(state.Deleted, r, audio.Info.Duration) : Regions.Normalize(state.Deleted.Concat(new[] {r}), audio.Info.Duration); Changed(); };
            timeline.Canvas.Seek += async position => { if (busy) return; bool wasPlaying = player.IsPlaying; StopPlayback(); timeline.Canvas.Position = position; RefreshStats(); timeline.Canvas.Invalidate(); if (wasPlaying) await StartPlayback(); };
            timer.Tick += (s, e) => TickPlayback(); timer.Start();
            DragEnter += (s, e) => { if (!busy && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += async (s, e) => { var paths = (string[])e.Data.GetData(DataFormats.FileDrop); if (paths.Length == 1 && !Directory.Exists(paths[0])) await Open(paths[0]); else { StopPlayback(); using (var form = new BatchForm(engine, paths)) form.ShowDialog(this); } };
            KeyDown += async (s, e) => {
                if (ActiveControl is TextBoxBase || ActiveControl is StyledNumber || ActiveControl is StyledSelect || busy) return;
                if (e.KeyCode == Keys.Space) { e.Handled = e.SuppressKeyPress = true; await TogglePlayback(); }
                if (e.Control && e.KeyCode == Keys.Z) { e.Handled = true; Undo(); }
            };
            FormClosing += (s, e) => { if (busy) { operation.Cancel(); e.Cancel = true; status.Text = "正在取消，请完成后关闭窗口。"; } else if (!ConfirmReplace()) e.Cancel = true; };
            FormClosed += (s, e) => { windowPreferences.SaveWidth(normalWindowWidth);timer.Stop(); player.Dispose(); DeletePreview(); if (audio != null) Release(audio); };
            Resize += (s,e)=>{LayoutRows();if(WindowState==FormWindowState.Normal)normalWindowWidth=Width;}; options.Layout += (s,e)=>LayoutRows();
            Shown += async (s, e) => {
                string path = startup.FirstOrDefault(x => !x.StartsWith("--") && File.Exists(x));
                if (path != null && await Open(path)) {
                    if (startup.Contains("--quick")) { var choice=audio.Info.Channels>2?TrackDialog.ConfirmMp3(this,audio.Info):DialogResult.No;if(choice==DialogResult.Cancel)return;Push(); state.Options = AudioEngine.Quick(audio.Info);if(choice==DialogResult.Yes)state.Options.SetFormat(audio.Info,false,true); state.Deleted = Analysis.Silence(audio, Math.Pow(10, -36 / 20.0), 0.1, 0.3, true); options.Bind(audio.Info, state.Options); Changed(); status.Text = "快速转换方案已准备：头尾保留 0.3 s + 50 Hz / 21 kHz 滤波；点击另存为导出。"; }
                }
            };
            Palette.Apply(this); UpdateControls(); LayoutRows();
            Width=windowPreferences.ReadWidth(InitialWindowWidth,MinimumSize.Width,Screen.FromControl(this).WorkingArea.Width);
            normalWindowWidth=Width;
        }
        static string ThemePath { get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GapGone", "theme.txt"); } }
        public static Button Button(string text, EventHandler handler = null) { var b = new StyledButton { Text = text, AutoSize = true, Height = 34, MinimumSize = new Size(90, 34), Margin = new Padding(3) }; if (handler != null) b.Click += handler; return b; }
        public static Label InlineLabel(string text,int width) {return new StyledLabel {Text=text,Width=width,Height=34,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(3)};}
        void LayoutFilename() {
            int buttonWidth=switchTrack.Visible?switchTrack.PreferredSize.Width:0;
            int available=Math.Max(0,fileRow.ClientSize.Width-24-buttonWidth-(buttonWidth>0?8:0));
            int width=Math.Min(available,TextRenderer.MeasureText(file.Text,file.Font,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Width+4);
            file.SetBounds(12,0,width,fileRow.ClientSize.Height);switchTrack.SetBounds(file.Right+8,Math.Max(0,(fileRow.ClientSize.Height-34)/2),buttonWidth,34);
        }
        void LayoutRows(){if(layout==null || resizingLayout)return;resizingLayout=true;try{layout.RowStyles[1].Height=toolbar.Controls.Cast<Control>().Select(c=>c.Bottom+c.Margin.Bottom).DefaultIfEmpty(42).Max()+toolbar.Padding.Bottom;layout.RowStyles[2].Height=options.RequiredHeight;layout.RowStyles[5].Height=editbar.Controls.Cast<Control>().Select(c=>c.Bottom+c.Margin.Bottom).DefaultIfEmpty(42).Max()+4;int required=(int)layout.RowStyles.Cast<RowStyle>().Where(r=>r.SizeType==SizeType.Absolute).Sum(r=>r.Height)+297;MinimumSize=new Size(960,Math.Max(590,required));}finally{resizingLayout=false;}}
        void ToggleTheme() { Palette.Dark = !Palette.Dark; Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ThemePath)); File.WriteAllText(ThemePath, Palette.Dark ? "dark" : "light"); Palette.Apply(this); }
        void MenuSettings() {
            bool current = ShellIntegration.Enabled;
            if (MessageBox.Show(this, current ? "关闭当前用户的 Gap Audio 右键菜单？" : "添加当前用户的右键菜单：快速转换为 MP3、音频编辑。Windows 11 中位于「显示更多选项」。", "资源管理器集成", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            try { ShellIntegration.Set(!current, Application.ExecutablePath); status.Text = current ? "右键菜单已关闭。" : "右键菜单已添加。移动程序后请关闭再开启。"; } catch (Exception e) { Error(e); }
        }
        public const string HelpText = AppIdentity.Title+"\n\n支持格式（完全离线）\n" + MediaFormats.SupportedText + "\n\n打开与剪辑\n主窗口会记住正常关闭前的宽度；最大化或最小化关闭时保留此前普通窗口宽度，再次启动按当前屏幕调整。\n点击「打开音频」或拖入单个文件；拖入多个文件或目录会进入批量工具。视频含多条音轨时先选择一条，列表第一行显示 title 标题，第二行显示 default、language、编码与声道布局（如 2、2.1、5.1、7.1）。文件名右侧的「切换音轨」可重新选择；未保存操作会询问保存、放弃或取消，保存成功后才切换。\n\n大文件加载\n源文件上限 70 GB；超过 500 MB 先显示预估加载时间，并提示先保存办公文档。加载可能造成电脑卡顿、无响应或宕机。源文件快照、解码、滤镜、预览和测量数据保存在软件安装目录下的 temp 文件夹（例如 C:\\Program Files (x86)\\Gap Audio\\temp），请预留该磁盘的空间。安装版已设置 temp 的普通用户写入权限；便携版应放在可写目录。正常关闭或取消清理本实例的临时文件；卸载删除整个 temp 及残留内容，请勿在其中保存需要保留的文件。导出提交临时文件仍位于输出目录。加载超过 5 秒，底部显示「加载进度」与「取消本次加载」；取消保留当前编辑并清理此次临时数据。时间和进度为估计值，受设备性能、编码和音轨长度影响。右下角显示本实例及其媒体处理进程已占用内存和当前可用内存；磁盘读取大音频时追加临时磁盘占用。小于 1 GB 使用 MB，否则以两位小数 GB 显示。\n\n简单时间线上方表示保留、下方红色表示删除；高级波形按行展示音频，缩放会自动调整每行时间。\n两种模式均使用左键单击定位、左键拖拽保留（恢复删除区域）、右键拖拽删除、中键拖拽平移。空格播放/暂停，Ctrl+Z 撤销。\n\n去静音与输出设置\n「RMSE 去全部静音」标记整段音频中的静音；「一键去头尾静音」只处理片头和片尾。默认阈值 −36 dB，默认保留 0.3 秒，可自行调整。\n音频默认保持原格式，视频单/双声道默认 MP3，多声道默认 FLAC。可选择转换 MP3 或 FLAC，并调整滤波、速度和音调。采样率、位深、码率默认「保持」源值，也可降低；目标格式不支持的源参数按格式限制调整（MP3 最高 48 kHz / 320 kbps，FLAC 最高 24 bit，有损源默认 16 bit）。FLAC 码率由无损编码决定，转换有损源不能恢复已丢失的音质。选择 MP3 且超过双声道时，提示改用 FLAC 保留全部声道，或明确选择混音为双声道 MP3；取消保留此前格式。预览使用单/双声道 PCM，不改变 FLAC 导出声道数。灰色选项表示不适用。\n修改输出设置后，「应用处理」以黄色呼吸效果提示；点击后设置才生效，并生成预览、更新音频参数与有效频率。待应用时播放仍使用已生效设置。\n「重置设置」只恢复默认输出参数，保留剪辑区域，变更仍须应用；工具栏「重置」恢复全部编辑，可撤销。\n\n保存与批量处理\n「直接保存」更新当前文件；文件被占用或无法替换时自动另存。「另存为导出」保存到新文件，重名自动添加递增序号。变更格式、速度或音调，以及提取视频音轨时请另存为。\n批量工具统一应用去静音、滤波和输出参数，不支持逐轨手工剪辑。「批量导出」处理全部文件，「勾选项转 MP3」只处理勾选文件，多声道同样可改用 FLAC。去静音模式、保留、阈值位于单独一行；列表显示文件名、音频参数和处理状态，格式与声道数包含在音频参数中。添加文件后读取参数，多音轨视频逐文件选择要处理的音轨。导出失败会保留原件。\n保存/导出前，所有操作仅用于编辑和临时预览，源文件保持原样。\n\n主题与资源管理器菜单\n「白天 / 黑夜」切换配色并记住选择。灰色按钮表示当前不可用，准备处理时状态文字显示黄色。\n「资源管理器菜单」可开启或关闭「音频编辑」和「快速转换为 MP3」入口；Windows 11 位于「显示更多选项」。快速转换只准备方案，点击导出后才写入文件。移动程序目录后请关闭再开启菜单。";
        void Help() { using(var dialog=new ResultDialog("使用说明",HelpText))dialog.ShowDialog(this); }
        bool ConfirmReplace() { return !dirty && !pendingOptions || MessageBox.Show(this, "当前音频有未保存的编辑或未应用的设置。继续将放弃这些操作；取消可返回编辑并保存。", "未保存的编辑", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK; }
        async Task OpenDialog() {
            using (var dialog = new OpenFileDialog { Filter = MediaFormats.OpenFilter }) if (dialog.ShowDialog(this) == DialogResult.OK) await Open(dialog.FileName);
        }
        static void Release(AudioData data) { data.Dispose(); AudioEngine.ReleaseSnapshot(data.Info); }
        async Task<bool> Open(string path) {
            if (busy || !ConfirmReplace()) return false;
            try { LoadPolicy.ValidateSize(new FileInfo(path).Length); } catch(Exception e) {Error(e);return false;}
            bool loaded=false; StopPlayback();
            await Work("正在检测音轨…", async token=>{
                var tracks=await Task.Run(()=>engine.ProbeTracks(path,token),token);token.ThrowIfCancellationRequested();
                if(!LoadPolicy.Confirm(this,path,tracks.Max(x=>x.Duration))){status.Text="已取消大文件加载。";return;}
                int track=MediaFormats.IsVideo(path)?TrackDialog.Choose(this,tracks):-1;
                if(!MediaFormats.IsVideo(path))track=0;
                if(track<0){status.Text="已取消音轨选择。";return;}
                loaded=await LoadSelected(path,tracks,track,token);
            },true);return loaded;
        }
        async Task<bool> LoadSelected(string path,List<AudioInfo> tracks,int track,CancellationToken token) {
            status.Text="正在复制、解码并分析所选音轨…"; AudioData data=null;
            try {
                data=await Task.Run(()=>engine.LoadSnapshot(path,token,track,p=>loading.Progress(p*95/100)),token);
                var measured=await Task.Run(()=>Analysis.MeasureFrequencyRange(data,token),token);token.ThrowIfCancellationRequested();
                loading.Progress(100);StopPlayback();DeletePreview();if(audio!=null)Release(audio);audio=data;data=null;
                frequencyRange=measured;measuredInfo=audio.Info;state=new EditState {Options=OutputOptions.For(audio.Info)};savedSignature=state.Signature();history.Clear();dirty=false;version++;previewVersion=-1;
                timeline.Canvas.SetData(audio);timeline.Canvas.Deleted=state.Deleted;zoom.Value=(decimal)Math.Min(20,Math.Max(1,audio.Info.Duration/600));timeline.Zoom=(double)zoom.Value;timeline.LayoutTrack();
                options.Bind(audio.Info,state.Options);pendingOptions=false;file.Text=System.IO.Path.GetFileName(path);Text=AppIdentity.Title+" · "+file.Text;
                videoPath=audio.Info.VideoSource?path:null;videoTracks=audio.Info.VideoSource?tracks:null;videoTrack=track;switchTrack.Visible=videoPath!=null;
                RefreshInfo();RefreshStats();status.Text=audio.Info.VideoSource?"已加载音轨 "+(track+1)+" / "+tracks.Count+"；另存为音频文件，原视频保留。":"已打开音频。";return true;
            } finally {if(data!=null)Release(data);}
        }
        async Task SwitchTrack() {
            if(busy || videoPath==null)return;
            if(dirty || pendingOptions){
                var answer=MessageBox.Show(this,"当前音轨有未保存的操作，是否先保存？\n是：保存后切换；否：放弃操作并切换；取消：继续编辑。","切换音轨",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
                if(answer==DialogResult.Cancel)return;
                if(answer==DialogResult.Yes){if(pendingOptions){await ApplyProcessing();if(pendingOptions || previewVersion!=version)return;}if(!await Save(false,false))return;}
            }
            int selected=TrackDialog.Choose(this,videoTracks,videoTrack);if(selected<0)return;
            string path=videoPath;var tracks=videoTracks;
            try{LoadPolicy.ValidateSize(new FileInfo(path).Length);}catch(Exception e){Error(e);return;}
            await Work("正在切换音轨…",async token=>{await LoadSelected(path,tracks,selected,token);},true);
        }
        async Task Work(string message, Func<CancellationToken, Task> action, bool isLoading=false) {
            if (busy) return; busy = true; status.Preparing=true; operation = new CancellationTokenSource(); status.Text = message; UpdateControls();
            if(isLoading)loading.Begin(operation);
            try { await action(operation.Token); } catch (OperationCanceledException) { status.Text = "操作已取消，当前编辑与原文件保留。"; } catch (Exception e) { Error(e); }
            finally { loading.End();operation.Dispose(); operation = null; busy = false; status.Preparing=false; UpdateControls(); }
        }
        void Error(Exception e) { status.Text = "操作失败：" + e.Message; MessageBox.Show(this, e.Message, "Gap Audio", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        void UpdateControls() {
            switchTrack.Enabled=!busy && videoPath!=null;
            foreach (Control c in toolbar.Controls) c.Enabled = !busy && (c.Text == "打开音频" || c.Text == "批量工具" || audio != null) && (c.Text != "另存为导出" || !pendingOptions);
            foreach (Control c in editbar.Controls) c.Enabled = !busy && audio != null;
            play.Enabled = !busy && audio != null; undo.Enabled = !busy && history.Count > 0;
            direct.Enabled = !busy && audio != null && !pendingOptions && !state.Options.RequiresSaveAs(audio.Info);
            options.ApplyButton.Enabled=!busy && audio!=null;
            options.ApplyButton.Pending=pendingOptions && !busy;
            options.Enabled = !busy && audio != null; timeline.Enabled = !busy; advanced.Enabled = zoom.Enabled = !busy;
        }
        void Push() { if (state != null) history.Push(state.Copy()); }
        void Changed() { StopPlayback(); dirty = state.Signature() != savedSignature; frequencyRange = null; measuredInfo = null; version++; previewVersion = -1; timeline.Canvas.Deleted = state.Deleted; timeline.Canvas.Invalidate(); RefreshStats(); RefreshInfo(); UpdateControls(); status.Text="编辑已更新，点击「应用处理」准备预览。"; }
        void Undo() { if (busy || history.Count == 0) return; state = history.Pop(); options.Bind(audio.Info, state.Options);pendingOptions=false; Changed(); status.Text = "已撤销上一步。"; }
        void Reset() { if (busy || audio == null) return; Push(); state = new EditState { Options = OutputOptions.For(audio.Info) }; options.Bind(audio.Info, state.Options);pendingOptions=false; Changed(); status.Text = "已重置；可以撤销重置。"; }
        void UpdateDraftPending(){pendingOptions=new EditState {Options=options.Values(),Deleted=state.Deleted}.Signature()!=state.Signature();UpdateControls();}
        void ResetOutputSettings(){if(audio==null || busy)return;options.Bind(audio.Info,OutputOptions.For(audio.Info));UpdateDraftPending();status.Text=pendingOptions?"输出设置已恢复默认，点击「应用处理」生效。":"输出设置已恢复默认；剪辑区域保留。";}
        async Task ApplyProcessing(){if(audio==null || busy)return;try{var next=options.Values();next.Validate(audio.Info);StopPlayback();if(new EditState{Options=next,Deleted=state.Deleted}.Signature()!=state.Signature()){Push();state.Options=next;pendingOptions=false;Changed();}else pendingOptions=false;}catch(Exception e){Error(e);return;}await Work("正在准备：应用处理并测量音频…",async token=>{await EnsurePreview(token);await MeasureCurrent(token);status.Text="处理已应用，预览准备完成。";});}
        async Task MarkSilence(bool edges) {
            if (audio == null) return; StopPlayback();
            double limit = Math.Pow(10, (double)threshold.Value / 20), keep = (double)padding.Value;
            await Work("正在使用 RMSE 检测静音…", async token => {
                var detected = await Task.Run(() => Analysis.Silence(audio, limit, 0.1, keep, edges, token), token);
                token.ThrowIfCancellationRequested(); Push(); state.Deleted = Regions.Normalize(state.Deleted.Concat(detected), audio.Info.Duration); Changed(); status.Text = "已标记 " + detected.Count + " 段静音，导出前原文件保持原样。";
            });
        }
        double EditedDuration { get { return audio == null ? 0 : Regions.Kept(state.Deleted, audio.Info.Duration).Sum(x => x.End - x.Start) / state.Options.Speed; } }
        void RefreshInfo() {
            if (audio == null) return;
            var o = state.Options;
            string hz = frequencyRange == null ? "待应用处理测量" : frequencyRange.MaxHz == 0 ? "无有效信号" : String.Format("{0:N0}–{1:N0} Hz", frequencyRange.MinHz, frequencyRange.MaxHz);
            int outputBitrate = o.Mp3 ? Math.Min(320, o.Bitrate) : audio.Info.Codec.StartsWith("pcm_") ? o.SampleRate * audio.Info.Channels * o.Bits / 1000 : o.Bitrate;
            if (measuredInfo != null) outputBitrate = measuredInfo.Bitrate / 1000;
            bool variableLossless=o.IsLossless(audio.Info) && (o.Flac || !audio.Info.Codec.StartsWith("pcm_"));
            string bitrateText=variableLossless && measuredInfo==null?"码率由无损编码决定":outputBitrate+" kbps";
            info.Text = "源文件：" + audio.Info.Summary + "\n当前输出：" + o.SampleRate.ToString("N0") + " Hz · " + (o.IsLossless(audio.Info) ? o.Bits + " bit" : "位深不适用") + " · " + bitrateText + (measuredInfo == null ? "（目标值）" : "（实测）") + " · 最低 / 最高有效频率：" + hz;
        }
        void RefreshStats() { if (audio == null) return; stats.Text = "光标 " + TimelineCanvas.Time(timeline.Canvas.Position) + " / " + TimelineCanvas.Time(audio.Info.Duration) + "     编辑后 " + TimelineCanvas.Time(EditedDuration) + "     删除 " + state.Deleted.Count + " 段 · " + state.Deleted.Sum(x => x.End - x.Start).ToString("F2") + " s\n" + (dirty ? "● 有未保存编辑" : "已保存 / 尚未编辑") + (state.Options.RequiresSaveAs(audio.Info) ? " · 速度、音调或格式已变更，请另存为" : ""); }
        void DeletePreview() { if (previewPath != null && File.Exists(previewPath)) { try { File.Delete(previewPath); } catch (IOException) { } } ResourceUsage.ForgetFile(previewPath);previewPath = null; previewVersion = -1; }
        async Task EnsurePreview(CancellationToken token) {
            if (previewVersion == version && previewPath != null && File.Exists(previewPath)) return;
            DeletePreview(); string path = engine.CreateTemporaryPath("preview-", ".wav");
            try { await Task.Run(() => engine.Render(audio.Info, state.Deleted, state.Options, path, true, token), token); previewPath = path; previewVersion = version; } finally { if (previewPath != path) {if(File.Exists(path)) File.Delete(path);ResourceUsage.ForgetFile(path);} }
        }
        double SourceToPreview(double source) { double elapsed = 0; foreach (var r in Regions.Kept(state.Deleted, audio.Info.Duration)) { if (source <= r.End) return (elapsed + Math.Max(0, source - r.Start)) / state.Options.Speed; elapsed += r.End - r.Start; } return elapsed / state.Options.Speed; }
        double PreviewToSource(double preview) { double elapsed = preview * state.Options.Speed; var kept = Regions.Kept(state.Deleted, audio.Info.Duration); foreach (var r in kept) { if (elapsed <= r.End - r.Start) return r.Start + elapsed; elapsed -= r.End - r.Start; } return kept.Count > 0 ? kept.Last().End : 0; }
        async Task StartPlayback() {
            if (audio == null || busy) return;
            await Work("正在准备编辑后预览…", async token => { await EnsurePreview(token); double offset = SourceToPreview(timeline.Canvas.Position); if (offset >= EditedDuration - 0.01) offset = 0; player.Play(previewPath, offset); play.Text = "Ⅱ 暂停"; status.Text = pendingOptions?"正在播放已生效设置；待应用的输出设置尚未生效。":"播放编辑后预览。"; });
        }
        async Task TogglePlayback() { if (player.IsPlaying) StopPlayback(); else await StartPlayback(); }
        void StopPlayback() { if (player.IsPlaying && audio != null) timeline.Canvas.Position = PreviewToSource(player.Position); player.Stop(); play.Text = "▶ 播放"; timeline.Canvas.Invalidate(); RefreshStats(); }
        void TickPlayback() {
            if (!player.IsPlaying || audio == null) return;
            if (player.Ended) { timeline.Canvas.Position = PreviewToSource(EditedDuration); player.Stop(); play.Text = "▶ 播放"; status.Text = "播放结束。"; }
            else timeline.Canvas.Position = PreviewToSource(player.Position);
            timeline.Canvas.Invalidate(); RefreshStats();
        }
        async Task MeasureCurrent(CancellationToken token) {
                string path = engine.CreateTemporaryPath("analysis-", state.Options.Extension(audio.Info));
                AudioData current=null;
                try {
                    if (!dirty) current = audio;
                    else {
                        await Task.Run(() => engine.Render(audio.Info, state.Deleted, state.Options, path, false, token), token);
                        current = await Task.Run(() => engine.Load(path, token), token);
                    }
                    frequencyRange = await Task.Run(() => Analysis.MeasureFrequencyRange(current, token), token); measuredInfo = current.Info; RefreshInfo();
                } finally { if(current!=null && current!=audio)current.Dispose(); if (File.Exists(path)) File.Delete(path);ResourceUsage.ForgetFile(path); }
        }
        async Task<bool> Save(bool overwrite, bool reload=true) {
            if (audio == null || busy || pendingOptions) return false; StopPlayback();
            string target = audio.Info.Path;
            if (overwrite && state.Options.RequiresSaveAs(audio.Info)) return false;
            if (!overwrite) using (var dialog = new SaveFileDialog { OverwritePrompt = false, AddExtension = true, DefaultExt = state.Options.Extension(audio.Info).TrimStart('.'), Filter = "音频|*" + state.Options.Extension(audio.Info), FileName = System.IO.Path.GetFileNameWithoutExtension(audio.Info.Path) + "_edited" + state.Options.Extension(audio.Info), InitialDirectory = System.IO.Path.GetDirectoryName(audio.Info.Path) }) { if (dialog.ShowDialog(this) != DialogResult.OK) return false; target = dialog.FileName; }
            bool saved=false;
            await Work("正在生成并验证导出结果…", async token => {
                string result = await Task.Run(() => engine.Export(audio.Info, state.Deleted, state.Options, target, overwrite, token), token);
                saved=true;dirty = false; savedSignature=state.Signature(); status.Text = "已保存：" + result + (overwrite && result != target ? "（原文件被占用或无法替换，已自动另存）" : ""); RefreshStats();
                if(!reload)return;loading.Begin(operation);
                // Continue editing the committed result.
                AudioData data;try{data=await Task.Run(()=>engine.LoadSnapshot(result,token,0,loading.Progress),token);}catch(OperationCanceledException){status.Text="导出已保存："+result+"；已取消重新加载，当前编辑保留。";return;}catch(Exception e){status.Text="导出已保存："+result+"；重新加载失败："+e.Message;return;}Release(audio); audio = data; state = new EditState { Options = OutputOptions.For(audio.Info) }; savedSignature = state.Signature(); history.Clear(); DeletePreview(); version++; timeline.Canvas.SetData(audio); options.Bind(audio.Info, state.Options); timeline.LayoutTrack(); frequencyRange = null; measuredInfo = null; file.Text = System.IO.Path.GetFileName(result); Text = AppIdentity.Title+" · " + file.Text; RefreshInfo(); RefreshStats();
            });return saved;
        }
    }
}
