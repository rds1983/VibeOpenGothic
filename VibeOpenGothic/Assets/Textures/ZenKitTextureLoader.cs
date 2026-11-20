using ZenKit;
using VibeOpenGothic.Data.Vfs;

namespace VibeOpenGothic.Assets.Textures;

public sealed class ZenKitTextureLoader : ITextureLoader
{
	private readonly IGameVfs _vfs;

	public ZenKitTextureLoader(IGameVfs vfs)
	{
		_vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
	}

	public TextureData? Load(string name)
	{
		var entry = TextureNameResolver.Resolve(_vfs, name);
		if (entry is null)
		{
			return null;
		}

		try
		{
			using var stream = _vfs.OpenRead(entry.Path);
			using var memory = new MemoryStream();
			stream.CopyTo(memory);

			var read = new Read(memory.ToArray());
			var texture = new Texture(read);
			var data = TextureDecoder.Decode(texture, entry.Path);

			GC.KeepAlive(texture);
			GC.KeepAlive(read);
			return data;
		}
		catch
		{
			return null;
		}
	}
}
