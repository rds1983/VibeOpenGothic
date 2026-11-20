using VibeOpenGothic.Core.CommandLine;
using VibeOpenGothic.Core.Settings;
using VibeOpenGothic.Data;

namespace VibeOpenGothic.App;

public sealed record GothicGameOptions(
    GothicInstall Install,
    AppSettings Settings,
    CommandLineOptions CommandLine,
    bool Windowed);
