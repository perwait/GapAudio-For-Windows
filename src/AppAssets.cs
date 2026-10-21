using System.Drawing;
using System.IO;
using System.Resources;

namespace GapGone {
    public static class AppAssets {
        static readonly ResourceManager resources=new ResourceManager("GapGone.AppAssets",typeof(AppAssets).Assembly);
        public static Image Logo() {using(var stream=new MemoryStream((byte[])resources.GetObject("Logo")))using(var image=Image.FromStream(stream))return new Bitmap(image);}
        public static Icon Icon() {using(var stream=new MemoryStream((byte[])resources.GetObject("Icon")))using(var icon=new Icon(stream))return (Icon)icon.Clone();}
    }
}
