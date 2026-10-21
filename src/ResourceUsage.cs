using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GapGone {
    public sealed class ResourceSnapshot {
        public long UsedBytes, AvailableBytes, DiskBytes;
        public bool DiskBacked;
        public string Description {get {return "内存已占用："+ResourceUsage.FormatBytes(UsedBytes)+" / 可用："+ResourceUsage.FormatBytes(AvailableBytes)+(DiskBacked?" · 磁盘占用："+ResourceUsage.FormatBytes(DiskBytes):"");}}
    }
    public static class ResourceUsage {
        // Track only this instance's temporary files and the media processes it starts.
        static readonly ConcurrentDictionary<string,bool> files=new ConcurrentDictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
        static readonly ConcurrentDictionary<int,byte> processes=new ConcurrentDictionary<int,byte>();
        [StructLayout(LayoutKind.Sequential)] struct MemoryStatus {
            public uint Length, Load;
            public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
        }
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        public static string FormatBytes(long bytes) {
            return bytes<1024L*1024*1024?(Math.Max(0,bytes)/(1024.0*1024)).ToString("F0",CultureInfo.InvariantCulture)+" MB":(bytes/(1024.0*1024*1024)).ToString("F2",CultureInfo.InvariantCulture)+" GB";
        }
        public static void TrackFile(string path,bool decoded=false) {files[path]=decoded;}
        public static void ForgetFile(string path) {bool ignored;if(path!=null)files.TryRemove(path,out ignored);}
        public static void TrackProcess(int id) {processes[id]=0;}
        public static void ForgetProcess(int id) {byte ignored;processes.TryRemove(id,out ignored);}
        public static ResourceSnapshot Read() {
            var result=new ResourceSnapshot();
            using(var process=Process.GetCurrentProcess())result.UsedBytes=process.PrivateMemorySize64;
            foreach(int id in processes.Keys)try{using(var process=Process.GetProcessById(id))result.UsedBytes+=process.PrivateMemorySize64;}catch(ArgumentException){}catch(InvalidOperationException){}
            var memory=new MemoryStatus {Length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};
            if(GlobalMemoryStatusEx(ref memory))result.AvailableBytes=(long)Math.Min(memory.AvailableVirtual,Math.Min(memory.AvailablePhysical,memory.AvailablePageFile));
            foreach(var item in files)try{var file=new FileInfo(item.Key);if(file.Exists){result.DiskBytes+=file.Length;if(item.Value && file.Length>128L*1024*1024)result.DiskBacked=true;}}catch(IOException){}catch(UnauthorizedAccessException){}
            return result;
        }
    }
    public sealed class ResourceUsageLabel : StyledLabel {
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer {Interval=1000};
        readonly ToolTip tooltip=new ToolTip();
        public ResourceUsageLabel() {
            AutoSize=false;AutoEllipsis=true;TextAlign=System.Drawing.ContentAlignment.MiddleRight;Dock=DockStyle.Fill;Margin=new Padding(4,0,12,0);
            tooltip.SetToolTip(this,"已占用：本实例及其媒体处理进程的专用内存。可用：Windows 当前可分配的物理内存、提交额度与进程地址空间的较小值，会随其他程序变化。磁盘占用：本实例的解码缓存、源快照及临时处理文件。");
            timer.Tick+=(s,e)=>RefreshUsage();RefreshUsage();timer.Start();
        }
        public void RefreshUsage() {var snapshot=ResourceUsage.Read();Text=snapshot.Description;Width=Math.Max(240,TextRenderer.MeasureText(Text,Font).Width+18);}
        protected override void Dispose(bool disposing) {if(disposing){timer.Dispose();tooltip.Dispose();}base.Dispose(disposing);}
    }
}
