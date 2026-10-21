using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GapGone;
using Region = GapGone.Region;

static class CoreTests {
    static int assertions;
    static readonly CancellationToken Token = CancellationToken.None;
    static void Check(bool condition, string description) { assertions++; if (!condition) throw new Exception("FAIL: " + description); Console.WriteLine("PASS: " + description); }
    static void Near(double actual, double expected, double tolerance, string description) { Check(Math.Abs(actual - expected) <= tolerance, description + " (" + actual.ToString("F4") + ")"); }
    static void Throws(Action action, string description) { bool threw = false; try { action(); } catch (InvalidOperationException) { threw = true; } Check(threw, description); }
    static string Hash(string path) { using (var sha = SHA256.Create()) using (var f = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(f)); }
    static AudioData Signal(int rate, int channels, int seconds, int bits) {
        var data = new AudioData { Info = new AudioInfo { Codec = "pcm_s" + bits + "le", SampleRate = rate, Channels = channels, Bits = bits, Duration = seconds, Bitrate = rate * channels * bits, Format = "wav" }, Samples = new double[rate * channels * seconds] };
        for (int f = rate; f < rate * (seconds - 1); f++) {
            if (f >= rate * 2 && f < rate * 3) continue;
            // First channel silent, second active. A first-channel-only detector would delete everything.
            data.Samples[f * channels + channels - 1] = Math.Round(0.4 * Math.Sin(2 * Math.PI * 1000 * f / rate) * 32768) / 32768;
        }
        return data;
    }
    static void Wav(string path, AudioData data, int bits) {
        using (var writer = new BinaryWriter(File.Create(path))) {
            int bytes = bits / 8; int size = data.Samples.Length * bytes;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(size + 36); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)data.Info.Channels); writer.Write(data.Info.SampleRate); writer.Write(data.Info.SampleRate * bytes * data.Info.Channels); writer.Write((short)(bytes * data.Info.Channels)); writer.Write((short)bits); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(size);
            foreach (double sample in data.Samples) { int v = (int)Math.Round(sample * (bits == 24 ? 8388608 : 32768)); if (bits == 16) writer.Write((short)v); else { writer.Write((byte)v); writer.Write((byte)(v >> 8)); writer.Write((byte)(v >> 16)); } }
        }
    }
    [STAThread] static int Main(string[] args) {
        try { Run(args); return 0; } catch (Exception e) { Console.WriteLine(e.GetType().FullName); Console.WriteLine(e.Message); Console.WriteLine(e.StackTrace); return 1; }
    }
    static void Run(string[] args) {
        string directory = Path.GetFullPath(args[0]); Directory.CreateDirectory(directory);
        var engine = new AudioEngine(Path.GetFullPath(args[1]));
        Check(engine.TemporaryDirectory==Path.Combine(engine.BinDirectory,"temp"),"temporary media directory is inside application directory");
        var normalized = Regions.Normalize(new[] {new Region(3,5),new Region(-1,1),new Region(0.5,3),new Region(8,20)}, 10);
        Check(normalized.Count == 2 && normalized[0].Start == 0 && normalized[0].End == 5 && normalized[1].End == 10, "normalize, clamp and merge intervals");
        var restored = Regions.Subtract(normalized, new Region(2,4), 10);
        Check(restored.Count == 3 && restored[0].End == 2 && restored[1].Start == 4, "restore splits deletion");
        var kept = Regions.Kept(normalized, 10); Check(kept.Count == 1 && kept[0].Start == 5 && kept[0].End == 8, "kept intervals complement");
        var signal = Signal(48000, 2, 5, 16);
        var edge = Analysis.Silence(signal, 0.015, 0.1, 0.3, true);
        Check(edge.Count == 2, "edge-only RMSE uses all channels"); Near(edge[0].End, 0.7, 0.025, "leading silence retains 0.3 s"); Near(edge[1].Start, 4.3, 0.025, "trailing silence retains 0.3 s");
        var all = Analysis.Silence(signal, 0.015, 0.1, 0.05, false); Check(all.Count == 3, "interior silence detected");
        var silent = Signal(100, 1, 1, 16); var silence = Analysis.Silence(silent, 0.015, 0.1, 0.3, true); Near(Regions.Kept(silence, 1).Sum(x => x.End - x.Start), 0.6, 0.001, "all-silent input retains padding");
        string source = Path.Combine(directory, "测试 音频 & sample.wav"); Wav(source, signal, 16); string originalHash = Hash(source);
        var temporaryBefore=Directory.Exists(engine.TemporaryDirectory)?Directory.GetFiles(engine.TemporaryDirectory):new string[0];
        using(var snapshotAudio=engine.LoadSnapshot(source,Token)) {
            Check(Path.GetDirectoryName(snapshotAudio.Info.InputPath)==engine.TemporaryDirectory && File.Exists(snapshotAudio.Info.InputPath),"source snapshot is created in installation temp directory");
            AudioEngine.ReleaseSnapshot(snapshotAudio.Info);
            Check(!File.Exists(snapshotAudio.Info.InputPath),"released source snapshot is removed from installation temp directory");
        }
        Check(!Directory.GetFiles(engine.TemporaryDirectory).Except(temporaryBefore).Any(),"small audio load leaves no decoded or snapshot cache");
        var audio = engine.Load(source, Token); Check(audio.Info.SampleRate == 48000 && audio.Info.Bits == 16 && audio.Info.Channels == 2, "probe and decode Unicode filename"); Near(audio.Info.Duration, 5, 0.0001, "decoded duration");
        var measured = Analysis.MeasureFrequencyRange(audio, Token); Check(measured.MaxHz > 900 && measured.MinHz < 1100, "FFT measures the active frequency range");
        var silentRange = Analysis.MeasureFrequencyRange(silent, Token); Check(silentRange.MinHz == 0 && silentRange.MaxHz == 0, "silent audio has no active frequency range");
        string padded = Path.Combine(directory, "padded24.wav"); Wav(padded, signal, 24); var paddedData = engine.Load(padded, Token);
        var options = OutputOptions.For(audio.Info); options.SampleRate = 96000; Throws(() => options.Validate(audio.Info), "reject upward sampling"); options.SampleRate = 48000; options.Bits = 24; Throws(() => options.Validate(audio.Info), "reject upward bit depth"); options.Bits = 16; options.Bitrate = 2000; Throws(() => options.Validate(audio.Info), "reject upward bitrate"); options = OutputOptions.For(audio.Info);
        var history = new EditState { Options = options.Copy(), Deleted = normalized }; var snapshot = history.Copy(); string signature = snapshot.Signature(); history.Deleted[0].End = 9; history.Options.Speed = 2; Check(snapshot.Signature() == signature, "undo snapshot isolates regions and output options");
        var delete = new[] { new Region(0,0.7), new Region(4.3,5) };
        string wavOut = engine.Export(audio.Info, delete, options, Path.Combine(directory, "output.wav"), false, Token); var output = engine.Load(wavOut, Token);
        Near(output.Info.Duration, 3.6, 0.005, "WAV trims exactly"); Check(output.Info.Bits == 16 && output.Info.SampleRate == 48000, "WAV output precision"); Check(Hash(source) == originalHash, "non-destructive export preserves original bytes");
        string again = engine.Export(audio.Info, delete, options, Path.Combine(directory, "output.wav"), false, Token); Check(again.EndsWith("output (1).wav"), "duplicate output gets incrementing name");
        var mp3 = AudioEngine.Quick(audio.Info); mp3.SampleRate = 44100; string mp3Out = engine.Export(audio.Info, delete, mp3, Path.Combine(directory, "output.mp3"), false, Token); var compressed = engine.Probe(mp3Out, Token); Check(compressed.Codec == "mp3" && compressed.Bitrate <= 320000 && compressed.SampleRate == 44100, "MP3 codec, bitrate and sample rate"); Near(compressed.Duration, 3.6, 0.1, "MP3 duration including encoder padding");
        var tempo = options.Copy(); tempo.Speed = 2; tempo.Pitch = 3; string fast = engine.Export(audio.Info, delete, tempo, Path.Combine(directory, "fast.wav"), false, Token); Near(engine.Probe(fast, Token).Duration, 1.8, 0.07, "independent tempo and pitch processing"); Throws(() => engine.Export(audio.Info, delete, tempo, source, true, Token), "tempo or pitch disallows direct save");
        string locked = Path.Combine(directory, "locked.wav"); File.Copy(source, locked, true); var lockedInfo = engine.Probe(locked, Token);
        using (var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            string fallback = engine.Export(lockedInfo, delete, options, locked, true, Token); Check(fallback != locked && File.Exists(fallback), "locked original automatically exports with new name"); Check(Hash(locked) == originalHash, "locked original remains unchanged");
        }
        var snapshotData = engine.LoadSnapshot(locked, Token); string snapshotPath = snapshotData.Info.InputPath;
        try { using (var hold = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { string fallback = engine.Export(snapshotData.Info, delete, options, locked, true, Token); Check(fallback != locked && File.Exists(fallback), "exclusive source lock after open uses snapshot and save-as fallback"); } } finally { AudioEngine.ReleaseSnapshot(snapshotData.Info); }
        Check(!File.Exists(snapshotPath), "source snapshot cleaned");
        string replace = Path.Combine(directory, "replace.wav"); File.Copy(source, replace, true); var replaceInfo = engine.Probe(replace, Token); Check(engine.Export(replaceInfo, delete, options, replace, true, Token) == replace, "direct save commits original path"); Near(engine.Probe(replace, Token).Duration, 3.6, 0.005, "direct saved audio verified");
        string flac = Path.Combine(directory, "source.flac"); engine.Run("ffmpeg.exe", "-v error -nostdin -y -i " + AudioEngine.Quote(source) + " -c:a flac " + AudioEngine.Quote(flac), Token); var flacInfo = engine.Probe(flac, Token); var fo = OutputOptions.For(flacInfo); string flacOut = engine.Export(flacInfo, delete, fo, Path.Combine(directory, "output.flac"), false, Token); Check(engine.Probe(flacOut, Token).Codec == "flac", "original FLAC format export");
        foreach (var fixture in new[] {new[] {"aiff","pcm_s16be"},new[] {"m4a","alac"},new[] {"wv","wavpack"},new[] {"ogg","libvorbis"},new[] {"opus","libopus"},new[] {"aac","aac"},new[] {"wma","wmav2"}}) {
            string encoded = Path.Combine(directory,"source." + fixture[0]); engine.Run("ffmpeg.exe","-v error -nostdin -y -i " + AudioEngine.Quote(source) + " -c:a " + fixture[1] + (fixture[0] == "ogg" || fixture[0] == "opus" || fixture[0] == "aac" || fixture[0] == "wma" ? " -b:a 128k" : "") + " " + AudioEngine.Quote(encoded),Token);
            var encodedInfo = engine.Probe(encoded,Token); var originalFormatOptions = OutputOptions.For(encodedInfo); if (!encodedInfo.Lossless) originalFormatOptions.Bitrate = Math.Min(96,encodedInfo.Bitrate / 1000);
            string encodedOutput = engine.Export(encodedInfo,delete,originalFormatOptions,Path.Combine(directory,"output." + fixture[0]),false,Token); Check(engine.Probe(encodedOutput,Token).Codec == encodedInfo.Codec,"original " + fixture[0].ToUpperInvariant() + " output");
        }
        var depth = OutputOptions.For(paddedData.Info); depth.SampleRate = 32000; depth.Bits = 16; string lowered = engine.Export(paddedData.Info, delete, depth, Path.Combine(directory, "lowered.wav"), false, Token); Check(engine.Probe(lowered, Token).Bits == 16 && engine.Probe(lowered, Token).SampleRate == 32000, "lower sample rate and bit depth verified");
        var filtered = options.Copy(); filtered.LowCut = true; filtered.LowHz = 2000; string filteredOut = engine.Export(audio.Info, new Region[0], filtered, Path.Combine(directory, "filtered.wav"), false, Token); var filteredData = engine.Load(filteredOut, Token); Check(Rms(filteredData) < Rms(audio) * Math.Pow(10, -8 / 20.0), "high-pass filter removes low-frequency energy");
        string multi = engine.Export(audio.Info, new[] {new Region(1,2),new Region(3,4)}, options, Path.Combine(directory, "multi.wav"), false, Token); Near(engine.Probe(multi, Token).Duration, 3, 0.005, "multiple kept regions concatenate");
        string corrected = engine.Export(audio.Info, delete, mp3, Path.Combine(directory, "wrong-extension.wav"), false, Token); Check(corrected.EndsWith(".mp3"), "export suffix matches selected codec");
        string preview = Path.Combine(directory, "preview.wav"); engine.Render(audio.Info, delete, options, preview, true, Token); Check(engine.Probe(preview, Token).Codec == "pcm_s16le", "playback preview is PCM16");
        using (var playback = new Playback()) { playback.Play(preview, 3.45); Check(playback.IsPlaying, "native waveOut playback starts"); var timeout = System.Diagnostics.Stopwatch.StartNew(); while (!playback.Ended && timeout.Elapsed.TotalSeconds < 3) Thread.Sleep(20); Check(playback.Ended, "native playback reports end of stream"); playback.Stop(); double stopped = playback.Position; Thread.Sleep(80); Near(playback.Position, stopped, 0.001, "playback cursor clock stops after completion"); }
        Throws(() => engine.Export(audio.Info, new[] {new Region(0,5)}, options, Path.Combine(directory, "empty.wav"), false, Token), "empty result rejected");
        var cancellation = new CancellationTokenSource(); cancellation.Cancel(); bool canceled = false; try { engine.Load(source, cancellation.Token); } catch (OperationCanceledException) { canceled = true; } Check(canceled && Hash(source) == originalHash, "cancellation preserves source");
        Check(!Directory.EnumerateFiles(directory, ".gapgone-*").Any(), "temporary export files cleaned");
        VideoInputs(engine,directory);
        NewMediaFeatures(engine,audio,directory);
        RefinedUi(engine,directory);
        WidthPreferences(engine,directory);
        UiSmoke(engine, audio, directory);
        Console.WriteLine("TOTAL: " + assertions + " assertions passed");
    }
    static double Rms(AudioData data) { return Math.Sqrt(data.Samples.Sum(x=>x*x)/data.Samples.Length); }
    static void NewMediaFeatures(AudioEngine engine,AudioData stereo,string directory) {
        LoadPolicy.ValidateSize(LoadPolicy.MaximumBytes);Throws(()=>LoadPolicy.ValidateSize(LoadPolicy.MaximumBytes+1),"70 GB boundary rejects only oversized inputs");
        Check(LoadPolicy.WarningBytes==500L*1024*1024 && LoadPolicy.Estimate(600L*1024*1024).Contains("秒"),"large-file warning boundary and estimated loading duration");
        var defaults=OutputOptions.For(stereo.Info);Check(defaults.KeepRate && defaults.KeepBits && defaults.KeepBitrate && defaults.SampleRate==stereo.Info.SampleRate && defaults.Bits==stereo.Info.Bits,"default parameters keep source values");
        using(var panel=new OptionsPanel()){panel.Bind(stereo.Info,defaults);Check(new[]{"rate","bits","bitrate"}.All(name=>Convert.ToString(Get<StyledSelect>(panel,name).SelectedItem)=="保持"),"all three output choices display Keep by default");Get<StyledSelect>(panel,"format").SelectedIndex=2;var o=panel.Values();Check(o.Flac && !o.Mp3 && Get<StyledSelect>(panel,"bits").Enabled && !Get<StyledSelect>(panel,"bitrate").Enabled,"FLAC enables lossless depth and disables fixed bitrate");}
        string surround=Path.Combine(directory,"surround.wav");Wav(surround,Signal(48000,6,5,16),16);
        string fixtures=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"video-fixtures"),video=Path.Combine(directory,"multiple-tracks.mkv");
        File.Copy(Path.Combine(fixtures,"multiple-tracks.mkv"),video,true);
        var tracks=engine.ProbeTracks(video,Token);Check(tracks.Count==2 && tracks[0].StreamIndex==1 && tracks[1].StreamIndex==2,"enumerates both audio tracks with absolute stream indexes");
        Check(tracks[1].DefaultTrack && tracks[1].Title=="Surround" && tracks[1].Language=="zho" && tracks[1].TrackDescription.Contains("\ndefault") && tracks[1].TrackDescription.Contains("flac") && tracks[1].TrackDescription.Contains("5.1"),"track description includes separate title, default, language, codec and layout");
        using(var dialog=new TrackDialog(tracks,-1)){Check(dialog.SelectedTrack==1,"track chooser preselects default track");dialog.ShowInTaskbar=false;dialog.Show();using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(directory,"track-chooser.png"));}dialog.Hide();}
        using(var selected=engine.LoadSnapshot(video,Token,1,null)) {
            try {
                Check(selected.Info.Channels==6 && selected.Info.StreamIndex==2 && selected.Info.AudioTrackIndex==1,"selected surround track is decoded in isolation");var o=OutputOptions.For(selected.Info);Check(o.Flac && !o.Mp3,"surround video defaults to FLAC");
                string flac=engine.Export(selected.Info,new Region[0],o,Path.Combine(directory,"surround-output.flac"),false,Token);var result=engine.Probe(flac,Token);Check(result.Codec=="flac" && result.Channels==6 && result.SampleRate==48000 && result.Bits==16,"FLAC export preserves all surround channels and precision");
                var mp3=AudioEngine.Quick(selected.Info);string file=engine.Export(selected.Info,new Region[0],mp3,Path.Combine(directory,"surround-output.mp3"),false,Token);Check(engine.Probe(file,Token).Channels==2 && !mp3.Flac,"explicit MP3 path downmixes surround to stereo");
            }finally{AudioEngine.ReleaseSnapshot(selected.Info);}
        }
        var compressed=engine.Probe(Path.Combine(directory,"surround-output.mp3"),Token);var lossless=OutputOptions.For(compressed);lossless.SetFormat(compressed,false,true);string converted=engine.Export(compressed,new Region[0],lossless,Path.Combine(directory,"converted.flac"),false,Token);Check(engine.Probe(converted,Token).Codec=="flac" && lossless.Bits==16,"lossy source can explicitly convert to FLAC with sensible depth");
        string eight=Path.Combine(directory,"eight-channels.wav");Wav(eight,Signal(48000,8,5,16),16);var eightInfo=engine.Probe(eight,Token);var eightOptions=OutputOptions.For(eightInfo);eightOptions.SetFormat(eightInfo,false,true);string eightFlac=engine.Export(eightInfo,new Region[0],eightOptions,Path.Combine(directory,"eight.flac"),false,Token);Check(engine.Probe(eightFlac,Token).Channels==8,"7.1 FLAC export retains eight channels");
        Check(new AudioInfo{Channels=3,ChannelLayout="2.1"}.ChannelDescription=="2.1" && new AudioInfo{Channels=8,ChannelLayout="7.1"}.ChannelDescription=="7.1","channel descriptions retain 2.1 and 7.1 layouts");
        Check(typeof(AppAssets).Assembly.GetManifestResourceNames().Contains("GapGone.AppAssets.resources"),"logo and icon are embedded in assembly resources");using(var logo=AppAssets.Logo())using(var icon=AppAssets.Icon())Check(logo.Width==1024 && logo.Height==1024 && icon.Width>0,"new embedded logo and icon decode without external files");
        using(var form=new MainForm(engine,new string[0],Path.Combine(directory,"test-window-width.txt"))){
            form.ShowInTaskbar=false;form.Location=new Point(-32000,-32000);form.Show();
            using(var operation=new CancellationTokenSource()){
                var loading=Get<LoadingIndicator>(form,"loading");loading.Begin(operation);
                Await((System.Threading.Tasks.Task)form.GetType().GetMethod("LoadSelected",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{video,tracks,1,operation.Token}));loading.End();form.GetType().GetMethod("UpdateControls",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
                var button=Get<Button>(form,"switchTrack");Check(button.Visible && button.Parent==Get<Label>(form,"file").Parent,"video filename row shows Switch Track button");Check(button.Left-Get<Label>(form,"file").Right==8 && button.Right<form.ClientSize.Width/2,"Switch Track follows filename instead of window right edge");
                var original=Get<AudioData>(form,"audio");using(var cancel=new CancellationTokenSource()){cancel.Cancel();bool canceled=false;try{Await((System.Threading.Tasks.Task)form.GetType().GetMethod("LoadSelected",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{video,tracks,0,cancel.Token}));}catch(OperationCanceledException){canceled=true;}Check(canceled && Get<AudioData>(form,"audio")==original && original.Info.Channels==6,"canceled replacement keeps previously loaded audio");}
            }
            using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(directory,"video-workbench.png"));}form.Close();
        }
        string mappedWav=Path.Combine(directory,"mapped-large.wav");
        using(var f=File.Create(mappedWav))using(var w=new BinaryWriter(f)){int count=48000*8*45,bytes=count*2;w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(bytes+36);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)8);w.Write(48000);w.Write(48000*8*2);w.Write((short)16);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(bytes);f.SetLength(bytes+44);}
        int maximum=0;using(var data=engine.Load(mappedWav,Token,0,p=>maximum=Math.Max(maximum,p))){Check(ResourceUsage.Read().DiskBacked && ResourceUsage.Read().DiskBytes>=data.Frames*8*8,"mapped decode is included in this instance disk usage");Check(data.Samples==null && data.Frames==48000L*45 && data.Sample(data.Frames*8-1)==0,"decoded audio beyond 128 MB uses disk-backed samples");Check(maximum==100 && data.Peaks.All(x=>x==0),"mapped loading reports completion and builds full waveform");var silence=Analysis.Silence(data,0.01,0.1,0.3,false);Near(silence.Sum(x=>x.End-x.Start),44.4,0.001,"mapped RMSE includes all channels and frames");Check(Analysis.MeasureFrequencyRange(data,Token).MaxHz==0,"FFT reads mapped audio without a giant array");}
        foreach(int stage in new[]{5,40,90,100}){
            var before=Directory.GetFiles(engine.TemporaryDirectory,"GapGone-*").ToArray();using(var cancel=new CancellationTokenSource()){bool canceled=false;try{using(var data=engine.LoadSnapshot(mappedWav,cancel.Token,0,p=>{if(p>=stage)cancel.Cancel();}))AudioEngine.ReleaseSnapshot(data.Info);}catch(OperationCanceledException){canceled=true;}Check(canceled,"load cancellation at progress stage "+stage);}
            Check(!Directory.GetFiles(engine.TemporaryDirectory,"GapGone-*").Except(before).Any(),"canceled snapshot, decode and waveform clean installation temporary files at "+stage);
        }
        using(var host=new Form())using(var indicator=new LoadingIndicator())using(var token=new CancellationTokenSource()){
            host.Controls.Add(indicator);host.ShowInTaskbar=false;host.Location=new Point(-32000,-32000);host.Show();indicator.Begin(token);indicator.Progress(42);Check(!Descendants(indicator).Any(c=>c.Visible),"load indicator remains hidden during first five seconds");
            var watch=System.Diagnostics.Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<5.3){Application.DoEvents();Thread.Sleep(20);}Check(Descendants(indicator).Any(c=>c.Visible && c.Text=="加载进度：42%"),"load indicator appears after five seconds with progress");Get<Button>(indicator,"cancel").PerformClick();Check(token.IsCancellationRequested,"loading cancel button cancels the active token");indicator.End();Check(!Descendants(indicator).Any(c=>c.Visible),"load indicator clears after completion or cancellation");host.Hide();
        }
        using(var batch=new BatchForm(engine)){Check(Get<StyledSelect>(batch,"format").Items.Contains("FLAC"),"batch offers FLAC output");var bar=Get<FlowLayoutPanel>(batch,"silencebar");Check(Descendants(bar).Contains(Get<StyledSelect>(batch,"silence")) && Descendants(bar).Contains(Get<StyledNumber>(batch,"keep")) && Descendants(bar).Contains(Get<StyledNumber>(batch,"threshold")),"batch silence mode, padding and threshold share a dedicated row");}
    }
    static void RefinedUi(AudioEngine engine,string directory) {
        var assembly=typeof(MainForm).Assembly;var product=(AssemblyProductAttribute)assembly.GetCustomAttributes(typeof(AssemblyProductAttribute),false)[0];var version=(AssemblyInformationalVersionAttribute)assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute),false)[0];
        Check(product.Product=="Gap Audio" && version.InformationalVersion=="V1.0.0" && assembly.GetName().Version.ToString()=="1.0.0.0","application name and release version are embedded in executable metadata");
        using(var main=new MainForm(engine,new string[0],Path.Combine(directory,"test-window-width.txt")))Check(main.Text.StartsWith("Gap Audio V1.0.0") && main.Width==new WindowPreferences(Path.Combine(directory,"test-window-width.txt")).ReadWidth(MainForm.InitialWindowWidth,main.MinimumSize.Width,Screen.FromControl(main).WorkingArea.Width),"main title uses new name and version with configurable initial width");
        Check(ResourceUsage.FormatBytes(500L*1024*1024)=="500 MB" && ResourceUsage.FormatBytes(1024L*1024*1024)=="1.00 GB" && ResourceUsage.FormatBytes(1536L*1024*1024)=="1.50 GB","resource units switch at 1 GB with two decimals");
        var usage=ResourceUsage.Read();Check(usage.UsedBytes>0 && usage.AvailableBytes>0,"resource monitor reads process allocation and available memory");
        string tracked=Path.Combine(directory,"usage.tmp");File.WriteAllBytes(tracked,new byte[1024]);long initial=ResourceUsage.Read().DiskBytes;ResourceUsage.TrackFile(tracked);Check(ResourceUsage.Read().DiskBytes==initial+1024,"disk accounting includes only tracked instance files");ResourceUsage.ForgetFile(tracked);File.Delete(tracked);Check(ResourceUsage.Read().DiskBytes==initial,"resource tracking releases deleted files");
        foreach(bool dark in new[]{true,false}){
            Palette.Dark=dark;
            using(var dialog=LoadPolicy.CreateDialog(600L*1024*1024,600)){
                dialog.ShowInTaskbar=false;dialog.Show();Application.DoEvents();
                Check(dialog.BackColor==Palette.Background && Descendants(dialog).OfType<StyledLabel>().Any(c=>c.Text.Contains("600 MB") && c.Text.Contains("办公文档") && c.Text.Contains("预估加载时间")),"large-file dialog matches theme and preserves loading warning in "+(dark?"dark":"light"));
                Check(((Button)dialog.AcceptButton).Text=="取消加载","large-file dialog keyboard default cancels loading");
                using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(directory,"large-file-"+(dark?"dark":"light")+".png"));}
                Descendants(dialog).OfType<Button>().First(c=>c.Text=="继续加载").PerformClick();Check(dialog.DialogResult==DialogResult.Yes,"custom loading confirm retains Yes result");dialog.Close();
            }
            using(var dialog=TrackDialog.CreateMp3Dialog(new AudioInfo{Channels=6,ChannelLayout="5.1"})){
                dialog.ShowInTaskbar=false;dialog.Show();Application.DoEvents();Check(dialog.BackColor==Palette.Background && Descendants(dialog).OfType<Button>().Select(c=>c.Text).Contains("改用 FLAC"),"MP3 surround warning uses themed explicit choices");
                using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(directory,"mp3-warning-"+(dark?"dark":"light")+".png"));}
                Descendants(dialog).OfType<Button>().First(c=>c.Text=="双声道 MP3").PerformClick();Check(dialog.DialogResult==DialogResult.No,"stereo MP3 choice retains No result");dialog.Close();
            }
        }
        using(var form=new BatchForm(engine)){
            form.ShowInTaskbar=false;form.Location=new Point(-32000,-32000);form.Show();Application.DoEvents();
            foreach(string name in new[]{"format","rate","bits","bitrate"}){var select=Get<StyledSelect>(form,name);int expected=Math.Max(select.MinimumSize.Width,select.Items.Cast<object>().Select(x=>TextRenderer.MeasureText(Convert.ToString(x),form.Font,Size.Empty,TextFormatFlags.NoPadding).Width).Max()+36);Check(select.Width==expected,"batch "+name+" width follows its longest option");}
            var settings=Get<FlowLayoutPanel>(form,"settings");var bar=Get<FlowLayoutPanel>(form,"silencebar");int contentBottom=settings.Controls.Cast<Control>().Max(c=>c.Bottom);Check(bar.PointToScreen(Point.Empty).Y-settings.PointToScreen(new Point(0,contentBottom)).Y<=4,"batch silence row has no empty row above it");
            Check(!Get<DataGridView>(form,"grid").Columns.Contains("format") && !Get<DataGridView>(form,"grid").Columns.Contains("channels"),"batch duplicate format and channels columns removed");
            var resource=Get<ResourceUsageLabel>(form,"resources");resource.RefreshUsage();Check(resource.Right<=resource.Parent.ClientSize.Width && resource.Text.Contains("可用"),"batch resources are displayed in footer right corner");form.Close();
        }
    }
    static void WidthPreferences(AudioEngine engine,string directory) {
        string settings=Path.Combine(directory,"window-preferences","width.txt");var preferences=new WindowPreferences(settings);
        Check(preferences.ReadWidth(1280,960,1920)==1280,"window without saved preference uses default width");preferences.SaveWidth(1440);Check(preferences.ReadWidth(1280,960,1920)==1440,"window width round trips through local settings");
        preferences.SaveWidth(5000);Check(preferences.ReadWidth(1280,960,1920)==1920,"restored width is constrained to current screen");preferences.SaveWidth(800);Check(preferences.ReadWidth(1280,960,1920)==960,"restored width respects minimum window width");File.WriteAllText(settings,"broken");Check(preferences.ReadWidth(1280,960,1920)==1280,"invalid width settings fall back without startup failure");
        using(var main=new MainForm(engine,new string[0],settings)){
            main.ShowInTaskbar=false;main.Show();main.Width=1120;Application.DoEvents();main.Close();
        }
        Check(preferences.ReadWidth(1280,960,1920)==1120,"successful window close saves resized normal width");
        using(var main=new MainForm(engine,new string[0],settings)){
            Check(main.Width==1120,"reopening the main window restores saved width");main.ShowInTaskbar=false;main.Show();main.Width=1160;Application.DoEvents();main.WindowState=FormWindowState.Maximized;Application.DoEvents();main.Close();
        }
        Check(preferences.ReadWidth(1280,960,1920)==1160,"closing maximized window preserves last normal width");
        using(var batch=new BatchForm(engine)){
            batch.ShowInTaskbar=false;batch.Show();Application.DoEvents();var grid=Get<DataGridView>(batch,"grid");
            foreach(int width in new[]{1260,1040}){
                batch.Width=width;Application.DoEvents();int total=grid.ClientSize.Width-2;int ten=TextRenderer.MeasureText(new string('字',10),grid.Font,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Width;
                Check(grid.Columns["name"].Width==(int)Math.Round(total*25.0/108)+ten,"batch filename gains ten character widths at window width "+width);
                Check(Math.Abs(grid.Columns["params"].Width-((int)Math.Round(total*50.0/108)-ten))<=2,"batch parameters release ten character widths at window width "+width);
                Check(Math.Abs(grid.Columns["status"].Width-total*25.0/108)<=1,"batch status column retains original allocation");
            }batch.Close();
        }
    }
    static void VideoInputs(AudioEngine engine,string directory) {
        string fixtures=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"video-fixtures");
        foreach(string ext in new[]{"mp4","m4s","mkv","webm","avi","wmv","ts","mpg","flv"}){
            string path=Path.Combine(fixtures,"sample."+ext);string before=Hash(path);var data=engine.Load(path,Token);
            Check(data.Info.VideoSource && data.Info.Channels>0 && data.Info.Duration>1.8,"extract first audio track from "+ext.ToUpperInvariant());
            var output=OutputOptions.For(data.Info);Check(output.Mp3 && output.RequiresSaveAs(data.Info),"video defaults to MP3 and save-as for "+ext.ToUpperInvariant());
            Throws(()=>engine.Export(data.Info,new Region[0],output,path,true,Token),"direct save cannot overwrite "+ext.ToUpperInvariant());
            string exported=engine.Export(data.Info,new Region[0],output,Path.Combine(directory,"video-"+ext+".mp3"),false,Token);
            Check(engine.Probe(exported,Token).Codec=="mp3" && Hash(path)==before,"video extraction exports MP3 and preserves "+ext.ToUpperInvariant());
            Near(engine.Probe(exported,Token).Duration,data.Info.Duration,0.12,"video extraction duration for "+ext.ToUpperInvariant());
        }
        var mkv=engine.Load(Path.Combine(fixtures,"sample.mkv"),Token);
        Check(mkv.Info.Bitrate>0 && mkv.Info.Bitrate<200000,"missing stream bitrate is calculated from audio packets rather than video bitrate");
        var m4s=engine.Load(Path.Combine(fixtures,"sample.m4s"),Token);var native=OutputOptions.For(m4s.Info);native.Mp3=false;
        string extracted=engine.Export(m4s.Info,new Region[0],native,Path.Combine(directory,"m4s-track.m4a"),false,Token);
        Check(engine.Probe(extracted,Token).Codec=="aac","M4S original audio codec exports as M4A");
        Throws(()=>engine.Load(Path.Combine(fixtures,"no-audio.mp4"),Token),"video without audio reports an actionable error");
        Check(MediaFormats.OpenFilter.Contains("*.m4s") && MediaFormats.Supported("x.MP4") && MediaFormats.Supported("x.m2ts"),"file picker and recursive imports include common videos");
        using(var batch=new BatchForm(engine,fixtures)){
            var items=Get<List<BatchItem>>(batch,"items");Check(items.Any(x=>x.Path.EndsWith(".m4s")) && items.Any(x=>x.Path.EndsWith(".mp4")),"directory import includes M4S and MP4");
        }
    }
    static void Set(object target, string name, object value) { target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value); }
    static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target); }
    static void Mouse(Control control,string method,MouseButtons button,int x,int y) {control.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(control,new object[] {new MouseEventArgs(button,1,x,y,0)});}
    static void Await(System.Threading.Tasks.Task task){while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(10);}task.GetAwaiter().GetResult();}
    static IEnumerable<Control> Descendants(Control root){foreach(Control child in root.Controls){yield return child;foreach(var nested in Descendants(child))yield return nested;}}
    static void UiSmoke(AudioEngine engine, AudioData audio, string directory) {
        using (var form = new MainForm(engine, new string[0],Path.Combine(directory,"test-window-width.txt"))) {
            Palette.Dark=true;Palette.Apply(form);
            Set(form, "audio", audio); var edit = new EditState {Options = OutputOptions.For(audio.Info), Deleted = new List<Region> {new Region(2,3)}}; Set(form, "state", edit); Set(form, "frequencyRange", Analysis.MeasureFrequencyRange(audio, Token));
            Get<OptionsPanel>(form, "options").Bind(audio.Info, edit.Options);
            var view = Get<TimelineView>(form, "timeline"); view.Canvas.SetData(audio); view.Canvas.Deleted = edit.Deleted;
            form.GetType().GetMethod("RefreshInfo", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null); form.GetType().GetMethod("RefreshStats", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null); form.GetType().GetMethod("UpdateControls", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
            form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-32000,-32000); form.Show(); form.PerformLayout(); view.LayoutTrack();
            var toolbar = Get<FlowLayoutPanel>(form, "toolbar"); Check(toolbar.Controls.Cast<Control>().All(c => c.Bottom <= toolbar.ClientSize.Height), "desktop toolbar fits available rows");
            Check(!Descendants(form).Any(c=>c.Text.Contains("鉴定")) && form.GetType().GetMethod("Assess",BindingFlags.Instance|BindingFlags.NonPublic)==null,"main window removes assessment entry and action");
            Check(Get<Button>(form, "direct").Enabled, "direct save enabled for original speed and pitch"); edit.Options.Speed = 1.2; form.GetType().GetMethod("UpdateControls", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null); Check(!Get<Button>(form, "direct").Enabled, "desktop disables direct save after speed change");
            edit.Options.Speed=1;var optionPanel=Get<OptionsPanel>(form,"options");optionPanel.Bind(audio.Info,edit.Options);
            var speed=Get<StyledNumber>(optionPanel,"speed");speed.Value=1.25m;Check(edit.Options.Speed==1 && Get<bool>(form,"pendingOptions"),"output setting changes wait for Apply Processing");
            Check(Get<Button>(form,"play").Enabled && optionPanel.ApplyButton.Pending,"pending output settings allow playback and activate breathing prompt");
            double glowBefore=optionPanel.ApplyButton.Glow;Thread.Sleep(150);Check(optionPanel.ApplyButton.Glow>glowBefore,"yellow apply prompt animates over time");
            var playbackTask=(System.Threading.Tasks.Task)form.GetType().GetMethod("StartPlayback",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);Await(playbackTask);
            Check(Get<Playback>(form,"player").IsPlaying && Get<EditState>(form,"state").Options.Speed==1 && Get<bool>(form,"pendingOptions"),"playback leaves pending output settings unapplied");
            Near(engine.Probe(Get<string>(form,"previewPath"),Token).Duration,4,0.02,"unapplied speed is excluded from actual playback preview");
            form.GetType().GetMethod("StopPlayback",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
            var applyTask=(System.Threading.Tasks.Task)form.GetType().GetMethod("ApplyProcessing",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
            Check(Get<StatusLabel>(form,"status").Preparing,"preparation status uses yellow state");Await(applyTask);
            Check(!Get<bool>(form,"pendingOptions") && Get<EditState>(form,"state").Options.Speed==1.25 && Get<int>(form,"previewVersion")==Get<int>(form,"version"),"Apply Processing commits options and prepares preview");
            Check(!optionPanel.ApplyButton.Pending,"successful application stops the breathing prompt");
            Check(Get<FrequencyRange>(form,"frequencyRange")!=null && !Get<StatusLabel>(form,"status").Preparing,"Apply Processing measures result and ends preparation");
            Check(Get<StatusLabel>(form,"status").Font.Size>=12,"bottom status uses larger font");
            Check(optionPanel.Top>=toolbar.Bottom && optionPanel.Width>form.ClientSize.Width-40,"horizontal output controls below toolbar");
            Check(optionPanel.Controls.Cast<Control>().All(c=>c.Bottom<=optionPanel.Height),"output settings fit horizontal wrapped layout");
            Check(optionPanel.ApplyButton.Parent.Top==optionPanel.Controls[0].Top && optionPanel.ApplyButton.Top==optionPanel.Controls[0].Controls[1].Top,"Apply and Reset fit the output field row");
            Check(Get<StyledSelect>(optionPanel,"bits").Width<Get<StyledSelect>(optionPanel,"format").Width,"output field widths adapt to available content");
            Check(!Descendants(form).Any(c=>c is Button && c.Text=="取消处理"),"main window removes bottom-right Cancel Processing");
            string regionsBefore=String.Join(";",Get<EditState>(form,"state").Deleted.Select(r=>r.Start+":"+r.End));
            form.GetType().GetMethod("ResetOutputSettings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
            Check(optionPanel.Values().Speed==1 && Get<EditState>(form,"state").Options.Speed==1.25 && optionPanel.ApplyButton.Pending,"reset defaults remains pending until applied");
            Check(regionsBefore==String.Join(";",Get<EditState>(form,"state").Deleted.Select(r=>r.Start+":"+r.End)),"Reset Settings preserves clip deletion regions");
            optionPanel.Bind(audio.Info,Get<EditState>(form,"state").Options);form.GetType().GetMethod("UpdateDraftPending",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
            Check(!Get<bool>(form,"pendingOptions") && !optionPanel.ApplyButton.Pending,"returning to effective values clears pending prompt");
            var formatSelect=Get<StyledSelect>(optionPanel,"format");formatSelect.OpenPopup();var popup=Get<Form>(formatSelect,"popup");Check(popup!=null && popup.Controls[0] is StyledScrollHost,"dropdown uses custom popup and scrolling surface");popup.Close();
            var rateSelect=Get<StyledSelect>(optionPanel,"rate");rateSelect.SelectedItem=32000;formatSelect.SelectedIndex=1;Check(optionPanel.Values().SampleRate==32000 && optionPanel.Values().Bitrate<=320,"custom selections retain rate when changing format");optionPanel.Bind(audio.Info,Get<EditState>(form,"state").Options);form.GetType().GetMethod("UpdateDraftPending",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
            var inline=Get<FlowLayoutPanel>(form,"editbar");Check(inline.Controls.Cast<Control>().All(c=>c.Height==34 && c.Top==inline.Controls[0].Top),"labels and buttons share vertical alignment");
            Check(inline.Controls.GetChildIndex(Get<StyledNumber>(form,"zoom"))<inline.Controls.GetChildIndex(inline.Controls.Cast<Control>().First(c=>c.Text=="RMSE 去全部静音")),"silence tools follow zoom controls on the same row");
            var instructions=Descendants(form).First(c=>c.Text.StartsWith("左键拖拽保留"));Check(instructions.PointToScreen(Point.Empty).Y>inline.PointToScreen(new Point(0,inline.Height)).Y,"mouse instructions occupy a separate row");
            int simpleHeight=form.Height;
            Check(view.ViewSize.Height>=146 && view.Canvas.Height<=view.ViewSize.Height,"compact timeline keeps all retained and deleted tracks visible " + view.ViewSize.Height + "/" + view.Canvas.Height + " window " + form.Height+" minimum "+form.MinimumSize.Height);
            using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(0,0,image.Width,image.Height)); image.Save(Path.Combine(directory, "desktop-dark.png")); }
            using(var canvasImage=new Bitmap(view.Canvas.Width,view.Canvas.Height)){view.Canvas.DrawToBitmap(canvasImage,new Rectangle(0,0,canvasImage.Width,canvasImage.Height));var region=Get<EditState>(form,"state").Deleted[0];int x=(int)((region.Start+region.End)/2/audio.Info.Duration*view.Canvas.Width);var pixel=canvasImage.GetPixel(x,view.Canvas.Height/2+38);Check(pixel.R>pixel.G*2 && pixel.R>120,"dark deletion track has a clearly red opaque fill");}
            Palette.Dark = false; Palette.Apply(form);Get<CheckBox>(form,"advanced").Checked=true;view.LayoutTrack();Check(form.Height>simpleHeight,"simple timeline uses shorter window than advanced mode");
            using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(0,0,image.Width,image.Height)); image.Save(Path.Combine(directory, "desktop-light.png")); }
            Palette.Dark=true;Palette.Apply(form);using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(directory,"advanced-dark.png"));}Palette.Dark=false;Palette.Apply(form);
            Check(view.Canvas.Width <= view.ClientSize.Width, "waveform adapts to window width at 1x");
            int initialHeight=view.Canvas.Height;view.Zoom=4;view.LayoutTrack();Check(view.Canvas.Width<=view.ViewSize.Width && view.Canvas.Height>initialHeight,"advanced zoom reflows rows without horizontal blank area");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(directory,"advanced-zoom.png"));}
            foreach(bool advanced in new[]{false,true}){
                view.Canvas.Advanced=advanced;view.Zoom=1;view.LayoutTrack();bool? restore=null;int edits=0,seeks=0;Action<Region,bool> capture=(r,keep)=>{restore=keep;edits++;};Action<double> seek=t=>seeks++;view.Canvas.Edit+=capture;view.Canvas.Seek+=seek;
                Mouse(view.Canvas,"OnMouseDown",MouseButtons.Left,100,60);Mouse(view.Canvas,"OnMouseMove",MouseButtons.Left,200,80);Mouse(view.Canvas,"OnMouseUp",MouseButtons.Left,200,80);Check(restore==true && edits==1,"left drag retains region in " +(advanced?"advanced":"simple")+" mode");
                Mouse(view.Canvas,"OnMouseDown",MouseButtons.Right,100,60);Mouse(view.Canvas,"OnMouseMove",MouseButtons.Right,200,30);Mouse(view.Canvas,"OnMouseUp",MouseButtons.Right,200,30);Check(restore==false && edits==2,"right drag deletes regardless of vertical direction");
                Mouse(view.Canvas,"OnMouseDown",MouseButtons.Middle,200,60);Mouse(view.Canvas,"OnMouseMove",MouseButtons.Middle,100,60);Mouse(view.Canvas,"OnMouseUp",MouseButtons.Middle,100,60);Check(edits==2 && seeks==0,"middle drag never changes edit or playback position");
                Mouse(view.Canvas,"OnMouseDown",MouseButtons.Left,100,60);Mouse(view.Canvas,"OnMouseUp",MouseButtons.Left,100,60);Check(seeks==1,"left click seeks without an edit");view.Canvas.Edit-=capture;view.Canvas.Seek-=seek;
            }
            view.Canvas.Advanced=false;view.Zoom=2;view.LayoutTrack();Check(view.Canvas.Width>view.ViewSize.Width,"simple timeline zoom enables custom horizontal scrolling");view.ScrollTo(0,0);Mouse(view.Canvas,"OnMouseDown",MouseButtons.Middle,300,60);Mouse(view.Canvas,"OnMouseMove",MouseButtons.Middle,200,60);Mouse(view.Canvas,"OnMouseUp",MouseButtons.Middle,200,60);Check(view.ScrollX>0,"middle drag pans horizontally when track exceeds width");
            Get<CheckBox>(form,"advanced").Checked=false;form.Width=960;form.PerformLayout();view.LayoutTrack();Check(optionPanel.Controls.Cast<Control>().All(c=>c.Bottom<=optionPanel.Height) && view.ViewSize.Height>=146,"narrow window reflows settings without clipping timeline");
            Set(form,"dirty",false);Set(form,"pendingOptions",false);
        }
        string previousClipboard=Clipboard.ContainsText()?Clipboard.GetText():null;
        using(var result=new ResultDialog("使用说明",MainForm.HelpText)){
            result.ShowInTaskbar=false;result.StartPosition=FormStartPosition.Manual;result.Location=new Point(-32000,-32000);result.Show();
            Check(result.CopyResult() && Clipboard.GetText()==MainForm.HelpText,"help dialog copies the complete updated instructions");
            using(var bitmap=new Bitmap(result.Width,result.Height)){result.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(directory,"help-dialog.png"));}
        }
        if(previousClipboard!=null)Clipboard.SetText(previousClipboard);else Clipboard.Clear();
        string corrupt=Path.Combine(directory,"corrupt.wav");File.WriteAllText(corrupt,"not audio");
        using(var form=new BatchForm(engine,corrupt,audio.Info.Path)){
            form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-32000,-32000);form.Show();form.PerformLayout();
            var rows=Get<List<BatchItem>>(form,"items");string sourceHash=Hash(audio.Info.Path);
            Await((System.Threading.Tasks.Task)form.GetType().GetMethod("ConvertFilesTo",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{rows.ToArray(),directory,false}));
            Check(rows[0].Output==null && rows[1].Output!=null && File.Exists(rows[1].Output),"batch export continues after corrupt input");
            Check(rows[0].Status.StartsWith("原件保留") && rows[1].Status=="转换成功","batch exposes per-file export status");
            Check(Hash(audio.Info.Path)==sourceHash,"batch export preserves source bytes");
            var grid=Get<DataGridView>(form,"grid");Check(grid.Columns.Count==4 && !grid.Columns.Contains("result") && !grid.Columns.Contains("band"),"batch table contains selection, file, audio parameters and status");
            Check(!Descendants(form).Any(c=>c.Text.Contains("鉴定") || c.Text.Contains("报告") || c.Text.Contains("可疑") || c.Text.Contains("删除原件")),"batch removes all assessment and source deletion actions");
            Check(form.GetType().GetMethod("WriteReports")==null && form.GetType().GetMethod("Inspect",BindingFlags.Instance|BindingFlags.NonPublic)==null,"retired batch endpoints are removed");
            rows[0].Selected=false;rows[1].Selected=true;
            string mp3Directory=Path.Combine(directory,"batch-mp3");Directory.CreateDirectory(mp3Directory);
            Await((System.Threading.Tasks.Task)form.GetType().GetMethod("ConvertFilesTo",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{rows.Where(x=>x.Selected).ToArray(),mp3Directory,true}));
            Check(engine.Probe(rows[1].Output,Token).Codec=="mp3" && rows[0].Output==null,"selected MP3 export works without assessment");
            foreach(bool dark in new[]{true,false}){
                Palette.Dark=dark;Palette.Apply(form);
                Check(grid.DefaultCellStyle.SelectionForeColor==Palette.Text && grid.DefaultCellStyle.SelectionBackColor==Palette.Selection,"batch selection uses theme colors in " +(dark?"dark":"light")+" mode");
                Get<FlowLayoutPanel>(form,"settings").Enabled=false;
                using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(directory,dark?"batch-dark-disabled.png":"batch-light-disabled.png"));}
                Get<FlowLayoutPanel>(form,"settings").Enabled=true;
            }
        }
        ThemeRendering(directory);
        Palette.Dark = true;
    }
    static double Luminance(Color color) {
        return new[] {color.R,color.G,color.B}.Select((v,i)=>{
            double c=v/255.0;return (c<=0.04045?c/12.92:Math.Pow((c+0.055)/1.055,2.4))*new[]{0.2126,0.7152,0.0722}[i];
        }).Sum();
    }
    static double Contrast(Color a,Color b) {double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+0.05)/(Math.Min(x,y)+0.05);}
    static bool HasReadableText(Control control,Rectangle area,Color expected) {
        using(var bitmap=new Bitmap(control.Width,control.Height)){
            control.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));int pixels=0;
            for(int x=area.Left;x<Math.Min(area.Right,bitmap.Width);x++)for(int y=area.Top;y<Math.Min(area.Bottom,bitmap.Height);y++){
                var pixel=bitmap.GetPixel(x,y);if(Math.Abs(pixel.R-expected.R)+Math.Abs(pixel.G-expected.G)+Math.Abs(pixel.B-expected.B)<45)pixels++;
            }
            return pixels>12;
        }
    }
    static void ThemeRendering(string directory) {
        using(var form=new Form {ClientSize=new Size(480,300),ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-32000,-32000)}){
            var host=new Panel {Dock=DockStyle.Fill};form.Controls.Add(host);
            var button=MainForm.Button("直接保存");button.SetBounds(20,20,160,34);host.Controls.Add(button);
            var label=new StyledLabel {Text="采样率 Hz",Location=new Point(20,70),Size=new Size(180,34)};host.Controls.Add(label);
            var check=new StyledCheckBox {Text="低频消除",Checked=true,Location=new Point(20,120),Size=new Size(180,34)};host.Controls.Add(check);
            var number=new StyledNumber {Minimum=0,Maximum=10,DecimalPlaces=2,Value=1.25m,Location=new Point(20,170),Width=160};host.Controls.Add(number);
            var select=new StyledSelect {Location=new Point(220,20),Width=160};select.Items.Add("原格式输出");select.SelectedIndex=0;host.Controls.Add(select);
            var apply=new ApplyButton {Location=new Point(220,70),Pending=true};host.Controls.Add(apply);
            bool clicked=false;button.Click+=(s,e)=>clicked=true;form.Show();
            foreach(bool dark in new[]{true,false}){
                Palette.Dark=dark;Palette.Apply(form);host.Enabled=false;Application.DoEvents();
                string mode=dark?"dark":"light";
                Check(Contrast(Palette.DisabledText,Palette.DisabledSurface)>=4.5 && Contrast(Palette.Text,Palette.Surface)>=4.5,"normal and disabled text meet 4.5:1 contrast in "+mode+" mode");
                Check(HasReadableText(button,new Rectangle(8,5,144,24),Palette.DisabledText),"disabled button text renders clearly in "+mode+" mode");
                Check(HasReadableText(label,label.ClientRectangle,Palette.Text),"static label stays readable inside disabled panel in "+mode+" mode");
                Check(HasReadableText(check,new Rectangle(24,0,156,34),Palette.DisabledText),"disabled checkbox caption renders clearly in "+mode+" mode");
                Check(HasReadableText(number,new Rectangle(10,2,100,30),Palette.DisabledText),"disabled numeric value renders clearly in "+mode+" mode");
                Check(HasReadableText(select,new Rectangle(10,2,120,30),Palette.DisabledText),"disabled selection value renders clearly in "+mode+" mode");
                Check(HasReadableText(apply,new Rectangle(5,4,80,26),Palette.DisabledText),"disabled Apply button renders clearly in "+mode+" mode");
                button.PerformClick();select.OpenPopup();Check(!clicked && Get<Form>(select,"popup")==null,"disabled controls cannot activate in "+mode+" mode");
                using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(directory,"controls-"+mode+"-disabled.png"));}
                host.Enabled=true;Application.DoEvents();Check(number.Controls[0].Visible && number.Value==1.25m,"numeric editor is restored with its value after enabling in "+mode+" mode");
            }
        }
    }
}
