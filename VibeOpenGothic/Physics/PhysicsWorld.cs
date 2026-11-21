using System.Diagnostics;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuPhysics.Trees;
using BepuUtilities;
using BepuUtilities.Collections;
using BepuUtilities.Memory;
using Microsoft.Xna.Framework;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsVector3 = System.Numerics.Vector3;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;

namespace VibeOpenGothic.Physics;

public readonly record struct PhysicsBuildStats(
	int MeshCount,
	int TriangleCount,
	int DegenerateTriangles,
	int InstanceCount,
	int SkippedInstances,
	int NonUnitScaleInstances,
	double BuildMilliseconds);

/// <summary>
/// One Bepu simulation per world. The public API uses engine centimetres; Bepu
/// data and gravity are in metres and converted at this boundary.
/// </summary>
public sealed unsafe class PhysicsWorld : IPhysicsWorld, IDisposable
{
	public const float FixedTimeStep = 1f / 60f;
	public const int MaxSubsteps = 5;
	private const float CmToMetres = 0.01f;
	private const double FixedTimeStepSeconds = 1.0 / 60.0;

	private readonly BufferPool _pool;
	private readonly CollidableProperty<CollisionFilter> _filters;
	private double _accumulator;
	private bool _disposed;

	public PhysicsWorld(
		PhysicsWorldDescription description,
		Action<string>? progress = null)
	{
		ArgumentNullException.ThrowIfNull(description);

		_pool = new BufferPool();
		_filters = new CollidableProperty<CollisionFilter>(_pool);
		Simulation = Simulation.Create(
			_pool,
			new NarrowPhaseCallbacks(_filters),
			new PoseIntegratorCallbacks(new NumericsVector3(0f, -9.81f, 0f)),
			new SolveDescription(8, 1));
		_filters.Initialize(Simulation);

		var timer = Stopwatch.StartNew();
		var shapes = BuildMeshes(description, progress, out var meshCount, out var triangleCount, out var degenerate);
		var instanceCount = AddInstances(
			description,
			shapes,
			progress,
			out var skippedInstances,
			out var nonUnitScaleInstances);
		Stats = new PhysicsBuildStats(
			meshCount,
			triangleCount,
			degenerate,
			instanceCount,
			skippedInstances,
			nonUnitScaleInstances,
			timer.Elapsed.TotalMilliseconds);

		progress?.Invoke(
			$"Collision world built: {meshCount} meshes, {triangleCount} triangles, " +
			$"{instanceCount} statics in {timer.Elapsed.TotalMilliseconds:0} ms");
	}

	public Simulation Simulation { get; }

	public PhysicsBuildStats Stats { get; }

	public int LastSubstepCount { get; private set; }

	public double SimulationTime { get; private set; }

	internal CollidableProperty<CollisionFilter> Filters => _filters;

	public void Step(float elapsedSeconds)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		LastSubstepCount = 0;
		if (elapsedSeconds <= 0f || !float.IsFinite(elapsedSeconds))
		{
			return;
		}

		_accumulator += elapsedSeconds;
		var maximumAccumulator = FixedTimeStepSeconds * MaxSubsteps;
		if (_accumulator > maximumAccumulator)
		{
			_accumulator = maximumAccumulator;
		}

		while (_accumulator + 1e-9 >= FixedTimeStepSeconds &&
			LastSubstepCount < MaxSubsteps)
		{
			Simulation.Timestep(FixedTimeStep);
			_accumulator -= FixedTimeStepSeconds;
			SimulationTime += FixedTimeStepSeconds;
			LastSubstepCount++;
		}
	}

	public BodyHandle AddDynamicBody(
		BodyDescription description,
		CollisionLayer layer)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var handle = Simulation.Bodies.Add(description);
		_filters.Allocate(handle) = CollisionMasks.Filter(layer);
		return handle;
	}

	public bool Raycast(
		XnaVector3 from,
		XnaVector3 to,
		out PhysicsHit hit,
		CollisionLayer mask)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var delta = to - from;
		var distance = delta.Length();
		if (distance <= 1e-4f || mask == CollisionLayer.None)
		{
			hit = default;
			return false;
		}

		var direction = delta / distance;
		var handler = new RayHitHandler
		{
			Filters = _filters,
			Mask = mask,
			T = float.MaxValue,
		};
		Simulation.RayCast(
			ToMetres(from),
			ToNumerics(direction),
			distance * CmToMetres,
			ref handler);

		if (!handler.Hit)
		{
			hit = default;
			return false;
		}

		var hitDistance = handler.T / CmToMetres;
		hit = new PhysicsHit(
			from + direction * hitDistance,
			new XnaVector3(handler.Normal.X, handler.Normal.Y, handler.Normal.Z),
			hitDistance,
			handler.Layer);
		return true;
	}

	public int OverlapBox(
		XnaVector3 center,
		XnaVector3 halfExtents,
		CollisionLayer mask,
		List<PhysicsOverlap>? results = null)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		results?.Clear();

		var enumerator = new OverlapEnumerator
		{
			Filters = _filters,
			Mask = mask,
			Results = results,
		};
		Simulation.BroadPhase.GetOverlaps(
			ToMetres(center - halfExtents),
			ToMetres(center + halfExtents),
			ref enumerator);
		return enumerator.Count;
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		Simulation.Dispose();
		_filters.Dispose();
		_pool.Clear();
	}

	private Dictionary<string, TypedIndex> BuildMeshes(
		PhysicsWorldDescription description,
		Action<string>? progress,
		out int meshCount,
		out int triangleCount,
		out int degenerateTriangles)
	{
		var shapes = new Dictionary<string, TypedIndex>(StringComparer.OrdinalIgnoreCase);
		meshCount = 0;
		triangleCount = 0;
		degenerateTriangles = 0;

		foreach (var source in description.Meshes)
		{
			if (shapes.ContainsKey(source.Key))
			{
				progress?.Invoke($"Duplicate collision mesh '{source.Key}' ignored.");
				continue;
			}

			var triangles = new List<Triangle>(source.Indices.Length / 3);
			for (var index = 0; index < source.Indices.Length; index += 3)
			{
				var aIndex = source.Indices[index];
				var bIndex = source.Indices[index + 1];
				var cIndex = source.Indices[index + 2];
				if ((uint)aIndex >= (uint)source.Vertices.Length ||
					(uint)bIndex >= (uint)source.Vertices.Length ||
					(uint)cIndex >= (uint)source.Vertices.Length)
				{
					degenerateTriangles++;
					continue;
				}

				var a = ToMetres(source.Vertices[aIndex]);
				var b = ToMetres(source.Vertices[bIndex]);
				var c = ToMetres(source.Vertices[cIndex]);
				if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c) ||
					NumericsVector3.Cross(b - a, c - a).LengthSquared() <= 1e-10f)
				{
					degenerateTriangles++;
					continue;
				}

				triangles.Add(new Triangle(a, b, c));
			}

			if (triangles.Count == 0)
			{
				progress?.Invoke($"Collision mesh '{source.Key}' has no valid triangles.");
				continue;
			}

			_pool.Take<Triangle>(triangles.Count, out var buffer);
			for (var index = 0; index < triangles.Count; index++)
			{
				buffer[index] = triangles[index];
			}

			var mesh = new Mesh(buffer, new NumericsVector3(1f), _pool);
			shapes.Add(source.Key, Simulation.Shapes.Add(mesh));
			meshCount++;
			triangleCount += triangles.Count;
		}

		return shapes;
	}

	private int AddInstances(
		PhysicsWorldDescription description,
		IReadOnlyDictionary<string, TypedIndex> shapes,
		Action<string>? progress,
		out int skippedInstances,
		out int nonUnitScaleInstances)
	{
		var instanceCount = 0;
		skippedInstances = 0;
		nonUnitScaleInstances = 0;

		foreach (var instance in description.Instances)
		{
			if (!shapes.TryGetValue(instance.MeshKey, out var shape))
			{
				skippedInstances++;
				continue;
			}

			if (!instance.Transform.Decompose(
				out var scale,
				out var rotation,
				out var translation))
			{
				skippedInstances++;
				continue;
			}

			if (MathF.Abs(scale.X - 1f) > 1e-3f ||
				MathF.Abs(scale.Y - 1f) > 1e-3f ||
				MathF.Abs(scale.Z - 1f) > 1e-3f)
			{
				nonUnitScaleInstances++;
				continue;
			}

			var pose = new RigidPose(
				ToMetres(translation),
				new NumericsQuaternion(
					rotation.X,
					rotation.Y,
					rotation.Z,
					rotation.W));
			var handle = Simulation.Statics.Add(new StaticDescription(pose, shape));
			_filters.Allocate(handle) = CollisionMasks.Filter(instance.Layer);
			instanceCount++;
		}

		if (skippedInstances > 0)
		{
			progress?.Invoke($"Skipped {skippedInstances} collision instances.");
		}

		if (nonUnitScaleInstances > 0)
		{
			progress?.Invoke(
				$"Skipped {nonUnitScaleInstances} non-unit-scale collision instances.");
		}

		return instanceCount;
	}

	private static NumericsVector3 ToMetres(XnaVector3 value)
	{
		return new NumericsVector3(
			value.X * CmToMetres,
			value.Y * CmToMetres,
			value.Z * CmToMetres);
	}

	private static NumericsVector3 ToNumerics(XnaVector3 value)
	{
		return new NumericsVector3(value.X, value.Y, value.Z);
	}

	private static bool IsFinite(NumericsVector3 value)
	{
		return float.IsFinite(value.X) &&
			float.IsFinite(value.Y) &&
			float.IsFinite(value.Z);
	}

	private unsafe struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
	{
		private readonly CollidableProperty<CollisionFilter> _filters;

		public NarrowPhaseCallbacks(CollidableProperty<CollisionFilter> filters)
		{
			_filters = filters;
		}

		public void Initialize(Simulation simulation)
		{
		}

		public bool AllowContactGeneration(
			int workerIndex,
			CollidableReference a,
			CollidableReference b,
			ref float speculativeMargin)
		{
			return _filters[a].Allows(_filters[b]);
		}

		public bool AllowContactGeneration(
			int workerIndex,
			CollidablePair pair,
			int childIndexA,
			int childIndexB)
		{
			return true;
		}

		public bool ConfigureContactManifold<TManifold>(
			int workerIndex,
			CollidablePair pair,
			ref TManifold manifold,
			out PairMaterialProperties pairMaterial)
			where TManifold : unmanaged, IContactManifold<TManifold>
		{
			pairMaterial = new PairMaterialProperties(
				1f,
				2f,
				new SpringSettings(30f, 1f));
			return true;
		}

		public bool ConfigureContactManifold(
			int workerIndex,
			CollidablePair pair,
			int childIndexA,
			int childIndexB,
			ref ConvexContactManifold manifold)
		{
			return true;
		}

		public void Dispose()
		{
		}
	}

	private struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
	{
		private Vector3Wide _gravityWideDt;

		public PoseIntegratorCallbacks(NumericsVector3 gravity)
		{
			Gravity = gravity;
			_gravityWideDt = default;
		}

		public NumericsVector3 Gravity;

		public readonly AngularIntegrationMode AngularIntegrationMode =>
			AngularIntegrationMode.Nonconserving;

		public readonly bool AllowSubstepsForUnconstrainedBodies => false;

		public readonly bool IntegrateVelocityForKinematics => false;

		public void Initialize(Simulation simulation)
		{
		}

		public void PrepareForIntegration(float dt)
		{
			_gravityWideDt = Vector3Wide.Broadcast(Gravity * dt);
		}

		public void IntegrateVelocity(
			Vector<int> bodyIndices,
			Vector3Wide position,
			QuaternionWide orientation,
			BodyInertiaWide localInertia,
			Vector<int> integrationMask,
			int workerIndex,
			Vector<float> dt,
			ref BodyVelocityWide velocity)
		{
			velocity.Linear += _gravityWideDt;
		}
	}

	private unsafe struct RayHitHandler : IRayHitHandler
	{
		public CollidableProperty<CollisionFilter> Filters;
		public CollisionLayer Mask;
		public bool Hit;
		public float T;
		public NumericsVector3 Normal;
		public CollisionLayer Layer;

		public bool AllowTest(CollidableReference collidable)
		{
			return (Mask & Filters[collidable].Layer) != 0;
		}

		public bool AllowTest(CollidableReference collidable, int childIndex)
		{
			return AllowTest(collidable);
		}

		public void OnRayHit(
			in RayData ray,
			ref float maximumT,
			float t,
			in NumericsVector3 normal,
			CollidableReference collidable,
			int childIndex)
		{
			if (t >= maximumT)
			{
				return;
			}

			maximumT = t;
			Hit = true;
			T = t;
			Normal = normal;
			Layer = Filters[collidable].Layer;
		}
	}

	private struct OverlapEnumerator : IBreakableForEach<CollidableReference>
	{
		public CollidableProperty<CollisionFilter> Filters;
		public CollisionLayer Mask;
		public List<PhysicsOverlap>? Results;
		public int Count;

		public bool LoopBody(CollidableReference reference)
		{
			var layer = Filters[reference].Layer;
			if ((Mask & layer) == 0)
			{
				return true;
			}

			Count++;
			Results?.Add(new PhysicsOverlap(layer));
			return true;
		}
	}
}
