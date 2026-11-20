using System.Diagnostics;
using ZenKit;
using VibeOpenGothic.Data.Vfs;

namespace VibeOpenGothic.Assets.Textures;

/// <summary>
/// Developer diagnostic for <c>--check-textures</c>: decodes a sample of the
/// mounted TEX files and reports dimensions, mip counts and alpha.
/// </summary>
public static class TextureCheckTool
{
	public static int Run(IGameVfs vfs, Action<string>? output = null, int maxTextures = 200)
	{
		ArgumentNullException.ThrowIfNull(vfs);

		output ??= Console.WriteLine;
		if (maxTextures <= 0)
		{
			maxTextures = 200;
		}

		var checkedCount = 0;
		var failureCount = 0;
		var alphaCount = 0;
		var totalMips = 0;
		var minWidth = int.MaxValue;
		var maxWidth = 0;
		var minHeight = int.MaxValue;
		var maxHeight = 0;
		var timer = Stopwatch.StartNew();

		foreach (var file in vfs.EnumerateFiles())
		{
			if (checkedCount >= maxTextures)
			{
				break;
			}

			if (!file.Name.EndsWith(".TEX", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			try
			{
				using var stream = vfs.OpenRead(file.Path);
				using var memory = new MemoryStream();
				stream.CopyTo(memory);

				var read = new Read(memory.ToArray());
				var texture = new Texture(read);
				var data = TextureDecoder.Decode(texture, file.Path);

				checkedCount++;
				totalMips += data.MipLevels.Count;
				minWidth = Math.Min(minWidth, data.Width);
				maxWidth = Math.Max(maxWidth, data.Width);
				minHeight = Math.Min(minHeight, data.Height);
				maxHeight = Math.Max(maxHeight, data.Height);

				if (data.HasAlpha)
				{
					alphaCount++;
				}

				if (checkedCount <= 10 || checkedCount % 50 == 0)
				{
					output(
						$"{file.Path}: {data.Width}x{data.Height}, {data.MipLevels.Count} mips, alpha={data.HasAlpha}");
				}

				GC.KeepAlive(texture);
				GC.KeepAlive(read);
			}
			catch (Exception exception)
			{
				failureCount++;
				output($"FAIL {file.Path}: {exception.GetType().Name}: {exception.Message}");
			}
		}

		timer.Stop();

		if (checkedCount == 0)
		{
			output($"No textures checked ({failureCount} failure(s)).");
			return 1;
		}

		output(
			$"Checked {checkedCount} texture(s), {failureCount} failure(s) in {timer.Elapsed.TotalSeconds:0.0}s.");
		output(
			$"Dimensions: {minWidth}x{minHeight}..{maxWidth}x{maxHeight}; " +
			$"{totalMips} mip levels; {alphaCount} texture(s) with alpha.");

		return failureCount == 0 ? 0 : 1;
	}
}
