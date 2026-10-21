using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace GapGone {
    public sealed class Playback : IDisposable {
        [StructLayout(LayoutKind.Sequential)] struct Format { public ushort tag, channels; public uint rate, bytesPerSecond; public ushort align, bits, extra; }
        [StructLayout(LayoutKind.Sequential)] struct Header { public IntPtr data; public uint length, recorded; public IntPtr user; public uint flags, loops; public IntPtr next, reserved; }
        [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr handle, uint device, ref Format format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr handle);
        [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr handle);
        IntPtr handle; readonly Stopwatch clock = new Stopwatch(); readonly object sync=new object();
        readonly IntPtr[] memories=new IntPtr[3],headers=new IntPtr[3];
        FileStream stream; System.Threading.Timer pump; long remaining; int blockSize; bool failed;
        double offset, duration;
        public bool IsPlaying { get { lock(sync)return handle != IntPtr.Zero; } }
        public bool Ended { get { lock(sync)return handle!=IntPtr.Zero && (failed || remaining==0 && AllDone()); } }
        bool AllDone() {foreach(var header in headers)if(header!=IntPtr.Zero && (((Header)Marshal.PtrToStructure(header,typeof(Header))).flags&1)==0)return false;return true;}
        public double Position { get { return Math.Min(duration, offset + clock.Elapsed.TotalSeconds); } }
        public double Duration { get { return duration; } }
        public void Play(string path, double start) {
            Stop();lock(sync){
                try {
                    stream=File.OpenRead(path);var reader=new BinaryReader(stream);
                    string container=new string(reader.ReadChars(4));if(container!="RIFF" && container!="RF64")throw new InvalidDataException("预览 WAV 无效。");reader.ReadUInt32();reader.ReadBytes(4);
                    Format format=new Format();long dataStart=0,dataLength=0,rf64Length=0;
                    while(stream.Position+8<=stream.Length){string id=new string(reader.ReadChars(4));uint length=reader.ReadUInt32();long next=stream.Position+length+(length&1);
                        if(id=="ds64"){reader.ReadUInt64();rf64Length=checked((long)reader.ReadUInt64());}
                        if(id=="fmt "){format.tag=reader.ReadUInt16();format.channels=reader.ReadUInt16();format.rate=reader.ReadUInt32();format.bytesPerSecond=reader.ReadUInt32();format.align=reader.ReadUInt16();format.bits=reader.ReadUInt16();if(format.tag==65534 && length>=40){reader.ReadUInt16();reader.ReadUInt16();reader.ReadUInt32();format.tag=(ushort)reader.ReadUInt32();}}
                        if(id=="data"){dataStart=stream.Position;dataLength=Math.Min(container=="RF64" && length==UInt32.MaxValue?rf64Length:length,stream.Length-dataStart);break;}stream.Position=next;
                    }
                    if(format.tag!=1 || format.bits!=16 || dataStart==0 || format.align==0 || format.bytesPerSecond==0)throw new InvalidDataException("预览必须为 16 bit PCM。");
                    duration=(double)dataLength/format.bytesPerSecond;long skipped=Math.Min(dataLength,(long)(Math.Max(0,start)*format.rate)*format.align);remaining=dataLength-skipped;offset=(double)skipped/format.bytesPerSecond;
                    if(remaining==0){stream.Dispose();stream=null;return;}stream.Position=dataStart+skipped;failed=false;
                    if(waveOutOpen(out handle,0xFFFFFFFF,ref format,IntPtr.Zero,IntPtr.Zero,0)!=0)throw new InvalidOperationException("无法打开音频输出设备。");
                    blockSize=Math.Max(format.align,(int)(format.bytesPerSecond/4/format.align)*format.align);
                    for(int i=0;i<3;i++){memories[i]=Marshal.AllocHGlobal(blockSize);headers[i]=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Header)));Marshal.StructureToPtr(new Header{data=memories[i],flags=1},headers[i],false);Fill(i);}
                    clock.Restart();pump=new System.Threading.Timer(Pump,null,20,20);
                }catch{Stop();throw;}
            }
        }
        void Fill(int i) {
            if(remaining<=0)return;
            var previous=(Header)Marshal.PtrToStructure(headers[i],typeof(Header));if((previous.flags&2)!=0)waveOutUnprepareHeader(handle,headers[i],(uint)Marshal.SizeOf(typeof(Header)));
            int count=(int)Math.Min(blockSize,remaining);var bytes=new byte[count];int read=0;while(read<count){int n=stream.Read(bytes,read,count-read);if(n==0)throw new EndOfStreamException();read+=n;}
            Marshal.Copy(bytes,0,memories[i],count);Marshal.StructureToPtr(new Header{data=memories[i],length=(uint)count},headers[i],false);
            if(waveOutPrepareHeader(handle,headers[i],(uint)Marshal.SizeOf(typeof(Header)))!=0 || waveOutWrite(handle,headers[i],(uint)Marshal.SizeOf(typeof(Header)))!=0)throw new InvalidOperationException("音频播放失败。");remaining-=count;
        }
        void Pump(object ignored) {lock(sync){if(handle==IntPtr.Zero)return;try{for(int i=0;i<3;i++)if(remaining>0 && (((Header)Marshal.PtrToStructure(headers[i],typeof(Header))).flags&1)!=0)Fill(i);}catch{failed=true;clock.Stop();}}}
        public void Stop() {lock(sync){
            if(pump!=null){pump.Dispose();pump=null;}clock.Stop();if(handle!=IntPtr.Zero)waveOutReset(handle);
            for(int i=0;i<3;i++){if(headers[i]!=IntPtr.Zero){if(handle!=IntPtr.Zero)waveOutUnprepareHeader(handle,headers[i],(uint)Marshal.SizeOf(typeof(Header)));Marshal.FreeHGlobal(headers[i]);headers[i]=IntPtr.Zero;}if(memories[i]!=IntPtr.Zero){Marshal.FreeHGlobal(memories[i]);memories[i]=IntPtr.Zero;}}
            if(handle!=IntPtr.Zero){waveOutClose(handle);handle=IntPtr.Zero;}if(stream!=null){stream.Dispose();stream=null;}
        }}
        public void Dispose() { Stop(); }
    }
}
