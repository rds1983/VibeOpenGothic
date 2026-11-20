namespace VibeOpenGothic.Core.CommandLine;

public static class HelpText
{
	public const string Value = """
        VibeOpenGothic - Gothic II: Night of the Raven client

        Usage:
          VibeOpenGothic [options]

        Options:
          -g <path>          Gothic installation root (required for now)
          -game:<file.ini>   Mod manifest, GothicStarter compatible
          -w <world.zen>     Startup world
          -save <slot|q>     Save to load on startup
          -window            Run windowed
          -devmode           Enable developer logging and tools
          -g1                Assume a Gothic 1 installation
          -g2                Assume Gothic II: Night of the Raven
          -g2c               Assume Gothic II Classic
          --data-dir <path>  Settings, log and save directory
          --probe            Print installation diagnostics and exit
          --check-meshes     Convert every ZEN world mesh and report counts
          --check-textures   Decode a sample of TEX files and report dimensions
          --check-models     Bake every MDS model and report skin diagnostics
          --check-animations Load every MDS animation and pose it
          --check-world      Load and convert a ZEN world without rendering
          --screenshot <path> Render the test scene to a PNG and exit
          --help             Show this help
          --version          Show version information

        Exit codes:
          0  success
          1  invalid arguments or runtime error
          2  Gothic installation error

        Examples:
          VibeOpenGothic -g "C:\Gothic II"
          VibeOpenGothic -g "C:\Gothic II" -game:Karibik.ini
          VibeOpenGothic -g "C:\Gothic II" --probe
        """;
}
