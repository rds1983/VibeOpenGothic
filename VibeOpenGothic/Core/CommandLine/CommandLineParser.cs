namespace VibeOpenGothic.Core.CommandLine;

public static class CommandLineParser
{
	public static CommandLineParseResult Parse(IReadOnlyList<string> args)
	{
		ArgumentNullException.ThrowIfNull(args);

		var warnings = new List<string>();
		var errors = new List<string>();

		string? gothicPath = null;
		string? modFile = null;
		string? startupWorld = null;
		string? saveToLoad = null;
		string? dataDirectory = null;
		var showHelp = false;
		var showVersion = false;
		var probe = false;
		var checkMeshes = false;
		var checkTextures = false;
		var checkModels = false;
		var checkAnimations = false;
		var devMode = false;
		var windowed = false;
		var positionalOnly = false;
		var versionOverrides = new HashSet<GameVersionOverride>();

		for (var index = 0; index < args.Count; index++)
		{
			var argument = args[index];

			if (positionalOnly)
			{
				warnings.Add($"Unexpected argument '{argument}' ignored.");
				continue;
			}

			switch (argument)
			{
				case "--":
					positionalOnly = true;
					break;
				case "--help" or "-h" or "-?":
					showHelp = true;
					break;
				case "--version":
					showVersion = true;
					break;
				case "--probe":
					probe = true;
					break;
				case "--check-meshes":
					checkMeshes = true;
					break;
				case "--check-textures":
					checkTextures = true;
					break;
				case "--check-models":
					checkModels = true;
					break;
				case "--check-animations":
					checkAnimations = true;
					break;
				case "--devmode" or "-devmode":
					devMode = true;
					break;
				case "--window" or "-window":
					windowed = true;
					break;
				case "-g1":
					versionOverrides.Add(GameVersionOverride.Gothic1);
					break;
				case "-g2":
					versionOverrides.Add(GameVersionOverride.Gothic2NotR);
					break;
				case "-g2c":
					versionOverrides.Add(GameVersionOverride.Gothic2Classic);
					break;
				case "-g":
					if (TryTakeValue(args, ref index, argument, errors, out var path))
					{
						gothicPath = path;
					}

					break;
				case "-w":
					if (TryTakeValue(args, ref index, argument, errors, out var world))
					{
						startupWorld = world;
					}

					break;
				case "-save":
					if (TryTakeValue(args, ref index, argument, errors, out var save))
					{
						saveToLoad = save;
					}

					break;
				case "--data-dir":
					if (TryTakeValue(args, ref index, argument, errors, out var dataDir))
					{
						dataDirectory = dataDir;
					}

					break;
				default:
					if (argument.StartsWith("-game:", StringComparison.OrdinalIgnoreCase))
					{
						var value = argument["-game:".Length..];
						if (string.IsNullOrWhiteSpace(value))
						{
							errors.Add("Option '-game:' requires a mod file name.");
						}
						else
						{
							modFile = value;
						}
					}
					else if (argument.StartsWith('-'))
					{
						warnings.Add($"Unknown option '{argument}' ignored.");
					}
					else
					{
						warnings.Add($"Unexpected argument '{argument}' ignored.");
					}

					break;
			}
		}

		if (versionOverrides.Count > 1)
		{
			errors.Add("Conflicting game version options; use only one of -g1, -g2, -g2c.");
		}

		var options = new CommandLineOptions
		{
			GothicPath = gothicPath,
			ModFile = modFile,
			StartupWorld = startupWorld,
			SaveToLoad = saveToLoad,
			DataDirectory = dataDirectory,
			ShowHelp = showHelp,
			ShowVersion = showVersion,
			Probe = probe,
			CheckMeshes = checkMeshes,
			CheckTextures = checkTextures,
			CheckModels = checkModels,
			CheckAnimations = checkAnimations,
			DevMode = devMode,
			Windowed = windowed,
			VersionOverride = versionOverrides.Count == 1
				? versionOverrides.First()
				: GameVersionOverride.Auto,
		};

		return new CommandLineParseResult(options, warnings, errors);
	}

	private static bool TryTakeValue(
		IReadOnlyList<string> args,
		ref int index,
		string option,
		List<string> errors,
		out string value)
	{
		if (index + 1 >= args.Count)
		{
			errors.Add($"Option '{option}' requires a value.");
			value = string.Empty;
			return false;
		}

		value = args[++index];
		if (string.IsNullOrWhiteSpace(value))
		{
			errors.Add($"Option '{option}' requires a non-empty value.");
			return false;
		}

		return true;
	}
}
