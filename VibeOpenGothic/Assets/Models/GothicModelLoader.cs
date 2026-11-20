using DigitalRiseModel;
using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Animation;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Assets.Skins;
using VibeOpenGothic.Assets.Textures;
using VibeOpenGothic.Data.Vfs;
using ZenKit;

namespace VibeOpenGothic.Assets.Models;

/// <summary>
/// Loads one MDS visual into a runtime <see cref="DrModel"/> plus baked skinned
/// meshes. Only the model's default skeleton mesh is loaded; armour and weapon
/// variants are separate models/attachments and are not equipped yet.
/// </summary>
public static class GothicModelLoader
{
	private static readonly string[] IdleCandidates =
	{
		"S_RUN",
		"S_WALK",
		"S_FISTRUN",
		"S_FISTWALK",
		"S_1HRUN",
		"S_1HWALK",
		"S_2HRUN",
		"S_2HWALK",
	};

	public static GothicModelAsset Load(
		IGameVfs vfs,
		string mdsPath,
		TextureCache textures,
		Action<string>? progress = null,
		bool allowDefaultBodyMesh = false)
	{
		ArgumentNullException.ThrowIfNull(vfs);
		ArgumentException.ThrowIfNullOrWhiteSpace(mdsPath);
		ArgumentNullException.ThrowIfNull(textures);
		progress ??= _ => { };

		var entry = FindModelEntry(vfs, mdsPath) ??
			throw new FileNotFoundException($"Model '{mdsPath}' was not found in the VFS.", mdsPath);
		var sourcePath = entry.Path;
		if (Path.GetExtension(sourcePath).Equals(".MDL", StringComparison.OrdinalIgnoreCase))
		{
			return LoadBundle(vfs, sourcePath, textures, progress);
		}

		var scriptRead = new Read(ReadBytes(vfs, sourcePath));
		var script = new ModelScript(scriptRead);
		var resources = ModelResourceResolver.Resolve(
			vfs,
			sourcePath,
			script.SkeletonName,
			script.Meshes);

		IModelHierarchy hierarchy;
		IModelMesh? modelMesh = null;
		Read? hierarchyRead = null;
		Read? meshRead = null;

		if (resources.HierarchyPath is not null)
		{
			hierarchyRead = new Read(ReadBytes(vfs, resources.HierarchyPath));
			hierarchy = new ModelHierarchy(hierarchyRead);

			var meshPath = SelectPrimaryMeshPath(resources, script, allowDefaultBodyMesh);
			if (meshPath is not null)
			{
				meshRead = new Read(ReadBytes(vfs, meshPath));
				modelMesh = new ModelMesh(meshRead);
			}
			else if (!script.SkeletonMeshDisabled)
			{
				progress($"Model '{sourcePath}' has no compiled primary mesh.");
			}
		}
		else if (resources.BundlePath is not null)
		{
			var bundleRead = new Read(ReadBytes(vfs, resources.BundlePath));
			var bundle = new Model(bundleRead);
			hierarchy = bundle.Hierarchy;
			modelMesh = bundle.Mesh;
			GC.KeepAlive(bundle);
			GC.KeepAlive(bundleRead);
		}
		else
		{
			throw new FileNotFoundException(
				$"No MDH or MDL hierarchy found for '{sourcePath}'.");
		}

		var animations = ZenKitAnimationAdapter.Load(vfs, sourcePath, script);
		var warnings = new List<string>();
		foreach (var missing in resources.MissingMeshNames)
		{
			warnings.Add($"Missing compiled mesh '{missing}'.");
		}

		var asset = BuildAsset(
			sourcePath,
			hierarchy,
			modelMesh,
			animations,
			warnings,
			textures,
			progress);

		GC.KeepAlive(hierarchy);
		GC.KeepAlive(modelMesh);
		GC.KeepAlive(hierarchyRead);
		GC.KeepAlive(meshRead);
		GC.KeepAlive(script);
		GC.KeepAlive(scriptRead);
		return asset;
	}

	private static GothicModelAsset LoadBundle(
		IGameVfs vfs,
		string sourcePath,
		TextureCache textures,
		Action<string> progress)
	{
		var read = new Read(ReadBytes(vfs, sourcePath));
		var bundle = new Model(read);
		var animations = new AnimationLoadResult(
			Array.Empty<AnimationSource>(),
			Array.Empty<AnimationAliasSource>(),
			Array.Empty<string>(),
			Array.Empty<string>());

		var asset = BuildAsset(
			sourcePath,
			bundle.Hierarchy,
			bundle.Mesh,
			animations,
			new List<string>(),
			textures,
			progress);

		GC.KeepAlive(bundle);
		GC.KeepAlive(read);
		return asset;
	}

	private static GothicModelAsset BuildAsset(
		string sourcePath,
		IModelHierarchy hierarchy,
		IModelMesh? modelMesh,
		AnimationLoadResult animations,
		List<string> warnings,
		TextureCache textures,
		Action<string> progress)
	{
		var nodes = ZenKitSkinAdapter.FromModelHierarchy(hierarchy);
		var root = hierarchy.RootTranslation;
		var skeleton = SkinSkeletonBuilder.Build(
			nodes,
			new Vector3(root.X, root.Y, root.Z));
		var rootMesh = modelMesh is not null &&
			(modelMesh.MeshCount > 0 || modelMesh.AttachmentCount > 0)
			? new DrMesh { Name = sourcePath }
			: null;
		var build = GothicModelBuilder.Build(
			Path.GetFileNameWithoutExtension(sourcePath),
			skeleton,
			animations,
			rootMesh);
		warnings.AddRange(build.Warnings);

		var meshes = new List<GothicModelMesh>();
		if (modelMesh is not null)
		{
			var softMeshes = modelMesh.Meshes;
			for (var index = 0; index < softMeshes.Count; index++)
			{
				try
				{
					var source = ZenKitSkinAdapter.FromSoftSkinMesh(
						softMeshes[index],
						hierarchy);
					var result = SkinnedMeshBuilder.Build(
						source,
						$"{sourcePath}:{index}",
						build.SourceToDrBoneIndex);
					var materials = MaterialSetBuilder.FromMultiResolutionMesh(
						softMeshes[index].Mesh,
						textures);
					var skinResult = ModelSkinBuilder.Build(
						result.Mesh,
						build.Model,
						index,
						sourcePath,
						warnings);
					meshes.Add(new GothicModelMesh(
						skinResult.Geometry,
						materials,
						skinResult.Skin,
						skinResult.JointCount,
						skinResult.ExceedsSkinnedEffectLimit));
				}
				finally
				{
					GC.KeepAlive(softMeshes[index]);
				}
			}
		}

		var idleClip = SelectIdleClip(build.Model, warnings);
		if (idleClip is not null)
		{
			progress($"Model '{sourcePath}': idle '{idleClip.Name}'");
		}
		else
		{
			warnings.Add($"Model '{sourcePath}' has no animation clip for the idle pose.");
		}

		var attachments = new List<GothicModelAttachment>();
		if (modelMesh is not null)
		{
			foreach (var pair in modelMesh.Attachments)
			{
				try
				{
					var geometry = ZenKitMeshAdapter.FromMultiResolutionMesh(pair.Value);
					var mesh = MeshBuilder.Build(
						geometry,
						$"{sourcePath}:{pair.Key}");
					var materials = MaterialSetBuilder.FromMultiResolutionMesh(
						pair.Value,
						textures);
					attachments.Add(new GothicModelAttachment(
						pair.Key,
						mesh.Mesh,
						materials));
				}
				finally
				{
					GC.KeepAlive(pair.Value);
				}
			}
		}

		return new GothicModelAsset(
			sourcePath,
			sourcePath,
			build.Model,
			meshes,
			attachments,
			idleClip,
			warnings);
	}

	private static VfsFileEntry? FindModelEntry(IGameVfs vfs, string name)
	{
		var entry = vfs.Find(name);
		if (entry is not null)
		{
			return entry;
		}

		if (Path.GetExtension(name).Equals(".ASC", StringComparison.OrdinalIgnoreCase))
		{
			return vfs.Find(Path.ChangeExtension(name, "MDL"));
		}

		return null;
	}

	private static string? SelectPrimaryMeshPath(
		ModelResources resources,
		IModelScript script,
		bool allowDefaultBodyMesh)
	{
		if (resources.MeshPaths.Count == 0)
		{
			return null;
		}

		if (script.SkeletonMeshDisabled)
		{
			return allowDefaultBodyMesh ? resources.MeshPaths[0] : null;
		}

		var skeletonStem = Path.GetFileNameWithoutExtension(script.SkeletonName);
		if (skeletonStem.Length > 0)
		{
			foreach (var path in resources.MeshPaths)
			{
				if (Path.GetFileNameWithoutExtension(path).Equals(
					skeletonStem,
					StringComparison.OrdinalIgnoreCase))
				{
					return path;
				}
			}
		}

		return resources.MeshPaths[0];
	}

	private static GothicAnimationClip? SelectIdleClip(
		DrModel model,
		ICollection<string> warnings)
	{
		foreach (var name in IdleCandidates)
		{
			if (model.Animations.TryGetValue(name, out var clip) &&
				clip is GothicAnimationClip gothic)
			{
				return gothic;
			}
		}

		var loops = model.Animations.Values
			.OfType<GothicAnimationClip>()
			.Where(clip => clip.IsLoop)
			.ToList();
		if (loops.Count > 0)
		{
			warnings.Add("No standard idle clip; using the first looping clip.");
			return loops[0];
		}

		var first = model.Animations.Values.OfType<GothicAnimationClip>().FirstOrDefault();
		if (first is not null)
		{
			warnings.Add("No idle or looping clip; using the first clip.");
		}

		return first;
	}

	private static byte[] ReadBytes(IGameVfs vfs, string name)
	{
		using var stream = vfs.OpenRead(name);
		using var memory = new MemoryStream();
		stream.CopyTo(memory);
		return memory.ToArray();
	}
}
