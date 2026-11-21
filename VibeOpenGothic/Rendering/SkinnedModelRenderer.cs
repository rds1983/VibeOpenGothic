using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Assets.Animation;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Assets.Models;
using VibeOpenGothic.Assets.Skins;
using VibeOpenGothic.World;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// Uploads and draws skinned MDS VOBs with stock <see cref="SkinnedEffect"/>.
/// The runtime containers are DigitalRiseModel types: <see cref="DrModel"/>,
/// <see cref="DrModelInstance"/>, <see cref="DrMesh"/> and <see cref="DrMeshPart"/>.
/// Per-model, per-part and per-instance state lives in their <c>Tag</c> objects.
/// </summary>
public sealed class SkinnedModelRenderer : IDisposable
{
	private readonly Dictionary<string, DrModel> _models = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<ModelInstanceState> _instances = new();
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

		foreach (var asset in world.Models.Values)
		{
			var model = CreateModel(device, textures, asset, progress);
			if (model is not null)
			{
				_models[asset.Key] = model;
			}
		}

		foreach (var vob in world.ModelVobs)
		{
			if (_models.TryGetValue(vob.ModelKey, out var model))
			{
				CreateInstance(model, vob.Transform);
			}
		}

		progress(
			$"Skinned models uploaded: {_models.Count} models, " +
			$"{_instances.Count} instances");
	}

	public void Draw(ForwardRenderer renderer, GameTime gameTime)
	{
		ArgumentNullException.ThrowIfNull(renderer);
		ArgumentNullException.ThrowIfNull(gameTime);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var elapsed = gameTime.ElapsedGameTime;
		foreach (var instance in _instances)
		{
			instance.Controller?.Update(elapsed);

			var mesh = instance.Instance.Model.Root.Mesh;
			if (mesh is null)
			{
				continue;
			}

			foreach (var part in mesh.MeshParts)
			{
				if (part.Skin is not null)
				{
					renderer.DrawMeshPart(
						part,
						instance.Transform,
						instance.Instance.GetSkinTransforms(part.Skin.SkinIndex));
				}
				else if (part.Tag is DrMeshPartTag attachmentTag)
				{
					var nodeTransform = instance.Instance.GetBoneGlobalTransform(
						attachmentTag.BoneIndex);
					renderer.DrawMeshPart(part, nodeTransform * instance.Transform);
				}
			}
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		foreach (var model in _models.Values)
		{
			if (model.Tag is not DrModelTag)
			{
				continue;
			}

			model.Root.Mesh?.Dispose();
			model.Tag = null;
		}

		_models.Clear();
		_instances.Clear();
	}

	private static DrModel? CreateModel(
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

		var mesh = asset.Model.Meshes.Length > 0
			? asset.Model.Meshes[0]
			: new DrMesh { Name = asset.Key };

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
					Tag = new DrMaterialTag(descriptor),
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
			}
		}

		foreach (var source in asset.Attachments)
		{
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

			var bone = asset.Model.FindBoneByName(source.NodeName) ?? asset.Model.Root;
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
					Tag = new DrMaterialTag(descriptor),
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
					Tag = new DrMeshPartTag(bone.Index),
				};

				mesh.MeshParts.Add(part);
			}
		}

		asset.Model.Tag = new DrModelTag(asset);
		return asset.Model;
	}

	private void CreateInstance(DrModel model, Matrix transform)
	{
		if (model.Tag is not DrModelTag info)
		{
			return;
		}

		var instance = new DrModelInstance(model);
		AnimationController? controller = null;
		var idle = info.Asset.IdleClip;
		if (idle is not null)
		{
			controller = new AnimationController(instance);
			controller.StartClip(
				idle,
				idle.Tag is AnimationClipTag tag
					? tag.PlaybackFlags
					: DigitalRiseModel.Animation.AnimationFlags.None);
			controller.Update(TimeSpan.Zero);
		}

		_instances.Add(new ModelInstanceState(instance, controller, transform));
	}

	private sealed record ModelInstanceState(
		DrModelInstance Instance,
		AnimationController? Controller,
		Matrix Transform);
}
