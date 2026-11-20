using System.Globalization;
using System.Text;

namespace VibeOpenGothic.Data.Ini;

/// <summary>
/// Minimal INI reader for Gothic.ini, SystemPack.ini and modfile.ini.
/// Section and key names are case-insensitive; the last duplicate wins.
/// </summary>
public sealed class IniFile
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections =
        new(StringComparer.OrdinalIgnoreCase);

    public static IniFile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllText(path, Encoding.Latin1));
    }

    public static IniFile Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var result = new IniFile();
        string? section = null;

        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 ||
                trimmed.StartsWith(";", StringComparison.Ordinal) ||
                trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed[0] == '[' && trimmed[^1] == ']')
            {
                section = trimmed[1..^1].Trim();
                continue;
            }

            if (section is null)
            {
                continue;
            }

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = trimmed[..separator].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            var value = StripInlineComment(trimmed[(separator + 1)..].Trim());
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                value = value[1..^1];
            }

            result.Set(section, key, value);
        }

        return result;
    }

    public bool HasSection(string section)
    {
        return _sections.ContainsKey(section);
    }

    public bool HasKey(string section, string key)
    {
        return _sections.TryGetValue(section, out var values) && values.ContainsKey(key);
    }

    public string? GetString(string section, string key)
    {
        return _sections.TryGetValue(section, out var values) &&
               values.TryGetValue(key, out var value)
            ? value
            : null;
    }

    public int GetInt(string section, string key, int fallback = 0)
    {
        var value = GetString(section, key);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    public float GetFloat(string section, string key, float fallback = 0)
    {
        var value = GetString(section, key);
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    public bool GetBool(string section, string key, bool fallback = false)
    {
        var value = GetString(section, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => fallback,
        };
    }

    public IReadOnlyDictionary<string, string> GetSection(string section)
    {
        return _sections.TryGetValue(section, out var values)
            ? values
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private void Set(string section, string key, string value)
    {
        if (!_sections.TryGetValue(section, out var values))
        {
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _sections[section] = values;
        }

        values[key] = value;
    }

    private static string StripInlineComment(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var atTokenStart = index == 0 || char.IsWhiteSpace(value[index - 1]);

            if (value[index] == ';' && atTokenStart)
            {
                return value[..index].TrimEnd();
            }

            if (value[index] == '/' && index + 1 < value.Length && value[index + 1] == '/' && atTokenStart)
            {
                return value[..index].TrimEnd();
            }
        }

        return value;
    }
}
