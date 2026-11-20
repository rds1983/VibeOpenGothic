using VibeOpenGothic.Assets.Skins;
using VibeOpenGothic.Data.Vfs;

namespace VibeOpenGothic.Tests.Unit;

public sealed class ModelResourceResolverTests
{
	[Fact]
	public void Resolve_PrefersSkeletonHierarchyAndCollectsMeshes()
	{
		var vfs = Vfs(
			"/anims/Skel.MDH",
			"/anims/Overlay.MDH",
			"/anims/Skel.MDM",
			"/anims/Armor.MDM");

		var resources = ModelResourceResolver.Resolve(
			vfs,
			"/anims/Overlay.MDS",
			"Skel.ASC",
			new[] { "Skel.ASC", "Armor.ASC" });

		Assert.Equal("/anims/Skel.MDH", resources.HierarchyPath);
		Assert.Null(resources.BundlePath);
		Assert.Equal(
			new[] { "/anims/Skel.MDM", "/anims/Armor.MDM" },
			resources.MeshPaths);
		Assert.Empty(resources.MissingMeshNames);
		Assert.True(resources.HasHierarchy);
	}

	[Fact]
	public void Resolve_FallsBackToScriptNameHierarchy()
	{
		var vfs = Vfs("/anims/Overlay.MDH", "/anims/Skel.MDM");

		var resources = ModelResourceResolver.Resolve(
			vfs,
			"/anims/Overlay.MDS",
			"Skel.ASC",
			Array.Empty<string>());

		Assert.Equal("/anims/Overlay.MDH", resources.HierarchyPath);
		Assert.Equal(new[] { "/anims/Skel.MDM" }, resources.MeshPaths);
	}

	[Fact]
	public void Resolve_UsesSkeletonBundleWithoutHierarchy()
	{
		var vfs = Vfs("/anims/Rock_Body.MDL", "/anims/Rock_Body.MDM");

		var resources = ModelResourceResolver.Resolve(
			vfs,
			"/anims/Rock.MDS",
			"Rock_Body.ASC",
			Array.Empty<string>());

		Assert.Null(resources.HierarchyPath);
		Assert.Equal("/anims/Rock_Body.MDL", resources.BundlePath);
		Assert.Empty(resources.MeshPaths);
		Assert.Empty(resources.MissingMeshNames);
		Assert.True(resources.HasHierarchy);
	}

	[Fact]
	public void Resolve_UsesScriptBundleWhenSkeletonIsEmpty()
	{
		var vfs = Vfs("/anims/Chest.MDL");

		var resources = ModelResourceResolver.Resolve(
			vfs,
			"/anims/Chest.MDS",
			null,
			Array.Empty<string>());

		Assert.Null(resources.HierarchyPath);
		Assert.Equal("/anims/Chest.MDL", resources.BundlePath);
		Assert.Empty(resources.MeshPaths);
		Assert.Empty(resources.MissingMeshNames);
	}

	[Fact]
	public void Resolve_WithoutSkeleton_UsesScriptStemForMdhAndMdm()
	{
		var vfs = Vfs("/anims/Chest.MDH", "/anims/Chest.MDM");

		var resources = ModelResourceResolver.Resolve(
			vfs,
			"/anims/Chest.MDS",
			null,
			Array.Empty<string>());

		Assert.Equal("/anims/Chest.MDH", resources.HierarchyPath);
		Assert.Equal(new[] { "/anims/Chest.MDM" }, resources.MeshPaths);
	}

	[Fact]
	public void Resolve_ReportsMissingMeshes()
	{
		var vfs = Vfs("/anims/Skel.MDH", "/anims/Skel.MDM");

		var resources = ModelResourceResolver.Resolve(
			vfs,
			"/anims/Skel.MDS",
			"Skel.ASC",
			new[] { "Missing.ASC" });

		Assert.Equal(new[] { "/anims/Skel.MDM" }, resources.MeshPaths);
		Assert.Equal(new[] { "Missing.ASC" }, resources.MissingMeshNames);
	}

	[Fact]
	public void Resolve_DeduplicatesMeshNamesCaseInsensitively()
	{
		var vfs = Vfs("/anims/Skel.MDH", "/anims/Skel.MDM");

		var resources = ModelResourceResolver.Resolve(
			vfs,
			"/anims/Skel.MDS",
			"skel.asc",
			new[] { "Skel.ASC", "SKEL.ASC" });

		Assert.Single(resources.MeshPaths);
		Assert.Empty(resources.MissingMeshNames);
	}

	[Fact]
	public void Resolve_NoFiles_ReportsNoHierarchy()
	{
		var resources = ModelResourceResolver.Resolve(
			Vfs(),
			"/anims/Skel.MDS",
			"Skel.ASC",
			new[] { "Skel.ASC" });

		Assert.False(resources.HasHierarchy);
		Assert.Null(resources.HierarchyPath);
		Assert.Null(resources.BundlePath);
		Assert.Empty(resources.MeshPaths);
		Assert.Single(resources.MissingMeshNames);
	}

	[Fact]
	public void Resolve_RejectsNullVfs()
	{
		Assert.Throws<ArgumentNullException>(
			() => ModelResourceResolver.Resolve(null!, "/anims/Skel.MDS", null, Array.Empty<string>()));
	}

	private static InMemoryGameVfs Vfs(params string[] paths)
	{
		var vfs = new InMemoryGameVfs();
		foreach (var path in paths)
		{
			vfs.Add(path, Array.Empty<byte>());
		}

		return vfs;
	}
}
