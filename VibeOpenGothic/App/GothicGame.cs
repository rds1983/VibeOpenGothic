using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using VibeOpenGothic.Core.Logging;
using VibeOpenGothic.Core.Timing;
using VibeOpenGothic.Rendering;

namespace VibeOpenGothic.App;

/// <summary>
/// MonoGame host. T1.5 owns the window and renderer; world loading arrives in
/// later tasks.
/// </summary>
public sealed class GothicGame : Game
{
	private const int ScreenshotWidth = 1280;
	private const int ScreenshotHeight = 720;

	private static readonly TimeSpan LogicStep = TimeSpan.FromSeconds(1.0 / 60.0);

	private readonly GraphicsDeviceManager _graphics;
	private readonly GothicGameOptions _options;
	private readonly FixedStepRunner _fixedStep = new(LogicStep, maxStepsPerFrame: 4);
	private readonly FrameStats _frames = new();

	private GothicRenderer? _renderer;
	private double _lastUpdateMs;
	private double _titleRefreshSeconds;
	private bool _firstFrameLogged;
	private bool _screenshotAttempted;

	public GothicGame(GothicGameOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options;

		_graphics = new GraphicsDeviceManager(this)
		{
			PreferredBackBufferWidth = options.Settings.WindowWidth,
			PreferredBackBufferHeight = options.Settings.WindowHeight,
			SynchronizeWithVerticalRetrace = options.Settings.VSync,
			IsFullScreen = !options.Windowed,
			HardwareModeSwitch = false,
			PreferMultiSampling = false,
			PreferHalfPixelOffset = false,
			GraphicsProfile = GraphicsProfile.HiDef,
			PreferredDepthStencilFormat = DepthFormat.Depth24,
		};

		Content.RootDirectory = "Content";
		IsFixedTimeStep = false;
		IsMouseVisible = true;

		Window.Title = GetBaseTitle();
		Window.AllowUserResizing = true;

		WindowWidth = options.Settings.WindowWidth;
		WindowHeight = options.Settings.WindowHeight;
		IsWindowed = options.Windowed;
	}

	public int WindowWidth { get; private set; }

	public int WindowHeight { get; private set; }

	public bool IsWindowed { get; private set; }

	public bool ScreenshotSaved { get; private set; }

	public string? ScreenshotError { get; private set; }

	protected override void Initialize()
	{
		// The device exists by now (Game.DoInitialize creates it first), but
		// DesktopGL's initial window is created with the default size. Force
		// the requested backbuffer through a device reset.
		_graphics.PreferredBackBufferWidth = _options.Settings.WindowWidth;
		_graphics.PreferredBackBufferHeight = _options.Settings.WindowHeight;
		_graphics.ApplyChanges();

		base.Initialize();

		CaptureWindowState(preferClientBounds: false);
		var presentation = GraphicsDevice.PresentationParameters;
		var bounds = Window.ClientBounds;

		Log.Debug(
			"App",
			$"Backbuffer {presentation.BackBufferWidth}x{presentation.BackBufferHeight}, client {bounds.Width}x{bounds.Height}");
		Log.Info(
			"App",
			$"MonoGame initialized: {WindowWidth}x{WindowHeight}, windowed={IsWindowed}, renderer={GraphicsDevice.GraphicsProfile}");
	}

	protected override void LoadContent()
	{
		base.LoadContent();
		_renderer = new GothicRenderer(GraphicsDevice, _options.Vfs);
	}

	protected override void UnloadContent()
	{
		_renderer?.Dispose();
		_renderer = null;
		base.UnloadContent();
	}

	protected override void Update(GameTime gameTime)
	{
		var updateStart = Stopwatch.GetTimestamp();

		if (Keyboard.GetState().IsKeyDown(Keys.Escape) ||
			GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
		{
			Exit();
		}

		var elapsed = gameTime.ElapsedGameTime.TotalSeconds;
		_fixedStep.Advance(elapsed, _ => _frames.AddLogicStep());

		_lastUpdateMs = Stopwatch.GetElapsedTime(updateStart).TotalMilliseconds;

		base.Update(gameTime);
	}

	protected override void Draw(GameTime gameTime)
	{
		var drawStart = Stopwatch.GetTimestamp();

		if (!_screenshotAttempted &&
			!string.IsNullOrWhiteSpace(_options.CommandLine.ScreenshotPath))
		{
			_screenshotAttempted = true;
			try
			{
				CaptureScreenshot(gameTime, _options.CommandLine.ScreenshotPath);
			}
			catch (Exception exception)
			{
				ScreenshotError = exception.Message;
				Log.Error("Render", $"Screenshot failed: {exception}");
			}

			Exit();
			return;
		}

		if (_renderer is not null)
		{
			_renderer.Draw(gameTime);
		}
		else
		{
			GraphicsDevice.Clear(GothicRenderer.ClearColor);
		}

		var drawMs = Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;
		var elapsed = gameTime.ElapsedGameTime.TotalSeconds;

		_frames.AddFrame(elapsed, _lastUpdateMs, drawMs);
		_titleRefreshSeconds += elapsed;

		if (_titleRefreshSeconds >= 1.0)
		{
			_titleRefreshSeconds = 0;
			CaptureWindowState(preferClientBounds: true);
			UpdateTitle();
		}

		if (!_firstFrameLogged)
		{
			Log.Info("Render", "First frame presented");
			_firstFrameLogged = true;
		}

		base.Draw(gameTime);
	}

	private void CaptureScreenshot(GameTime gameTime, string path)
	{
		if (_renderer is null)
		{
			throw new InvalidOperationException("Renderer is not initialized.");
		}

		var fullPath = Path.GetFullPath(path);
		var directory = Path.GetDirectoryName(fullPath);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		using var target = new RenderTarget2D(
			GraphicsDevice,
			ScreenshotWidth,
			ScreenshotHeight,
			mipMap: false,
			SurfaceFormat.Color,
			DepthFormat.Depth24);

		try
		{
			GraphicsDevice.SetRenderTarget(target);
			_renderer.Draw(gameTime);
		}
		finally
		{
			GraphicsDevice.SetRenderTarget(null);
		}

		using var stream = File.Create(fullPath);
		target.SaveAsPng(stream, target.Width, target.Height);
		ScreenshotSaved = true;
		Log.Info("Render", $"Screenshot saved: {fullPath}");
	}

	protected override void OnExiting(object sender, EventArgs args)
	{
		CaptureWindowState(preferClientBounds: true);
		Log.Info(
			"App",
			$"Exit: {_frames.FrameCount} frames, {_frames.LogicStepCount} logic steps, {_frames.Fps:0.0} FPS, update {_frames.SmoothedUpdateMs:0.00} ms, draw {_frames.SmoothedDrawMs:0.00} ms");
		base.OnExiting(sender, args);
	}

	private void CaptureWindowState(bool preferClientBounds)
	{
		var presentation = GraphicsDevice.PresentationParameters;
		if (presentation.BackBufferWidth > 0)
		{
			WindowWidth = presentation.BackBufferWidth;
		}

		if (presentation.BackBufferHeight > 0)
		{
			WindowHeight = presentation.BackBufferHeight;
		}

		if (preferClientBounds)
		{
			var bounds = Window.ClientBounds;
			if (bounds.Width > 0)
			{
				WindowWidth = bounds.Width;
			}

			if (bounds.Height > 0)
			{
				WindowHeight = bounds.Height;
			}
		}

		IsWindowed = !_graphics.IsFullScreen;
	}

	private void UpdateTitle()
	{
		var frameMs = _frames.Fps > 0 ? 1000.0 / _frames.Fps : 0;
		Window.Title =
			$"{GetBaseTitle()} | {WindowWidth}x{WindowHeight} {ModeText()} | {_frames.Fps:0.0} FPS | {frameMs:0.00} ms";
	}

	private string GetBaseTitle()
	{
		return $"{BootstrapInfo.Name} {BootstrapInfo.Version}";
	}

	private string ModeText()
	{
		return IsWindowed ? "windowed" : "fullscreen";
	}
}
