using VibeOpenGothic.Core.CommandLine;

namespace VibeOpenGothic.Tests.Unit;

public sealed class CommandLineParserTests
{
	[Fact]
	public void Parse_NoArguments_ReturnsDefaults()
	{
		var result = CommandLineParser.Parse(Array.Empty<string>());

		Assert.False(result.HasErrors);
		Assert.Empty(result.Warnings);
		Assert.Null(result.Options.GothicPath);
		Assert.Null(result.Options.ModFile);
		Assert.Null(result.Options.StartupWorld);
		Assert.Null(result.Options.SaveToLoad);
		Assert.Null(result.Options.DataDirectory);
		Assert.Null(result.Options.ScreenshotPath);
		Assert.Null(result.Options.SpawnModel);
		Assert.Null(result.Options.JsonPath);
		Assert.Null(result.Options.DumpDataPath);
		Assert.False(result.Options.ShowHelp);
		Assert.False(result.Options.ShowVersion);
		Assert.False(result.Options.Probe);
		Assert.False(result.Options.CheckMeshes);
		Assert.False(result.Options.CheckTextures);
		Assert.False(result.Options.CheckModels);
		Assert.False(result.Options.CheckAnimations);
		Assert.False(result.Options.CheckWorld);
		Assert.False(result.Options.CheckAssets);
		Assert.False(result.Options.DevMode);
		Assert.False(result.Options.Windowed);
		Assert.Equal(GameVersionOverride.Auto, result.Options.VersionOverride);
	}

	[Theory]
	[InlineData("--help")]
	[InlineData("-h")]
	[InlineData("-?")]
	public void Parse_HelpFlags_AreRecognized(string argument)
	{
		var result = CommandLineParser.Parse(new[] { argument });

		Assert.True(result.Options.ShowHelp);
		Assert.False(result.HasErrors);
	}

	[Fact]
	public void Parse_VersionFlag_IsRecognized()
	{
		var result = CommandLineParser.Parse(new[] { "--version" });

		Assert.True(result.Options.ShowVersion);
		Assert.False(result.HasErrors);
	}

	[Fact]
	public void Parse_CheckMeshesFlag_IsRecognized()
	{
		var result = CommandLineParser.Parse(new[] { "--check-meshes" });

		Assert.True(result.Options.CheckMeshes);
		Assert.False(result.HasErrors);
	}

	[Fact]
	public void Parse_CheckTexturesFlag_IsRecognized()
	{
		var result = CommandLineParser.Parse(new[] { "--check-textures" });

		Assert.True(result.Options.CheckTextures);
		Assert.False(result.HasErrors);
	}

	[Fact]
	public void Parse_CheckModelsFlag_IsRecognized()
	{
		var result = CommandLineParser.Parse(new[] { "--check-models" });

		Assert.True(result.Options.CheckModels);
		Assert.False(result.HasErrors);
	}

	[Fact]
	public void Parse_CheckAnimationsFlag_IsRecognized()
	{
		var result = CommandLineParser.Parse(new[] { "--check-animations" });

		Assert.True(result.Options.CheckAnimations);
		Assert.False(result.HasErrors);
	}

	[Fact]
	public void Parse_ScreenshotPath_IsRecognized()
	{
		var result = CommandLineParser.Parse(new[] { "--screenshot", "shots/test.png" });

		Assert.Equal("shots/test.png", result.Options.ScreenshotPath);
		Assert.False(result.HasErrors);
	}

	[Fact]
	public void Parse_SpawnModel_IsRecognized()
	{
		var result = CommandLineParser.Parse(new[] { "--spawn-model", "HumanS.mds" });

		Assert.Equal("HumanS.mds", result.Options.SpawnModel);
		Assert.False(result.HasErrors);
	}

	[Fact]
	public void Parse_CheckAssetsAndOutputs_AreRecognized()
	{
		var result = CommandLineParser.Parse(new[]
		{
			"--check-assets",
			"--json", "report.json",
			"--dump-data", "dump",
		});

		Assert.True(result.Options.CheckAssets);
		Assert.Equal("report.json", result.Options.JsonPath);
		Assert.Equal("dump", result.Options.DumpDataPath);
		Assert.False(result.HasErrors);
	}

	[Fact]
	public void Parse_CheckWorldFlag_IsRecognized()
	{
		var result = CommandLineParser.Parse(new[] { "--check-world" });

		Assert.True(result.Options.CheckWorld);
		Assert.False(result.HasErrors);
	}

	[Fact]
	public void Parse_GothicPathAndModFile()
	{
		var result = CommandLineParser.Parse(new[]
		{
			"-g", @"C:\Gothic II",
			"-game:Karibik.ini",
		});

		Assert.Equal(@"C:\Gothic II", result.Options.GothicPath);
		Assert.Equal("Karibik.ini", result.Options.ModFile);
		Assert.False(result.HasErrors);
	}

	[Fact]
	public void Parse_ModFilePrefix_IsCaseInsensitive()
	{
		var result = CommandLineParser.Parse(new[] { "-GAME:Karibik.ini" });

		Assert.Equal("Karibik.ini", result.Options.ModFile);
	}

	[Fact]
	public void Parse_FullOptionSet()
	{
		var result = CommandLineParser.Parse(new[]
		{
			"-g", @"C:\Gothic II",
			"-game:Karibik.ini",
			"-w", "newworld.zen",
			"-save", "q",
			"-window",
			"-devmode",
			"--probe",
			"--data-dir", @"C:\data",
			"-g2c",
		});

		Assert.False(result.HasErrors);
		Assert.Equal(@"C:\Gothic II", result.Options.GothicPath);
		Assert.Equal("Karibik.ini", result.Options.ModFile);
		Assert.Equal("newworld.zen", result.Options.StartupWorld);
		Assert.Equal("q", result.Options.SaveToLoad);
		Assert.Equal(@"C:\data", result.Options.DataDirectory);
		Assert.True(result.Options.Windowed);
		Assert.True(result.Options.DevMode);
		Assert.True(result.Options.Probe);
		Assert.Equal(GameVersionOverride.Gothic2Classic, result.Options.VersionOverride);
	}

	[Theory]
	[InlineData("-g1", GameVersionOverride.Gothic1)]
	[InlineData("-g2", GameVersionOverride.Gothic2NotR)]
	[InlineData("-g2c", GameVersionOverride.Gothic2Classic)]
	public void Parse_VersionOverride(string argument, GameVersionOverride expected)
	{
		var result = CommandLineParser.Parse(new[] { argument });

		Assert.Equal(expected, result.Options.VersionOverride);
		Assert.False(result.HasErrors);
	}

	[Fact]
	public void Parse_ConflictingVersionOverrides_AddsError()
	{
		var result = CommandLineParser.Parse(new[] { "-g1", "-g2" });

		Assert.True(result.HasErrors);
		Assert.Contains("Conflicting", result.Errors[0]);
		Assert.Equal(GameVersionOverride.Auto, result.Options.VersionOverride);
	}

	[Fact]
	public void Parse_UnknownOption_AddsWarning()
	{
		var result = CommandLineParser.Parse(new[] { "--nope" });

		Assert.False(result.HasErrors);
		Assert.Single(result.Warnings);
		Assert.Contains("--nope", result.Warnings[0]);
	}

	[Fact]
	public void Parse_UnexpectedPositionalArgument_AddsWarning()
	{
		var result = CommandLineParser.Parse(new[] { "newworld.zen" });

		Assert.False(result.HasErrors);
		Assert.Single(result.Warnings);
		Assert.Contains("newworld.zen", result.Warnings[0]);
	}

	[Fact]
	public void Parse_EndOfOptions_TreatsRemainingTokensAsPositional()
	{
		var result = CommandLineParser.Parse(new[] { "--", "-g", @"C:\Gothic II" });

		Assert.Null(result.Options.GothicPath);
		Assert.Equal(2, result.Warnings.Count);
	}

	[Theory]
	[InlineData("-g")]
	[InlineData("-w")]
	[InlineData("-save")]
	[InlineData("--data-dir")]
	[InlineData("--screenshot")]
	[InlineData("--spawn-model")]
	[InlineData("--json")]
	[InlineData("--dump-data")]
	public void Parse_MissingValue_AddsError(string argument)
	{
		var result = CommandLineParser.Parse(new[] { argument });

		Assert.True(result.HasErrors);
		Assert.Single(result.Errors);
	}

	[Fact]
	public void Parse_EmptyModFile_AddsError()
	{
		var result = CommandLineParser.Parse(new[] { "-game:" });

		Assert.True(result.HasErrors);
	}

	[Fact]
	public void Parse_EmptyValue_AddsError()
	{
		var result = CommandLineParser.Parse(new[] { "-g", "   " });

		Assert.True(result.HasErrors);
	}
}
