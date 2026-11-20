using VibeOpenGothic.Core.CommandLine;
using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Data.Vfs;
using VibeOpenGothic.World;
using ZenKit;

namespace VibeOpenGothic.Tests.Unit;

public sealed class WorldLoaderTests
{
	[Fact]
	[Trait("Category", "GameData")]
	public void Load_NewWorld_ReportsExpectedCountsAndBounds()
	{
		// Opt-in data test. Set VOG_GOTHIC_PATH to a Gothic installation to run it.
		var root = Environment.GetEnvironmentVariable("VOG_GOTHIC_PATH");
		if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
		{
			return;
		}

		var lookup = GothicInstallLocator.Locate(
			root,
			null,
			null,
			GameVersionOverride.Auto,
			null);
		Assert.True(lookup.Success, string.Join("; ", lookup.Errors));

		var install = lookup.Install!;
		using var vfs = ZenKitGameVfs.Mount(install, out _);
		var version = install.Version.IsGothic1 ? GameVersion.Gothic1 : GameVersion.Gothic2;
		var world = WorldLoader.Load(vfs, install.DefaultWorldName, version);

		Assert.True(world.Mesh.TriangleCount > 200_000, $"triangles={world.Mesh.TriangleCount}");
		Assert.True(world.Mesh.VertexCount > 300_000, $"vertices={world.Mesh.VertexCount}");
		Assert.True(world.StaticVobs.Count > 5_000, $"static vobs={world.StaticVobs.Count}");
		Assert.True(world.ModelVobs.Count > 0, $"model vobs={world.ModelVobs.Count}");
		Assert.True(world.Models.Count > 0, $"models={world.Models.Count}");
		Assert.True(world.Waypoints.Count > 1_000, $"waypoints={world.Waypoints.Count}");
		Assert.NotNull(world.StartPosition);

		var size = world.Bounds.Max - world.Bounds.Min;
		Assert.True(size.X > 100_000f && size.Z > 100_000f, $"bounds={size}");
		Assert.Equal(0, world.Stats.FailedVisualCount);
		Assert.Equal(0, world.Stats.FailedModelCount);
	}
}
