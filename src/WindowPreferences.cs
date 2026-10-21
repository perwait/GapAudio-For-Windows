using System;
using System.Globalization;
using System.IO;

namespace GapGone {
    public sealed class WindowPreferences {
        readonly string path;
        public static string DefaultPath {get {return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GapAudio","window-width.txt");}}
        public WindowPreferences(string file) {path=file;}
        public int ReadWidth(int fallback,int minimum,int maximum) {
            int width=fallback;
            try {int saved;if(File.Exists(path) && Int32.TryParse(File.ReadAllText(path).Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out saved) && saved>0)width=saved;}
            catch(IOException){}catch(UnauthorizedAccessException){}
            return Math.Max(minimum,Math.Min(Math.Max(minimum,maximum),width));
        }
        public void SaveWidth(int width) {
            if(width<=0)return;
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllText(temporary,width.ToString(CultureInfo.InvariantCulture));
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
            }catch(IOException){}catch(UnauthorizedAccessException){}
            finally {try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
    }
}
