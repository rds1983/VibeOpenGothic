using Microsoft.Xna.Framework;
using ZenKit;

namespace VibeOpenGothic.Assets.Textures;

public static class TextureDecoder
{
	public static TextureData Decode(Texture texture, string name)
	{
		ArgumentNullException.ThrowIfNull(texture);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		var rawLevels = texture.AllMipmapsRgba;
		if (rawLevels.Count == 0)
		{
			throw new InvalidDataException($"Texture '{name}' has no RGBA mip levels.");
		}

		var levels = new List<TextureMipLevel>(rawLevels.Count);

		for (var index = 0; index < rawLevels.Count; index++)
		{
			var width = texture.GetWidth(index);
			var height = texture.GetHeight(index);

			if (width <= 0 || height <= 0)
			{
				continue;
			}

			var expected = checked(width * height * 4);
			var rgba = rawLevels[index];
			if (rgba.Length < expected)
			{
				throw new InvalidDataException(
					$"Texture '{name}' mip {index} has {rgba.Length} bytes; expected {expected}.");
			}

			levels.Add(new TextureMipLevel(width, height, rgba.AsMemory(0, expected)));
		}

		if (levels.Count == 0)
		{
			throw new InvalidDataException($"Texture '{name}' has no valid mip levels.");
		}

		var average = texture.AverageColor;

		return new TextureData(
			name,
			levels,
			new Color(average.R, average.G, average.B, average.A));
	}
}
