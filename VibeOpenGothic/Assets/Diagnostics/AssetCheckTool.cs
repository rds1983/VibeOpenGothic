using System.Diagnostics;
using System.Text.Json;
using VibeOpenGothic.Assets.Animation;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Assets.Skins;
using VibeOpenGothic.Assets.Textures;
using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Data.Vfs;
using ZenKit;
using ZenKit.Vobs;

namespace VibeOpenGothic.Assets.Diagnostics;

/// <summary>
/// Developer diagnostic for <c>--check-assets</c>: walks model, static mesh,
/// texture, script and ZEN assets, checks cross-references and reports geometry
/// problems. It never writes into the installation.
/// </summary>
public static class AssetCheckTool
{
	public static int Run(
		GothicInstall install,
		IGameVfs vfs,
		string? jsonPath = null,
		Action<string>? output = null)
	{
		ArgumentNullException.ThrowIfNull(install);
		ArgumentNullException.ThrowIfNull(vfs);
		output ??= Console.WriteLine;

		var timer = Stopwatch.StartNew();
		var issues = new List<AssetIssue>();
		var fileCounts = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		var textures = new TextureCache(new ZenKitTextureLoader(vfs));
		var gameVersion = install.Version.IsGothic1
			? GameVersion.Gothic1
			: GameVersion.Gothic2;

		var modelScripts = 0;
		var modelHierarchies = 0;
		var modelMeshes = 0;
		var animations = 0;
		var aliases = 0;
		var missingHierarchies = 0;
		var missingModelMeshes = 0;
		var missingAnimations = 0;
		var missingVobAssets = 0;
		var missingScripts = 0;
		var staticMeshes = 0;
		var morphMeshes = 0;
		var textureFiles = 0;
		var worlds = 0;
		var scriptFiles = 0;
		var attachments = 0;
		var triangles = 0;
		var vertices = 0;
		var degenerate = 0;
		var invalid = 0;
		var unsupported = 0;
		var failures = 0;
		var warnings = 0;

		foreach (var file in vfs.EnumerateFiles())
		{
			var extension = Path.GetExtension(file.Name).ToUpperInvariant();
			if (extension.Length == 0)
			{
				extension = "(none)";
			}

			fileCounts.TryGetValue(extension, out var count);
			fileCounts[extension] = count + 1;

			try
			{
				switch (extension)
				{
					case ".MDS":
						ScanModelScript(
							vfs,
							file.Path,
							ref modelScripts,
							ref animations,
							ref aliases,
							ref missingHierarchies,
							ref missingModelMeshes,
							ref missingAnimations,
							ref warnings,
							issues);
						break;
					case ".MDH":
					case ".MDL":
						modelHierarchies++;
						break;
					case ".MDM":
						ScanModelMesh(vfs, file.Path, textures, ref modelMeshes, ref attachments,
							ref triangles, ref vertices, ref degenerate, ref invalid, issues);
						break;
					case ".MRM":
					case ".MSH":
						ScanStaticMesh(vfs, file.Path, extension, textures, ref staticMeshes,
							ref triangles, ref vertices, ref degenerate, ref invalid, issues);
						break;
					case ".MMB":
					case ".MMS":
						morphMeshes++;
						unsupported++;
						break;
					case ".TEX":
						textureFiles++;
						break;
					case ".ZEN":
						if (file.Path.Contains("/WORLDS/", StringComparison.OrdinalIgnoreCase))
						{
							ScanWorld(
								vfs,
								file.Path,
								gameVersion,
								textures,
								ref worlds,
								ref missingVobAssets,
								ref triangles,
								ref vertices,
								ref degenerate,
								ref invalid,
								ref unsupported,
								issues);
						}

						break;
					case ".DAT":
					case ".MOB":
					case ".D":
						scriptFiles++;
						break;
				}
			}
			catch (Exception exception)
			{
				failures++;
				issues.Add(new AssetIssue("FAIL", file.Path, $"{exception.GetType().Name}: {exception.Message}"));
			}
		}

		foreach (var missing in textures.MissingTextures)
		{
			issues.Add(new AssetIssue("MISSING", "texture", missing));
		}

		if (!vfs.FileExists(install.DefaultGameDatName))
		{
			missingScripts++;
			issues.Add(new AssetIssue(
				"MISSING",
				install.DefaultGameDatName,
				"Default game scripts were not found in the VFS."));
		}

		timer.Stop();
		var report = new AssetCheckReport(
			install.RootPath,
			install.Version.ToString(),
			timer.Elapsed.TotalMilliseconds,
			new Dictionary<string, int>(fileCounts, StringComparer.OrdinalIgnoreCase),
			modelScripts,
			modelHierarchies,
			modelMeshes,
			animations,
			aliases,
			missingHierarchies,
			missingModelMeshes,
			missingAnimations,
			textures.MissingTextures.Count,
			missingVobAssets,
			missingScripts,
			staticMeshes,
			morphMeshes,
			textureFiles,
			worlds,
			scriptFiles,
			attachments,
			triangles,
			vertices,
			degenerate,
			invalid,
			unsupported,
			failures,
			warnings,
			issues);

		PrintSummary(output, report);
		if (!string.IsNullOrWhiteSpace(jsonPath))
		{
			var fullPath = Path.GetFullPath(jsonPath);
			if (IsInside(install.RootPath, fullPath))
			{
				output("error: JSON output must not be written inside the Gothic installation.");
				return 1;
			}

			var directory = Path.GetDirectoryName(fullPath);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}

			File.WriteAllText(
				fullPath,
				JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
			output($"JSON: {fullPath}");
		}

		return failures == 0 ? 0 : 1;
	}

	private static void ScanModelScript(
		IGameVfs vfs,
		string path,
		ref int modelScripts,
		ref int animations,
		ref int aliases,
		ref int missingHierarchies,
		ref int missingModelMeshes,
		ref int missingAnimations,
		ref int warnings,
		ICollection<AssetIssue> issues)
	{
		var scriptRead = new Read(ReadBytes(vfs, path));
		var script = new ModelScript(scriptRead);
		var resources = ModelResourceResolver.Resolve(vfs, path, script.SkeletonName, script.Meshes);
		var loaded = ZenKitAnimationAdapter.Load(vfs, path, script);

		modelScripts++;
		animations += loaded.Animations.Count;
		aliases += loaded.Aliases.Count;
		missingAnimations += loaded.MissingAnimations.Count;
		warnings += loaded.Warnings.Count;

		if (!resources.HasHierarchy)
		{
			missingHierarchies++;
			issues.Add(new AssetIssue("MISSING", path, "No MDH/MDL hierarchy was resolved."));
		}

		foreach (var missing in resources.MissingMeshNames)
		{
			missingModelMeshes++;
			issues.Add(new AssetIssue("MISSING", path, $"No compiled MDM for '{missing}'."));
		}

		foreach (var missing in loaded.MissingAnimations)
		{
			issues.Add(new AssetIssue("MISSING", path, $"Animation file '{missing}'."));
		}

		foreach (var warning in loaded.Warnings)
		{
			issues.Add(new AssetIssue("WARN", path, warning));
		}

		GC.KeepAlive(script);
		GC.KeepAlive(scriptRead);
	}

	private static void ScanModelMesh(
		IGameVfs vfs,
		string path,
		TextureCache textures,
		ref int modelMeshes,
		ref int attachments,
		ref int triangles,
		ref int vertices,
		ref int degenerate,
		ref int invalid,
		ICollection<AssetIssue> issues)
	{
		var read = new Read(ReadBytes(vfs, path));
		var model = new ModelMesh(read);
		modelMeshes++;
		attachments += model.AttachmentCount;

		foreach (var softMesh in model.Meshes)
		{
			var source = ZenKitMeshAdapter.FromMultiResolutionMesh(softMesh.Mesh);
			var result = MeshBuilder.Build(source, $"{path}:mesh");
			AddGeometry(result.Stats, ref triangles, ref vertices, ref degenerate, ref invalid);
			MaterialSetBuilder.FromMultiResolutionMesh(softMesh.Mesh, textures);
			GC.KeepAlive(softMesh);
		}

		foreach (var attachment in model.Attachments.Values)
		{
			MaterialSetBuilder.FromMultiResolutionMesh(attachment, textures);
			GC.KeepAlive(attachment);
		}

		GC.KeepAlive(model);
		GC.KeepAlive(read);
	}

	private static void ScanStaticMesh(
		IGameVfs vfs,
		string path,
		string extension,
		TextureCache textures,
		ref int staticMeshes,
		ref int triangles,
		ref int vertices,
		ref int degenerate,
		ref int invalid,
		ICollection<AssetIssue> issues)
	{
		var read = new Read(ReadBytes(vfs, path));
		MeshSource source;
		MaterialSet materials;
		if (extension == ".MSH")
		{
			var mesh = new ZenKit.Mesh(read);
			source = ZenKitMeshAdapter.FromMesh(mesh);
			materials = MaterialSetBuilder.FromMesh(mesh, textures);
			GC.KeepAlive(mesh);
		}
		else
		{
			var mesh = new MultiResolutionMesh(read);
			source = ZenKitMeshAdapter.FromMultiResolutionMesh(mesh);
			materials = MaterialSetBuilder.FromMultiResolutionMesh(mesh, textures);
			GC.KeepAlive(mesh);
		}

		var result = MeshBuilder.Build(source, path);
		staticMeshes++;
		AddGeometry(result.Stats, ref triangles, ref vertices, ref degenerate, ref invalid);
		GC.KeepAlive(materials);
		GC.KeepAlive(read);
	}

	private static void ScanWorld(
		IGameVfs vfs,
		string path,
		GameVersion gameVersion,
		TextureCache textures,
		ref int worlds,
		ref int missingVobAssets,
		ref int triangles,
		ref int vertices,
		ref int degenerate,
		ref int invalid,
		ref int unsupported,
		ICollection<AssetIssue> issues)
	{
		var read = new Read(ReadBytes(vfs, path));
		var world = new ZenKit.World(read, gameVersion);
		worlds++;

		if (world.Mesh is not null)
		{
			var source = ZenKitMeshAdapter.FromMesh(world.Mesh);
			var result = MeshBuilder.Build(source, $"{path}:mesh");
			AddGeometry(result.Stats, ref triangles, ref vertices, ref degenerate, ref invalid);
			MaterialSetBuilder.FromMesh(world.Mesh, textures);
		}

		var stack = new Stack<IVirtualObject>(world.RootObjects);
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		while (stack.Count > 0)
		{
			var vob = stack.Pop();
			if (vob.ShowVisual && vob.Visual is not null)
			{
				CheckVisual(vfs, path, vob.Visual, seen, ref missingVobAssets, ref unsupported, issues);
			}

			foreach (var child in vob.Children)
			{
				stack.Push(child);
			}
		}

		GC.KeepAlive(world);
		GC.KeepAlive(read);
	}

	private static void CheckVisual(
		IGameVfs vfs,
		string worldPath,
		IVisual visual,
		ISet<string> seen,
		ref int missing,
		ref int unsupported,
		ICollection<AssetIssue> issues)
	{
		var name = visual.Name;
		if (string.IsNullOrWhiteSpace(name))
		{
			return;
		}

		if (!seen.Add(visual.Type + "|" + name))
		{
			return;
		}

		bool exists;
		switch (visual.Type)
		{
			case VisualType.Mesh:
				exists = CheckFile(vfs, name, ".MSH");
				break;
			case VisualType.MultiResolutionMesh:
				exists = CheckFile(vfs, name, ".MRM");
				break;
			case VisualType.Model:
				exists = CheckModelVisual(vfs, name);
				break;
			case VisualType.MorphMesh:
				exists = CheckFile(vfs, name, ".MMB", ".MMS");
				break;
			case VisualType.Decal:
				exists = TextureNameResolver.Resolve(vfs, name) is not null;
				break;
			case VisualType.ParticleEffect:
				// PFX effects are script/runtime definitions, not VFS assets.
				unsupported++;
				return;
			default:
				return;
		}

		if (!exists)
		{
			missing++;
			issues.Add(new AssetIssue(
				"MISSING",
				worldPath,
				$"VOB visual '{name}' ({visual.Type}) was not found."));
		}
	}

	private static bool CheckModelVisual(IGameVfs vfs, string name)
	{
		var extension = Path.GetExtension(name);
		if (extension.Equals(".ASC", StringComparison.OrdinalIgnoreCase))
		{
			return vfs.FileExists(Path.ChangeExtension(name, "MDL")) ||
				vfs.FileExists(Path.ChangeExtension(name, "MDM"));
		}

		if (extension.Equals(".MDS", StringComparison.OrdinalIgnoreCase))
		{
			return vfs.FileExists(name) ||
				vfs.FileExists(Path.ChangeExtension(name, "MSB"));
		}

		return vfs.FileExists(name) ||
			vfs.FileExists(Path.ChangeExtension(name, "MDL")) ||
			vfs.FileExists(Path.ChangeExtension(name, "MDM"));
	}

	private static bool CheckFile(IGameVfs vfs, string name, params string[] extensions)
	{
		if (vfs.FileExists(name))
		{
			return true;
		}

		foreach (var extension in extensions)
		{
			if (vfs.FileExists(Path.ChangeExtension(name, extension)))
			{
				return true;
			}
		}

		return false;
	}

	private static void AddGeometry(
		MeshConversionStats stats,
		ref int triangles,
		ref int vertices,
		ref int degenerate,
		ref int invalid)
	{
		triangles += stats.SourceTriangleCount;
		vertices += stats.VertexCount;
		degenerate += stats.DegenerateTriangleCount;
		invalid += stats.InvalidPolygonCount;
	}

	private static void PrintSummary(Action<string> output, AssetCheckReport report)
	{
		output(
			$"Assets: {report.ModelScripts} MDS scripts, {report.ModelHierarchies} MDH/MDL, " +
			$"{report.ModelMeshes} MDM, {report.StaticMeshes} MRM/MSH, " +
			$"{report.MorphMeshes} MMB/MMS, {report.Textures} TEX, {report.Worlds} ZEN, " +
			$"{report.ScriptFiles} DAT/MOB/D files.");
		output(
			$"Models: {report.Animations} animation(s), {report.Aliases} alias(es), " +
			$"{report.Attachments} attachment(s); missing hierarchy {report.MissingHierarchies}, " +
			$"mesh {report.MissingModelMeshes}, animation {report.MissingAnimations}, " +
			$"texture {report.MissingTextures}, VOB asset {report.MissingVobAssets}.");
		output(
			$"Geometry: {report.Triangles} triangles, {report.Vertices} vertices, " +
			$"{report.DegenerateTriangles} degenerate, {report.InvalidPolygons} invalid.");
		output(
			$"Warnings: {report.Warnings}; unsupported: {report.UnsupportedFeatures}; " +
			$"failures: {report.Failures}; issues: {report.Issues.Count}; " +
			$"time: {report.Milliseconds:0} ms.");

		foreach (var issue in report.Issues.Take(20))
		{
			output($"{issue.Kind} {issue.Asset}: {issue.Message}");
		}

		if (report.Issues.Count > 20)
		{
			output($"... {report.Issues.Count - 20} more issue(s) in the JSON report.");
		}
	}

	private static bool IsInside(string root, string path)
	{
		var fullRoot = Path.GetFullPath(root)
			.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		var fullPath = Path.GetFullPath(path);
		var comparison = OperatingSystem.IsWindows()
			? StringComparison.OrdinalIgnoreCase
			: StringComparison.Ordinal;
		return fullPath.Equals(fullRoot, comparison) ||
			fullPath.StartsWith(
				fullRoot + Path.DirectorySeparatorChar,
				comparison);
	}

	private static byte[] ReadBytes(IGameVfs vfs, string name)
	{
		using var stream = vfs.OpenRead(name);
		using var memory = new MemoryStream();
		stream.CopyTo(memory);
		return memory.ToArray();
	}
}
