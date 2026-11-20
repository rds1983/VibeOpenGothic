using System.Buffers.Binary;
using VibeOpenGothic.Core.CommandLine;
using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class GothicInstallLocatorTests
{
	[Fact]
	public void Locate_ValidInstallation_Succeeds()
	{
		using var temp = new TempDirectory();
		CreateInstall(temp, patchVersion: 5);

		var result = GothicInstallLocator.Locate(
			temp.Root,
			null,
			null,
			GameVersionOverride.Auto,
			null);

		Assert.True(result.Success);
		var install = result.Install;
		Assert.NotNull(install);
		Assert.Equal(Path.GetFullPath(temp.Root), install.RootPath);
		Assert.EndsWith("system", install.SystemDirectory, StringComparison.Ordinal);
		Assert.Equal(GothicVersion.Gothic2NotR, install.Version.Version);
		Assert.Single(install.Archives);
		Assert.Equal(Path.GetFullPath(temp.Root), result.SearchedPaths[0]);
	}

	[Fact]
	public void Locate_CommandLinePathWinsOverAllOtherSources()
	{
		using var cli = new TempDirectory();
		using var environment = new TempDirectory();
		using var settings = new TempDirectory();
		using var known = new TempDirectory();
		CreateInstall(cli);
		CreateInstall(environment);
		CreateInstall(settings);
		CreateInstall(known);

		var result = GothicInstallLocator.Locate(
			cli.Root,
			environment.Root,
			settings.Root,
			GameVersionOverride.Auto,
			null,
			new[] { known.Root });

		Assert.True(result.Success);
		Assert.Equal(Path.GetFullPath(cli.Root), result.Install?.RootPath);
	}

	[Fact]
	public void Locate_EnvironmentPathWinsOverSettingsAndKnown()
	{
		using var environment = new TempDirectory();
		using var settings = new TempDirectory();
		using var known = new TempDirectory();
		CreateInstall(environment);
		CreateInstall(settings);
		CreateInstall(known);

		var result = GothicInstallLocator.Locate(
			null,
			environment.Root,
			settings.Root,
			GameVersionOverride.Auto,
			null,
			new[] { known.Root });

		Assert.True(result.Success);
		Assert.Equal(Path.GetFullPath(environment.Root), result.Install?.RootPath);
	}

	[Fact]
	public void Locate_SettingsPathWinsOverKnownLocations()
	{
		using var settings = new TempDirectory();
		using var known = new TempDirectory();
		CreateInstall(settings);
		CreateInstall(known);

		var result = GothicInstallLocator.Locate(
			null,
			null,
			settings.Root,
			GameVersionOverride.Auto,
			null,
			new[] { known.Root });

		Assert.True(result.Success);
		Assert.Equal(Path.GetFullPath(settings.Root), result.Install?.RootPath);
	}

	[Fact]
	public void Locate_KnownLocation_IsUsedAsFallback()
	{
		using var known = new TempDirectory();
		CreateInstall(known);

		var result = GothicInstallLocator.Locate(
			null,
			null,
			null,
			GameVersionOverride.Auto,
			null,
			new[] { known.Root });

		Assert.True(result.Success);
		Assert.Equal(Path.GetFullPath(known.Root), result.Install?.RootPath);
	}

	[Fact]
	public void Locate_InvalidCommandLinePath_DoesNotFallBack()
	{
		using var valid = new TempDirectory();
		CreateInstall(valid);

		var result = GothicInstallLocator.Locate(
			Path.Combine(valid.Root, "missing"),
			null,
			null,
			GameVersionOverride.Auto,
			null,
			new[] { valid.Root });

		Assert.False(result.Success);
		Assert.Contains("not found", result.Errors[0]);
	}

	[Fact]
	public void Locate_InvalidEnvironmentPath_FallsBackAndWarns()
	{
		using var valid = new TempDirectory();
		CreateInstall(valid);

		var result = GothicInstallLocator.Locate(
			null,
			Path.Combine(valid.Root, "missing"),
			null,
			GameVersionOverride.Auto,
			null,
			new[] { valid.Root });

		Assert.True(result.Success);
		Assert.Contains(result.Warnings, warning => warning.StartsWith("VOG_GOTHIC_PATH:", StringComparison.Ordinal));
	}

	[Fact]
	public void Locate_NoPath_ReportsError()
	{
		var result = GothicInstallLocator.Locate(
			null,
			null,
			null,
			GameVersionOverride.Auto,
			null);

		Assert.False(result.Success);
		Assert.Single(result.Errors);
		Assert.Contains("-g", result.Errors[0]);
		Assert.Empty(result.SearchedPaths);
	}

	[Theory]
	[InlineData("Data")]
	[InlineData("_work/Data/Scripts/_compiled")]
	[InlineData("system/Gothic.ini")]
	public void Locate_MissingRequiredPart_ReportsError(string missingPart)
	{
		using var temp = new TempDirectory();
		CreateInstall(temp, includeData: missingPart != "Data",
			includeWork: missingPart != "_work/Data/Scripts/_compiled",
			includeSystemIni: missingPart != "system/Gothic.ini");

		var result = GothicInstallLocator.Locate(
			temp.Root,
			null,
			null,
			GameVersionOverride.Auto,
			null);

		Assert.False(result.Success);
		Assert.NotEmpty(result.Errors);
	}

	[Fact]
	public void Locate_NoArchives_ReportsError()
	{
		using var temp = new TempDirectory();
		CreateInstall(temp, includeArchive: false);

		var result = GothicInstallLocator.Locate(
			temp.Root,
			null,
			null,
			GameVersionOverride.Auto,
			null);

		Assert.False(result.Success);
		Assert.Contains("No VDF archives", result.Errors[0]);
	}

	[Fact]
	public void Locate_VersionOverride_WinsOverDetection()
	{
		using var temp = new TempDirectory();
		CreateInstall(temp, patchVersion: 5);

		var result = GothicInstallLocator.Locate(
			temp.Root,
			null,
			null,
			GameVersionOverride.Gothic2Classic,
			null);

		Assert.True(result.Success);
		Assert.Equal(GothicVersion.Gothic2Classic, result.Install?.Version.Version);
		Assert.Equal("override -g2c", result.Install?.Version.Source);
	}

	private static void CreateInstall(
		TempDirectory temp,
		int patchVersion = 5,
		bool includeData = true,
		bool includeWork = true,
		bool includeSystemIni = true,
		bool includeArchive = true)
	{
		if (includeSystemIni)
		{
			temp.CreateFile(
				Path.Combine("system", "Gothic.ini"),
				$"[GAME]\nPATCHVERSION={patchVersion}\n");
		}

		if (includeData)
		{
			temp.CreateDirectory("Data");
			if (includeArchive)
			{
				WriteVdf(Path.Combine(temp.Root, "Data", "GOTHIC.VDF"), timestamp: 100);
			}
		}

		if (includeWork)
		{
			temp.CreateDirectory(Path.Combine("_work", "Data", "Scripts", "_compiled"));
			temp.CreateFile(
				Path.Combine("_work", "Data", "Scripts", "_compiled", "GOTHIC.DAT"),
				string.Empty);
		}
	}

	private static void WriteVdf(string path, uint timestamp)
	{
		var bytes = new byte[300];
		BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(280), timestamp);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllBytes(path, bytes);
	}
}
