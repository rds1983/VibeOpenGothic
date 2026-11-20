using VibeOpenGothic.Core.Timing;

namespace VibeOpenGothic.Tests.Unit;

public sealed class FixedStepRunnerTests
{
	[Fact]
	public void Advance_BelowStep_RunsNoLogic()
	{
		var runner = new FixedStepRunner(TimeSpan.FromMilliseconds(10), maxStepsPerFrame: 3);
		var steps = 0;

		var executed = runner.Advance(0.004, _ => steps++);

		Assert.Equal(0, executed);
		Assert.Equal(0, steps);
	}

	[Fact]
	public void Advance_AccumulatesFractionalTime()
	{
		var runner = new FixedStepRunner(TimeSpan.FromMilliseconds(10), maxStepsPerFrame: 3);
		var steps = 0;

		Assert.Equal(0, runner.Advance(0.004, _ => steps++));
		Assert.Equal(1, runner.Advance(0.007, _ => steps++));
		Assert.Equal(1, steps);
	}

	[Fact]
	public void Advance_ClampsCatchUpToMaxSteps()
	{
		var runner = new FixedStepRunner(TimeSpan.FromMilliseconds(10), maxStepsPerFrame: 3);
		var steps = 0;

		var executed = runner.Advance(1.0, _ => steps++);

		Assert.Equal(3, executed);
		Assert.Equal(3, steps);
	}

	[Fact]
	public void Advance_NegativeElapsed_RunsNoLogic()
	{
		var runner = new FixedStepRunner(TimeSpan.FromMilliseconds(10), maxStepsPerFrame: 3);

		var executed = runner.Advance(-1.0, _ => { });

		Assert.Equal(0, executed);
	}

	[Fact]
	public void Reset_ClearsAccumulatedTime()
	{
		var runner = new FixedStepRunner(TimeSpan.FromMilliseconds(10), maxStepsPerFrame: 3);
		runner.Advance(0.005, _ => { });

		runner.Reset();

		Assert.Equal(0, runner.Advance(0.005, _ => { }));
	}

	[Fact]
	public void Constructor_RejectsNonPositiveStep()
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new FixedStepRunner(TimeSpan.Zero));
	}

	[Fact]
	public void Constructor_RejectsNonPositiveMaxSteps()
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new FixedStepRunner(TimeSpan.FromMilliseconds(10), maxStepsPerFrame: 0));
	}
}
