using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Core;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// Low-level forward renderer for the stock 3D effects. Callers provide converted
/// <see cref="DrMeshPart"/> / <see cref="DrMesh"/> objects and a world transform;
/// this class chooses the effect, owns frustum/distance culling and records frame
/// stats.
/// </summary>
public sealed class ForwardRenderer : IDisposable
{
	private static readonly Color FogColor = new(92, 103, 112);
	private const float SkinnedBoundsMargin = 50f;

	private readonly GraphicsDevice _device;
	private readonly GpuTextureCache _textures;
	private readonly BasicEffect _basic;
	private readonly AlphaTestEffect _alphaTest;
	private readonly SkinnedEffect _skinned;
	private BoundingFrustum _frustum = new(Matrix.Identity);
	private DepthStencilState _savedDepthStencilState = null!;
	private BlendState _savedBlendState = null!;
	private RasterizerState _savedRasterizerState = null!;
	private SamplerState _savedSamplerState0 = null!;
	private bool _frameActive;
	private bool _disposed;

	public ForwardRenderer(GraphicsDevice device, GpuTextureCache textures)
	{
		ArgumentNullException.ThrowIfNull(device);
		ArgumentNullException.ThrowIfNull(textures);

		_device = device;
		_textures = textures;

		_basic = new BasicEffect(device)
		{
			PreferPerPixelLighting = false,
			TextureEnabled = false,
			LightingEnabled = true,
			VertexColorEnabled = false,
			FogEnabled = true,
			FogColor = FogColor.ToVector3(),
			FogStart = 15000f,
			FogEnd = 140000f,
		};
		_basic.EnableDefaultLighting();
		_basic.AmbientLightColor = new Vector3(0.62f, 0.64f, 0.68f);

		_alphaTest = new AlphaTestEffect(device)
		{
			AlphaFunction = CompareFunction.Greater,
			ReferenceAlpha = 128,
			FogEnabled = true,
			FogColor = FogColor.ToVector3(),
			FogStart = 15000f,
			FogEnd = 140000f,
		};

		_skinned = new SkinnedEffect(device)
		{
			WeightsPerVertex = 4,
			PreferPerPixelLighting = false,
			FogEnabled = true,
			FogColor = FogColor.ToVector3(),
			FogStart = 15000f,
			FogEnd = 140000f,
		};
		_skinned.EnableDefaultLighting();
		_skinned.AmbientLightColor = new Vector3(0.62f, 0.64f, 0.68f);
	}

	public int DrawCalls { get; private set; }

	public int Polygons { get; private set; }

	public int StaticDrawCalls { get; private set; }

	public int SkinnedDrawCalls { get; private set; }

	public int CulledBatches { get; private set; }

	public int CulledInstances { get; private set; }

	public int InstancedDrawCalls { get; private set; }

	/// <summary>
	/// Frustum culling distance in world units (cm). Applies to every draw path;
	/// fog range is configured separately.
	/// </summary>
	public float MaxDrawDistance { get; set; } = 140000f;

	private Vector3 _cameraPosition;

	private bool IsVisible(BoundingBox bounds)
	{
		if (!_frustum.Intersects(bounds))
		{
			return false;
		}

		var closest = Vector3.Clamp(_cameraPosition, bounds.Min, bounds.Max);
		return Vector3.DistanceSquared(closest, _cameraPosition) <=
			MaxDrawDistance * MaxDrawDistance;
	}

	public void BeginFrame(Matrix view, Matrix projection)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (_frameActive)
		{
			throw new InvalidOperationException("A forward-render frame is already active.");
		}

		DrawCalls = 0;
		Polygons = 0;
		StaticDrawCalls = 0;
		SkinnedDrawCalls = 0;
		CulledBatches = 0;
		CulledInstances = 0;
		InstancedDrawCalls = 0;
		_frustum = new BoundingFrustum(view * projection);
		_cameraPosition = Matrix.Invert(view).Translation;

		_savedDepthStencilState = _device.DepthStencilState;
		_savedBlendState = _device.BlendState;
		_savedRasterizerState = _device.RasterizerState;
		_savedSamplerState0 = _device.SamplerStates[0];
		_frameActive = true;

		_device.DepthStencilState = DepthStencilState.Default;
		_device.BlendState = BlendState.Opaque;
		_device.SamplerStates[0] = SamplerState.LinearWrap;

		_basic.View = view;
		_basic.Projection = projection;
		_alphaTest.View = view;
		_alphaTest.Projection = projection;
		_skinned.View = view;
		_skinned.Projection = projection;
	}

	public void EndFrame()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (!_frameActive)
		{
			return;
		}

		_device.DepthStencilState = _savedDepthStencilState;
		_device.BlendState = _savedBlendState;
		_device.RasterizerState = _savedRasterizerState;
		_device.SamplerStates[0] = _savedSamplerState0;
		_frameActive = false;
	}

	/// <summary>
	/// Culls and draws one mesh part using its local bounds transformed by
	/// <paramref name="transform"/>. Skinned parts get a small animation margin.
	/// </summary>
	/// <returns>True when the part was submitted.</returns>
	public bool DrawMeshPart(
		DrMeshPart part,
		Matrix transform,
		Matrix[]? skinTransforms = null)
	{
		ArgumentNullException.ThrowIfNull(part);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var bounds = GetDrawBounds(part, transform);
		if (!IsVisible(bounds))
		{
			CulledBatches++;
			return false;
		}

		DrawMeshPartCore(part, transform, skinTransforms);
		return true;
	}

	/// <summary>
	/// Culls a spatial cell and dispatches its visible instanced batches.
	/// </summary>
	public void DrawInstanced(SpatialCell cell)
	{
		ArgumentNullException.ThrowIfNull(cell);
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!IsVisible(cell.Bounds))
		{
			CulledBatches += cell.PartCount;
			CulledInstances += cell.InstanceCount;
			return;
		}

		foreach (var batch in cell.Parts)
		{
			DrawInstanced(batch);
		}
	}

	/// <summary>
	/// Temporary non-instanced batch path. Culls the batch and each instance,
	/// submits visible instances through <see cref="DrawMeshPartCore"/> and
	/// records one potential instanced draw for the batch. Replace the loop with
	/// hardware instancing once the instanced effect exists.
	/// </summary>
	/// <returns>The number of instances submitted.</returns>
	public int DrawInstanced(InstancedMeshPart batch)
	{
		ArgumentNullException.ThrowIfNull(batch);
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!IsVisible(batch.Bounds))
		{
			CulledBatches++;
			CulledInstances += batch.Count;
			return 0;
		}

		var visible = 0;
		for (var index = 0; index < batch.Count; index++)
		{
			if (!IsVisible(batch.InstanceBounds[index]))
			{
				CulledInstances++;
				continue;
			}

			DrawMeshPartCore(batch.Part, batch.Transforms[index]);
			visible++;
		}

		if (visible > 0)
		{
			InstancedDrawCalls++;
		}

		return visible;
	}

	private void DrawMeshPartCore(
		DrMeshPart part,
		Matrix transform,
		Matrix[]? skinTransforms = null)
	{
		var material = GetMaterial(part);

		if (part.Skin is not null)
		{
			if (skinTransforms is null)
			{
				throw new ArgumentException(
					"Skinned mesh parts require bone transforms.",
					nameof(skinTransforms));
			}

			if (skinTransforms.Length != part.Skin.Joints.Length)
			{
				throw new ArgumentException(
					"Bone transform count does not match the skin joint count.",
					nameof(skinTransforms));
			}

			DrawSkinned(material, transform, skinTransforms);
			SkinnedDrawCalls++;
		}
		else
		{
			DrawStatic(material, transform);
			StaticDrawCalls++;
		}

		DrawIndexed(
			_device,
			part.PrimitiveType,
			part.VertexBuffer,
			part.IndexBuffer,
			part.VertexOffset,
			part.StartIndex,
			part.PrimitiveCount);

		DrawCalls++;
		Polygons += part.PrimitiveCount;
	}

	private static BoundingBox GetDrawBounds(DrMeshPart part, Matrix transform)
	{
		var bounds = transform == Matrix.Identity
			? part.BoundingBox
			: part.BoundingBox.Transform(transform);
		return part.Skin is not null
			? bounds.Expand(SkinnedBoundsMargin)
			: bounds;
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_basic.Dispose();
		_alphaTest.Dispose();
		_skinned.Dispose();
	}

	private void DrawStatic(GothicMaterial material, Matrix transform)
	{
		var texture = material.Texture;
		_device.RasterizerState = material.IsTwoSided
			? RasterizerState.CullNone
			: RasterizerState.CullCounterClockwise;
		_device.BlendState = BlendState.Opaque;

		var color = material.Color.ToVector3();
		if (material.AlphaMode == MaterialAlphaMode.Solid)
		{
			_basic.World = transform;
			_basic.LightingEnabled = true;
			_basic.TextureEnabled = texture is not null;
			_basic.Texture = texture;
			_basic.DiffuseColor = color;
			_basic.Alpha = material.AlphaWeight;
			_basic.CurrentTechnique.Passes[0].Apply();
		}
		else
		{
			_alphaTest.World = transform;
			_alphaTest.Texture = texture ?? _textures.Placeholder;
			_alphaTest.DiffuseColor = color;
			_alphaTest.Alpha = material.AlphaWeight;
			_alphaTest.CurrentTechnique.Passes[0].Apply();
		}
	}

	private void DrawSkinned(
		GothicMaterial material,
		Matrix transform,
		Matrix[] skinTransforms)
	{
		var texture = material.Texture;
		_device.RasterizerState = material.IsTwoSided
			? RasterizerState.CullNone
			: RasterizerState.CullCounterClockwise;
		_device.BlendState = material.AlphaMode == MaterialAlphaMode.Solid
			? BlendState.Opaque
			: BlendState.AlphaBlend;

		_skinned.World = transform;
		_skinned.Texture = texture ?? _textures.White;
		_skinned.DiffuseColor = material.Color.ToVector3();
		_skinned.SetBoneTransforms(skinTransforms);
		_skinned.CurrentTechnique.Passes[0].Apply();
	}

	private static void DrawIndexed(
		GraphicsDevice device,
		PrimitiveType primitiveType,
		VertexBuffer vertexBuffer,
		IndexBuffer indexBuffer,
		int vertexOffset,
		int startIndex,
		int primitiveCount)
	{
		device.SetVertexBuffer(vertexBuffer);
		device.Indices = indexBuffer;
		device.DrawIndexedPrimitives(
			primitiveType,
			vertexOffset,
			startIndex,
			primitiveCount);
	}

	private static GothicMaterial GetMaterial(DrMeshPart part)
	{
		return part.Tag is DrMeshPartTag tag
			? tag.Material
			: GothicMaterial.Default;
	}
}
