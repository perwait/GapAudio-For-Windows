using System;
using System.IO;
using System.Windows.Forms;

namespace GapGone {
    public static class LoadPolicy {
        public const long MaximumBytes = 70L * 1024 * 1024 * 1024;
        public const long WarningBytes = 500L * 1024 * 1024;
        public static void ValidateSize(long bytes) { if(bytes > MaximumBytes)throw new InvalidOperationException("文件超过 70 GB 加载上限。"); }
        public static string Estimate(long bytes, double duration = 0) {
            double low=Math.Max(5,bytes/(100.0*1024*1024)+duration/80), high=Math.Max(low*2,bytes/(20.0*1024*1024)+duration/10);
            return FormatSeconds(low)+"–"+FormatSeconds(high);
        }
        static string FormatSeconds(double seconds) { return seconds<60?Math.Ceiling(seconds)+" 秒":seconds<3600?Math.Ceiling(seconds/60)+" 分钟":(seconds/3600).ToString("F1")+" 小时"; }
        public static bool Confirm(IWin32Window owner,string path,double duration=0) {
            long bytes=new FileInfo(path).Length;ValidateSize(bytes);if(bytes<=WarningBytes)return true;
            using(var dialog=CreateDialog(bytes,duration))return dialog.ShowDialog(owner)==DialogResult.Yes;
        }
        public static ChoiceDialog CreateDialog(long bytes,double duration=0) { return new ChoiceDialog("大文件加载确认","文件大小："+ResourceUsage.FormatBytes(bytes)+"。\n预估加载时间："+Estimate(bytes,duration)+"（实际受设备性能、音轨长度和编码影响）。\n\n请先保存办公文档和其他未保存工作。加载大文件可能引起电脑卡顿、无响应，甚至宕机；临时解码数据还需额外磁盘空间。\n\n是否继续加载？","继续加载",null,"取消加载"); }
    }
}
