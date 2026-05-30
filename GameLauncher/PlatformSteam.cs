using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Drawing;
using Wox.Plugin;
using Wox.Plugin.Logger;

// Steam: parse libraryfolders.vdf + appmanifest_*.acf


namespace Community.PowerToys.Run.Plugin.GameLauncher
{
    public class PlatformSteam : IGamePlatform
    {
        private static readonly string SteamPath = GetSteamPath();

        static string PlatformName => "Steam";
        public static string PluginDirectory { get; set; } = string.Empty;

        private readonly string librarycachePath = Path.Combine(SteamPath, "appcache", "librarycache");

        private static string GetSteamPath()
        {
            try
            {
                var candidates = new[]
                {
                    ReadRegistryPath(RegistryHive.CurrentUser, "Software\\Valve\\Steam", "SteamPath"),
                    ReadRegistryPath(RegistryHive.CurrentUser, "Software\\Valve\\Steam", "InstallPath"),
                    ReadRegistryPath(RegistryHive.LocalMachine, "SOFTWARE\\WOW6432Node\\Valve\\Steam", "InstallPath"),
                    ReadRegistryPath(RegistryHive.LocalMachine, "SOFTWARE\\Valve\\Steam", "InstallPath")
                };
                foreach (var c in candidates)
                {
                    if (!string.IsNullOrEmpty(c) && Directory.Exists(c))
                        return c;
                }
            }
            catch { }

            var pf86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            var pf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam");
            if (Directory.Exists(pf86)) return pf86;
            if (Directory.Exists(pf)) return pf;
            return pf86;
        }

        private static string ReadRegistryPath(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    try
                    {
                        using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
                        using (var key = baseKey.OpenSubKey(subKey))
                        {
                            if (key == null) continue;
                            var val = key.GetValue(valueName) as string;
                            if (!string.IsNullOrEmpty(val)) return val;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return string.Empty;
        }

        string IGamePlatform.GetPlatformIcon()
        {
            var platformIconFile = "steam.png";
            var pluginDir = !string.IsNullOrEmpty(PluginDirectory)
                ? PluginDirectory
                : Path.GetDirectoryName(typeof(PlatformSteam).Assembly.Location) ?? string.Empty;
            var imagesDir = Path.Combine(pluginDir, "Images");
            var platformIconPath = Path.Combine(imagesDir, platformIconFile);
            if (File.Exists(platformIconPath))
                return platformIconPath;

            var steamExe = Path.Combine(SteamPath, "steam.exe");
            if (File.Exists(steamExe))
            {
                try
                {
                    var extracted = IconExtractor.Extract(steamExe, "steam");
                    if (!string.IsNullOrEmpty(extracted))
                        return extracted;
                }
                catch { }
            }
            return string.Empty;
        }

        private static string ParseAcfValue(string text, string key)
        {
            var needle = $"\"{key}\"";
            var idx = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return string.Empty;
            idx += needle.Length;
            var start = text.IndexOf('"', idx);
            if (start < 0) return string.Empty;
            var end = text.IndexOf('"', start + 1);
            return end < 0 ? string.Empty : text.Substring(start + 1, end - start - 1);
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
            }
            catch { return path; }
        }

        private string LoadSteamAppIcon(string appId)
        {
            try
            {
                var iconPath = string.Empty;
                // acf files are not have icons
                // vdf has it but it is not a standard format and hard to parse robustly also don't need to use 3rd party library for just one value, \
                //     so instead of parsing it, just look for the icon directly via librarycache which is where steam stores icons for each appid.
                // The path is typically %steampath%\appcache\librarycache\appid\{somehash}.jpg.



                // best bet is to look for cached icons in librarycache (named by appid)
                // for example  appid 12345 would be at $librarycache\12345\{somehash-of-40-char-lenght}.jpg
                // but need to be careful becaause there was another images on that folder with very diffrent aspect ratio (bg image etc.)

                var cacheDir = Path.Combine(librarycachePath, appId);
                Log.Info($"Looking for Steam icon in {cacheDir}", typeof(PlatformSteam));

                if (Directory.Exists(cacheDir))
                {
                    // filter for files with 40 char hash names which are the icons, other files have different naming pattern
                    var jpg = Directory.EnumerateFiles(cacheDir, "*.jpg", SearchOption.TopDirectoryOnly)
                        .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Length == 40); 
                    Log.Info($"Looking for Steam icon in {cacheDir}, found: {iconPath}", typeof(PlatformSteam));
                    if (!string.IsNullOrEmpty(jpg) && File.Exists(jpg))
                        iconPath = jpg;
                }
                return iconPath;
            }
            catch { return string.Empty; }
        }


        private IEnumerable<GameEntry> LoadGames()
        {
            Log.Info("Loading Steam games...", GetType());
            var games = new List<GameEntry>();
            try
            {
                var libraryFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Default library (normalize before adding to avoid duplicates)
                var defaultLib = Path.Combine(SteamPath, "steamapps");
                Log.Info($"Default Steam library: {defaultLib}", GetType());
                if (Directory.Exists(defaultLib))
                    libraryFolders.Add(NormalizePath(defaultLib));

                // Extra libraries from libraryfolders.vdf (robustly extract quoted path tokens)
                var vdf = Path.Combine(defaultLib, "libraryfolders.vdf");
                if (File.Exists(vdf))
                {
                    try
                    {
                        var vdfText = File.ReadAllLines(vdf);
                        foreach (var line in vdfText)
                        {
                            var trimmed = line.Trim();

                            if (trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = trimmed.Split('"');
                                if (parts.Length >= 4)
                                {
                                    var extraPath = Path.Combine(parts[3].Replace("\\\\", "\\"), "steamapps");
                                    if (Directory.Exists(extraPath))
                                    {
                                        var extraNorm = NormalizePath(extraPath);
                                        Log.Info($"Adding Steam library from vdf: {extraPath}", GetType());
                                        libraryFolders.Add(extraNorm);
                                    }
                                }
                            }
                        }
                    }
                    catch { /* ignore malformed vdf */ }
                }

                var seenAppIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var lib in libraryFolders)
                {
                    try
                    {
                        foreach (var acf in Directory.EnumerateFiles(lib, "appmanifest_*.acf"))
                        {
                            try
                            {
                                var appId = Path.GetFileNameWithoutExtension(acf).Replace("appmanifest_", "");
                                if (string.IsNullOrEmpty(appId) || seenAppIds.Contains(appId))
                                    continue;

                                seenAppIds.Add(appId);

                                var acfText = File.ReadAllText(acf);
                                var name = ParseAcfValue(acfText, "name");
                                if (string.IsNullOrEmpty(name)) continue;

                                // extract photo
                                var icon = LoadSteamAppIcon(appId);
                                // if icon is null or empty, fallback to platform icon
                                if (string.IsNullOrEmpty(icon))
                                    icon = ((IGamePlatform)this).GetPlatformIcon();

                                games.Add(new GameEntry(name, "Steam", $"steam://rungameid/{appId}", icon));
                            }
                            catch { /* skip bad manifests */ }
                        }
                    }
                    catch { /* skip broken library path */ }
                }
            }
            catch { /* Steam not installed or unavailable */ }
            return games;
        }

        IEnumerable<GameEntry> IGamePlatform.LoadGames()
        {
            return LoadGames();
        }
    }

}