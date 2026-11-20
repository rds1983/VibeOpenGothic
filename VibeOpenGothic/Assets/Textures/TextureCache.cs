namespace VibeOpenGothic.Assets.Textures;

/// <summary>
/// Name-keyed texture cache with a shared placeholder for missing or broken
/// textures. Missing names are reported once.
/// </summary>
public sealed class TextureCache
{
	private readonly ITextureLoader _loader;
	private readonly Action<string>? _onMissing;
	private readonly Dictionary<string, TextureData> _cache = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _missing = new(StringComparer.OrdinalIgnoreCase);

	public TextureCache(ITextureLoader loader, Action<string>? onMissing = null)
	{
		_loader = loader ?? throw new ArgumentNullException(nameof(loader));
		_onMissing = onMissing;
		Placeholder = TextureData.CreatePlaceholder();
	}

	public TextureData Placeholder { get; }

	public IReadOnlyCollection<string> MissingTextures => _missing;

	/// <summary>
	/// Returns null for an empty texture name (untextured material), a decoded
	/// texture for a hit, or the placeholder for a missing/broken texture.
	/// </summary>
	public TextureData? Get(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}

		var key = name.Trim();

		if (_cache.TryGetValue(key, out var cached))
		{
			return cached;
		}

		if (_missing.Contains(key))
		{
			return Placeholder;
		}

		TextureData? texture;
		try
		{
			texture = _loader.Load(key);
		}
		catch
		{
			texture = null;
		}

		if (texture is null)
		{
			_missing.Add(key);
			_onMissing?.Invoke(key);
			return Placeholder;
		}

		_cache[key] = texture;
		return texture;
	}

	public void Clear()
	{
		_cache.Clear();
		_missing.Clear();
	}
}
