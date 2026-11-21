using Microsoft.Xna.Framework;
using VibeOpenGothic.Physics;

namespace VibeOpenGothic.Game;

/// <summary>
/// First/third-person player camera plus a debug free camera. Produces a pose
/// in engine space; the app applies it to the renderer camera.
/// </summary>
public sealed class PlayerCamera
{
	private const float EyeOffset = PlayerController.Height * 0.5f - 10f;
	private const float ThirdPersonDistance = 300f;
	private const float CameraCollisionSkin = 20f;
	private const float LookSensitivity = 0.0025f;
	private const float FreeSpeed = 900f;
	private const float PitchLimit = MathF.PI * 0.49f;

	private Vector3 _freePosition;
	private bool _freeInitialized;

	public PlayerCamera(PlayerController player)
	{
		ArgumentNullException.ThrowIfNull(player);

		Yaw = player.Yaw;
		Pitch = 0f;
		_freePosition = player.Position + Vector3.Up * EyeOffset;
		Mode = CameraMode.ThirdPerson;
	}

	public CameraMode Mode { get; private set; }

	public float Yaw { get; private set; }

	public float Pitch { get; private set; }

	public void Look(PlayerController player, float lookX, float lookY)
	{
		ArgumentNullException.ThrowIfNull(player);

		Yaw -= lookX * LookSensitivity;
		Pitch = Math.Clamp(
			Pitch - lookY * LookSensitivity,
			-PitchLimit,
			PitchLimit);
		player.Yaw = Yaw;
	}

	public void CycleMode()
	{
		Mode = Mode switch
		{
			CameraMode.FirstPerson => CameraMode.ThirdPerson,
			CameraMode.ThirdPerson => CameraMode.Free,
			_ => CameraMode.FirstPerson,
		};

		if (Mode == CameraMode.Free)
		{
			_freeInitialized = false;
		}
	}

	public CameraPose Update(
		PlayerController player,
		IPhysicsWorld physics,
		PlayerInput input,
		float elapsedSeconds)
	{
		ArgumentNullException.ThrowIfNull(player);
		ArgumentNullException.ThrowIfNull(physics);

		var forward = new Vector3(
			MathF.Sin(Yaw) * MathF.Cos(Pitch),
			MathF.Sin(Pitch),
			MathF.Cos(Yaw) * MathF.Cos(Pitch));
		var right = new Vector3(-MathF.Cos(Yaw), 0f, MathF.Sin(Yaw));

		if (Mode == CameraMode.Free)
		{
			if (!_freeInitialized)
			{
				_freePosition = player.Position + Vector3.Up * EyeOffset;
				_freeInitialized = true;
			}

			var speed = input.Run ? FreeSpeed * 2f : FreeSpeed;
			_freePosition +=
				(forward * input.Forward +
				right * input.Right +
				Vector3.Up * input.Vertical) *
				(speed * elapsedSeconds);
			return new CameraPose(_freePosition, _freePosition + forward * 1000f);
		}

		var eye = player.Position + Vector3.Up * EyeOffset;
		if (Mode == CameraMode.FirstPerson)
		{
			return new CameraPose(eye, eye + forward * 1000f);
		}

		var desired = eye - forward * ThirdPersonDistance;
		if (physics.Raycast(
			eye,
			desired,
			out var hit,
			CollisionLayer.Camera))
		{
			var distance = MathF.Max(30f, hit.Distance - CameraCollisionSkin);
			desired = eye - forward * distance;
		}

		return new CameraPose(desired, eye);
	}
}
