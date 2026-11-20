using VibeOpenGothic.Data.Ini;

namespace VibeOpenGothic.Data.Installs;

/// <summary>
/// Parsed GothicStarter-compatible modfile.ini.
/// </summary>
public sealed class ModManifest
{
	private static readonly char[] VdfSeparators = { ' ', '\t', ',', ';' };

	private ModManifest(
		string? manifestPath,
		string? gameDat,
		string? world,
		string? player,
		string? outputUnits,
		bool hasVdfFilter,
		IReadOnlyList<string> vdfFiles)
	{
		ManifestPath = manifestPath;
		GameDat = gameDat;
		World = world;
		Player = player;
		OutputUnits = outputUnits;
		HasVdfFilter = hasVdfFilter;
		VdfFiles = vdfFiles;
	}

	public static ModManifest Empty { get; } = new(
		null, null, null, null, null, false, Array.Empty<string>());

	public string? ManifestPath { get; }

	public bool HasManifest => ManifestPath is not null;

	public string? GameDat { get; }

	public string? World { get; }

	public string? Player { get; }

	public string? OutputUnits { get; }

	public bool HasVdfFilter { get; }

	public IReadOnlyList<string> VdfFiles { get; }

	public bool IncludesModArchive(string fileName)
	{
		// OpenGothic only mounts .mod files when a mod manifest is selected.
		// Without one, files like the shipped g2_classic.mod would replace NotR data.
		if (!HasManifest)
		{
			return false;
		}

		return !HasVdfFilter ||
			   VdfFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Loads a mod manifest. Relative names are resolved against the
	/// installation's system directory, matching GothicStarter behavior.
	/// </summary>
	public static ModManifest Load(string? requestedPath, string systemDirectory, out string? error)
	{
		error = null;

		if (string.IsNullOrWhiteSpace(requestedPath))
		{
			return Empty;
		}

		var fullPath = ResolveManifestPath(requestedPath, systemDirectory);
		if (fullPath is null)
		{
			error = $"Mod manifest not found: '{requestedPath}' (looked in '{systemDirectory}').";
			return Empty;
		}

		IniFile ini;
		try
		{
			ini = IniFile.Load(fullPath);
		}
		catch (Exception exception) when (
			exception is IOException or UnauthorizedAccessException)
		{
			error = $"Unable to read mod manifest '{fullPath}': {exception.Message}";
			return Empty;
		}

		var gameDat = ini.GetString("FILES", "GAME")?.Trim();
		if (!string.IsNullOrWhiteSpace(gameDat) &&
			!gameDat.EndsWith(".DAT", StringComparison.OrdinalIgnoreCase))
		{
			gameDat += ".DAT";
		}

		var world = ini.GetString("SETTINGS", "WORLD")?.Trim();
		if (!string.IsNullOrWhiteSpace(world))
		{
			var slash = world.LastIndexOfAny(new[] { '\\', '/' });
			if (slash >= 0)
			{
				world = world[(slash + 1)..];
			}
		}

		var vdfFiles = (ini.GetString("FILES", "VDF") ?? string.Empty)
			.Split(VdfSeparators, StringSplitOptions.RemoveEmptyEntries)
			.ToArray();

		return new ModManifest(
			fullPath,
			string.IsNullOrWhiteSpace(gameDat) ? null : gameDat,
			string.IsNullOrWhiteSpace(world) ? null : world,
			NullIfWhiteSpace(ini.GetString("SETTINGS", "PLAYER")),
			NullIfWhiteSpace(ini.GetString("FILES", "OUTPUTUNITS")),
			ini.HasKey("FILES", "VDF"),
			vdfFiles);
	}

	private static string? ResolveManifestPath(string requestedPath, string systemDirectory)
	{
		var direct = Path.IsPathRooted(requestedPath)
			? requestedPath
			: Path.Combine(systemDirectory, requestedPath);

		if (File.Exists(direct))
		{
			return Path.GetFullPath(direct);
		}

		if (!Path.IsPathRooted(requestedPath) && File.Exists(requestedPath))
		{
			return Path.GetFullPath(requestedPath);
		}

		var resolved = PathResolver.ResolveExisting(systemDirectory, requestedPath);
		return resolved is not null && File.Exists(resolved)
			? Path.GetFullPath(resolved)
			: null;
	}

	private static string? NullIfWhiteSpace(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}
}
