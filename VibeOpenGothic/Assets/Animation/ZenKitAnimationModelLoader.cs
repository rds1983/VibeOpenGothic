using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Skins;
using VibeOpenGothic.Data.Vfs;
using ZenKit;

namespace VibeOpenGothic.Assets.Animation;

/// <summary>
/// Loads an MDS model's hierarchy and animation clips without touching meshes.
/// This is the data path used by <c>--check-animations</c> and by later model
/// spawning tasks.
/// </summary>
public static class ZenKitAnimationModelLoader
{
	public static AnimationModelLoadResult Load(IGameVfs vfs, string mdsPath)
	{
		ArgumentNullException.ThrowIfNull(vfs);
		ArgumentException.ThrowIfNullOrWhiteSpace(mdsPath);

		var scriptRead = new Read(ReadBytes(vfs, mdsPath));
		var script = new ModelScript(scriptRead);
		var resources = ModelResourceResolver.Resolve(
			vfs,
			mdsPath,
			script.SkeletonName,
			Array.Empty<string>());

		IModelHierarchy hierarchy;
		Read? hierarchyRead = null;
		Model? bundle = null;
		Read? bundleRead = null;

		if (resources.HierarchyPath is not null)
		{
			hierarchyRead = new Read(ReadBytes(vfs, resources.HierarchyPath));
			hierarchy = new ModelHierarchy(hierarchyRead);
		}
		else if (resources.BundlePath is not null)
		{
			bundleRead = new Read(ReadBytes(vfs, resources.BundlePath));
			bundle = new Model(bundleRead);
			hierarchy = bundle.Hierarchy;
		}
		else
		{
			throw new FileNotFoundException(
				$"No MDH or MDL hierarchy found for '{mdsPath}'.");
		}

		var nodes = ZenKitSkinAdapter.FromModelHierarchy(hierarchy);
		var root = hierarchy.RootTranslation;
		var skeleton = SkinSkeletonBuilder.Build(
			nodes,
			new Vector3(root.X, root.Y, root.Z));
		var animations = ZenKitAnimationAdapter.Load(vfs, mdsPath, script);
		var build = GothicModelBuilder.Build(
			Path.GetFileNameWithoutExtension(mdsPath),
			skeleton,
			animations);

		GC.KeepAlive(hierarchy);
		GC.KeepAlive(hierarchyRead);
		GC.KeepAlive(bundle);
		GC.KeepAlive(bundleRead);
		GC.KeepAlive(script);
		GC.KeepAlive(scriptRead);

		return new AnimationModelLoadResult(build.Model, animations, build.Warnings);
	}

	private static byte[] ReadBytes(IGameVfs vfs, string name)
	{
		using var stream = vfs.OpenRead(name);
		using var memory = new MemoryStream();
		stream.CopyTo(memory);
		return memory.ToArray();
	}
}
