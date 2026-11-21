using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Assets.Materials;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// Low-level forward renderer for the stock 3D effects. Callers provide converted
/// <see cref="DrMeshPart"/> / <see cref="DrMesh"/> objects and a world transform;
/// this class chooses the effect, applies material state and records frame stats.
/// </summary>
public sealed class ForwardRenderer : IDisposable
{
	private static readonly Color FogColor = new(92, 103, 112);

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

	public Vector3 CameraPosition { get; private set; }

	public void RecordCulledBatch()
	{
		CulledBatches++;
	}

	public void RecordCulledInstance()
	{
		CulledInstances++;
	}

	public bool IsVisible(BoundingBox? bounds, float maxDistance)
	{
		if (bounds is null)
		{
			return true;
		}

		var value = bounds.Value;
		if (!_frustum.Intersects(value))
		{
			return false;
		}

		var closest = Vector3.Clamp(CameraPosition, value.Min, value.Max);
		return Vector3.DistanceSquared(closest, CameraPosition) <= maxDistance * maxDistance;
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
		_frustum = new BoundingFrustum(view * projection);
		CameraPosition = Matrix.Invert(view).Translation;

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

	public void DrawMeshPart(
		DrMeshPart part,
		Matrix transform,
		Matrix[]? skinTransforms = null)
	{
		ArgumentNullException.ThrowIfNull(part);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var material = GetMaterial(part);
		var texture = material.Texture;

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

			DrawSkinned(material, texture, transform, skinTransforms);
			SkinnedDrawCalls++;
		}
		else
		{
			DrawStatic(material, texture, transform);
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

	private void DrawStatic(
		GothicMaterial material,
		Texture2D? texture,
		Matrix transform)
	{
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
		Texture2D? texture,
		Matrix transform,
		Matrix[] skinTransforms)
	{
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
