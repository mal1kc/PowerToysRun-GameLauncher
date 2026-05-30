using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Wox.Plugin;
using Wox.Plugin.Logger;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Community.PowerToys.Run.Plugin.GameLauncher
{
    public class Main : IPlugin, IPluginI18n, IDisposable
    {
        public static string PluginID => "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
        public string Name => "GameLauncher";
        public string Description => "Launch Steam, Epic Games, and Xbox games";

        private IGamePlatform[] _platforms = [];
        private PluginInitContext? _context;
        private string? _iconPathDark;
        private string? _iconPathLight;
        private bool _disposed;

        private List<GameEntry> _games = new();

        // ── Paths ──────────────────────────────────────────────────────────────
        // Platform-specific paths moved to platform implementations

        // ──────────────────────────────────────────────────────────────────────
        public void Init(PluginInitContext context)
        {
            Log.Info("Initializing GameLauncher plugin", GetType());
            _context = context ?? throw new ArgumentNullException(nameof(context));
            // provide plugin directory to platforms that need it
            PlatformSteam.PluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
            PlatformEpic.PluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
            PlatformXbox.PluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
            _iconPathDark = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "Images", "gamelauncher.dark.png");
            _iconPathLight = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "Images", "gamelauncher.light.png");
            // ensure simple launcher icons exist (32x32 PNGs)
            try { EnsureLauncherIcons(context.CurrentPluginMetadata.PluginDirectory); } catch { }
            LoadGames();
        }

        // ── Query ──────────────────────────────────────────────────────────────
        public List<Result> Query(Query query)
        {
            var search = query.Search.Trim();

            var results = _games
                .Where(g => g.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(g => g.Name.StartsWith(search, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(g => g.Name)
                .Select(g => new Result
                {
                    Title = g.Name,
                    SubTitle = $"{g.Platform}  —  {g.LaunchUri}",
                    IcoPath = GetIcon(g),
                    Action = _ =>
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = g.LaunchUri,
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
                    Title = "No games found",
                    SubTitle = $"No match for \"{search}\"",
                    IcoPath = _iconPathDark,
                    Action = _ => false,
                });
            }

            return results;
        }

        // ── Game loading ───────────────────────────────────────────────────────
        private void LoadGames()
        {
            Log.Info("Loading games...", GetType());
            _games.Clear();
            _platforms = new IGamePlatform[]
            {
                new PlatformSteam(),
                new PlatformEpic(),
                new PlatformXbox()
            };

            foreach (var platform in _platforms)
            {
                var platformIconFile = platform.GetPlatformIcon() ?? string.Empty;
                var platformIconPath = _iconPathDark ?? string.Empty;
                if (!string.IsNullOrEmpty(platformIconFile))
                {
                    // If platform returned an absolute path to an icon, prefer it
                    try
                    {
                        if (Path.IsPathRooted(platformIconFile) && File.Exists(platformIconFile))
                        {
                            platformIconPath = platformIconFile;
                        }
                        else if (_context != null)
                        {
                            var candidate = Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "Images", platformIconFile);
                            if (File.Exists(candidate))
                                platformIconPath = candidate;
                            else if (File.Exists(platformIconFile))
                                platformIconPath = platformIconFile;
                        }
                    }
                    catch { }
                }

                var platformGames = platform.LoadGames()
                    .Select(g => string.IsNullOrEmpty(g.Icon) ? g with { Icon = platformIconPath } : g)
                    .ToList();

                _games.AddRange(platformGames);
            }
        }

      

        private string GetIcon(GameEntry game)
        {
            Log.Debug($"Getting icon for {game.Name} on {game.Platform}, possible icon: {game.Icon}", GetType());
            if (!string.IsNullOrEmpty(game.Icon))
                return game.Icon;
            IGamePlatform? platform = game.Platform switch
            {
                "Steam" => _platforms.OfType<PlatformSteam>().FirstOrDefault(),
                "Epic" => _platforms.OfType<PlatformEpic>().FirstOrDefault(),
                "Xbox" => _platforms.OfType<PlatformXbox>().FirstOrDefault(),
                _ => null,
            };

            var platformDefault = platform?.GetPlatformIcon() ?? string.Empty;
            if (_context != null)
            {
                var candidate = Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "Images", $"{game.Platform.ToLower()}.png");
                if (File.Exists(candidate))
                    platformDefault = candidate;
            }
            return platformDefault;
        }

        private void EnsureLauncherIcons(string pluginDir)
        {
            try
            {
                var imagesDir = Path.Combine(pluginDir, "Images");
                Directory.CreateDirectory(imagesDir);
                var dark = Path.Combine(imagesDir, "gamelauncher.dark.png");
                var light = Path.Combine(imagesDir, "gamelauncher.light.png");
                CreateLauncherIcon(dark, isLight: false);
                CreateLauncherIcon(light, isLight: true);
            }
            catch { }
        }

        private void CreateLauncherIcon(string path, bool isLight)
        {
            try
            {
                const int size = 32;
                const int dpi = 96;
                var rtb = new RenderTargetBitmap(size, size, dpi, dpi, PixelFormats.Pbgra32);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    var color = isLight ? Colors.Black : Colors.White;
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();

                    // draw dash (rounded rectangle)
                    double dashW = 18, dashH = 4;
                    double rx = (size - dashW) / 2.0;
                    double ry = (size - dashH) / 2.0;
                    dc.DrawRoundedRectangle(brush, null, new Rect(rx, ry, dashW, dashH), 1, 1);

                    // draw slash line
                    var pen = new Pen(brush, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                    pen.Freeze();
                    dc.DrawLine(pen, new Point(10, 6), new Point(22, 26));
                }
                rtb.Render(dv);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
                encoder.Save(fs);
            }
            catch { }
        }

// ── IPluginI18n ────────────────────────────────────────────────────────
public string GetTranslatedPluginTitle() => Name;
        public string GetTranslatedPluginDescription() => Description;

        // ── IDisposable ────────────────────────────────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

  
}
