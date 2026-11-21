using Microsoft.Xna.Framework;
using Myra.Graphics2D.Brushes;

namespace VibeOpenGothic.Ui;

public partial class DebugOverlay
{
	public DebugOverlay()
	{
		BuildUI();

		Background = new SolidBrush(new Color(0, 0, 0, 180));
		Visible = false;

		_fpsLabel.TextColor = Color.White;
		_drawCallsLabel.TextColor = Color.White;
		_polygonsLabel.TextColor = Color.White;
		_culledLabel.TextColor = Color.White;
		_instancedLabel.TextColor = Color.White;
	}

	public void SetStats(
		float fps,
		int drawCalls,
		int polygons,
		int culledBatches,
		int culledInstances,
		int instancedDrawCalls)
	{
		_fpsLabel.Text = $"FPS: {fps:0.0}";
		_drawCallsLabel.Text = $"Draw calls: {drawCalls:N0}";
		_polygonsLabel.Text = $"Polygons: {polygons:N0}";
		_culledLabel.Text = $"Culled: {culledBatches:N0} batches / {culledInstances:N0} instances";
		_instancedLabel.Text = $"Instanced: {instancedDrawCalls:N0}";
	}
}
