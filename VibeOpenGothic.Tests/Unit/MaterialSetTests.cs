using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Meshes;

namespace VibeOpenGothic.Tests.Unit;

public sealed class MaterialSetTests
{
	[Fact]
	public void Get_InRange_ReturnsMaterial()
	{
		var material = GothicMaterial.Default with { Name = "stone" };
		var set = new MaterialSet(MeshSourceKind.World, new[] { material });

		Assert.Same(material, set.Get(0));
		Assert.Equal(MeshSourceKind.World, set.SourceKind);
	}

	[Fact]
	public void Get_OutOfRange_ReturnsDefault()
	{
		var set = new MaterialSet(MeshSourceKind.Model, Array.Empty<GothicMaterial>());

		Assert.Same(GothicMaterial.Default, set.Get(0));
		Assert.Same(GothicMaterial.Default, set.Get(-1));
	}
}
