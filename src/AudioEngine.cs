using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace GapGone {
    public sealed class AudioEngine {
        public readonly string BinDirectory;
        public string TemporaryDirectory { get { return System.IO.Path.Combine(BinDirectory, "temp"); } }
        public AudioEngine(string directory) { BinDirectory = System.IO.Path.GetFullPath(directory); }
        public string CreateTemporaryPath(string prefix, string extension) {
            try { Directory.CreateDirectory(TemporaryDirectory); }
            catch (UnauthorizedAccessException e) { throw new IOException("无法写入程序临时目录：" + TemporaryDirectory + "。请重新安装以修复 temp 文件夹权限，或将便携版放在可写目录。", e); }
            catch (IOException e) { throw new IOException("无法创建程序临时目录：" + TemporaryDirectory + "。请检查磁盘空间和目录状态。", e); }
            return System.IO.Path.Combine(TemporaryDirectory, "GapGone-" + prefix + Guid.NewGuid() + extension);
        }
        public static string Quote(string value) {
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value) { if (c == '\\') { slashes++; continue; } if (c == '"') { result.Append('\\', slashes * 2 + 1); result.Append(c); } else { result.Append('\\', slashes); result.Append(c); } slashes = 0; }
            result.Append('\\', slashes * 2); result.Append('"'); return result.ToString();
        }
        static string N(double value) { return value.ToString("0.########", CultureInfo.InvariantCulture); }
        public string Run(string exe, string args, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            var psi = new ProcessStartInfo(System.IO.Path.Combine(BinDirectory, exe), args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
            using (var p = Process.Start(psi)) {
                ResourceUsage.TrackProcess(p.Id);try {
                var error = p.StandardError.ReadToEndAsync(); var output = p.StandardOutput.ReadToEndAsync();
                using (token.Register(() => { try { if (!p.HasExited) p.Kill(); } catch { } })) {
                    p.WaitForExit(); token.ThrowIfCancellationRequested();
                    string err = error.GetAwaiter().GetResult(); string result = output.GetAwaiter().GetResult();
                    if (p.ExitCode != 0) throw new InvalidOperationException(err.Length > 3000 ? err.Substring(err.Length - 3000) : err);
                    return result;
                }
                }finally{ResourceUsage.ForgetProcess(p.Id);}
            }
        }
        static int Int(Dictionary<string, object> d, string key) { object v; int n; return d.TryGetValue(key, out v) && Int32.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out n) ? n : 0; }
        static double Number(Dictionary<string, object> d, string key) { object v; double n; return d.TryGetValue(key, out v) && Double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out n) ? n : 0; }
        static string Str(Dictionary<string, object> d, string key) { object v; return d.TryGetValue(key, out v) ? Convert.ToString(v) : ""; }
        public List<AudioInfo> ProbeTracks(string path, CancellationToken token) {
            LoadPolicy.ValidateSize(new FileInfo(path).Length);
            string json=Run("ffprobe.exe", "-v error -protocol_whitelist file,pipe -select_streams a -show_streams -show_format -of json "+Quote(path),token);
            var root=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);
            var streams=(System.Collections.IList)root["streams"];var format=(Dictionary<string,object>)root["format"];var tracks=new List<AudioInfo>();
            foreach(Dictionary<string,object> stream in streams){
                object value;var tags=stream.TryGetValue("tags",out value)?(Dictionary<string,object>)value:new Dictionary<string,object>();
                var disposition=stream.TryGetValue("disposition",out value)?(Dictionary<string,object>)value:new Dictionary<string,object>();
                var info=new AudioInfo {Path=System.IO.Path.GetFullPath(path),StreamIndex=Int(stream,"index"),AudioTrackIndex=tracks.Count,Codec=Str(stream,"codec_name"),Format=Str(format,"format_name"),Title=Str(tags,"title"),Language=Str(tags,"language"),DefaultTrack=Int(disposition,"default")!=0,ChannelLayout=Str(stream,"channel_layout"),SampleRate=Int(stream,"sample_rate"),Channels=Int(stream,"channels"),Bits=Int(stream,"bits_per_raw_sample"),Bitrate=Int(stream,"bit_rate"),Duration=Number(stream,"duration")};
                if(String.IsNullOrWhiteSpace(info.Title))info.Title=Str(tags,"handler_name");if(info.Bits==0)info.Bits=Int(stream,"bits_per_sample");if(info.Duration<=0)info.Duration=Number(format,"duration");if(info.Bitrate==0 && !info.VideoSource)info.Bitrate=Int(format,"bit_rate");
                tracks.Add(info);
            }
            if(tracks.Count==0)throw new InvalidOperationException("文件不包含音频流。");return tracks;
        }
        public AudioInfo Probe(string path, CancellationToken token) { return Probe(path,token,0); }
        public AudioInfo Probe(string path,CancellationToken token,int trackIndex) {
            var tracks=ProbeTracks(path,token);if(trackIndex<0 || trackIndex>=tracks.Count)throw new InvalidOperationException("所选音轨不存在。");var info=tracks[trackIndex];
            bool rawAac=String.Equals(System.IO.Path.GetExtension(path),".aac",StringComparison.OrdinalIgnoreCase);
            if(rawAac){string counts=Run("ffprobe.exe","-v error -protocol_whitelist file,pipe -count_frames -select_streams a:"+trackIndex+" -show_entries stream=nb_read_frames -of csv=p=0 "+Quote(path),token);long frames;if(Int64.TryParse(counts.Trim(),out frames) && info.SampleRate>0)info.Duration=frames*1024.0/info.SampleRate;}
            if(info.SampleRate<=0 || info.Channels<=0 || info.Duration<=0)throw new InvalidOperationException("无法读取有效音频参数。");
            if(info.Bitrate==0 || rawAac){long bytes=0;RunLines("ffprobe.exe","-v error -protocol_whitelist file,pipe -select_streams a:"+trackIndex+" -show_packets -show_entries packet=size -of csv=p=0 "+Quote(path),token,line=>{long size;if(Int64.TryParse(line.Trim().Split(',')[0],out size))bytes+=size;});info.Bitrate=(int)Math.Min(Int32.MaxValue,Math.Round(bytes*8/info.Duration));}
            return info;
        }
        void RunLines(string exe,string args,CancellationToken token,Action<string> line) {
            token.ThrowIfCancellationRequested();var psi=new ProcessStartInfo(System.IO.Path.Combine(BinDirectory,exe),args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,StandardOutputEncoding=System.Text.Encoding.UTF8,StandardErrorEncoding=System.Text.Encoding.UTF8};
            using(var process=Process.Start(psi)){ResourceUsage.TrackProcess(process.Id);try{var error=process.StandardError.ReadToEndAsync();using(token.Register(()=>{try{if(!process.HasExited)process.Kill();}catch{}})){
                string text;while((text=process.StandardOutput.ReadLine())!=null){token.ThrowIfCancellationRequested();line(text);}process.WaitForExit();token.ThrowIfCancellationRequested();string failure=error.GetAwaiter().GetResult();if(process.ExitCode!=0)throw new InvalidOperationException(failure.Length>3000?failure.Substring(failure.Length-3000):failure);
            }}finally{ResourceUsage.ForgetProcess(process.Id);}}
        }
        public AudioData Load(string path,CancellationToken token) {return Load(path,token,0,null);}
        public AudioData Load(string path,CancellationToken token,int trackIndex,Action<int> progress) {
            var info=Probe(path,token,trackIndex);if(progress!=null)progress(2);
            string temporary=CreateTemporaryPath("", ".f64");AudioData data=null;ResourceUsage.TrackFile(temporary,true);
            try {
                RunLines("ffmpeg.exe","-v error -nostdin -protocol_whitelist file,pipe -i "+Quote(path)+" -map 0:"+info.StreamIndex+" -vn -c:a pcm_f64le -f f64le -progress pipe:1 -nostats "+Quote(temporary),token,line=>{if(progress!=null && line.StartsWith("out_time_us=")){long time;if(Int64.TryParse(line.Substring(12),out time))progress(2+(int)Math.Min(72,Math.Max(0,time/1000000.0/info.Duration*72)));}});
                long count=new FileInfo(temporary).Length/8;if(count==0)throw new InvalidOperationException("音频为空。");
                data=new AudioData{Info=info};
                if(count<=16*1024*1024){data.Samples=new double[(int)count];using(var f=File.OpenRead(temporary)){byte[] block=new byte[65536];int offset=0,read;while((read=f.Read(block,0,block.Length))>0){token.ThrowIfCancellationRequested();if(read%8!=0)throw new InvalidDataException("解码数据不完整。");Buffer.BlockCopy(block,0,data.Samples,offset*8,read);offset+=read/8;}}}
                else {data.Storage=new SampleStorage(temporary);temporary=null;}
                info.Duration=(double)data.Frames/info.SampleRate;data.Peaks=SampleStorage.BuildPeaks(data,token,p=>{if(progress!=null)progress(75+p*25/100);});token.ThrowIfCancellationRequested();return data;
            }catch{if(data!=null)data.Dispose();throw;}finally{if(temporary!=null){if(File.Exists(temporary))File.Delete(temporary);ResourceUsage.ForgetFile(temporary);}}
        }
        public static string Filters(AudioInfo info, IEnumerable<Region> deleted, OutputOptions options) {
            options.Validate(info);
            var kept = Regions.Kept(deleted, info.Duration);
            if (kept.Count == 0) throw new InvalidOperationException("所有片段都被删除，无法导出空音频。");
            var b = new StringBuilder();
            b.Append("[0:" + info.StreamIndex + "]asetpts=PTS-STARTPTS[source];");
            if (kept.Count > 1) { b.Append("[source]asplit=" + kept.Count); for (int i = 0; i < kept.Count; i++) b.Append("[s" + i + "]"); b.Append(';'); }
            for (int i = 0; i < kept.Count; i++) { b.Append(kept.Count == 1 ? "[source]" : "[s" + i + "]"); b.Append("atrim=start=" + N(kept[i].Start) + ":end=" + N(kept[i].End) + ",asetpts=PTS-STARTPTS[k" + i + "];"); }
            for (int i = 0; i < kept.Count; i++) b.Append("[k" + i + "]");
            b.Append(kept.Count > 1 ? "concat=n=" + kept.Count + ":v=0:a=1" : "anull");
            if (options.LowCut) b.Append(",highpass=f=" + N(Math.Min(options.LowHz, options.SampleRate * 0.49)));
            if (options.HighCut && options.HighHz < options.SampleRate / 2.0) b.Append(",lowpass=f=" + N(options.HighHz));
            if (Math.Abs(options.Speed - 1) > 0.00001 || Math.Abs(options.Pitch) > 0.00001) b.Append(",rubberband=tempo=" + N(options.Speed) + ":pitch=" + N(Math.Pow(2, options.Pitch / 12)));
            b.Append(",aresample=" + options.SampleRate + "[out]"); return b.ToString();
        }
        string Encoding(AudioInfo info, OutputOptions options, bool preview) {
            if (preview) return "-ac " + Math.Min(2,info.Channels) + " -c:a pcm_s16le -rf64 auto -f wav";
            string ext = options.Extension(info), codec;
            bool mp3 = ext == ".mp3";
            if (mp3) {
                if (options.SampleRate > 48000) throw new InvalidOperationException("MP3 最高支持 48 kHz，请降低采样率。");
                int ceiling = info.Bitrate > 0 ? Math.Min(320, info.Bitrate / 1000) : 320;
                int target = Math.Min(ceiling, options.Bitrate > 0 ? options.Bitrate : ceiling);
                int[] rates = options.SampleRate >= 32000 ? new[] {32,40,48,56,64,80,96,112,128,160,192,224,256,320} : new[] {8,16,24,32,40,48,56,64,80,96,112,128,144,160};
                int chosen = rates.Where(x => x <= target).DefaultIfEmpty(0).Max();
                if (chosen == 0) throw new InvalidOperationException("源码率过低，无法在不提升码率的前提下转换为 MP3。");
                return (info.Channels > 2 ? "-ac 2 " : "") + "-c:a libmp3lame -b:a " + chosen + "k -id3v2_version 3 -f mp3";
            }
            switch (ext) {
                case ".wav": case ".wave": codec = options.Bits <= 8 ? "pcm_u8" : options.Bits <= 16 ? "pcm_s16le" : options.Bits <= 24 ? "pcm_s24le" : options.Bits == 64 && info.Codec == "pcm_f64le" ? "pcm_f64le" : options.Bits == 32 && info.Codec == "pcm_f32le" ? "pcm_f32le" : "pcm_s32le"; return "-c:a " + codec + " -rf64 auto -f wav";
                case ".flac": if (options.Bits > 24) throw new InvalidOperationException("当前 FLAC 输出支持至多 24 bit。"); return "-c:a flac -sample_fmt " + (options.Bits <= 16 ? "s16" : "s32") + " -bits_per_raw_sample " + options.Bits + " -f flac";
                case ".aiff": case ".aif": return "-c:a " + (options.Bits <= 8 ? "pcm_s8" : options.Bits <= 16 ? "pcm_s16be" : options.Bits <= 24 ? "pcm_s24be" : "pcm_s32be") + " -f aiff";
                case ".m4a": case ".mp4": if (info.Codec == "alac") { if (options.Bits != 16 && options.Bits != 24 && options.Bits != 32) throw new InvalidOperationException("ALAC 位深请选择 16、24 或 32 bit。"); return "-c:a alac -sample_fmt " + (options.Bits <= 16 ? "s16p" : "s32p") + " -bits_per_raw_sample " + options.Bits + " -f ipod"; } codec = "aac"; break;
                case ".aac": codec = "aac"; break;
                case ".ogg": codec = info.Codec == "opus" ? "libopus" : "libvorbis"; break;
                case ".opus": codec = "libopus"; break;
                case ".wma": codec = "wmav2"; break;
                case ".wv": return "-c:a wavpack -sample_fmt " + (options.Bits <= 16 ? "s16p" : "s32p") + " -f wv";
                default: throw new InvalidOperationException("暂不支持写入原格式 " + ext + "，请选择 MP3 导出。");
            }
            int bitrate = Math.Min(options.Bitrate, info.Bitrate > 0 ? info.Bitrate / 1000 : options.Bitrate);
            if (bitrate <= 0) throw new InvalidOperationException("无法确定源码率，请使用可读取码率的文件。");
            return "-c:a " + codec + " -b:a " + bitrate + "k" + (codec == "libopus" ? " -vbr off" : "");
        }
        public void Render(AudioInfo info, IEnumerable<Region> deleted, OutputOptions options, string target, bool preview, CancellationToken token) {
            string graph = CreateTemporaryPath("", ".filter");
            ResourceUsage.TrackFile(target);
            try {
                File.WriteAllText(graph, Filters(info, deleted, options), new UTF8Encoding(false));
                var encodingOptions = options.Copy(); AudioInfo result = null;
                for (int attempt = 0; attempt < 5; attempt++) {
                    Run("ffmpeg.exe", "-v error -nostdin -y -protocol_whitelist file,pipe -i " + Quote(info.InputPath ?? info.Path) + " -/filter_complex " + Quote(graph) + " -map [out] -map_metadata 0 -vn -ar " + options.SampleRate + " " + Encoding(info, encodingOptions, preview) + " " + Quote(target), token);
                    result = Probe(target, token);
                    // VBR encoders can exceed the requested rate. Re-encode downward before committing any output.
                    if (preview || result.Lossless || info.Bitrate <= 0 || result.Bitrate <= info.Bitrate || attempt == 4) break;
                    int reduced = (int)(encodingOptions.Bitrate * ((double)info.Bitrate / result.Bitrate) * 0.9);
                    encodingOptions.Bitrate = Math.Max(1, Math.Min(encodingOptions.Bitrate - 1, reduced));
                }
                if (result.Duration <= 0 || new FileInfo(target).Length == 0) throw new InvalidDataException("导出结果校验失败。");
                if (!preview) {
                    if (result.SampleRate > info.SampleRate) throw new InvalidDataException("输出采样率高于源文件，已取消提交。");
                    if (info.Lossless && result.Lossless && info.Bits > 0 && result.Bits > info.Bits) throw new InvalidDataException("输出位深高于源文件，已取消提交。");
                    if (!result.Lossless && info.Bitrate > 0 && result.Bitrate > info.Bitrate) throw new InvalidDataException("输出实测码率高于源文件，已取消提交。请选择更低码率。");
                }
            } finally { if (File.Exists(graph)) File.Delete(graph); }
        }
        public AudioData LoadSnapshot(string source, CancellationToken token) { return LoadSnapshot(source,token,0,null); }
        public AudioData LoadSnapshot(string source,CancellationToken token,int trackIndex,Action<int> progress) {
            LoadPolicy.ValidateSize(new FileInfo(source).Length);
            string copy = CreateTemporaryPath("source-", System.IO.Path.GetExtension(source));
            ResourceUsage.TrackFile(copy);
            try {
                using (var input = File.OpenRead(source)) using (var output = new FileStream(copy, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                    var buffer = new byte[1048576]; int count; while ((count = input.Read(buffer, 0, buffer.Length)) > 0) { token.ThrowIfCancellationRequested(); output.Write(buffer, 0, count); if(progress!=null)progress((int)(input.Position*15/Math.Max(1,input.Length))); }
                }
                var data = Load(copy, token,trackIndex,p=>{if(progress!=null)progress(15+p*85/100);}); data.Info.Path = System.IO.Path.GetFullPath(source); data.Info.InputPath = copy; return data;
            } catch { if (File.Exists(copy)) File.Delete(copy);ResourceUsage.ForgetFile(copy); throw; }
        }
        public static void ReleaseSnapshot(AudioInfo info) {
            if (info == null || info.InputPath == null) return;
            try { File.Delete(info.InputPath);ResourceUsage.ForgetFile(info.InputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        public string Export(AudioInfo info, IEnumerable<Region> deleted, OutputOptions options, string requested, bool direct, CancellationToken token) {
            if (direct && options.RequiresSaveAs(info)) throw new InvalidOperationException(info.VideoSource ? "视频音轨只能另存为音频文件，原视频保持不变。" : "修改速度、音调或格式后必须另存为。");
            string target = System.IO.Path.GetFullPath(requested);
            if (direct && !String.Equals(target, info.Path, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("直接保存仅能写入当前源文件路径。");
            if (!direct && !String.Equals(System.IO.Path.GetExtension(target), options.Extension(info), StringComparison.OrdinalIgnoreCase)) target = System.IO.Path.ChangeExtension(target, options.Extension(info));
            if (!direct && String.Equals(target, info.Path, StringComparison.OrdinalIgnoreCase)) target = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(target), System.IO.Path.GetFileNameWithoutExtension(target) + "_edited" + options.Extension(info));
            string folder = System.IO.Path.GetDirectoryName(target);
            Directory.CreateDirectory(folder);
            string temp = System.IO.Path.Combine(folder, ".gapgone-" + Guid.NewGuid() + options.Extension(info));
            try {
                Render(info, deleted, options, temp, false, token); token.ThrowIfCancellationRequested();
                if (direct) {
                    try {
                        using (new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                        File.Replace(temp, target, null); return target;
                    } catch (IOException) { target = System.IO.Path.Combine(folder, System.IO.Path.GetFileNameWithoutExtension(target) + "_edited" + options.Extension(info)); }
                    catch (UnauthorizedAccessException) { target = System.IO.Path.Combine(folder, System.IO.Path.GetFileNameWithoutExtension(target) + "_edited" + options.Extension(info)); }
                }
                return CommitUnique(temp, target);
            } finally { if (File.Exists(temp)) File.Delete(temp);ResourceUsage.ForgetFile(temp); }
        }
        public static string CommitUnique(string temp, string requested) {
            for (int n = 0; ; n++) {
                string path = n == 0 ? requested : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(requested), System.IO.Path.GetFileNameWithoutExtension(requested) + " (" + n + ")" + System.IO.Path.GetExtension(requested));
                // Rename is atomic and refuses an existing destination, including concurrent exports.
                try { File.Move(temp, path); return path; } catch (IOException) { if (!File.Exists(path)) throw; }
            }
        }
        public static OutputOptions Quick(AudioInfo info) {
            var o = OutputOptions.For(info); o.SetFormat(info,true,false); o.LowCut = o.HighCut = true;
            o.SampleRate = new[] {8000,11025,12000,16000,22050,24000,32000,44100,48000}.Where(x => x <= info.SampleRate).DefaultIfEmpty(8000).Max();
            o.Bitrate = Math.Min(320, info.Bitrate > 0 ? info.Bitrate / 1000 : 320); return o;
        }
    }
}
