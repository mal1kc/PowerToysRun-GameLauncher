using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.GameLauncher
{
    public class PlatformEpic : IGamePlatform
    {
        private static readonly string EpicManifestPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                         "Epic", "EpicGamesLauncher", "Data", "Manifests");
        public static string PluginDirectory { get; set; } = string.Empty;

        public IEnumerable<GameEntry> LoadGames()
        {
            Log.Info("Loading Epic games...", GetType());
            var games = new List<GameEntry>();
            if (!Directory.Exists(EpicManifestPath)) return games;
            try
            {
                foreach (var item in Directory.GetFiles(EpicManifestPath, "*.item"))
                {
                    try
                    {
                        var json = File.ReadAllText(item);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (!root.TryGetProperty("bIsIncompleteInstall", out var incomplete) ||
                            incomplete.GetBoolean())
                            continue;

                        var name = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() : null;
                        var catalogId = root.TryGetProperty("CatalogItemId", out var ci) ? ci.GetString() : null;
                        var ns = root.TryGetProperty("CatalogNamespace", out var cns) ? cns.GetString() : null;
                        var appName = root.TryGetProperty("AppName", out var an) ? an.GetString() : null;
                        var launchExecutable = root.TryGetProperty("LaunchExecutable", out var le) ? le.GetString() : null;

                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(appName)) continue;

                        var uri = string.IsNullOrEmpty(ns) || string.IsNullOrEmpty(catalogId)
                            ? $"com.epicgames.launcher://apps/{appName}?action=launch"
                            : $"com.epicgames.launcher://apps/{Uri.EscapeDataString(ns)}%3A{Uri.EscapeDataString(catalogId)}%3A{Uri.EscapeDataString(appName)}?action=launch";

                        var icon = string.Empty;
                        if (root.TryGetProperty("DisplayIcon", out var di) && di.ValueKind == JsonValueKind.String)
                            icon = di.GetString() ?? string.Empty;
                        if (string.IsNullOrEmpty(icon) && !string.IsNullOrEmpty(launchExecutable))
                            icon = IconExtractor.Extract(launchExecutable, "epic");

                        games.Add(new GameEntry(name, "Epic", uri, icon));
                    }
                    catch { /* skip bad items */ }
                }
            }
            catch { }
            return games;
        }

        public string GetPlatformIcon()
        {
            var platformIconFile = "epic.png";
            var pluginDir = !string.IsNullOrEmpty(PluginDirectory)
                ? PluginDirectory
                : Path.GetDirectoryName(typeof(PlatformEpic).Assembly.Location) ?? string.Empty;
            var imagesDir = Path.Combine(pluginDir, "Images");
            var candidate = Path.Combine(imagesDir, platformIconFile);
            if (File.Exists(candidate))
                return candidate;

            var exeCandidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Epic Games", "Launcher", "Portal", "Binaries", "Win64", "EpicGamesLauncher.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Epic Games", "Launcher", "Portal", "Binaries", "Win32", "EpicGamesLauncher.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Epic Games", "Launcher", "Portal", "Binaries", "Win64", "EpicGamesLauncher.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Epic Games", "Launcher", "Portal", "Binaries", "Win32", "EpicGamesLauncher.exe")
            };

            foreach (var ex in exeCandidates)
            {
                try
                {
                    if (!string.IsNullOrEmpty(ex) && File.Exists(ex))
                    {
                        var extracted = IconExtractor.Extract(ex, "epic");
                        if (!string.IsNullOrEmpty(extracted))
                            return extracted;
                    }
                }
                catch { }
            }

            // try a broader search under ProgramFiles roots
            var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) };
            foreach (var root in roots)
            {
                try
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (var f in Directory.EnumerateFiles(root, "EpicGamesLauncher.exe", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var extracted = IconExtractor.Extract(f, "epic");
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
