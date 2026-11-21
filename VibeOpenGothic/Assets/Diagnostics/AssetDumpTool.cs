using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Assets.Models;
using VibeOpenGothic.Assets.Skins;
using VibeOpenGothic.Assets.Textures;
using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Data.Vfs;
using VibeOpenGothic.World;

namespace VibeOpenGothic.Assets.Diagnostics;

/// <summary>
/// Developer diagnostic for <c>--dump-data</c>: writes the converted world,
/// static and model meshes as OBJ/MTL plus top-mip textures as BMP. The target
/// directory must be outside the Gothic installation.
/// </summary>
public static class AssetDumpTool
{
	public static int Run(
		GothicInstall install,
		IGameVfs vfs,
		string outputDirectory,
		string? worldName = null,
		Action<string>? output = null)
	{
		ArgumentNullException.ThrowIfNull(install);
		ArgumentNullException.ThrowIfNull(vfs);
		ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
		output ??= Console.WriteLine;

		var fullOutput = Path.GetFullPath(outputDirectory);
		if (IsInside(install.RootPath, fullOutput))
		{
			output("error: Dump output must not be inside the Gothic installation.");
			return 1;
		}

		try
		{
			var name = string.IsNullOrWhiteSpace(worldName)
				? install.DefaultWorldName
				: worldName;
			var gameVersion = install.Version.IsGothic1
				? ZenKit.GameVersion.Gothic1
				: ZenKit.GameVersion.Gothic2;
			var world = WorldLoader.Load(vfs, name, gameVersion);
			var meshDir = Path.Combine(fullOutput, "meshes");
			var textureDir = Path.Combine(fullOutput, "textures");
			Directory.CreateDirectory(meshDir);
			Directory.CreateDirectory(textureDir);

			var files = new List<string>();
			var textures = new TextureWriter(textureDir);
			var meshCount = 0;
			WriteMesh(
				meshDir,
				"world",
				world.Mesh,
				world.Materials,
				textures,
				files);
			meshCount++;

			foreach (var source in world.StaticMeshes.Values)
			{
				var fileName = UniqueName(
					meshDir,
					$"static_{SafeName(source.Key)}");
				WriteMesh(meshDir, fileName, source.Mesh, source.Materials, textures, files);
				meshCount++;
			}

			foreach (var model in world.Models.Values)
			{
				var modelName = SafeName(Path.GetFileNameWithoutExtension(model.Key));
				for (var index = 0; index < model.Meshes.Count; index++)
				{
					var mesh = model.Meshes[index];
					var fileName = UniqueName(
						meshDir,
						$"model_{modelName}_{index}");
					WriteSkinnedMesh(
						meshDir,
						fileName,
						mesh.Geometry,
						mesh.Materials,
						textures,
						files);
					meshCount++;
				}

				for (var index = 0; index < model.Attachments.Count; index++)
				{
					var attachment = model.Attachments[index];
					var fileName = UniqueName(
						meshDir,
						$"model_{modelName}_attach_{index}_{SafeName(attachment.NodeName)}");
					WriteMesh(
						meshDir,
						fileName,
						attachment.Mesh,
						attachment.Materials,
						textures,
						files);
					meshCount++;
				}
			}

			var manifest = new DumpManifest(
				world.Name,
				install.Version.ToString(),
				world.Mesh.TriangleCount,
				world.Mesh.VertexCount,
				meshCount,
				textures.Count,
				files.Concat(textures.Files).ToArray());
			var manifestPath = Path.Combine(fullOutput, "manifest.json");
			File.WriteAllText(
				manifestPath,
				JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

			output(
				$"Dumped {meshCount} mesh(es) and {textures.Count} texture(s) to {fullOutput}.");
			return 0;
		}
		catch (Exception exception)
		{
			output($"FAIL --dump-data: {exception.GetType().Name}: {exception.Message}");
			return 1;
		}
	}

	private static void WriteMesh(
		string directory,
		string name,
		MeshData mesh,
		MaterialSet materials,
		TextureWriter textures,
		ICollection<string> files)
	{
		var vertexArray = mesh.Vertices.ToArray();
		var indexArray = mesh.Indices.ToArray();
		WriteObj(
			directory,
			name,
			vertexArray,
			indexArray,
			mesh.SubMeshes,
			materials,
			textures,
			files);
	}

	private static void WriteSkinnedMesh(
		string directory,
		string name,
		SkinnedMeshData mesh,
		MaterialSet materials,
		TextureWriter textures,
		ICollection<string> files)
	{
		var vertexArray = mesh.Vertices.ToArray();
		var indexArray = mesh.Indices.ToArray();
		WriteObj(
			directory,
			name,
			vertexArray,
			indexArray,
			mesh.SubMeshes,
			materials,
			textures,
			files);
	}

	private static void WriteObj(
		string directory,
		string name,
		MeshVertex[] vertices,
		int[] indices,
		IReadOnlyList<MeshSubMesh> subMeshes,
		MaterialSet materials,
		TextureWriter textures,
		ICollection<string> files)
	{
		var objPath = Path.Combine(directory, name + ".obj");
		var mtlPath = Path.Combine(directory, name + ".mtl");
		using (var writer = new StreamWriter(objPath, false, new UTF8Encoding(false)))
		{
			writer.WriteLine($"# VibeOpenGothic converted mesh: {name}");
			writer.WriteLine($"mtllib {name}.mtl");
			foreach (var vertex in vertices)
			{
				writer.WriteLine(
					$"v {vertex.Position.X:0.######} {vertex.Position.Y:0.######} {vertex.Position.Z:0.######}");
				writer.WriteLine(
					$"vt {vertex.TexCoord.X:0.######} {vertex.TexCoord.Y:0.######}");
				writer.WriteLine(
					$"vn {vertex.Normal.X:0.######} {vertex.Normal.Y:0.######} {vertex.Normal.Z:0.######}");
			}

			for (var index = 0; index < subMeshes.Count; index++)
			{
				var subMesh = subMeshes[index];
				writer.WriteLine($"usemtl material_{subMesh.MaterialIndex}");
				for (var offset = 0; offset < subMesh.IndexCount; offset += 3)
				{
					var a = indices[subMesh.IndexOffset + offset] + 1;
					var b = indices[subMesh.IndexOffset + offset + 1] + 1;
					var c = indices[subMesh.IndexOffset + offset + 2] + 1;
					writer.WriteLine($"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}");
				}
			}
		}

		using (var writer = new StreamWriter(mtlPath, false, new UTF8Encoding(false)))
		{
			for (var index = 0; index < materials.Materials.Count; index++)
			{
				var material = materials.Materials[index];
				writer.WriteLine($"newmtl material_{index}");
				writer.WriteLine(
					$"Kd {material.Color.R / 255f:0.######} " +
					$"{material.Color.G / 255f:0.######} " +
					$"{material.Color.B / 255f:0.######}");
				var texture = textures.Write(material.Texture);
				if (texture is not null)
				{
					writer.WriteLine($"map_Kd {texture}");
				}
			}
		}

		files.Add($"meshes/{name}.obj");
		files.Add($"meshes/{name}.mtl");
	}

	private static void WriteObj(
		string directory,
		string name,
		SkinnedVertex[] vertices,
		int[] indices,
		IReadOnlyList<MeshSubMesh> subMeshes,
		MaterialSet materials,
		TextureWriter textures,
		ICollection<string> files)
	{
		var objPath = Path.Combine(directory, name + ".obj");
		var mtlPath = Path.Combine(directory, name + ".mtl");
		using (var writer = new StreamWriter(objPath, false, new UTF8Encoding(false)))
		{
			writer.WriteLine($"# VibeOpenGothic converted skinned mesh: {name}");
			writer.WriteLine($"mtllib {name}.mtl");
			foreach (var vertex in vertices)
			{
				writer.WriteLine(
					$"v {vertex.Position.X:0.######} {vertex.Position.Y:0.######} {vertex.Position.Z:0.######}");
				writer.WriteLine(
					$"vt {vertex.TexCoord.X:0.######} {vertex.TexCoord.Y:0.######}");
				writer.WriteLine(
					$"vn {vertex.Normal.X:0.######} {vertex.Normal.Y:0.######} {vertex.Normal.Z:0.######}");
			}

			for (var index = 0; index < subMeshes.Count; index++)
			{
				var subMesh = subMeshes[index];
				writer.WriteLine($"usemtl material_{subMesh.MaterialIndex}");
				for (var offset = 0; offset < subMesh.IndexCount; offset += 3)
				{
					var a = indices[subMesh.IndexOffset + offset] + 1;
					var b = indices[subMesh.IndexOffset + offset + 1] + 1;
					var c = indices[subMesh.IndexOffset + offset + 2] + 1;
					writer.WriteLine($"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}");
				}
			}
		}

		using (var writer = new StreamWriter(mtlPath, false, new UTF8Encoding(false)))
		{
			for (var index = 0; index < materials.Materials.Count; index++)
			{
				var material = materials.Materials[index];
				writer.WriteLine($"newmtl material_{index}");
				writer.WriteLine(
					$"Kd {material.Color.R / 255f:0.######} " +
					$"{material.Color.G / 255f:0.######} " +
					$"{material.Color.B / 255f:0.######}");
				var texture = textures.Write(material.Texture);
				if (texture is not null)
				{
					writer.WriteLine($"map_Kd {texture}");
				}
			}
		}

		files.Add($"meshes/{name}.obj");
		files.Add($"meshes/{name}.mtl");
	}

	private static string SafeName(string value)
	{
		var invalid = Path.GetInvalidFileNameChars();
		var builder = new StringBuilder(value.Length);
		foreach (var character in value)
		{
			builder.Append(
				invalid.Contains(character) || char.IsWhiteSpace(character)
					? '_'
					: character);
		}

		var result = builder.ToString().Trim();
		return result.Length == 0 ? "unnamed" : result;
	}

	private static string UniqueName(
		string directory,
		string baseName)
	{
		var name = baseName;
		var suffix = 1;
		while (File.Exists(Path.Combine(directory, name + ".obj")) ||
			File.Exists(Path.Combine(directory, name + ".mtl")))
		{
			name = $"{baseName}_{suffix++}";
		}

		return name;
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

	private sealed record DumpManifest(
		string World,
		string GameVersion,
		int Triangles,
		int Vertices,
		int MeshFiles,
		int TextureFiles,
		IReadOnlyList<string> Files);

	private sealed class TextureWriter
	{
		private readonly string _directory;
		private readonly Dictionary<string, string> _written = new(StringComparer.OrdinalIgnoreCase);

		public TextureWriter(string directory)
		{
			_directory = directory;
		}

		public int Count => _written.Count;

		public IReadOnlyCollection<string> Files => _written.Values;

		public string? Write(TextureData? texture)
		{
			if (texture is null)
			{
				return null;
			}

			if (_written.TryGetValue(texture.Name, out var existing))
			{
				return existing;
			}

			var baseName = SafeName(Path.GetFileNameWithoutExtension(texture.Name));
			var fileName = baseName + ".bmp";
			var path = Path.Combine(_directory, fileName);
			WriteBmp(path, texture);
			var relative = "textures/" + fileName;
			_written[texture.Name] = relative;
			return relative;
		}

		private static void WriteBmp(string path, TextureData texture)
		{
			var level = texture.MipLevels[0];
			var rgba = level.Rgba.Span;
			var stride = level.Width * 4;
			var imageSize = stride * level.Height;
			using var stream = File.Create(path);
			using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
			writer.Write((byte)'B');
			writer.Write((byte)'M');
			writer.Write(14 + 40 + imageSize);
			writer.Write(0);
			writer.Write(14 + 40);
			writer.Write(40);
			writer.Write(level.Width);
			writer.Write(level.Height);
			writer.Write((short)1);
			writer.Write((short)32);
			writer.Write(0);
			writer.Write(imageSize);
			writer.Write(0);
			writer.Write(0);
			writer.Write(0);
			writer.Write(0);
			for (var y = level.Height - 1; y >= 0; y--)
			{
				for (var x = 0; x < level.Width; x++)
				{
					var offset = (y * level.Width + x) * 4;
					writer.Write(rgba[offset + 2]);
					writer.Write(rgba[offset + 1]);
					writer.Write(rgba[offset + 0]);
					writer.Write(rgba[offset + 3]);
				}
			}
		}
	}
}
