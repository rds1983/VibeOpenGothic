using System.Runtime.InteropServices;
using VibeOpenGothic.App;
using VibeOpenGothic.Assets.Animation;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Assets.Skins;
using VibeOpenGothic.Assets.Textures;
using VibeOpenGothic.Core.CommandLine;
using VibeOpenGothic.Core.Diagnostics;
using VibeOpenGothic.Core.Logging;
using VibeOpenGothic.Core.Settings;
using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Data.Probe;
using VibeOpenGothic.Data.Vfs;

namespace VibeOpenGothic;

internal static class Program
{
	private const int ExitSuccess = 0;
	private const int ExitFailure = 1;
	private const int ExitInstallError = 2;

	private static int Main(string[] args)
	{
		var parsed = CommandLineParser.Parse(args);
		var options = parsed.Options;

		if (options.ShowHelp)
		{
			Console.WriteLine(HelpText.Value);
			return ExitSuccess;
		}

		if (options.ShowVersion)
		{
			PrintVersion();
			return ExitSuccess;
		}

		AppPaths paths;
		try
		{
			paths = AppPaths.Create(options.DataDirectory);
			paths.EnsureCreated();
		}
		catch (Exception exception)
		{
			PrintError($"Unable to prepare the data directory: {exception.Message}");
			return ExitFailure;
		}

		try
		{
			Log.Start(
				paths.LogFilePath,
				options.DevMode ? LogLevel.Debug : LogLevel.Info);
		}
		catch (Exception exception)
		{
			PrintError($"Unable to initialize logging: {exception.Message}");
			return ExitFailure;
		}

		CrashHandler.Install(paths.DataDirectory, paths.LogFilePath);

		try
		{
			Log.Info("App", $"{BootstrapInfo.Name} {BootstrapInfo.Version}");
			Log.Info("App", $"OS: {RuntimeInformation.OSDescription}");
			Log.Info("App", $"Runtime: {RuntimeInformation.FrameworkDescription}");
			Log.Info("CLI", $"Arguments: {string.Join(" ", args.Select(QuoteForLog))}");

			foreach (var warning in parsed.Warnings)
			{
				Log.Warning("CLI", warning);
			}

			if (parsed.HasErrors)
			{
				foreach (var error in parsed.Errors)
				{
					Log.Error("CLI", error);
					PrintError(error);
				}

				Console.Error.WriteLine("Run with --help for usage.");
				return ExitFailure;
			}

			var settingsResult = SettingsStore.Load(paths.SettingsFilePath);
			foreach (var warning in settingsResult.Warnings)
			{
				Log.Warning("Settings", warning);
			}

			var settings = settingsResult.Settings;
			var environmentPath = Environment.GetEnvironmentVariable("VOG_GOTHIC_PATH");
			var knownCandidates = string.IsNullOrWhiteSpace(options.GothicPath)
				? KnownInstallLocations.Enumerate()
				: Array.Empty<string>();
			var installResult = GothicInstallLocator.Locate(
				options.GothicPath,
				environmentPath,
				settings.GothicPath,
				options.VersionOverride,
				options.ModFile,
				knownCandidates);

			foreach (var warning in installResult.Warnings)
			{
				Log.Warning("Data", warning);
			}

			if (!installResult.Success)
			{
				foreach (var error in installResult.Errors)
				{
					Log.Error("Data", error);
					PrintError(error);
				}

				if (installResult.SearchedPaths.Count > 0)
				{
					Log.Warning("Data", $"Searched: {string.Join(", ", installResult.SearchedPaths)}");
					Console.Error.WriteLine($"Searched: {string.Join(", ", installResult.SearchedPaths)}");
				}

				Console.Error.WriteLine($"See log: {paths.LogFilePath}");
				return ExitInstallError;
			}

			var install = installResult.Install!;
			Log.Info("Data", $"Gothic root: {install.RootPath}");
			Log.Info("Data", $"Gothic version: {install.Version}");
			Log.Info("Data", $"Archives: {install.Archives.Count}");

			using var vfs = ZenKitGameVfs.Mount(install, out var mountWarnings);
			foreach (var warning in mountWarnings)
			{
				Log.Warning("VFS", warning);
			}

			Log.Info("VFS", $"Mounted {install.Archives.Count} archive(s)");

			if (options.Probe)
			{
				var probe = InstallProbe.Create(install, vfs);
				foreach (var line in probe.Lines)
				{
					Console.WriteLine(line);
				}

				return probe.Success ? ExitSuccess : ExitFailure;
			}

			if (options.CheckMeshes)
			{
				return MeshCheckTool.Run(install, vfs);
			}

			if (options.CheckTextures)
			{
				return TextureCheckTool.Run(vfs);
			}

			if (options.CheckModels)
			{
				return ModelCheckTool.Run(install, vfs);
			}

			if (options.CheckAnimations)
			{
				return AnimationCheckTool.Run(install, vfs);
			}

			if (!vfs.FileExists(install.DefaultGameDatName))
			{
				var message = $"Gothic scripts not found: '{install.DefaultGameDatName}'.";
				Log.Error("Data", message);
				PrintError(message);
				Console.Error.WriteLine($"See log: {paths.LogFilePath}");
				return ExitInstallError;
			}

			settings.GothicPath = install.RootPath;
			var windowed = options.Windowed || settings.Windowed;
			var gameOptions = new GothicGameOptions(install, settings, options, windowed, vfs);

			Log.Info(
				"App",
				$"Starting MonoGame: {settings.WindowWidth}x{settings.WindowHeight}, windowed={windowed}, devMode={options.DevMode}");

			using (var game = new GothicGame(gameOptions))
			{
				game.Run();

				settings.WindowWidth = game.WindowWidth;
				settings.WindowHeight = game.WindowHeight;
				settings.Windowed = game.IsWindowed;
			}

			try
			{
				SettingsStore.Save(paths.SettingsFilePath, settings);
			}
			catch (Exception exception)
			{
				Log.Warning("Settings", $"Unable to save settings: {exception.Message}");
			}

			return ExitSuccess;
		}
		catch (Exception exception)
		{
			CrashHandler.Report(exception, "Unhandled error in the application shell", showDialog: true);
			return ExitFailure;
		}
		finally
		{
			Log.Stop();
		}
	}

	private static void PrintVersion()
	{
		Console.WriteLine($"{BootstrapInfo.Name} {BootstrapInfo.Version}");
		Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
		Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
	}

	private static string QuoteForLog(string argument)
	{
		return argument.Contains(' ') ? $"\"{argument}\"" : argument;
	}

	private static void PrintError(string message)
	{
		Console.Error.WriteLine($"error: {message}");
	}
}
