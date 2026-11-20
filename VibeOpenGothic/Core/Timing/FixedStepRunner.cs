namespace VibeOpenGothic.Core.Timing;

/// <summary>
/// Fixed-step accumulator for game logic. Rendering may run at any rate; logic
/// runs at a constant rate with a bounded catch-up to avoid spiral-of-death
/// behaviour after a long frame.
/// </summary>
public sealed class FixedStepRunner
{
    private const double Epsilon = 1e-9;

    private readonly double _stepSeconds;
    private readonly int _maxStepsPerFrame;
    private double _accumulator;

    public FixedStepRunner(TimeSpan step, int maxStepsPerFrame = 4)
    {
        if (step <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "The step must be positive.");
        }

        if (maxStepsPerFrame <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxStepsPerFrame), "At least one step is required.");
        }

        _stepSeconds = step.TotalSeconds;
        _maxStepsPerFrame = maxStepsPerFrame;
    }

    public double StepSeconds => _stepSeconds;

    public int MaxStepsPerFrame => _maxStepsPerFrame;

    /// <summary>
    /// Adds the elapsed frame time and invokes <paramref name="step"/> once per
    /// fixed step. Time beyond the catch-up cap is discarded.
    /// </summary>
    /// <returns>The number of logic steps executed.</returns>
    public int Advance(double elapsedSeconds, Action<double> step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            elapsedSeconds = 0;
        }

        var maxElapsed = _stepSeconds * _maxStepsPerFrame;
        if (elapsedSeconds > maxElapsed)
        {
            elapsedSeconds = maxElapsed;
        }

        _accumulator += elapsedSeconds;

        var steps = 0;
        while (steps < _maxStepsPerFrame && _accumulator + Epsilon >= _stepSeconds)
        {
            step(_stepSeconds);
            _accumulator -= _stepSeconds;
            steps++;
        }

        if (_accumulator < 0)
        {
            _accumulator = 0;
        }

        return steps;
    }

    public void Reset()
    {
        _accumulator = 0;
    }
}
