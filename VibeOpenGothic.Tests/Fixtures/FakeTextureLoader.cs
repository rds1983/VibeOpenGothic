using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Textures;

namespace VibeOpenGothic.Tests.Fixtures;

internal sealed class FakeTextureLoader : ITextureLoader
{
	public Dictionary<string, TextureData> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

	public int Calls { get; private set; }

	public TextureData? Load(string name)
	{
		Calls++;
		return Files.TryGetValue(name, out var texture) ? texture : null;
	}
}

internal static class TestTextures
{
	public static TextureData Create(bool hasAlpha, string name = "A.TEX")
	{
		var rgba = new byte[]
		{
			255, 0, 0,
			hasAlpha ? (byte)128 : (byte)255,
		};

		return new TextureData(
			name,
			new[] { new TextureMipLevel(1, 1, rgba) },
			new Color(255, 0, 0, 255));
	}
}
