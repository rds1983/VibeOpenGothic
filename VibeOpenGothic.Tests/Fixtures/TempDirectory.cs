namespace VibeOpenGothic.Tests.Fixtures;

internal sealed class TempDirectory : IDisposable
{
	public TempDirectory()
	{
		Root = Path.Combine(
			Path.GetTempPath(),
			"vibeopengothic-tests",
			Guid.NewGuid().ToString("N"));

		Directory.CreateDirectory(Root);
	}

	public string Root { get; }

	public string CreateFile(string relativePath, string content = "")
	{
		var path = Path.Combine(Root, relativePath);
		var directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		File.WriteAllText(path, content);
		return path;
	}

	public string CreateDirectory(string relativePath)
	{
		var path = Path.Combine(Root, relativePath);
		Directory.CreateDirectory(path);
		return path;
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(Root, recursive: true);
		}
		catch
		{
			// Best-effort cleanup; a locked file must not fail a test.
		}
	}
}
