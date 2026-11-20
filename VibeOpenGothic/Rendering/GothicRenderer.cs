using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Data.Vfs;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// R0 renderer owner. T1.5 renders a fixed test scene; world rendering is added
/// in T1.6 without changing this ownership boundary.
/// </summary>
public sealed class GothicRenderer : IDisposable
{
	public static readonly Color ClearColor = new(18, 20, 24);

	private readonly GraphicsDevice _device;
	private readonly TestScene _scene;
	private bool _disposed;

	public GothicRenderer(GraphicsDevice device, IGameVfs vfs)
	{
		_device = device ?? throw new ArgumentNullException(nameof(device));
		_scene = new TestScene(device, vfs);
		Camera = new GothicCamera(new Vector3(235f, 185f, 430f), new Vector3(0f, 75f, 0f));
	}

	public GothicCamera Camera { get; }

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
		_scene.Draw(_device, Camera.View, Camera.Projection);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_scene.Dispose();
	}
}
