using Microsoft.Xna.Framework;
using VibeOpenGothic.Physics;

namespace VibeOpenGothic.Game;

/// <summary>
/// Kinematic player capsule moved by shape sweeps against the static world.
/// The controller owns no Bepu body; it resolves horizontal slides, step ups,
/// gravity and ground snapping itself.
/// </summary>
public sealed class PlayerController
{
	public const float Radius = 25f;
	public const float Height = 180f;
	public const float StepHeight = 30f;
	public const float GroundSnap = 20f;
	public const float WalkSpeed = 220f;
	public const float RunSpeed = 420f;
	public const float SneakSpeed = 90f;
	public const float Gravity = 981f;

	private const float MaxSlopeCosine = 0.70710678f;
	private const float Skin = 0.2f;
	private const CollisionLayer CollisionMask =
		CollisionLayer.World |
		CollisionLayer.StaticProp |
		CollisionLayer.Npc |
		CollisionLayer.Item;

	private readonly IPhysicsWorld _physics;
	private Vector3 _horizontalVelocity;
	private float _verticalVelocity;
	private bool _grounded;

	public PlayerController(
		IPhysicsWorld physics,
		Vector3 startPosition,
		Vector3 startDirection)
	{
		ArgumentNullException.ThrowIfNull(physics);
		_physics = physics;

		StartPosition = startPosition + Vector3.Up * (Height * 0.5f + 2f);
		Position = StartPosition;
		Yaw = MathF.Atan2(startDirection.X, startDirection.Z);
		SnapToGround();
	}

	public Vector3 Position { get; private set; }

	public Vector3 StartPosition { get; }

	public Vector3 Velocity => new(
		_horizontalVelocity.X,
		_verticalVelocity,
		_horizontalVelocity.Z);

	public bool IsGrounded => _grounded;

	public float Yaw { get; set; }

	public void Teleport(Vector3 position)
	{
		Position = position;
		_horizontalVelocity = Vector3.Zero;
		_verticalVelocity = 0f;
		_grounded = false;
		SnapToGround();
	}

	public void Update(PlayerInput input, float elapsedSeconds)
	{
		if (elapsedSeconds <= 0f || !float.IsFinite(elapsedSeconds))
		{
			return;
		}

		if (Position.Y < StartPosition.Y - 10000f)
		{
			Teleport(StartPosition);
			return;
		}

		var forward = new Vector3(MathF.Sin(Yaw), 0f, MathF.Cos(Yaw));
		var right = new Vector3(-MathF.Cos(Yaw), 0f, MathF.Sin(Yaw));
		var desired = forward * input.Forward + right * input.Right;
		var desiredLength = desired.Length();
		if (desiredLength > 1f)
		{
			desired /= desiredLength;
		}

		var speed = input.Sneak
			? SneakSpeed
			: input.Run
				? RunSpeed
				: WalkSpeed;
		var target = desired * speed;
		var acceleration = _grounded ? 2200f : 700f;
		_horizontalVelocity = MoveTowards(
			_horizontalVelocity,
			target,
			acceleration * elapsedSeconds);

		MoveHorizontal(
			new Vector3(
				_horizontalVelocity.X,
				0f,
				_horizontalVelocity.Z) * elapsedSeconds);

		_verticalVelocity -= Gravity * elapsedSeconds;
		MoveVertical(_verticalVelocity * elapsedSeconds);
	}

	private void SnapToGround()
	{
		if (TryGetGround(1000f, out var groundY))
		{
			Position = new Vector3(
				Position.X,
				groundY + Height * 0.5f + Skin,
				Position.Z);
			_verticalVelocity = 0f;
			_grounded = true;
		}
	}

	private bool TryGetGround(float extraDistance, out float groundY)
	{
		groundY = 0f;
		if (_physics.Raycast(
			Position,
			Position + Vector3.Down * (Height * 0.5f + GroundSnap + extraDistance),
			out var hit,
			CollisionMask) &&
			hit.Normal.Y >= MaxSlopeCosine)
		{
			groundY = hit.Position.Y;
			return true;
		}

		return false;
	}

	private void MoveHorizontal(Vector3 displacement)
	{
		var remaining = displacement;
		for (var iteration = 0; iteration < 4; iteration++)
		{
			var distance = remaining.Length();
			if (distance <= 1e-4f)
			{
				return;
			}

			var direction = remaining / distance;
			if (!_physics.CapsuleCast(
				Position,
				Radius,
				Height,
				direction,
				distance,
				out var hit,
				CollisionMask))
			{
				Position += remaining;
				return;
			}

			if (hit.Distance > Skin)
			{
				Position += direction * (hit.Distance - Skin);
			}

			if (_grounded && hit.Normal.Y < MaxSlopeCosine &&
				TryStepUp(remaining, out var stepPosition))
			{
				Position = stepPosition;
				return;
			}

			if (hit.Distance <= 1e-4f && hit.Normal.LengthSquared() <= 1e-8f)
			{
				Position += Vector3.Up * (Skin * 2f);
				continue;
			}

			var into = Vector3.Dot(remaining, hit.Normal);
			if (into >= 0f)
			{
				return;
			}

			remaining -= hit.Normal * into;
			var velocityInto = Vector3.Dot(_horizontalVelocity, hit.Normal);
			if (velocityInto < 0f)
			{
				_horizontalVelocity -= hit.Normal * velocityInto;
			}
		}
	}

	private bool TryStepUp(Vector3 displacement, out Vector3 result)
	{
		result = Position;
		var distance = displacement.Length();
		if (distance <= 1e-4f)
		{
			return false;
		}

		var lifted = Position + Vector3.Up * StepHeight;
		var direction = displacement / distance;
		var stepDistance = distance + Radius;
		if (_physics.CapsuleCast(
			lifted,
			Radius,
			Height,
			direction,
			stepDistance,
			out _,
			CollisionMask))
		{
			return false;
		}

		var forwardPosition = lifted + direction * stepDistance;
		if (!_physics.CapsuleCast(
			forwardPosition,
			Radius,
			Height,
			Vector3.Down,
			StepHeight + GroundSnap,
			out var downHit,
			CollisionMask) ||
			downHit.Normal.Y < MaxSlopeCosine)
		{
			return false;
		}

		var landing = forwardPosition +
			Vector3.Down * MathF.Max(0f, downHit.Distance - Skin);
		if (landing.Y <= Position.Y + 0.5f)
		{
			return false;
		}

		result = landing;
		_verticalVelocity = 0f;
		_grounded = true;
		return true;
	}

	private void MoveVertical(float displacement)
	{
		if (displacement >= 0f)
		{
			Position += Vector3.Up * displacement;
			return;
		}

		var downDistance = -displacement;
		var groundSearch = downDistance + (_grounded ? GroundSnap : 0f);
		if (TryGetGround(groundSearch, out var groundY))
		{
			Position = new Vector3(
				Position.X,
				groundY + Height * 0.5f + Skin,
				Position.Z);
			_verticalVelocity = 0f;
			_grounded = true;
			return;
		}

		Position += Vector3.Down * downDistance;
		_grounded = false;
	}

	private static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDelta)
	{
		var delta = target - current;
		var distance = delta.Length();
		if (distance <= maxDelta || distance <= 1e-6f)
		{
			return target;
		}

		return current + delta / distance * maxDelta;
	}
}
