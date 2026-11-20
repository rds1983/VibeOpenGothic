using System.Text;

namespace VibeOpenGothic.Data.Installs;

/// <summary>
/// Reads the <c>path</c> entries from Valve's libraryfolders.vdf. The file is a
/// small KeyValues document; a full VDF parser is unnecessary.
/// </summary>
public static class SteamLibraryParser
{
    public static IReadOnlyList<string> ReadLibraryFolders(string steamRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(steamRoot);

        var path = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(path))
        {
            return Array.Empty<string>();
        }

        return Parse(File.ReadAllText(path, Encoding.Latin1));
    }

    public static IReadOnlyList<string> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var tokens = Tokenize(text).ToArray();
        var paths = new List<string>();

        for (var index = 0; index + 1 < tokens.Length; index++)
        {
            if (!tokens[index].Equals("path", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = tokens[index + 1];
            if (value.Length > 0 && (Path.IsPathRooted(value) || value.Contains('/') || value.Contains('\\')))
            {
                paths.Add(value);
            }

            index++;
        }

        return paths
            .Distinct(PathResolver.PathComparer)
            .ToArray();
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        for (var index = 0; index < text.Length;)
        {
            var current = text[index];

            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (current == '/' && index + 1 < text.Length && text[index + 1] == '/')
            {
                while (index < text.Length && text[index] != '\n')
                {
                    index++;
                }

                continue;
            }

            if (current is '{' or '}')
            {
                yield return current.ToString();
                index++;
                continue;
            }

            if (current == '"')
            {
                index++;
                var builder = new StringBuilder();

                while (index < text.Length && text[index] != '"')
                {
                    if (text[index] == '\\' && index + 1 < text.Length)
                    {
                        index++;
                        builder.Append(text[index]);
                    }
                    else
                    {
                        builder.Append(text[index]);
                    }

                    index++;
                }

                if (index < text.Length)
                {
                    index++;
                }

                yield return builder.ToString();
                continue;
            }

            var start = index;
            while (index < text.Length &&
                   !char.IsWhiteSpace(text[index]) &&
                   text[index] is not '{' and not '}')
            {
                index++;
            }

            yield return text[start..index];
        }
    }
}
