using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Textures;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// Uploads CPU-side <see cref="TextureData"/> to the GPU and caches by texture
/// name. Rendering is the only layer allowed to create these resources.
/// </summary>
public sealed class GpuTextureCache : IDisposable
{
	private readonly GraphicsDevice _device;
	private readonly Dictionary<string, Texture2D> _cache = new(StringComparer.OrdinalIgnoreCase);
	private Texture2D? _placeholder;
	private Texture2D? _white;
	private bool _disposed;

	public GpuTextureCache(GraphicsDevice device)
	{
		_device = device ?? throw new ArgumentNullException(nameof(device));
	}

	public Texture2D Get(TextureData? data)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (data is null)
		{
			return Placeholder;
		}

		if (_cache.TryGetValue(data.Name, out var cached))
		{
			return cached;
		}

		var texture = Upload(data);
		_cache[data.Name] = texture;
		return texture;
	}

	public Texture2D Placeholder
	{
		get
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			return _placeholder ??= Upload(TextureData.CreatePlaceholder());
		}
	}

	public Texture2D White
	{
		get
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			if (_white is null)
			{
				_white = new Texture2D(_device, 1, 1, false, SurfaceFormat.Color);
				_white.SetData(new[] { Color.White });
			}

			return _white;
		}
	}

	/// <summary>
	/// Converts a headless material into its GPU form. The returned copy references
	/// a cached <see cref="Texture2D"/> and no longer carries the CPU texture data.
	/// </summary>
	public GothicMaterial Resolve(GothicMaterial material)
	{
		ArgumentNullException.ThrowIfNull(material);
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (material.TextureSource is null || material.Texture is not null)
		{
			return material;
		}

		return material with
		{
			Texture = Get(material.TextureSource),
			TextureSource = null,
		};
	}

	public MaterialSet Resolve(MaterialSet materials)
	{
		ArgumentNullException.ThrowIfNull(materials);

		var resolved = new GothicMaterial[materials.Materials.Count];
		for (var index = 0; index < resolved.Length; index++)
		{
			resolved[index] = Resolve(materials.Materials[index]);
		}

		return new MaterialSet(materials.SourceKind, resolved);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		foreach (var texture in _cache.Values)
		{
			texture.Dispose();
		}

		_cache.Clear();
		_placeholder?.Dispose();
		_placeholder = null;
		_white?.Dispose();
		_white = null;
	}

	private Texture2D Upload(TextureData data)
	{
		var levels = data.MipLevels;
		var texture = new Texture2D(
			_device,
			data.Width,
			data.Height,
			false,
			SurfaceFormat.Color);

		var top = levels[0];
		texture.SetData(
			0,
			null,
			top.Rgba.ToArray(),
			0,
			top.Rgba.Length);

		return texture;
	}
}
