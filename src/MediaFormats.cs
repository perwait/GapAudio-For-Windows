using System;
using System.IO;
using System.Linq;

namespace GapGone {
    public static class MediaFormats {
        public static readonly string[] AudioExtensions = { ".wav", ".wave", ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".aiff", ".aif", ".wma", ".wv", ".ape" };
        public static readonly string[] VideoExtensions = { ".mp4", ".m4s", ".m4v", ".mov", ".mkv", ".webm", ".avi", ".wmv", ".ts", ".mts", ".m2ts", ".mpg", ".mpeg", ".flv" };
        public static readonly string[] Extensions = AudioExtensions.Concat(VideoExtensions).ToArray();
        public const string SupportedText = "音频：WAV/WAVE、MP3、FLAC、M4A（AAC/ALAC）、AAC、OGG（Vorbis/Opus）、OPUS、AIFF/AIF、WMA、WavPack（WV）、APE。\n视频音轨提取：MP4、M4S、M4V、MOV、MKV、WEBM、AVI、WMV、TS/MTS/M2TS、MPG/MPEG、FLV；多音轨视频先选择音轨，可查看 title、default、language、编码和声道布局；单/双声道默认导出 MP3，多声道默认 FLAC，原视频保留。视频中的音轨须为支持的音频编码（含 AAC、MP3、AC-3/E-AC-3、DTS、Vorbis、Opus、FLAC、ALAC、PCM、WMA）。\nM4S 必须包含初始化信息和音轨；只有视频轨的文件或缺少初始化片段的分段文件无法单独提取。加密音轨不支持。";
        public static string OpenFilter { get { return "音频 / 视频|" + String.Join(";", Extensions.Select(x => "*" + x)) + "|所有文件|*.*"; } }
        public static bool IsVideo(string path) { return VideoExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()); }
        public static bool Supported(string path) { return Extensions.Contains(Path.GetExtension(path).ToLowerInvariant()); }
        public static string ExtractedExtension(AudioInfo info) {
            switch (info.Codec) {
                case "aac": case "alac": return ".m4a";
                case "flac": return ".flac";
                case "vorbis": return ".ogg";
                case "opus": return ".opus";
                case "wavpack": return ".wv";
                default: return info.Codec != null && info.Codec.StartsWith("pcm_") ? ".wav" : ".mp3";
            }
        }
    }
}
