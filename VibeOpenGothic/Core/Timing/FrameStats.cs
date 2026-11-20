namespace VibeOpenGothic.Core.Timing;

/// <summary>
/// Frame and logic counters for the debug title and logs.
/// </summary>
public sealed class FrameStats
{
    private double _fpsWindowSeconds;
    private int _fpsWindowFrames;
    private double _fps;
    private double _smoothedUpdateMs;
    private double _smoothedDrawMs;

    public long FrameCount { get; private set; }

    public long LogicStepCount { get; private set; }

    public double Fps => _fps;

    public double SmoothedUpdateMs => _smoothedUpdateMs;

    public double SmoothedDrawMs => _smoothedDrawMs;

    public double SmoothedFrameMs => _smoothedUpdateMs + _smoothedDrawMs;

    public void AddLogicStep()
    {
        LogicStepCount++;
    }

    public void AddFrame(double elapsedSeconds, double updateMs, double drawMs)
    {
        FrameCount++;

        if (elapsedSeconds > 0 && double.IsFinite(elapsedSeconds))
        {
            _fpsWindowFrames++;
            _fpsWindowSeconds += elapsedSeconds;

            if (_fpsWindowSeconds >= 1.0)
            {
                _fps = _fpsWindowFrames / _fpsWindowSeconds;
                _fpsWindowFrames = 0;
                _fpsWindowSeconds = 0;
            }
        }

        _smoothedUpdateMs = Smooth(_smoothedUpdateMs, updateMs);
        _smoothedDrawMs = Smooth(_smoothedDrawMs, drawMs);
    }

    public void Reset()
    {
        FrameCount = 0;
        LogicStepCount = 0;
        _fpsWindowSeconds = 0;
        _fpsWindowFrames = 0;
        _fps = 0;
        _smoothedUpdateMs = 0;
        _smoothedDrawMs = 0;
    }

    private static double Smooth(double current, double sample)
    {
        return current <= 0 ? sample : current * 0.9 + sample * 0.1;
    }
}
