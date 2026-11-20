namespace VibeOpenGothic.Data.Installs;

public sealed record GothicInstallLayout(
	string RootPath,
	string SystemDirectory,
	string DataDirectory,
	string WorkDirectory);
