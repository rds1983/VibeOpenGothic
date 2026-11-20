namespace VibeOpenGothic.Core.Settings;

public sealed class AppSettings
{
    public const int DefaultWindowWidth = 1280;
    public const int DefaultWindowHeight = 720;
    public const int MinWindowWidth = 640;
    public const int MinWindowHeight = 480;
    public const int MaxWindowWidth = 7680;
    public const int MaxWindowHeight = 4320;

    public string? GothicPath { get; set; }

    public int WindowWidth { get; set; } = DefaultWindowWidth;

    public int WindowHeight { get; set; } = DefaultWindowHeight;

    public bool Windowed { get; set; } = true;

    public bool VSync { get; set; } = true;

    /// <summary>
    /// Clamps invalid values and returns human-readable warnings.
    /// </summary>
    public IReadOnlyList<string> Normalize()
    {
        var warnings = new List<string>();

        if (WindowWidth is < MinWindowWidth or > MaxWindowWidth)
        {
            warnings.Add(
                $"Window width {WindowWidth} is outside {MinWindowWidth}..{MaxWindowWidth}; using {DefaultWindowWidth}.");
            WindowWidth = DefaultWindowWidth;
        }

        if (WindowHeight is < MinWindowHeight or > MaxWindowHeight)
        {
            warnings.Add(
                $"Window height {WindowHeight} is outside {MinWindowHeight}..{MaxWindowHeight}; using {DefaultWindowHeight}.");
            WindowHeight = DefaultWindowHeight;
        }

        return warnings;
    }
}
