using Microsoft.Xna.Framework;
using VibeOpenGothic.Physics;
using NumericsVector3 = System.Numerics.Vector3;

namespace VibeOpenGothic.Tests.Unit;

public sealed class PhysicsWorldTests
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
		new(-100f, 0f, 0f),
		new(100f, 0f, 0f),
		new(100f, 200f, 0f),
		new(-100f, 200f, 0f),
	};

	private static readonly int[] WallIndices =
	{
		0, 2, 1,
		0, 3, 2,
	};

	[Fact]
	public void DynamicSphere_FallsAndRestsOnMesh()
	{
		using var world = CreateGroundWorld();
		var sphere = new BepuPhysics.Collidables.Sphere(0.5f);
		var body = BepuPhysics.BodyDescription.CreateDynamic(
			new NumericsVector3(0f, 5f, 0f),
			sphere.ComputeInertia(1f),
			world.Simulation.Shapes.Add(sphere),
			0.01f);
		var handle = world.AddDynamicBody(body, CollisionLayer.Npc);

		for (var index = 0; index < 240; index++)
		{
			world.Step(PhysicsWorld.FixedTimeStep);
		}

		var height = world.Simulation.Bodies[handle].Pose.Position.Y;
		Assert.InRange(height, 0.45f, 0.55f);
	}

	[Fact]
	public void Raycast_UsesLayerMask()
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

		Assert.True(world.Raycast(
			new Vector3(0f, 100f, 500f),
			new Vector3(0f, 100f, -500f),
			out var hit,
			CollisionLayer.StaticProp));
		Assert.Equal(CollisionLayer.StaticProp, hit.Layer);
		Assert.InRange(hit.Distance, 490f, 510f);

		Assert.False(world.Raycast(
			new Vector3(0f, 100f, 500f),
			new Vector3(0f, 100f, -500f),
			out _,
			CollisionLayer.World));
	}

	[Fact]
	public void Raycast_RotatedStaticInstance_UsesInstancePose()
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
				Matrix.CreateRotationY(MathHelper.PiOver2),
				CollisionLayer.StaticProp);
		});

		Assert.True(world.Raycast(
			new Vector3(500f, 100f, 0f),
			new Vector3(-500f, 100f, 0f),
			out var hit,
			CollisionLayer.StaticProp));
		Assert.Equal(CollisionLayer.StaticProp, hit.Layer);
	}

	[Fact]
	public void OverlapBox_FiltersLayers()
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

		var staticProps = new List<PhysicsOverlap>();
		var staticCount = world.OverlapBox(
			new Vector3(0f, 100f, 0f),
			new Vector3(150f, 150f, 50f),
			CollisionLayer.StaticProp,
			staticProps);
		Assert.Equal(staticProps.Count, staticCount);
		Assert.Contains(staticProps, item => item.Layer == CollisionLayer.StaticProp);
	}

	[Fact]
	public void Step_ClampsToMaxSubsteps()
	{
		using var world = CreateGroundWorld();

		world.Step(1f);

		Assert.Equal(PhysicsWorld.MaxSubsteps, world.LastSubstepCount);
		Assert.Equal(
			PhysicsWorld.MaxSubsteps * PhysicsWorld.FixedTimeStep,
			world.SimulationTime,
			6);
	}

	[Fact]
	public void Build_ReportsDegenerateTriangles()
	{
		var description = new PhysicsWorldDescription();
		description.AddMesh(
			"broken",
			new[]
			{
				Vector3.Zero,
				Vector3.UnitX,
				Vector3.UnitY,
			},
			new[] { 0, 1, 1, 0, 2, 3 },
			CollisionLayer.World);

		using var world = new PhysicsWorld(description);

		Assert.Equal(2, world.Stats.DegenerateTriangles);
		Assert.Equal(0, world.Stats.InstanceCount);
	}

	[Fact]
	public void AddMesh_RejectsNonTriangleIndexCount()
	{
		var description = new PhysicsWorldDescription();

		Assert.Throws<ArgumentException>(() => description.AddMesh(
			"bad",
			new[] { Vector3.Zero },
			new[] { 0 },
			CollisionLayer.World));
	}

	[Fact]
	public void CollisionFilter_UsesBothDirections()
	{
		var world = CollisionMasks.Filter(CollisionLayer.World);
		var prop = CollisionMasks.Filter(CollisionLayer.StaticProp);
		var npc = CollisionMasks.Filter(CollisionLayer.Npc);
		var item = CollisionMasks.Filter(CollisionLayer.Item);
		var projectile = CollisionMasks.Filter(CollisionLayer.Projectile);

		Assert.True(world.Allows(prop));
		Assert.True(world.Allows(npc));
		Assert.False(item.Allows(projectile));
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
}
