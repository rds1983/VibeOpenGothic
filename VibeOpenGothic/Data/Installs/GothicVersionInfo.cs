namespace VibeOpenGothic.Data.Installs;

public sealed record GothicVersionInfo(
	GothicVersion Version,
	int PatchVersion,
	string Source)
{
	public bool IsGothic1 => Version == GothicVersion.Gothic1;

	public override string ToString()
	{
		var patch = PatchVersion > 0 ? $", patch {PatchVersion}" : string.Empty;
		return $"{Version}{patch} ({Source})";
	}
}
