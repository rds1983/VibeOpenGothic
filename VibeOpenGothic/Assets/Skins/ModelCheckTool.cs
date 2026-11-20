using System.Diagnostics;
using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Data.Vfs;
using ZenKit;

namespace VibeOpenGothic.Assets.Skins;

/// <summary>
/// Developer diagnostic for <c>--check-models</c>: parses every MDS reachable
/// from the VFS, resolves its compiled hierarchy and meshes, bakes the skinned
/// data and reports bone/weight/bind diagnostics.
/// </summary>
public static class ModelCheckTool
{
	public static int Run(GothicInstall install, IGameVfs vfs, Action<string>? output = null)
	{
		ArgumentNullException.ThrowIfNull(install);
		ArgumentNullException.ThrowIfNull(vfs);

		output ??= Console.WriteLine;

		var scriptCount = 0;
		var meshCount = 0;
		var primaryMeshCount = 0;
		var variantMeshCount = 0;
		var duplicateCount = 0;
		var missingMeshCount = 0;
		var failureCount = 0;
		var totalTriangles = 0;
		var totalVertices = 0;
		var maxBones = 0;
		var overBoneLimit = 0;
		var maxInconsistency = 0f;
		var maxWeightedInconsistency = 0f;
		var primaryMaxInconsistency = 0f;
		var primaryMaxWeightedInconsistency = 0f;
		var totalRenormalized = 0;
		var totalZeroWeights = 0;
		var totalOutOfRange = 0;
		var totalTruncated = 0;
		var bakedMeshKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var bakedBundlePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var hierarchies = new Dictionary<string, LoadedHierarchy>(StringComparer.OrdinalIgnoreCase);
		var meshes = new Dictionary<string, LoadedMesh>(StringComparer.OrdinalIgnoreCase);
		var bundles = new Dictionary<string, LoadedBundle>(StringComparer.OrdinalIgnoreCase);
		var handles = new List<object>();
		var timer = Stopwatch.StartNew();

		foreach (var file in vfs.EnumerateFiles())
		{
			if (!file.Name.EndsWith(".MDS", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			try
			{
				var scriptRead = new Read(ReadBytes(vfs, file.Path));
				var script = new ModelScript(scriptRead);
				var scriptMeshes = script.Meshes;
				var resources = ModelResourceResolver.Resolve(
					vfs,
					file.Path,
					script.SkeletonName,
					scriptMeshes);
				var skeletonStem = Path.GetFileNameWithoutExtension(script.SkeletonName);

				scriptCount++;

				foreach (var missing in resources.MissingMeshNames)
				{
					missingMeshCount++;
					output($"MISSING {file.Path}: no compiled MDM for '{missing}'");
				}

				if (!resources.HasHierarchy)
				{
					throw new FileNotFoundException(
						$"No MDH or MDL hierarchy found for '{file.Path}' " +
						$"(skeleton '{script.SkeletonName}').");
				}

				var scriptBaked = 0;
				var scriptTriangles = 0;
				var scriptVertices = 0;
				var scriptMaxInconsistency = 0f;
				var scriptMaxWeightedInconsistency = 0f;

				if (resources.BundlePath is not null)
				{
					if (bakedBundlePaths.Add(resources.BundlePath))
					{
						var bundle = LoadBundle(vfs, resources.BundlePath, bundles, handles);
						BakeModel(
							bundle.Model.Mesh,
							bundle.Model.Hierarchy,
							$"{file.Path}:{resources.BundlePath}",
							isPrimary: true,
							ref scriptBaked,
							ref scriptTriangles,
							ref scriptVertices,
							ref scriptMaxInconsistency,
							ref scriptMaxWeightedInconsistency);
					}
					else
					{
						duplicateCount++;
					}
				}
				else
				{
					var hierarchy = LoadHierarchy(vfs, resources.HierarchyPath!, hierarchies, handles);

					foreach (var meshPath in resources.MeshPaths)
					{
						var key = resources.HierarchyPath + "|" + meshPath;
						if (!bakedMeshKeys.Add(key))
						{
							duplicateCount++;
							continue;
						}

						var mesh = LoadMesh(vfs, meshPath, meshes, handles);
						var isPrimary = skeletonStem.Length > 0 &&
							Path.GetFileNameWithoutExtension(meshPath).Equals(
								skeletonStem,
								StringComparison.OrdinalIgnoreCase);
						BakeModel(
							mesh.Model,
							hierarchy.Hierarchy,
							$"{file.Path}:{meshPath}",
							isPrimary,
							ref scriptBaked,
							ref scriptTriangles,
							ref scriptVertices,
							ref scriptMaxInconsistency,
							ref scriptMaxWeightedInconsistency);
					}
				}

				meshCount += scriptBaked;
				totalTriangles += scriptTriangles;
				totalVertices += scriptVertices;
				maxInconsistency = MathF.Max(maxInconsistency, scriptMaxInconsistency);
				maxWeightedInconsistency = MathF.Max(
					maxWeightedInconsistency,
					scriptMaxWeightedInconsistency);

				if (scriptBaked > 0 && (scriptCount <= 20 || scriptMaxInconsistency > 0.5f))
				{
					output(
						$"{file.Path}: {script.SkeletonName}, {scriptBaked} mesh(es), " +
						$"{scriptTriangles} tris, {scriptVertices} verts, " +
						$"max inconsistency {scriptMaxInconsistency:0.###} cm " +
						$"(weighted {scriptMaxWeightedInconsistency:0.###} cm)");
				}

				handles.Add(script);
				handles.Add(scriptRead);
			}
			catch (Exception exception)
			{
				failureCount++;
				output($"FAIL {file.Path}: {exception.GetType().Name}: {exception.Message}");
			}
		}

		timer.Stop();

		GC.KeepAlive(handles);

		output(
			$"Checked {scriptCount} MDS model(s), {meshCount} skinned mesh(es) " +
			$"({primaryMeshCount} primary, {variantMeshCount} variant), " +
			$"{duplicateCount} duplicate reference(s), {missingMeshCount} missing mesh(es), " +
			$"{failureCount} failure(s) in {timer.Elapsed.TotalSeconds:0.0}s.");
		output(
			$"Totals: {totalTriangles} triangles, {totalVertices} vertices; " +
			$"max bones {maxBones}, over-72 meshes {overBoneLimit}.");
		output(
			$"Bind inconsistency: primary max {primaryMaxInconsistency:0.###} cm " +
			$"(weighted {primaryMaxWeightedInconsistency:0.###} cm); " +
			$"all meshes max {maxInconsistency:0.###} cm " +
			$"(weighted {maxWeightedInconsistency:0.###} cm).");
		output(
			$"Weights: {totalRenormalized} renormalized, {totalZeroWeights} zero-sum, " +
			$"{totalOutOfRange} out-of-range, {totalTruncated} truncated.");

		return failureCount == 0 && scriptCount > 0 ? 0 : 1;

		void BakeModel(
			IModelMesh modelMesh,
			IModelHierarchy hierarchy,
			string name,
			bool isPrimary,
			ref int scriptBaked,
			ref int scriptTriangles,
			ref int scriptVertices,
			ref float scriptMaxInconsistency,
			ref float scriptMaxWeightedInconsistency)
		{
			var softMeshes = modelMesh.Meshes;
			foreach (var softMesh in softMeshes)
			{
				var source = ZenKitSkinAdapter.FromSoftSkinMesh(softMesh, hierarchy);
				var result = SkinnedMeshBuilder.Build(source, name);
				var stats = result.Stats;

				if (stats.Skin.MaxWeightedInconsistency > 2f)
				{
					output(
						$"WARN {name}: max bind inconsistency " +
						$"{stats.Skin.MaxInconsistency:0.###} cm " +
						$"(weighted {stats.Skin.MaxWeightedInconsistency:0.###} cm) in " +
						$"{stats.Skin.InconsistentVertexCount}/{stats.Skin.VertexCount} vertex(es)");
				}

				scriptBaked++;
				if (isPrimary)
				{
					primaryMeshCount++;
					primaryMaxInconsistency = MathF.Max(
						primaryMaxInconsistency,
						stats.Skin.MaxInconsistency);
					primaryMaxWeightedInconsistency = MathF.Max(
						primaryMaxWeightedInconsistency,
						stats.Skin.MaxWeightedInconsistency);
				}
				else
				{
					variantMeshCount++;
				}

				scriptTriangles += stats.SourceTriangleCount;
				scriptVertices += stats.VertexCount;
				scriptMaxInconsistency = MathF.Max(
					scriptMaxInconsistency,
					stats.Skin.MaxInconsistency);
				scriptMaxWeightedInconsistency = MathF.Max(
					scriptMaxWeightedInconsistency,
					stats.Skin.MaxWeightedInconsistency);
				maxBones = Math.Max(maxBones, stats.Skin.BoneCount);
				if (stats.Skin.BoneCount > 72)
				{
					overBoneLimit++;
				}

				totalRenormalized += stats.Skin.RenormalizedWeightCount;
				totalZeroWeights += stats.Skin.ZeroWeightCount;
				totalOutOfRange += stats.Skin.OutOfRangeBoneCount;
				totalTruncated += stats.Skin.TruncatedInfluenceCount;

				GC.KeepAlive(softMesh);
			}
		}
	}

	private static LoadedHierarchy LoadHierarchy(
		IGameVfs vfs,
		string path,
		Dictionary<string, LoadedHierarchy> cache,
		List<object> handles)
	{
		if (cache.TryGetValue(path, out var existing))
		{
			return existing;
		}

		var read = new Read(ReadBytes(vfs, path));
		var hierarchy = new ModelHierarchy(read);
		var loaded = new LoadedHierarchy(read, hierarchy);
		cache[path] = loaded;
		handles.Add(loaded);
		return loaded;
	}

	private static LoadedMesh LoadMesh(
		IGameVfs vfs,
		string path,
		Dictionary<string, LoadedMesh> cache,
		List<object> handles)
	{
		if (cache.TryGetValue(path, out var existing))
		{
			return existing;
		}

		var read = new Read(ReadBytes(vfs, path));
		var model = new ModelMesh(read);
		var loaded = new LoadedMesh(read, model);
		cache[path] = loaded;
		handles.Add(loaded);
		return loaded;
	}

	private static LoadedBundle LoadBundle(
		IGameVfs vfs,
		string path,
		Dictionary<string, LoadedBundle> cache,
		List<object> handles)
	{
		if (cache.TryGetValue(path, out var existing))
		{
			return existing;
		}

		var read = new Read(ReadBytes(vfs, path));
		var model = new Model(read);
		var loaded = new LoadedBundle(read, model);
		cache[path] = loaded;
		handles.Add(loaded);
		return loaded;
	}

	private static byte[] ReadBytes(IGameVfs vfs, string name)
	{
		using var stream = vfs.OpenRead(name);
		using var memory = new MemoryStream();
		stream.CopyTo(memory);
		return memory.ToArray();
	}

	private sealed record LoadedHierarchy(Read Read, IModelHierarchy Hierarchy);

	private sealed record LoadedMesh(Read Read, IModelMesh Model);

	private sealed record LoadedBundle(Read Read, Model Model);
}
