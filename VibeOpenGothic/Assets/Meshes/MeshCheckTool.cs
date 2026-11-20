using System.Diagnostics;
using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Data.Vfs;
using ZenKit;

namespace VibeOpenGothic.Assets.Meshes;

/// <summary>
/// Developer diagnostic for <c>--check-meshes</c>: converts every ZEN world
/// mesh reachable from the VFS and reports counts and failures.
/// </summary>
public static class MeshCheckTool
{
	public static int Run(GothicInstall install, IGameVfs vfs, Action<string>? output = null)
	{
		ArgumentNullException.ThrowIfNull(install);
		ArgumentNullException.ThrowIfNull(vfs);

		output ??= Console.WriteLine;

		var gameVersion = install.Version.IsGothic1
			? GameVersion.Gothic1
			: GameVersion.Gothic2;

		var worldCount = 0;
		var skippedCount = 0;
		var failureCount = 0;
		var totalPolygons = 0;
		var totalTriangles = 0;
		var totalVertices = 0;
		var totalDegenerate = 0;
		var totalInvalid = 0;

		var timer = Stopwatch.StartNew();

		foreach (var file in vfs.EnumerateFiles())
		{
			if (!file.Name.EndsWith(".ZEN", StringComparison.OrdinalIgnoreCase) ||
				!file.Path.Contains("/WORLDS/", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			try
			{
				output($"Checking {file.Path}");

				using var stream = vfs.OpenRead(file.Path);
				using var memory = new MemoryStream();
				stream.CopyTo(memory);

				var read = new Read(memory.ToArray());
				var world = new ZenKit.World(read, gameVersion);
				var mesh = world.Mesh;
				if (mesh is null)
				{
					skippedCount++;
					continue;
				}

				var source = ZenKitMeshAdapter.FromMesh(mesh);
				var result = MeshBuilder.Build(source, file.Path);
				var stats = result.Stats;

				// ZenKit's World owns the mesh. Keep both alive while the native
				// mesh is being read and converted.
				GC.KeepAlive(mesh);
				GC.KeepAlive(world);
				GC.KeepAlive(read);

				worldCount++;
				totalPolygons += stats.SourcePolygonCount;
				totalTriangles += stats.SourceTriangleCount;
				totalVertices += stats.VertexCount;
				totalDegenerate += stats.DegenerateTriangleCount;
				totalInvalid += stats.InvalidPolygonCount;

				output(
					$"{file.Path}: {stats.SourcePolygonCount} polys, {stats.SourceTriangleCount} tris, " +
					$"{stats.VertexCount} verts, {stats.SubMeshCount} submeshes, " +
					$"{stats.DegenerateTriangleCount} degenerate, {stats.InvalidPolygonCount} invalid");
			}
			catch (Exception exception)
			{
				failureCount++;
				output($"FAIL {file.Path}: {exception.GetType().Name}: {exception.Message}");
			}
		}

		timer.Stop();

		output(
			$"Checked {worldCount} world mesh(es), {skippedCount} without a mesh, {failureCount} failure(s) " +
			$"in {timer.Elapsed.TotalSeconds:0.0}s.");
		output(
			$"Totals: {totalPolygons} polygons, {totalTriangles} triangles, {totalVertices} vertices, " +
			$"{totalDegenerate} degenerate triangles, {totalInvalid} invalid polygons.");

		return failureCount == 0 && worldCount > 0 ? 0 : 1;
	}
}
