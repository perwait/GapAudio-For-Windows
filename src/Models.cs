using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GapGone {
    public sealed class Region {
        public double Start, End;
        public Region(double start, double end) { Start = start; End = end; }
        public Region Copy() { return new Region(Start, End); }
    }
    public static class Regions {
        public static List<Region> Normalize(IEnumerable<Region> input, double duration) {
            var result = new List<Region>();
            foreach (var r in input.Select(x => new Region(Math.Max(0, x.Start), Math.Min(duration, x.End))).Where(x => x.End > x.Start).OrderBy(x => x.Start)) {
                if (result.Count > 0 && r.Start <= result.Last().End) result.Last().End = Math.Max(result.Last().End, r.End);
                else result.Add(r);
            }
            return result;
        }
        public static List<Region> Subtract(IEnumerable<Region> input, Region cut, double duration) {
            var result = new List<Region>();
            foreach (var r in input) {
                if (r.End <= cut.Start || r.Start >= cut.End) result.Add(r.Copy());
                else {
                    if (r.Start < cut.Start) result.Add(new Region(r.Start, cut.Start));
                    if (r.End > cut.End) result.Add(new Region(cut.End, r.End));
                }
            }
            return Normalize(result, duration);
        }
        public static List<Region> Kept(IEnumerable<Region> deleted, double duration) {
            var result = new List<Region>(); double pos = 0;
            foreach (var r in Normalize(deleted, duration)) { if (pos < r.Start) result.Add(new Region(pos, r.Start)); pos = r.End; }
            if (pos < duration) result.Add(new Region(pos, duration));
            return result;
        }
    }
    public sealed class AudioInfo {
        public string Path, Codec, Format, InputPath, Title, Language, ChannelLayout;
        public int StreamIndex, AudioTrackIndex;
        public bool DefaultTrack;
        public double Duration;
        public int SampleRate, Channels, Bits, Bitrate;
        public bool Lossless { get { return Codec != null && (Codec.StartsWith("pcm_") || Codec == "flac" || Codec == "alac" || Codec == "wavpack" || Codec == "ape"); } }
        public bool VideoSource { get { return MediaFormats.IsVideo(Path); } }
        public string ChannelDescription { get { return Channels == 1 ? "1（单声道）" : Channels == 2 ? "2（立体声）" : !String.IsNullOrEmpty(ChannelLayout) ? ChannelLayout : Channels + " 声道"; } }
        public string TrackDescription { get { return (String.IsNullOrWhiteSpace(Title) ? "音轨 " + (AudioTrackIndex + 1) : Title) + "\n" + (DefaultTrack ? "default · " : "") + "language: " + (String.IsNullOrWhiteSpace(Language) ? "未标注" : Language) + " · " + Codec + " · " + ChannelDescription; } }
        public string Summary { get { return String.Format("{0} · {1:N0} Hz · {2} · {3} kbps · {4}", Codec, SampleRate, Bits > 0 ? Bits + " bit" : "位深不适用", Bitrate / 1000, ChannelDescription); } }
    }
    public sealed class AudioData : IDisposable {
        public AudioInfo Info;
        public double[] Samples;
        internal SampleStorage Storage;
        public float[] Peaks;
        public long Frames { get { return (Samples != null ? Samples.LongLength : Storage.Length) / Info.Channels; } }
        public double Sample(long index) { return Samples != null ? Samples[index] : Storage.Read(index); }
        public void ReadSamples(long index, double[] buffer, int count) { if (Samples != null) Array.Copy(Samples,index,buffer,0,count); else Storage.Read(index,buffer,count); }
        public void Dispose() { if (Storage != null) { Storage.Dispose(); Storage = null; } }
    }
    public sealed class OutputOptions {
        public bool Mp3, Flac, LowCut, HighCut;
        public bool KeepRate = true, KeepBits = true, KeepBitrate = true;
        public int SampleRate, Bits, Bitrate;
        public double LowHz = 50, HighHz = 21000, Speed = 1, Pitch;
        public OutputOptions Copy() { return (OutputOptions)MemberwiseClone(); }
        public bool RequiresSaveAs(AudioInfo info) { return info.VideoSource || Flac && info.Codec != "flac" || Mp3 && info.Codec != "mp3" || Math.Abs(Speed - 1) > 0.00001 || Math.Abs(Pitch) > 0.00001; }
        public string Extension(AudioInfo info) { return Flac ? ".flac" : Mp3 ? ".mp3" : info.VideoSource ? MediaFormats.ExtractedExtension(info) : System.IO.Path.GetExtension(info.Path).ToLowerInvariant(); }
        public bool IsLossless(AudioInfo info) { return Flac || !Mp3 && info.Lossless; }
        public static int Mp3Rate(int source) { return new[] {8000,11025,12000,16000,22050,24000,32000,44100,48000}.Where(x=>x<=source).DefaultIfEmpty(source).Max(); }
        public void SetFormat(AudioInfo info, bool mp3, bool flac) {
            Mp3 = mp3; Flac = flac;
            if (KeepRate) SampleRate = mp3 ? Mp3Rate(info.SampleRate) : info.SampleRate;
            else if(mp3) SampleRate = Mp3Rate(Math.Min(SampleRate,info.SampleRate));
            if (KeepBits) Bits = flac ? Math.Min(24,info.Bits > 0 ? info.Bits : 16) : info.Bits;
            if (KeepBitrate) Bitrate = mp3 ? Math.Min(320,info.Bitrate > 0 ? info.Bitrate / 1000 : 320) : info.Bitrate / 1000;
        }
        public void Validate(AudioInfo info) {
            if (SampleRate <= 0 || SampleRate > info.SampleRate) throw new InvalidOperationException("采样率只能保持或降低。");
            if (info.Bits > 0 && !Mp3 && (Bits <= 0 || Bits > info.Bits)) throw new InvalidOperationException("位深只能保持或降低。");
            if (!Flac && (Bitrate < 0 || info.Bitrate > 0 && Bitrate > info.Bitrate / 1000)) throw new InvalidOperationException("码率只能保持或降低。");
            if (Speed < 0.25 || Speed > 4 || Pitch < -12 || Pitch > 12) throw new InvalidOperationException("速度范围 0.25–4，音调范围 ±12 半音。");
            if (LowHz < 1 || HighHz < 1 || LowCut && HighCut && LowHz >= HighHz) throw new InvalidOperationException("滤波频率无效：低频界限必须小于高频界限。");
        }
        public static OutputOptions For(AudioInfo info) {
            var result = new OutputOptions(); result.SetFormat(info, info.VideoSource && info.Channels <= 2, info.VideoSource && info.Channels > 2); return result;
        }
    }
    public sealed class EditState {
        public List<Region> Deleted = new List<Region>();
        public OutputOptions Options;
        public EditState Copy() { return new EditState { Deleted = Deleted.Select(x => x.Copy()).ToList(), Options = Options.Copy() }; }
        public string Signature() {
            var o = Options;
            return String.Join("|", new object[] {o.Mp3, o.Flac, o.SampleRate, o.Bits, o.Bitrate, o.LowCut, o.HighCut, o.LowHz.ToString("R", System.Globalization.CultureInfo.InvariantCulture), o.HighHz.ToString("R", System.Globalization.CultureInfo.InvariantCulture), o.Speed.ToString("R", System.Globalization.CultureInfo.InvariantCulture), o.Pitch.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}) + ";" + String.Join(";", Deleted.Select(r => r.Start.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + r.End.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        }
    }
    public sealed class FrequencyRange {
        public double MinHz, MaxHz;
    }
    public sealed class BatchItem {
        public string Path, Status = "等待", Output;
        public AudioInfo Info;
        public bool Selected;
        public bool SizeApproved;
    }
}
