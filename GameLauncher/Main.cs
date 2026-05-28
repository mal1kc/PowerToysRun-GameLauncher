using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.GameLauncher
{
    public class Main : IPlugin, IPluginI18n, IDisposable
    {
        public static string PluginID => "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
        public string Name => "GameLauncher";
        public string Description => "Launch Steam, Epic Games, and Xbox games";

        private PluginInitContext? _context;
        private string? _iconPathDark;
        private string? _iconPathLight;
        private bool _disposed;

        private List<GameEntry> _games = new();

        // ── Paths ──────────────────────────────────────────────────────────────
        private static readonly string SteamPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        private static readonly string EpicManifestPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                         "Epic", "EpicGamesLauncher", "Data", "Manifests");
        private static readonly string XboxGamingRoot =
            @"C:\XboxGames";

        // ──────────────────────────────────────────────────────────────────────
        public void Init(PluginInitContext context)
        {
            Log.Info("Initializing GameLauncher plugin", GetType());
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _iconPathDark  = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "Images", "gamelauncher.dark.png");
            _iconPathLight = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "Images", "gamelauncher.light.png");
            LoadGames();
        }

        // ── Query ──────────────────────────────────────────────────────────────
        public List<Result> Query(Query query)
        {
            Log.Info("Query: " + query.Search, GetType());
            var search = query.Search.Trim();

            var results = _games
                .Where(g => g.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(g => g.Name.StartsWith(search, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(g => g.Name)
                .Select(g => new Result
                {
                    Title       = g.Name,
                    SubTitle    = $"{g.Platform}  —  {g.LaunchUri}",
                    IcoPath     = GetIcon(g.Platform),
                    Action      = _ =>
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName        = g.LaunchUri,
                                UseShellExecute = true,
                            });
                            return true;
                        }
                        catch (Exception ex)
                        {
                            _context?.API.ShowMsg("GameLauncher", $"Failed to launch: {ex.Message}");
                            return false;
                        }
                    },
                    ContextData = g,
                })
                .ToList();

            if (!results.Any() && !string.IsNullOrWhiteSpace(search))
            {
                results.Add(new Result
                {
                    Title    = "No games found",
                    SubTitle = $"No match for \"{search}\"",
                    IcoPath  = _iconPathDark,
                    Action   = _ => false,
                });
            }

            return results;
        }

        // ── Game loading ───────────────────────────────────────────────────────
        private void LoadGames()
        {
            Log.Info("Loading games...", GetType());
            _games.Clear();
            _games.AddRange(LoadSteamGames());
            _games.AddRange(LoadEpicGames());
            _games.AddRange(LoadXboxGames());
        }

        // Steam: parse libraryfolders.vdf + appmanifest_*.acf
        private IEnumerable<GameEntry> LoadSteamGames()
        {
            Log.Info("Loading Steam games...", GetType());
            var games = new List<GameEntry>();
            try
            {
                var libraryFolders = new List<string>();

                // Default library
                var defaultLib = Path.Combine(SteamPath, "steamapps");
                if (Directory.Exists(defaultLib))
                    libraryFolders.Add(defaultLib);

                // Extra libraries from libraryfolders.vdf
                var vdf = Path.Combine(defaultLib, "libraryfolders.vdf");
                if (File.Exists(vdf))
                {
                    foreach (var line in File.ReadAllLines(vdf))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = trimmed.Split('"');
                            if (parts.Length >= 4)
                            {
                                var extraPath = Path.Combine(parts[3].Replace("\\\\", "\\"), "steamapps");
                                if (Directory.Exists(extraPath) && !libraryFolders.Contains(extraPath))
                                    libraryFolders.Add(extraPath);
                            }
                        }
                    }
                }

                foreach (var lib in libraryFolders)
                {
                    foreach (var acf in Directory.GetFiles(lib, "appmanifest_*.acf"))
                    {
                        try
                        {
                            var appId = Path.GetFileNameWithoutExtension(acf).Replace("appmanifest_", "");
                            var name  = ParseAcfValue(File.ReadAllText(acf), "name");
                            if (!string.IsNullOrEmpty(name))
                            {
                                games.Add(new GameEntry(name, "Steam", $"steam://rungameid/{appId}"));
                            }
                        }
                        catch { /* skip bad manifests */ }
                    }
                }
            }
            catch { /* Steam not installed or unavailable */ }
            return games;
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

        // Epic: parse *.item JSON manifests
        private IEnumerable<GameEntry> LoadEpicGames()
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
                        var json    = File.ReadAllText(item);
                        using var doc = JsonDocument.Parse(json);
                        var root  = doc.RootElement;

                        if (!root.TryGetProperty("bIsIncompleteInstall", out var incomplete) ||
                            incomplete.GetBoolean())
                            continue;

                        var name       = root.TryGetProperty("DisplayName",    out var dn) ? dn.GetString() : null;
                        var catalogId  = root.TryGetProperty("CatalogItemId",  out var ci) ? ci.GetString() : null;
                        var ns         = root.TryGetProperty("CatalogNamespace", out var cns) ? cns.GetString() : null;
                        var appName    = root.TryGetProperty("AppName",        out var an) ? an.GetString() : null;

                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(appName)) continue;

                        // com.epicgames.launcher://apps/<Namespace>%3A<CatalogItemId>%3A<AppName>?action=launch
                        var uri = string.IsNullOrEmpty(ns) || string.IsNullOrEmpty(catalogId)
                            ? $"com.epicgames.launcher://apps/{appName}?action=launch"
                            : $"com.epicgames.launcher://apps/{Uri.EscapeDataString(ns)}%3A{Uri.EscapeDataString(catalogId)}%3A{Uri.EscapeDataString(appName)}?action=launch";

                        games.Add(new GameEntry(name, "Epic", uri));
                    }
                    catch { /* skip bad items */ }
                }
            }
            catch { }
            return games;
        }

        // Xbox/MS Store: scan XboxGames folder for gaming roots
        private IEnumerable<GameEntry> LoadXboxGames()
        {
            Log.Info("Loading Xbox games...", GetType());
            var games = new List<GameEntry>();

            // Check all drives for XboxGames folder
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => Path.Combine(d.RootDirectory.FullName, "XboxGames"))
                .Concat(new[] { XboxGamingRoot })
                .Distinct();

            foreach (var root in drives)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    foreach (var gameDir in Directory.GetDirectories(root))
                    {
                        try
                        {
                            // Look for MicrosoftGame.config or appxmanifest
                            var configFile = Directory.GetFiles(gameDir, "MicrosoftGame.config", SearchOption.AllDirectories)
                                             .FirstOrDefault()
                                          ?? Directory.GetFiles(gameDir, "appxmanifest.xml", SearchOption.AllDirectories)
                                             .FirstOrDefault();

                            if (configFile == null) continue;

                            var content = File.ReadAllText(configFile);
                            var name    = ExtractXmlValue(content, "ShellVisuals", "DisplayName")
                                       ?? ExtractXmlValue(content, "uap:VisualElements", "DisplayName")
                                       ?? Path.GetFileName(gameDir);

                            var identity = ExtractXmlValue(content, "Identity", "Name")
                                        ?? ExtractXmlValue(content, "mspc:ShellVisuals", "StoreLogo");

                            // Use ms-xbl-<titleId> if we can find it, else use xbox URI with game folder name
                            var titleId = ExtractXmlValue(content, "ExecutableList", "Executable");
                            var uri     = !string.IsNullOrEmpty(identity)
                                ? $"xbox://game-activity/launch/{Uri.EscapeDataString(identity)}"
                                : $"shell:AppsFolder\\{Uri.EscapeDataString(Path.GetFileName(gameDir))}";

                            if (!string.IsNullOrEmpty(name))
                                games.Add(new GameEntry(name, "Xbox", uri));
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return games;
        }

        private static string? ExtractXmlValue(string xml, string element, string attribute)
        {
            try
            {
                var tag = $"<{element}";
                var idx = xml.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return null;
                var attrNeedle = $"{attribute}=\"";
                var aIdx = xml.IndexOf(attrNeedle, idx, StringComparison.OrdinalIgnoreCase);
                if (aIdx < 0) return null;
                aIdx += attrNeedle.Length;
                var end = xml.IndexOf('"', aIdx);
                return end < 0 ? null : xml.Substring(aIdx, end - aIdx);
            }
            catch { return null; }
        }

        private string GetIcon(string platform) => platform switch
        {
            "Steam" => _iconPathDark ?? string.Empty,
            "Epic"  => _iconPathDark ?? string.Empty,
            "Xbox"  => _iconPathDark ?? string.Empty,
            _       => _iconPathDark ?? string.Empty,
        };

        // ── IPluginI18n ────────────────────────────────────────────────────────
        public string GetTranslatedPluginTitle()       => Name;
        public string GetTranslatedPluginDescription() => Description;

        // ── IDisposable ────────────────────────────────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public record GameEntry(string Name, string Platform, string LaunchUri);
}
