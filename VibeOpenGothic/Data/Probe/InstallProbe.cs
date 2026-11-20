using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Data.Vfs;

namespace VibeOpenGothic.Data.Probe;

public sealed record ProbeResult(IReadOnlyList<string> Lines, bool Success);

/// <summary>
/// Diagnostic used by <c>--probe</c>. Walks the mounted VFS once and resolves
/// the assets the loader will need first.
/// </summary>
public static class InstallProbe
{
	public static ProbeResult Create(GothicInstall install, IGameVfs vfs)
	{
		ArgumentNullException.ThrowIfNull(install);
		ArgumentNullException.ThrowIfNull(vfs);

		var gameDatName = install.DefaultGameDatName;
		var worldName = install.DefaultWorldName;

		VfsFileEntry? gameDat = null;
		VfsFileEntry? world = null;
		VfsFileEntry? outputUnitsDat = null;
		VfsFileEntry? outputUnitsBin = null;
		VfsFileEntry? sampleTexture = null;
		VfsFileEntry? sampleModel = null;
		var fileCount = 0;

		foreach (var file in vfs.EnumerateFiles())
		{
			fileCount++;

			if (gameDat is null && string.Equals(file.Name, gameDatName, StringComparison.OrdinalIgnoreCase))
			{
				gameDat = file;
			}

			if (world is null && string.Equals(file.Name, worldName, StringComparison.OrdinalIgnoreCase))
			{
				world = file;
			}

			if (outputUnitsDat is null && string.Equals(file.Name, "OU.DAT", StringComparison.OrdinalIgnoreCase))
			{
				outputUnitsDat = file;
			}

			if (outputUnitsBin is null && string.Equals(file.Name, "OU.BIN", StringComparison.OrdinalIgnoreCase))
			{
				outputUnitsBin = file;
			}

			if (sampleTexture is null &&
				file.Name.EndsWith(".TEX", StringComparison.OrdinalIgnoreCase) &&
				file.Path.Contains("/TEXTURES/", StringComparison.OrdinalIgnoreCase))
			{
				sampleTexture = file;
			}

			if (sampleModel is null &&
				file.Name.EndsWith(".MDS", StringComparison.OrdinalIgnoreCase) &&
				file.Path.Contains("/ANIMS/", StringComparison.OrdinalIgnoreCase))
			{
				sampleModel = file;
			}
		}

		var lines = new List<string>
		{
			$"Gothic root: {install.RootPath}",
			$"  version:      {install.Version}",
			$"  system:       {install.SystemDirectory}",
			$"  data:         {install.DataDirectory}",
			$"  work:         {install.WorkDirectory}",
			$"  mod:          {install.Mod.ManifestPath ?? "(none)"}",
			$"  archives:     {install.Archives.Count}",
			$"  VFS files:    {fileCount}",
			$"  {gameDatName}: {Format(gameDat)}",
			$"  {worldName}: {Format(world)}",
			$"  sample texture: {Format(sampleTexture)}",
			$"  sample model:   {Format(sampleModel)}",
			$"  output units:   OU.DAT {Format(outputUnitsDat)}, OU.BIN {Format(outputUnitsBin)}",
		};

		foreach (var archive in install.Archives)
		{
			var kind = archive.IsMod ? "mod" : "vdf";
			lines.Add($"  archive:      {archive.Name} ({kind}, {archive.SizeBytes} bytes, ts {archive.Timestamp})");
		}

		foreach (var warning in install.Warnings)
		{
			lines.Add($"  warning:      {warning}");
		}

		var success = gameDat is not null && world is not null;
		return new ProbeResult(lines, success);
	}

	private static string Format(VfsFileEntry? entry)
	{
		return entry is null ? "(missing)" : $"{entry.Path} ({entry.Name})";
	}
}
