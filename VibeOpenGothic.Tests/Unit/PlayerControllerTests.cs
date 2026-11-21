using Microsoft.Xna.Framework;
using VibeOpenGothic.Game;
using VibeOpenGothic.Physics;

namespace VibeOpenGothic.Tests.Unit;

public sealed class PlayerControllerTests
{
	private static readonly Vector3[] GroundVertices =
	{
		new(-1000f, 0f, -1000f),
		new(1000f, 0f, -1000f),
		new(1000f, 0f, 1000f),
		new(-1000f, 0f, 1000f),
	};

	private static readonly int[] GroundIndices =
	{
		0, 1, 2,
		0, 2, 3,
	};

	private static readonly Vector3[] WallVertices =
	{
		new(-1000f, 0f, 300f),
		new(1000f, 0f, 300f),
		new(1000f, 200f, 300f),
		new(-1000f, 200f, 300f),
	};

	private static readonly int[] WallIndices =
	{
		0, 1, 2,
		0, 2, 3,
	};

	[Fact]
	public void Spawn_SnapsToGround()
	{
		using var world = CreateGroundWorld();
		var player = new PlayerController(world, Vector3.Zero, Vector3.UnitZ);
		Advance(player, 60);

		Assert.True(player.IsGrounded);
		Assert.InRange(player.Position.Y, 89f, 91f);
	}

	[Fact]
	public void Walk_MovesAlongViewDirection()
	{
		using var world = CreateGroundWorld();
		var player = new PlayerController(world, Vector3.Zero, Vector3.UnitZ);
		Advance(player, 60);
		var start = player.Position;

		Advance(player, 60, forward: 1f);

		Assert.True(player.Position.Z > start.Z + 100f);
		Assert.InRange(player.Position.Y, 89f, 91f);
	}

	[Fact]
	public void CapsuleCast_DetectsWall()
	{
		using var world = CreateGroundWorld(description =>
		{
			description.AddMesh(
				"wall",
				WallVertices,
				WallIndices,
				CollisionLayer.StaticProp);
			description.AddInstance(
				"wall",
				Matrix.Identity,
				CollisionLayer.StaticProp);
		});

		var hit = world.CapsuleCast(
			new Vector3(0f, 90f, 200f),
			PlayerController.Radius,
			PlayerController.Height,
			Vector3.UnitZ,
			200f,
			out var result,
			CollisionLayer.World | CollisionLayer.StaticProp);

		Assert.True(hit, $"hit={result}");
	}

	[Fact]
	public void Wall_SlidesInsteadOfEntering()
	{
		using var world = CreateGroundWorld(description =>
		{
			description.AddMesh(
				"wall",
				WallVertices,
				WallIndices,
				CollisionLayer.StaticProp);
			description.AddInstance(
				"wall",
				Matrix.Identity,
				CollisionLayer.StaticProp);
		});
		var player = new PlayerController(world, Vector3.Zero, Vector3.UnitZ);
		Advance(player, 60);

		Advance(player, 180, forward: 1f, right: 1f);

		Assert.True(player.Position.Z < 280f, $"position={player.Position}");
	}

	[Fact]
	public void Step_UpLiftsPlayer()
	{
		using var world = CreateGroundWorld(description =>
		{
			AddBox(
				description,
				"step",
				new Vector3(-300f, 0f, 300f),
				new Vector3(300f, 25f, 600f),
				CollisionLayer.StaticProp);
		});
		var player = new PlayerController(world, Vector3.Zero, Vector3.UnitZ);
		Advance(player, 60);

		Advance(player, 100, forward: 1f);

		Assert.True(player.Position.Y > 110f, $"position={player.Position}");
		Assert.True(player.Position.Z > 300f, $"position={player.Position}");
	}

	[Fact]
	public void HighStep_BlocksPlayer()
	{
		using var world = CreateGroundWorld(description =>
		{
			AddBox(
				description,
				"step",
				new Vector3(-300f, 0f, 300f),
				new Vector3(300f, 50f, 600f),
				CollisionLayer.StaticProp);
		});
		var player = new PlayerController(world, Vector3.Zero, Vector3.UnitZ);
		Advance(player, 60);

		Advance(player, 180, forward: 1f);

		Assert.True(player.Position.Z < 280f, $"position={player.Position}");
		Assert.InRange(player.Position.Y, 89f, 91f);
	}

	[Fact]
	public void Look_ChangesViewDirection()
	{
		using var world = CreateGroundWorld();
		var player = new PlayerController(world, Vector3.Zero, Vector3.UnitZ);
		var camera = new PlayerCamera(player);

		camera.Look(player, 100f, 0f);

		Assert.Equal(camera.Yaw, player.Yaw);
		Assert.True(camera.Yaw < 0f);
	}

	[Fact]
	public void StrafeRight_MovesLeftOnScreen()
	{
		using var world = CreateGroundWorld();
		var player = new PlayerController(world, Vector3.Zero, Vector3.UnitZ);
		Advance(player, 60);
		var start = player.Position;

		Advance(player, 30, right: 1f);

		Assert.True(player.Position.X < start.X, $"position={player.Position}");
	}

	private static PhysicsWorld CreateGroundWorld(
		Action<PhysicsWorldDescription>? configure = null)
	{
		var description = new PhysicsWorldDescription();
		description.AddMesh(
			"ground",
			GroundVertices,
			GroundIndices,
			CollisionLayer.World);
		description.AddInstance(
			"ground",
			Matrix.Identity,
			CollisionLayer.World);
		configure?.Invoke(description);
		return new PhysicsWorld(description);
	}

	private static void AddBox(
		PhysicsWorldDescription description,
		string key,
		Vector3 min,
		Vector3 max,
		CollisionLayer layer)
	{
		var vertices = new[]
		{
			new Vector3(min.X, min.Y, min.Z),
			new Vector3(max.X, min.Y, min.Z),
			new Vector3(max.X, max.Y, min.Z),
			new Vector3(min.X, max.Y, min.Z),
			new Vector3(min.X, min.Y, max.Z),
			new Vector3(max.X, min.Y, max.Z),
			new Vector3(max.X, max.Y, max.Z),
			new Vector3(min.X, max.Y, max.Z),
		};
		var indices = new[]
		{
			0, 2, 1, 0, 3, 2,
			4, 5, 6, 4, 6, 7,
			0, 1, 5, 0, 5, 4,
			3, 2, 6, 3, 6, 7,
			0, 3, 7, 0, 7, 4,
			1, 2, 6, 1, 6, 5,
		};
		description.AddMesh(key, vertices, indices, layer);
		description.AddInstance(key, Matrix.Identity, layer);
	}

	private static void Advance(
		PlayerController player,
		int steps,
		float forward = 0f,
		float right = 0f)
	{
		var input = new PlayerInput(
			forward,
			right,
			0f,
			false,
			false,
			0f,
			0f);
		for (var index = 0; index < steps; index++)
		{
			player.Update(input, 1f / 60f);
		}
	}
}
