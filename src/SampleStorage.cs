using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace GapGone {
    public sealed class SampleStorage : IDisposable {
        readonly string path;
        readonly MemoryMappedFile map;
        readonly MemoryMappedViewAccessor view;
        public readonly long Length;
        public SampleStorage(string file) {
            path = file;ResourceUsage.TrackFile(file,true); Length = new FileInfo(file).Length / 8;
            map = MemoryMappedFile.CreateFromFile(file, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            view = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        }
        public double Read(long index) { return view.ReadDouble(index * 8); }
        public void Read(long index, double[] buffer, int count) { view.ReadArray(index * 8,buffer,0,count); }
        public void Dispose() { view.Dispose(); map.Dispose(); File.Delete(path);ResourceUsage.ForgetFile(path); }
        public static float[] BuildPeaks(AudioData data, CancellationToken token, Action<int> progress = null) {
            int bins = (int)Math.Min(2000000,Math.Max(1,(data.Frames + 255) / 256));
            var peaks = new float[bins]; int channels=data.Info.Channels;
            var buffer=new double[Math.Max(channels,65536 / channels * channels)];
            long total=data.Frames * channels;
            for(long offset=0;offset<total;offset+=buffer.Length){
                token.ThrowIfCancellationRequested(); int count=(int)Math.Min(buffer.Length,total-offset); data.ReadSamples(offset,buffer,count);
                for(int j=0;j<count;j++){int bin=(int)Math.Min(bins-1,((offset+j)/channels)*bins/data.Frames);peaks[bin]=Math.Max(peaks[bin],(float)Math.Abs(buffer[j]));}
                if(progress!=null)progress((int)((offset+count)*100/total));
            }
            return peaks;
        }
    }
}
