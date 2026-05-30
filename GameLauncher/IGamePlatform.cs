using System.Collections.Generic;

namespace Community.PowerToys.Run.Plugin.GameLauncher
{
    public interface IGamePlatform
    {
        static string? PlatformName { get; }
        static string? PluginDirectory { get; set; } // set by plugin on init
        IEnumerable<GameEntry> LoadGames();
        string GetPlatformIcon();
    }
}