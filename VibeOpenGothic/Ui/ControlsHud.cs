using Microsoft.Xna.Framework;
using Myra.Graphics2D.Brushes;

namespace VibeOpenGothic.Ui;

public partial class ControlsHud
{
	public ControlsHud()
	{
		BuildUI();

		Background = new SolidBrush(new Color(0, 0, 0, 180));
		_titleLabel.TextColor = new Color(210, 210, 210);
		_debugToggleLabel.TextColor = Color.White;
	}
}
