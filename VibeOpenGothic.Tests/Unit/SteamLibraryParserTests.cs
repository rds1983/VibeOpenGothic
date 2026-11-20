using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class SteamLibraryParserTests
{
	[Fact]
	public void Parse_ReadsAllPathValues()
	{
		const string text = """
            "libraryfolders"
            {
                "0"
                {
                    "path"		"C:\\Program Files (x86)\\Steam"
                    "label"		""
                }
                "1"
                {
                    "path"		"D:\\SteamLibrary"
                    "apps"
                    {
                        "22320"		"1142479546"
                    }
                }
            }
            """;

		var paths = SteamLibraryParser.Parse(text);

		Assert.Equal(2, paths.Count);
		Assert.Contains(@"C:\Program Files (x86)\Steam", paths);
		Assert.Contains(@"D:\SteamLibrary", paths);
	}

	[Fact]
	public void Parse_IgnoresCommentsAndUnrelatedKeys()
	{
		const string text = """
            // comment
            "libraryfolders"
            {
                "contentstatsid" "123"
                "0" { "path" "E:\\Games" }
            }
            """;

		var paths = SteamLibraryParser.Parse(text);

		Assert.Single(paths);
		Assert.Equal(@"E:\Games", paths[0]);
	}

	[Fact]
	public void ReadLibraryFolders_MissingFile_ReturnsEmpty()
	{
		using var temp = new TempDirectory();

		var paths = SteamLibraryParser.ReadLibraryFolders(temp.Root);

		Assert.Empty(paths);
	}

	[Fact]
	public void ReadLibraryFolders_ReadsSteamAppsFile()
	{
		using var temp = new TempDirectory();
		temp.CreateFile(
			Path.Combine("steamapps", "libraryfolders.vdf"),
			"\"libraryfolders\" { \"0\" { \"path\" \"D:\\\\SteamLibrary\" } }");

		var paths = SteamLibraryParser.ReadLibraryFolders(temp.Root);

		Assert.Single(paths);
		Assert.Equal(@"D:\SteamLibrary", paths[0]);
	}
}
