using VibeOpenGothic.Core.CommandLine;
using VibeOpenGothic.Core.Settings;
using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Data.Vfs;

namespace VibeOpenGothic.App;

public sealed record GothicGameOptions(
    GothicInstall Install,
    AppSettings Settings,
    CommandLineOptions CommandLine,
    bool Windowed,
    IGameVfs Vfs);
