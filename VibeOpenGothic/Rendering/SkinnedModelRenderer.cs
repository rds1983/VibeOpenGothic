using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Assets.Models;
using VibeOpenGothic.Assets.Skins;
using VibeOpenGothic.World;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// Uploads and draws skinned MDS VOBs with stock <see cref="SkinnedEffect"/>.
/// Each VOB gets its own <see cref="DrModelInstance"/> and animation controller;
/// meshes, skins and textures are shared per unique model.
/// </summary>
public sealed class SkinnedModelRenderer : IDisposable
{
	private static readonly Color FogColor = new(92, 103, 112);

	private readonly GpuTextureCache _textures;
	private readonly SkinnedEffect _effect;
	private readonly BasicEffect _basic;
	private readonly AlphaTestEffect _alphaTest;
	private readonly Dictionary<string, ModelGpu> _models = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<ModelInstance> _instances = new();
	private Matrix _view = Matrix.Identity;
	private Matrix _projection = Matrix.Identity;
	private bool _disposed;

	public SkinnedModelRenderer(
		GraphicsDevice device,
		GpuTextureCache textures,
		WorldAsset world,
		Action<string>? progress = null)
	{
		ArgumentNullException.ThrowIfNull(device);
		ArgumentNullException.ThrowIfNull(textures);
		ArgumentNullException.ThrowIfNull(world);
		progress ??= _ => { };

		_textures = textures;
		_effect = new SkinnedEffect(device)
		{
			WeightsPerVertex = 4,
			PreferPerPixelLighting = false,
			FogEnabled = true,
			FogColor = FogColor.ToVector3(),
			FogStart = 15000f,
			FogEnd = 140000f,
		};
		_effect.EnableDefaultLighting();
		_effect.AmbientLightColor = new Vector3(0.62f, 0.64f, 0.68f);

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

		foreach (var asset in world.Models.Values)
		{
			var gpu = CreateModel(device, textures, asset, progress);
			if (gpu is not null)
			{
				_models[asset.Key] = gpu;
			}
		}

		foreach (var vob in world.ModelVobs)
		{
			if (!_models.TryGetValue(vob.ModelKey, out var gpu))
			{
				continue;
			}

			_instances.Add(new ModelInstance(gpu, vob.Transform));
		}

		progress(
			$"Skinned models uploaded: {_models.Count} models, " +
			$"{_instances.Count} instances");
	}

	public int LastDrawCalls { get; private set; }

	public int LastDrawnPolygons { get; private set; }

	public void SetCamera(Matrix view, Matrix projection)
	{
		_view = view;
		_projection = projection;
	}

	public void Draw(GameTime gameTime)
	{
		ArgumentNullException.ThrowIfNull(gameTime);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var device = _effect.GraphicsDevice;
		device.DepthStencilState = DepthStencilState.Default;
		device.BlendState = BlendState.Opaque;
		device.SamplerStates[0] = SamplerState.LinearWrap;

		var drawCalls = 0;
		var polygons = 0;
		var elapsed = gameTime.ElapsedGameTime;

		foreach (var instance in _instances)
		{
			instance.Controller?.Update(elapsed);

			foreach (var part in instance.Gpu.Parts)
			{
				var descriptor = part.Descriptor;
				var texture = part.Texture;
				var skin = part.Part.Skin;
				if (skin is null)
				{
					continue;
				}

				var matrices = instance.Transforms[part.Index];
				for (var jointIndex = 0; jointIndex < skin.Joints.Length; jointIndex++)
				{
					var joint = skin.Joints[jointIndex];
					matrices[jointIndex] =
						joint.InverseBindTransform *
						instance.Instance.GetBoneGlobalTransform(joint.Bone.Index);
				}

				_effect.World = instance.Transform;
				_effect.View = _view;
				_effect.Projection = _projection;
				_effect.Texture = texture ?? _textures.White;
				_effect.DiffuseColor = descriptor.Color.ToVector3();
				_effect.SetBoneTransforms(matrices);

				var partDevice = part.Part.VertexBuffer.GraphicsDevice;
				partDevice.RasterizerState = descriptor.IsTwoSided
					? RasterizerState.CullNone
					: RasterizerState.CullCounterClockwise;
				partDevice.BlendState = descriptor.AlphaMode == MaterialAlphaMode.Solid
					? BlendState.Opaque
					: BlendState.AlphaBlend;

				_effect.CurrentTechnique.Passes[0].Apply();
				partDevice.SetVertexBuffer(part.Part.VertexBuffer);
				partDevice.Indices = part.Part.IndexBuffer;
				partDevice.DrawIndexedPrimitives(
					part.Part.PrimitiveType,
					part.Part.VertexOffset,
					part.Part.StartIndex,
					part.Part.PrimitiveCount);

				drawCalls++;
				polygons += part.Part.PrimitiveCount;
			}

			foreach (var attachment in instance.Gpu.Attachments)
			{
				var nodeTransform = instance.Instance.GetBoneGlobalTransform(
					attachment.BoneIndex);
				var world = nodeTransform * instance.Transform;

				foreach (var part in attachment.Parts)
				{
					ApplyAttachmentMaterial(part, world);
					var partDevice = part.Part.VertexBuffer.GraphicsDevice;
					partDevice.SetVertexBuffer(part.Part.VertexBuffer);
					partDevice.Indices = part.Part.IndexBuffer;
					partDevice.DrawIndexedPrimitives(
						part.Part.PrimitiveType,
						part.Part.VertexOffset,
						part.Part.StartIndex,
						part.Part.PrimitiveCount);

					drawCalls++;
					polygons += part.Part.PrimitiveCount;
				}
			}
		}

		LastDrawCalls = drawCalls;
		LastDrawnPolygons = polygons;
		device.RasterizerState = RasterizerState.CullCounterClockwise;
		device.BlendState = BlendState.Opaque;
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_effect.Dispose();
		_basic.Dispose();
		_alphaTest.Dispose();

		foreach (var model in _models.Values)
		{
			model.Dispose();
		}

		_models.Clear();
		_instances.Clear();
	}

	private void ApplyAttachmentMaterial(ModelGpuPart part, Matrix world)
	{
		var descriptor = part.Descriptor;
		var device = part.Part.VertexBuffer.GraphicsDevice;
		device.RasterizerState = descriptor.IsTwoSided
			? RasterizerState.CullNone
			: RasterizerState.CullCounterClockwise;
		device.BlendState = BlendState.Opaque;

		if (descriptor.AlphaMode == MaterialAlphaMode.Solid)
		{
			_basic.World = world;
			_basic.View = _view;
			_basic.Projection = _projection;
			_basic.LightingEnabled = true;
			_basic.TextureEnabled = part.Texture is not null;
			_basic.Texture = part.Texture;
			_basic.DiffuseColor = descriptor.Color.ToVector3();
			_basic.Alpha = descriptor.AlphaWeight;
			_basic.CurrentTechnique.Passes[0].Apply();
		}
		else
		{
			_alphaTest.World = world;
			_alphaTest.View = _view;
			_alphaTest.Projection = _projection;
			_alphaTest.Texture = part.Texture ?? _textures.Placeholder;
			_alphaTest.DiffuseColor = descriptor.Color.ToVector3();
			_alphaTest.Alpha = descriptor.AlphaWeight;
			_alphaTest.CurrentTechnique.Passes[0].Apply();
		}
	}

	private static ModelGpu? CreateModel(
		GraphicsDevice device,
		GpuTextureCache textures,
		GothicModelAsset asset,
		Action<string> progress)
	{
		if (asset.Meshes.Count == 0 && asset.Attachments.Count == 0)
		{
			progress($"Model '{asset.Key}' has no meshes and will not be drawn.");
			return null;
		}

		var mesh = new DrMesh
		{
			Name = asset.Key,
		};
		var parts = new List<ModelGpuPart>();

		foreach (var sourceMesh in asset.Meshes)
		{
			var geometry = sourceMesh.Geometry;
			var vertices = geometry.Vertices.ToArray();
			var indices = geometry.Indices.ToArray();

			var vertexBuffer = new VertexBuffer(
				device,
				SkinnedVertex.Declaration,
				vertices.Length,
				BufferUsage.None);
			vertexBuffer.SetData(vertices);

			var indexBuffer = new IndexBuffer(
				device,
				IndexElementSize.ThirtyTwoBits,
				indices.Length,
				BufferUsage.None);
			indexBuffer.SetData(indices);

			foreach (var subMesh in geometry.SubMeshes)
			{
				var descriptor = sourceMesh.Materials.Get(subMesh.MaterialIndex);
				var texture = descriptor.Texture is null
					? null
					: textures.Get(descriptor.Texture);
				var material = new DrMaterial
				{
					Name = descriptor.Name,
					DiffuseColor = descriptor.Color,
					DiffuseTexture = texture,
					Tag = descriptor,
				};

				var part = new DrMeshPart(
					vertexBuffer,
					indexBuffer,
					subMesh.Bounds,
					PrimitiveType.TriangleList,
					vertices.Length,
					subMesh.IndexCount / 3,
					0,
					subMesh.IndexOffset)
				{
					Material = material,
					Skin = sourceMesh.Skin,
				};

				mesh.MeshParts.Add(part);
				parts.Add(new ModelGpuPart(
					parts.Count,
					part,
					descriptor,
					texture));
			}
		}

		var attachments = new List<AttachmentGpu>();
		foreach (var source in asset.Attachments)
		{
			var attachmentMesh = new DrMesh
			{
				Name = $"{asset.Key}:{source.NodeName}",
			};
			var attachmentParts = new List<ModelGpuPart>();
			var vertices = source.Mesh.Vertices.ToArray();
			var indices = source.Mesh.Indices.ToArray();

			var vertexBuffer = new VertexBuffer(
				device,
				MeshVertex.Declaration,
				vertices.Length,
				BufferUsage.None);
			vertexBuffer.SetData(vertices);

			var indexBuffer = new IndexBuffer(
				device,
				IndexElementSize.ThirtyTwoBits,
				indices.Length,
				BufferUsage.None);
			indexBuffer.SetData(indices);

			foreach (var subMesh in source.Mesh.SubMeshes)
			{
				var descriptor = source.Materials.Get(subMesh.MaterialIndex);
				var texture = descriptor.Texture is null
					? null
					: textures.Get(descriptor.Texture);
				var material = new DrMaterial
				{
					Name = descriptor.Name,
					DiffuseColor = descriptor.Color,
					DiffuseTexture = texture,
					Tag = descriptor,
				};
				var part = new DrMeshPart(
					vertexBuffer,
					indexBuffer,
					subMesh.Bounds,
					PrimitiveType.TriangleList,
					vertices.Length,
					subMesh.IndexCount / 3,
					0,
					subMesh.IndexOffset)
				{
					Material = material,
				};

				attachmentMesh.MeshParts.Add(part);
				attachmentParts.Add(new ModelGpuPart(
					attachmentParts.Count,
					part,
					descriptor,
					texture));
			}

			var bone = asset.Model.FindBoneByName(source.NodeName) ?? asset.Model.Root;
			attachments.Add(new AttachmentGpu(
				attachmentMesh,
				bone.Index,
				attachmentParts));
		}

		return new ModelGpu(asset, mesh, parts, attachments);
	}

	private sealed class ModelGpu : IDisposable
	{
		public ModelGpu(
			GothicModelAsset asset,
			DrMesh mesh,
			IReadOnlyList<ModelGpuPart> parts,
			IReadOnlyList<AttachmentGpu> attachments)
		{
			Asset = asset;
			Mesh = mesh;
			Parts = parts;
			Attachments = attachments;
		}

		public GothicModelAsset Asset { get; }

		public DrMesh Mesh { get; }

		public IReadOnlyList<ModelGpuPart> Parts { get; }

		public IReadOnlyList<AttachmentGpu> Attachments { get; }

		public void Dispose()
		{
			Mesh.Dispose();
			foreach (var attachment in Attachments)
			{
				attachment.Dispose();
			}
		}
	}

	private sealed class AttachmentGpu : IDisposable
	{
		public AttachmentGpu(
			DrMesh mesh,
			int boneIndex,
			IReadOnlyList<ModelGpuPart> parts)
		{
			Mesh = mesh;
			BoneIndex = boneIndex;
			Parts = parts;
		}

		public DrMesh Mesh { get; }

		public int BoneIndex { get; }

		public IReadOnlyList<ModelGpuPart> Parts { get; }

		public void Dispose()
		{
			Mesh.Dispose();
		}
	}

	private sealed record ModelGpuPart(
		int Index,
		DrMeshPart Part,
		MaterialDesc Descriptor,
		Texture2D? Texture);

	private sealed class ModelInstance
	{
		public ModelInstance(ModelGpu gpu, Matrix transform)
		{
			Gpu = gpu;
			Transform = transform;
			Instance = new DrModelInstance(Gpu.Asset.Model);

			var idle = Gpu.Asset.IdleClip;
			if (idle is not null)
			{
				Controller = new AnimationController(Instance);
				Controller.StartClip(idle, idle.PlaybackFlags);
				Controller.Update(TimeSpan.Zero);
			}

			Transforms = new Matrix[Gpu.Parts.Count][];
			for (var index = 0; index < Transforms.Length; index++)
			{
				var skin = Gpu.Parts[index].Part.Skin;
				Transforms[index] = new Matrix[skin?.Joints.Length ?? 0];
			}
		}

		public ModelGpu Gpu { get; }

		public Matrix Transform { get; }

		public DrModelInstance Instance { get; }

		public AnimationController? Controller { get; }

		public Matrix[][] Transforms { get; }
	}
}
