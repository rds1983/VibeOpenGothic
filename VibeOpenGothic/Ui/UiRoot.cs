using Microsoft.Xna.Framework;
using Myra;
using Myra.Graphics2D.UI;

namespace VibeOpenGothic.Ui;

/// <summary>
/// Owns Myra initialization and the top-level desktop. UI stays out of the
/// renderer and gameplay layers.
/// </summary>
public sealed class UiRoot : IDisposable
{
	private readonly Desktop _desktop;
	private readonly DebugOverlay _debugOverlay;
	private readonly ControlsHud _controlsHud;
	private bool _disposed;

	public UiRoot(Game game)
	{
		ArgumentNullException.ThrowIfNull(game);

		// MyraEnvironment.Game throws on read when unset, so assign unconditionally.
		MyraEnvironment.Game = game;

		_debugOverlay = new DebugOverlay();
		_controlsHud = new ControlsHud();
		_desktop = new Desktop();
		_desktop.Widgets.Add(_debugOverlay);
		_desktop.Widgets.Add(_controlsHud);
	}

	public bool DebugOverlayVisible => _debugOverlay.Visible;

	public void ToggleDebugOverlay()
	{
		_debugOverlay.Visible = !_debugOverlay.Visible;
	}

	public void SetDebugStats(
		float fps,
		int drawCalls,
		int polygons,
		int culledBatches,
		int culledInstances)
	{
		_debugOverlay.SetStats(
			fps,
			drawCalls,
			polygons,
			culledBatches,
			culledInstances);
	}

	public void Render()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		_desktop.Render();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_desktop.Dispose();
	}
}
