using VibeOpenGothic.Data.Ini;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class IniFileTests
{
	[Fact]
	public void Parse_ReadsSectionsAndKeysCaseInsensitively()
	{
		var ini = IniFile.Parse("[GAME]\nPATCHVERSION=5\n[keysdefault1]\nForward=W\n");

		Assert.True(ini.HasSection("game"));
		Assert.True(ini.HasKey("Game", "patchversion"));
		Assert.Equal("5", ini.GetString("GAME", "PATCHVERSION"));
		Assert.Equal(5, ini.GetInt("game", "patchversion"));
		Assert.True(ini.HasSection("KEYSDEFAULT1"));
	}

	[Fact]
	public void Parse_IgnoresCommentsAndBlankLines()
	{
		var ini = IniFile.Parse("; comment\n\n// another\n[GAME]\n  VALUE = 1  \n");

		Assert.Equal("1", ini.GetString("GAME", "VALUE"));
	}

	[Fact]
	public void Parse_HandlesQuotedValuesAndInlineComments()
	{
		var ini = IniFile.Parse(
			"[PATH]\nROOT = \"C:\\Gothic II\" ; install root\nNAME = value ; trailing\nURL = a//b\n");

		Assert.Equal(@"C:\Gothic II", ini.GetString("PATH", "ROOT"));
		Assert.Equal("value", ini.GetString("PATH", "NAME"));
		Assert.Equal("a//b", ini.GetString("PATH", "URL"));
	}

	[Fact]
	public void GetInt_MissingValue_ReturnsFallback()
	{
		var ini = IniFile.Parse("[GAME]\n");

		Assert.Equal(7, ini.GetInt("GAME", "MISSING", 7));
		Assert.Null(ini.GetString("GAME", "MISSING"));
	}

	[Fact]
	public void GetBool_RecognizesCommonValues()
	{
		var ini = IniFile.Parse("[GAME]\nA=1\nB=true\nC=0\nD=no\n");

		Assert.True(ini.GetBool("GAME", "A"));
		Assert.True(ini.GetBool("GAME", "B"));
		Assert.False(ini.GetBool("GAME", "C", true));
		Assert.False(ini.GetBool("GAME", "D"));
	}

	[Fact]
	public void GetSection_ReturnsAllKeys()
	{
		var ini = IniFile.Parse("[A]\nX=1\nY=2\n");

		var section = ini.GetSection("A");

		Assert.Equal(2, section.Count);
		Assert.Equal("2", section["y"]);
	}

	[Fact]
	public void Load_ReadsFile()
	{
		using var temp = new TempDirectory();
		var path = temp.CreateFile("config.ini", "[A]\nX=42\n");

		var ini = IniFile.Load(path);

		Assert.Equal(42, ini.GetInt("A", "X"));
	}
}
