using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GapGone {
    public sealed class OptionsPanel : FlowLayoutPanel {
        readonly StyledSelect format = Combo(), rate = Combo(), bits = Combo(), bitrate = Combo();
        readonly CheckBox low = new StyledCheckBox { Text = "低频消除", Width = 120 }, high = new StyledCheckBox { Text = "高频去除", Width = 120 };
        readonly StyledNumber lowHz = Number(1, 192000, 50, 0), highHz = Number(1, 192000, 21000, 0), speed = Number(0.25m, 4, 1, 2), pitch = Number(-12, 12, 0, 1);
        public readonly ApplyButton ApplyButton = new ApplyButton();
        public readonly Button ResetButton = MainForm.Button("重置设置");
        AudioInfo info; bool binding;
        public event Action Changed;
        public OptionsPanel() {
            FlowDirection = FlowDirection.LeftToRight; WrapContents = true; AutoScroll = false; Padding = new Padding(8); Dock = DockStyle.Fill;
            Add("输出格式", format); Add("采样率 Hz", rate); Add("位深 bit", bits); Add("码率 kbps", bitrate);
            Add(null,lowHz,low); Add(null,highHz,high); Add("速度 ×", speed); Add("音调 半音", pitch);
            var actions=new Panel {Height=62,Width=188,Margin=new Padding(3)};
            ApplyButton.SetBounds(0,26,90,34); ResetButton.AutoSize=false;ResetButton.SetBounds(96,26,90,34);
            actions.Controls.Add(ApplyButton);actions.Controls.Add(ResetButton);Controls.Add(actions);
            format.Items.AddRange(new object[] { "原格式输出", "转换为 MP3", "转换为 FLAC" }); format.SelectedIndex = 0;
            foreach (var combo in new[] { format, rate, bits, bitrate }) combo.SelectedIndexChanged += Change;
            foreach (var number in new[] { lowHz, highHz, speed, pitch }) number.ValueChanged += Change;
            low.CheckedChanged += Change; high.CheckedChanged += Change;
            FontChanged += (s,e) => FitFields();
            FitFields();
        }
        static StyledSelect Combo() { return new StyledSelect { Width = 144 }; }
        static StyledNumber Number(decimal min, decimal max, decimal val, int decimals) { return new StyledNumber { Width = 144, Minimum = min, Maximum = max, DecimalPlaces = decimals, Value = val, Increment = decimals > 0 ? 0.1m : 1 }; }
        void Add(string text, Control control, Control header=null) {
            var group=new Panel {Width=144,Height=62,Margin=new Padding(3)};
            if(header==null)header=new StyledLabel {Text=text,TextAlign=ContentAlignment.MiddleLeft};header.SetBounds(0,0,144,24);header.Margin=Padding.Empty;
            control.SetBounds(0,26,136,34);group.Controls.Add(header);group.Controls.Add(control);Controls.Add(group);
        }
        void FitFields() {
            SuspendLayout();
            foreach (Panel group in Controls.OfType<Panel>()) {
                if (group.Controls.Contains(ApplyButton)) continue;
                var header=group.Controls[0];var field=group.Controls[1];
                int textWidth=0;
                var select=field as StyledSelect; var number=field as StyledNumber;
                if(select!=null) textWidth=select.Items.Select(x=>TextRenderer.MeasureText(Convert.ToString(x),Font,Size.Empty,TextFormatFlags.NoPadding).Width).DefaultIfEmpty(56).Max()+36;
                if(number!=null) textWidth=new[] {number.Minimum,number.Maximum}.Select(x=>TextRenderer.MeasureText(x.ToString("F"+number.DecimalPlaces),Font,Size.Empty,TextFormatFlags.NoPadding).Width).Max()+42;
                int headerWidth=TextRenderer.MeasureText(header.Text,Font,Size.Empty,TextFormatFlags.NoPadding).Width+(header is CheckBox?23:2);
                int width=Math.Max(Math.Max(field.MinimumSize.Width,textWidth),headerWidth);
                group.Width=width;header.Width=width;field.Width=width;
            }
            ResumeLayout(true);
        }
        public int RequiredHeight {get {return Controls.Cast<Control>().Where(c=>c.Visible).Select(c=>c.Bottom+c.Margin.Bottom).DefaultIfEmpty(100).Max()+Padding.Bottom;}}
        static readonly int[] Rates = {8000,11025,12000,16000,22050,24000,32000,44100,48000,88200,96000,176400,192000};
        static readonly int[] Bitrates = {8,16,24,32,40,48,56,64,80,96,112,128,144,160,192,224,256,320};
        int previousFormat;
        static void Fill(StyledSelect combo, int[] common, int ceiling, int current, bool keep) {
            combo.Items.Clear(); combo.Items.Add("保持");
            foreach (int value in common.Concat(new[] { ceiling, current }).Where(x => x > 0 && x <= ceiling).Distinct().OrderByDescending(x => x)) combo.Items.Add(value);
            combo.SelectedItem = keep ? (object)"保持" : current;
            if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
        }
        public void Bind(AudioInfo source, OutputOptions values) {
            binding = true; info = source;
            format.SelectedIndex = values.Flac ? 2 : values.Mp3 ? 1 : 0;
            FillFields(values);
            low.Checked = values.LowCut; high.Checked = values.HighCut; lowHz.Value = (decimal)values.LowHz; highHz.Value = (decimal)values.HighHz; speed.Value = (decimal)values.Speed; pitch.Value = (decimal)values.Pitch;
            previousFormat=format.SelectedIndex; binding = false; Enabled = true; FitFields();
        }
        void FillFields(OutputOptions values) {
            int rateCeiling = values.Mp3 ? OutputOptions.Mp3Rate(info.SampleRate) : info.SampleRate;
            int bitsCeiling = values.Flac ? Math.Min(24, info.Bits > 0 ? info.Bits : 16) : info.Bits;
            Fill(rate, Rates, rateCeiling, Math.Min(values.SampleRate,rateCeiling), values.KeepRate);
            Fill(bits, info.Codec == "alac" && !values.Flac ? new[] {16,24,32} : new[] {8,16,24,32,64}, bitsCeiling, Math.Min(values.Bits,bitsCeiling), values.KeepBits);
            Fill(bitrate, Bitrates, values.Mp3 ? Math.Min(320, info.Bitrate > 0 ? info.Bitrate / 1000 : 320) : info.Bitrate/1000, values.Bitrate, values.KeepBitrate);
            bits.Enabled = values.Flac || info.Bits > 0 && info.Lossless && !values.Mp3;
            bitrate.Enabled = !values.Flac && (values.Mp3 || !info.Lossless);
        }
        void Change(object sender, EventArgs e) {
            if (binding || info == null) return;
            if (sender == format && format.SelectedIndex == 1 && info.Channels > 2) {
                var choice = TrackDialog.ConfirmMp3(FindForm(), info);
                if (choice == DialogResult.Cancel) { binding=true;format.SelectedIndex=previousFormat;binding=false;return; }
                if (choice == DialogResult.Yes) { binding=true;format.SelectedIndex=2;binding=false; }
            }
            var values = Values();
            binding = true; FillFields(values); previousFormat=format.SelectedIndex; binding = false;
            FitFields(); if (Changed != null) Changed();
        }
        static bool Keep(StyledSelect combo) { return combo.SelectedItem == null || combo.SelectedItem is string; }
        static int Value(StyledSelect combo) { return Keep(combo) ? 0 : (int)combo.SelectedItem; }
        public OutputOptions Values() {
            var values = new OutputOptions { KeepRate=Keep(rate),KeepBits=Keep(bits),KeepBitrate=Keep(bitrate),SampleRate=Value(rate),Bits=Value(bits),Bitrate=Value(bitrate),LowCut=low.Checked,HighCut=high.Checked,LowHz=(double)lowHz.Value,HighHz=(double)highHz.Value,Speed=(double)speed.Value,Pitch=(double)pitch.Value };
            values.SetFormat(info,format.SelectedIndex==1,format.SelectedIndex==2);
            if(values.Flac && values.Bits>24) values.Bits=24;
            return values;
        }
    }
}
