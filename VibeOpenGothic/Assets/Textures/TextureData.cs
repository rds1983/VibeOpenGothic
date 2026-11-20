using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Assets.Textures;

public readonly record struct TextureMipLevel(
	int Width,
	int Height,
	ReadOnlyMemory<byte> Rgba);

/// <summary>
/// CPU-side RGBA texture data. No GraphicsDevice or ZenKit types.
/// </summary>
public sealed class TextureData
{
	public TextureData(
		string name,
		IReadOnlyList<TextureMipLevel> mipLevels,
		Color averageColor)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(mipLevels);

		if (mipLevels.Count == 0)
		{
			throw new ArgumentException("At least one mip level is required.", nameof(mipLevels));
		}

		for (var index = 0; index < mipLevels.Count; index++)
		{
			var level = mipLevels[index];
			if (level.Width <= 0 || level.Height <= 0)
			{
				throw new ArgumentException($"Mip level {index} has an invalid size.", nameof(mipLevels));
			}

			var expected = (long)level.Width * level.Height * 4;
			if (level.Rgba.Length != expected)
			{
				throw new ArgumentException(
					$"Mip level {index} has {level.Rgba.Length} bytes; expected {expected}.",
					nameof(mipLevels));
			}
		}

		Name = name;
		MipLevels = mipLevels;
		AverageColor = averageColor;
		HasAlpha = HasTransparentPixel(mipLevels[0].Rgba);
	}

	public string Name { get; }

	public int Width => MipLevels[0].Width;

	public int Height => MipLevels[0].Height;

	public IReadOnlyList<TextureMipLevel> MipLevels { get; }

	public Color AverageColor { get; }

	/// <summary>True when the top mip contains a non-opaque alpha value.</summary>
	public bool HasAlpha { get; }

	public static TextureData CreatePlaceholder()
	{
		const int size = 4;
		var rgba = new byte[size * size * 4];

		for (var y = 0; y < size; y++)
		{
			for (var x = 0; x < size; x++)
			{
				var offset = (y * size + x) * 4;
				var bright = ((x + y) & 1) == 0;
				rgba[offset + 0] = bright ? (byte)255 : (byte)64;
				rgba[offset + 1] = 0;
				rgba[offset + 2] = bright ? (byte)255 : (byte)64;
				rgba[offset + 3] = 255;
			}
		}

		return new TextureData(
			"<placeholder>",
			new[] { new TextureMipLevel(size, size, rgba) },
			new Color(255, 0, 255, 255));
	}

	private static bool HasTransparentPixel(ReadOnlyMemory<byte> rgba)
	{
		var span = rgba.Span;
		for (var index = 3; index < span.Length; index += 4)
		{
			if (span[index] < 255)
			{
				return true;
			}
		}

		return false;
	}
}
