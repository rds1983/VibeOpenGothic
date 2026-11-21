using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using VibeOpenGothic.Core.Logging;
using VibeOpenGothic.Core.Timing;
using VibeOpenGothic.Game;
using VibeOpenGothic.Physics;
using VibeOpenGothic.Rendering;
using VibeOpenGothic.Ui;
using VibeOpenGothic.World;
using ZenKit;
using XnaGame = Microsoft.Xna.Framework.Game;

namespace VibeOpenGothic.App;

/// <summary>
/// MonoGame host. T1.5 owns the window and renderer; world loading arrives in
/// later tasks.
/// </summary>
public sealed class GothicGame : XnaGame
{
	private const int ScreenshotWidth = 1280;
	private const int ScreenshotHeight = 720;
	private const string DefaultShowcaseModel = "Orc.mds";

	private static readonly TimeSpan LogicStep = TimeSpan.FromSeconds(1.0 / 60.0);

	private readonly GraphicsDeviceManager _graphics;
	private readonly GothicGameOptions _options;
	private readonly FixedStepRunner _fixedStep = new(LogicStep, maxStepsPerFrame: 4);
	private readonly FrameStats _frames = new();

	private GothicRenderer? _renderer;
	private PhysicsWorld? _physics;
	private PlayerController? _player;
	private PlayerCamera? _playerCamera;
	private PlayerInput _playerInput;
	private MouseState _lastMouse;
	private bool _cameraModeWasDown;
	private UiRoot? _ui;
	private double _lastUpdateMs;
	private double _titleRefreshSeconds;
	private double _debugStatsSeconds;
	private bool _firstFrameLogged;
	private bool _screenshotAttempted;
	private bool _debugToggleWasDown;

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

		if (OperatingSystem.IsWindows())
		{
			NativeMethods.SetForegroundWindow(Window.Handle);
		}
	}

	protected override void LoadContent()
	{
		base.LoadContent();

		var worldName = _options.CommandLine.StartupWorld ?? _options.Install.DefaultWorldName;
		var gameVersion = _options.Install.Version.IsGothic1
			? GameVersion.Gothic1
			: GameVersion.Gothic2;
		var spawnModel = _options.CommandLine.SpawnModel;
		if (string.IsNullOrWhiteSpace(spawnModel) &&
			string.IsNullOrWhiteSpace(_options.CommandLine.ScreenshotPath))
		{
			// TODO(T3.2): Remove once script startup spawns real NPCs.
			spawnModel = DefaultShowcaseModel;
			Log.Info("World", $"Showing default skinned-model showcase '{spawnModel}'.");
		}

		var world = WorldLoader.Load(
			_options.Vfs,
			worldName,
			gameVersion,
			message => Log.Info("World", message),
			spawnModel);
		_physics = WorldPhysicsBuilder.Build(
			world,
			message => Log.Info("Physics", message));
		_renderer = new GothicRenderer(GraphicsDevice, world);
		_ui = new UiRoot(this);

		if (string.IsNullOrWhiteSpace(_options.CommandLine.ScreenshotPath))
		{
			var start = world.StartPosition ??
				(world.Bounds.Min + world.Bounds.Max) * 0.5f;
			var direction = world.StartDirection;
			if (direction.LengthSquared() <= 0f)
			{
				direction = -Vector3.UnitZ;
			}

			_player = new PlayerController(
				_physics,
				start,
				Vector3.Normalize(direction));
			_playerCamera = new PlayerCamera(_player);
			_lastMouse = Mouse.GetState();
			UpdatePlayerCamera(_playerInput, 0f);
		}
	}

	protected override void UnloadContent()
	{
		_ui?.Dispose();
		_ui = null;
		_renderer?.Dispose();
		_renderer = null;
		_playerCamera = null;
		_player = null;
		_physics?.Dispose();
		_physics = null;
		base.UnloadContent();
	}

	protected override void Update(GameTime gameTime)
	{
		var updateStart = Stopwatch.GetTimestamp();

		var keyboard = Keyboard.GetState();
		if (keyboard.IsKeyDown(Keys.Escape) ||
			GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
		{
			Exit();
		}

		var debugToggle = keyboard.IsKeyDown(Keys.F3);
		if (debugToggle && !_debugToggleWasDown)
		{
			_ui?.ToggleDebugOverlay();
		}

		_debugToggleWasDown = debugToggle;

		var cameraMode = keyboard.IsKeyDown(Keys.F4);
		if (cameraMode && !_cameraModeWasDown && _playerCamera is not null)
		{
			_playerCamera.CycleMode();
		}

		_cameraModeWasDown = cameraMode;

		var mouse = Mouse.GetState();
		var lookX = 0f;
		var lookY = 0f;
		if (IsActive)
		{
			lookX = mouse.X - _lastMouse.X;
			lookY = mouse.Y - _lastMouse.Y;
		}

		_lastMouse = mouse;
		var forward = (keyboard.IsKeyDown(Keys.W) ? 1f : 0f) -
			(keyboard.IsKeyDown(Keys.S) ? 1f : 0f);
		var right = (keyboard.IsKeyDown(Keys.D) ? 1f : 0f) -
			(keyboard.IsKeyDown(Keys.A) ? 1f : 0f);
		var vertical = (keyboard.IsKeyDown(Keys.E) ? 1f : 0f) -
			(keyboard.IsKeyDown(Keys.Q) ? 1f : 0f);
		_playerInput = new PlayerInput(
			forward,
			right,
			vertical,
			keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift),
			keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl),
			lookX,
			lookY);

		if (_player is not null && _playerCamera is not null)
		{
			_playerCamera.Look(_player, lookX, lookY);
		}

		var elapsed = gameTime.ElapsedGameTime.TotalSeconds;
		var input = _playerInput;
		_fixedStep.Advance(elapsed, _ =>
		{
			_frames.AddLogicStep();
			if (_player is not null &&
				_playerCamera?.Mode != CameraMode.Free)
			{
				_player.Update(input, (float)LogicStep.TotalSeconds);
			}

			_physics?.Step((float)LogicStep.TotalSeconds);
		});
		UpdatePlayerCamera(input, (float)elapsed);

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

		if (_ui is not null && _renderer is not null)
		{
			// Stats are refreshed a few times per second; rebuilding label
			// strings every frame would allocate in the render loop.
			_debugStatsSeconds += elapsed;
			if (_ui.DebugOverlayVisible && _debugStatsSeconds >= 0.25)
			{
				_debugStatsSeconds = 0;
				_ui.SetDebugStats(
					(float)_frames.Fps,
					_renderer.LastDrawCalls,
					_renderer.LastDrawnPolygons,
					_renderer.LastCulledBatches,
					_renderer.LastCulledInstances,
					_renderer.LastInstancedDrawCalls);
			}

			_ui.Render();
		}

		if (_titleRefreshSeconds >= 1.0)
		{
			_titleRefreshSeconds = 0;
			CaptureWindowState(preferClientBounds: true);
			UpdateTitle();
		}

		if (!_firstFrameLogged)
		{
			if (_renderer is not null)
			{
				Log.Info(
					"Render",
					$"First frame: {_renderer.LastDrawCalls} draws, " +
					$"{_renderer.LastDrawnPolygons} polygons, " +
					$"{_renderer.LastInstancedDrawCalls} instanced draws, " +
					$"{_renderer.LastCulledBatches} culled batches, " +
					$"{_renderer.LastCulledInstances} culled instances");
			}

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
			if (_ui is not null)
			{
				_ui.SetDebugStats(
					(float)_frames.Fps,
					_renderer.LastDrawCalls,
					_renderer.LastDrawnPolygons,
					_renderer.LastCulledBatches,
					_renderer.LastCulledInstances,
					_renderer.LastInstancedDrawCalls);
				_ui.Render();
			}
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

	private void UpdatePlayerCamera(PlayerInput input, float elapsedSeconds)
	{
		if (_player is null ||
			_playerCamera is null ||
			_physics is null ||
			_renderer is null)
		{
			return;
		}

		var pose = _playerCamera.Update(
			_player,
			_physics,
			input,
			elapsedSeconds);
		_renderer.Camera.LookAt(pose.Position, pose.Target);
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

	private static class NativeMethods
	{
		[DllImport("user32.dll")]
		public static extern bool SetForegroundWindow(IntPtr window);
	}
}
