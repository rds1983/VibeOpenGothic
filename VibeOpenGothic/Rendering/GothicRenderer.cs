using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Core.Logging;
using VibeOpenGothic.World;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// Owns the world camera and GPU world resources. The CPU world is loaded by the
/// composition root and handed in, so this layer never reads game data.
/// </summary>
public sealed class GothicRenderer : IDisposable
{
	public static readonly Color ClearColor = new(92, 103, 112);

	private readonly GraphicsDevice _device;
	private readonly GpuTextureCache _textures;
	private readonly WorldRenderer _world;
	private bool _disposed;

	public GothicRenderer(GraphicsDevice device, WorldAsset world)
	{
		_device = device ?? throw new ArgumentNullException(nameof(device));
		ArgumentNullException.ThrowIfNull(world);

		_textures = new GpuTextureCache(device);
		_world = new WorldRenderer(
			device,
			_textures,
			world,
			message => Log.Info("World", message));

		var start = world.StartPosition ?? (world.Bounds.Min + world.Bounds.Max) * 0.5f;
		var direction = world.StartDirection;
		if (direction.LengthSquared() <= 0f)
		{
			direction = -Vector3.UnitZ;
		}
		else
		{
			direction = Vector3.Normalize(direction);
		}

		var eye = start + Vector3.Up * 170f;
		var target = eye + direction * 1200f - Vector3.Up * 120f;
		Camera = new GothicCamera(eye, target)
		{
			NearPlane = 5f,
			FarPlane = 160000f,
		};
	}

	public GothicCamera Camera { get; }

	public int LastDrawCalls => _world.LastDrawCalls;

	public int LastDrawnPolygons => _world.LastDrawnPolygons;

	public void Draw(GameTime gameTime)
	{
		ArgumentNullException.ThrowIfNull(gameTime);
		ObjectDisposedException.ThrowIf(_disposed, this);

		_device.Clear(
			ClearOptions.Target | ClearOptions.DepthBuffer,
			ClearColor,
			1f,
			0);

		Camera.SetAspectRatio(_device.Viewport.AspectRatio);
		_world.SetCamera(Camera.View, Camera.Projection);
		_world.Draw(gameTime);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_world.Dispose();
		_textures.Dispose();
	}
}
