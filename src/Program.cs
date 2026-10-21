using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GapGone {
    static class Program {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread] static void Main(string[] args) {
            SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            if (!File.Exists(Path.Combine(directory, "ffmpeg.exe")) || !File.Exists(Path.Combine(directory, "ffprobe.exe"))) { MessageBox.Show("未找到 FFmpeg / FFprobe。请把它们放在 GapAudio.exe 同目录，或运行 build.ps1。", "Gap Audio"); return; }
            Application.ThreadException += (s, e) => MessageBox.Show(e.Exception.Message, "Gap Audio");
            try { ShellIntegration.RemoveLegacyInfoMenu(); }
            catch (UnauthorizedAccessException) { }
            catch (System.Security.SecurityException) { }
            catch (IOException) { }
            Application.Run(new MainForm(new AudioEngine(directory), args));
        }
    }
}
