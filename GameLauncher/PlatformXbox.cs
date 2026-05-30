using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.GameLauncher
{
    public class PlatformXbox : IGamePlatform
    {
        private static readonly string XboxGamingRoot = @"C:\XboxGames";
        public static string PluginDirectory { get; set; } = string.Empty;

        public IEnumerable<GameEntry> LoadGames()
        {
            Log.Info("Loading Xbox games...", GetType());
            var games = new List<GameEntry>();

            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => Path.Combine(d.RootDirectory.FullName, "XboxGames"))
                .Concat(new[] { XboxGamingRoot })
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var root in drives)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    foreach (var gameDir in Directory.EnumerateDirectories(root))
                    {
                        try
                        {
                            var name = Path.GetFileName(gameDir);
                            var identity = string.Empty;
                            var icon = string.Empty;
                            var uri = $"shell:AppsFolder\\{Uri.EscapeDataString(name)}";

                            // look for appxmanifest.xml under the game folder
                            var manifest = Directory.EnumerateFiles(gameDir, "appxmanifest.xml", SearchOption.AllDirectories).FirstOrDefault();
                            if (!string.IsNullOrEmpty(manifest))
                            {
                                try
                                {
                                    var doc = XDocument.Load(manifest);
                                    var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                                    var uap = doc.Root?.GetNamespaceOfPrefix("uap") ?? XNamespace.None;

                                    var displayName = doc.Descendants(ns + "DisplayName").FirstOrDefault()?.Value
                                                      ?? doc.Descendants(uap + "VisualElements").Attributes("DisplayName").FirstOrDefault()?.Value;
                                    if (!string.IsNullOrEmpty(displayName)) name = displayName;

                                    var id = doc.Descendants(ns + "Identity").FirstOrDefault()?.Attribute("Name")?.Value;
                                    if (!string.IsNullOrEmpty(id))
                                        identity = id;

                                    if (!string.IsNullOrEmpty(identity))
                                        uri = $"xbox://game-activity/launch/{Uri.EscapeDataString(identity)}";

                                    // prefer uap logos then <Logo>
                                    var visual = doc.Descendants(uap + "VisualElements").FirstOrDefault();
                                    var logo = visual?.Attribute("Square150x150Logo")?.Value
                                               ?? visual?.Attribute("Square44x44Logo")?.Value
                                               ?? doc.Descendants(ns + "Logo").FirstOrDefault()?.Value
                                               ?? doc.Descendants(ns + "StoreLogo").FirstOrDefault()?.Value;

                                    if (!string.IsNullOrEmpty(logo))
                                    {
                                        var logoRel = logo.Replace('\\', Path.DirectorySeparatorChar).TrimStart('\\', '/');
                                        var candidate = Path.Combine(Path.GetDirectoryName(manifest) ?? gameDir, logoRel);
                                        if (File.Exists(candidate)) icon = candidate;
                                        else
                                        {
                                            candidate = Path.Combine(gameDir, logoRel);
                                            if (File.Exists(candidate)) icon = candidate;
                                            else
                                            {
                                                var fileName = Path.GetFileName(logoRel);
                                                icon = Directory.EnumerateFiles(gameDir, fileName, SearchOption.AllDirectories).FirstOrDefault() ?? string.Empty;
                                            }
                                        }
                                    }

                                    if (string.IsNullOrEmpty(icon))
                                    {
                                        var app = doc.Descendants(ns + "Application").FirstOrDefault();
                                        var exeAttr = app?.Attribute("Executable")?.Value;
                                        if (!string.IsNullOrEmpty(exeAttr))
                                        {
                                            var exe = Path.Combine(Path.GetDirectoryName(manifest) ?? gameDir, exeAttr);
                                            if (!File.Exists(exe))
                                            {
                                                exe = Directory.EnumerateFiles(gameDir, Path.GetFileName(exeAttr), SearchOption.AllDirectories).FirstOrDefault() ?? string.Empty;
                                            }
                                            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                                            {
                                                var extracted = IconExtractor.Extract(exe, "xbox");
                                                if (!string.IsNullOrEmpty(extracted)) icon = extracted;
                                            }
                                        }
                                    }
                                }
                                catch { }
                            }
                            else
                            {
                                // fallback: look for common logo files
                                icon = Directory.EnumerateFiles(gameDir, "*StoreLogo*.png", SearchOption.AllDirectories).FirstOrDefault()
                                        ?? Directory.EnumerateFiles(gameDir, "*Logo*.png", SearchOption.AllDirectories).FirstOrDefault()
                                        ?? string.Empty;
                            }

                            games.Add(new GameEntry(name, "Xbox", uri, icon ?? string.Empty));
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return games;
        }

        

        public string GetPlatformIcon()
        {
            var platformIconFile = "xbox.png";
            var pluginDir = !string.IsNullOrEmpty(PluginDirectory)
                ? PluginDirectory
                : Path.GetDirectoryName(typeof(PlatformXbox).Assembly.Location) ?? string.Empty;
            var imagesDir = Path.Combine(pluginDir, "Images");
            var candidate = Path.Combine(imagesDir, platformIconFile);
            if (File.Exists(candidate))
                return candidate;

            var exeCandidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Xbox", "XboxApp.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Xbox", "XboxApp.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps", "XboxApp.exe")
            };

            foreach (var ex in exeCandidates)
            {
                try
                {
                    if (!string.IsNullOrEmpty(ex) && File.Exists(ex))
                    {
                        var extracted = IconExtractor.Extract(ex, "xbox");
                        if (!string.IsNullOrEmpty(extracted))
                            return extracted;
                    }
                }
                catch { }
            }

            // best effort search under ProgramFiles roots (may be slow or inaccessible)
            var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) };
            foreach (var root in roots)
            {
                try
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (var f in Directory.EnumerateFiles(root, "XboxApp.exe", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var extracted = IconExtractor.Extract(f, "xbox");
                            if (!string.IsNullOrEmpty(extracted))
                                return extracted;
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return string.Empty;
        }
    }
}
