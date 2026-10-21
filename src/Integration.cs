using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GapGone {
    public static class ShellIntegration {
        static readonly string[] extensions = MediaFormats.Extensions;
        public static bool Enabled { get { using (var key = Registry.CurrentUser.OpenSubKey("Software\\Classes\\SystemFileAssociations\\.wav\\shell\\GapGone.Edit")) return key != null; } }
        [DllImport("shell32.dll")] static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
        // Remove the retired information/assessment entry when upgrading an existing installation.
        public static void RemoveLegacyInfoMenu() {
            foreach (string ext in extensions) Registry.CurrentUser.DeleteSubKeyTree(MenuPath(ext, "Info"), false);
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }
        static string MenuPath(string extension, string mode) { return "Software\\Classes\\SystemFileAssociations\\" + extension + "\\shell\\GapGone." + mode; }
        public static void Set(bool enabled, string executable) {
            RemoveLegacyInfoMenu();
            foreach (string ext in extensions) foreach (string mode in new[] { "Quick", "Edit" }) {
                string path = MenuPath(ext, mode);
                if (!enabled) { Registry.CurrentUser.DeleteSubKeyTree(path, false); continue; }
                using (var key = Registry.CurrentUser.CreateSubKey(path)) {
                    key.SetValue("", mode == "Quick" ? "Gap Audio：快速转换为 MP3" : "Gap Audio：音频编辑");
                    key.SetValue("Icon", "\"" + executable + "\""); key.SetValue("MultiSelectModel", "Single");
                    using (var command = key.CreateSubKey("command")) command.SetValue("", "\"" + executable + "\" --" + mode.ToLowerInvariant() + " \"%1\"");
                }
            }
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
