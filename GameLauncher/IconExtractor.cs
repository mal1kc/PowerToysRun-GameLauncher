using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.GameLauncher
{
    internal static class IconExtractor
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_LARGEICON = 0x000000000;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static string Extract(string filePath, string prefix)
        {
            try
            {
                Log.Debug($"Extracting icon from {filePath}", typeof(IconExtractor));
                if (!File.Exists(filePath)) return string.Empty;
                var rc = SHGetFileInfo(filePath, 0, out SHFILEINFO shinfo, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
                if (shinfo.hIcon != IntPtr.Zero)
                {
                    Log.Debug($"Icon handle obtained for {filePath}, saving to temp file", typeof(IconExtractor));
                    var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                    var tempPath = Path.Combine(Path.GetTempPath(), $"{prefix}_icon_{Guid.NewGuid()}.png");
                    using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                    {
                        encoder.Save(stream);
                    }
                    DestroyIcon(shinfo.hIcon);
                    return tempPath;
                }
            }
            catch { }
            Log.Info($"Failed to extract icon from {filePath}", typeof(IconExtractor));
            return string.Empty;
        }
    }
}
