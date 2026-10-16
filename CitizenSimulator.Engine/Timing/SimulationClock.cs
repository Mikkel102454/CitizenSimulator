using System.Diagnostics;
using CitizenSimulator.Abstractions.Timing;

namespace CitizenSimulator.Engine.Timing;

/// <summary>
/// Single-threaded fixed-step clock. Feed real elapsed time before applying controls,
/// then run a bounded batch. A failed callback faults the clock because world changes
/// cannot be rolled back safely.
/// </summary>
public sealed class SimulationClock : ISimulationClock
{
    private double _pendingTicks;
    private ulong? _targetTick;
    private bool _faulted;
    private bool _executing;
    private double _speed = 1;
    private double _sampleSeconds;
    private ulong _sampleTicks;
    private double _sampleCost;
    private double _sampleMaxCost;
    private double _achievedRate;
    private TimeSpan _averageCost;
    private TimeSpan _maximumCost;

    public SimulationClock(int tickRate = 30, int maxTicksPerBatch = 8)
    {
        if (tickRate < 1 || tickRate > (int)TimeSpan.TicksPerSecond)
            throw new ArgumentOutOfRangeException(nameof(tickRate));
        if (maxTicksPerBatch < 1)
            throw new ArgumentOutOfRangeException(nameof(maxTicksPerBatch));

        TickRate = tickRate;
        MaxTicksPerBatch = maxTicksPerBatch;
        Delta = TimeSpan.FromSeconds(1.0 / tickRate);
    }

    public int TickRate { get; }
    public int MaxTicksPerBatch { get; }
    public ulong Tick { get; private set; }
    public TimeSpan Delta { get; }
    public TimeSpan Elapsed => TimeAt(Tick);
    public bool IsPaused { get; private set; }
    public double Speed => _speed;
    public bool IsAdvancing => _targetTick.HasValue;
    public ClockDiagnostics Diagnostics => new(
        _achievedRate, _averageCost, _maximumCost, _pendingTicks,
        !IsPaused && _pendingTicks >= MaxTicksPerBatch);

    public TimeSpan SuggestedWait
    {
        get
        {
            if (IsAdvancing || (!IsPaused && _pendingTicks >= 1))
                return TimeSpan.Zero;
            if (IsPaused)
                return TimeSpan.FromMilliseconds(20);
            return TimeSpan.FromSeconds(Math.Min(0.02, (1 - _pendingTicks) / TickRate / Speed));
        }
    }

    public void AddRealTime(TimeSpan elapsed)
    {
        EnsureControllable();
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed));

        if (!IsPaused)
        {
            var added = elapsed.TotalSeconds * TickRate * Speed;
            if (!double.IsFinite(added) || !double.IsFinite(_pendingTicks + added))
                throw new OverflowException("The requested speed exceeds clock backlog capacity.");
            _pendingTicks += added;
        }
        _sampleSeconds += elapsed.TotalSeconds;
        if (_sampleSeconds >= 1)
        {
            _achievedRate = _sampleTicks / _sampleSeconds;
            _averageCost = TimeSpan.FromSeconds(_sampleTicks == 0 ? 0 : _sampleCost / _sampleTicks);
            _maximumCost = TimeSpan.FromSeconds(_sampleMaxCost);
            _sampleSeconds = 0;
            _sampleTicks = 0;
            _sampleCost = 0;
            _sampleMaxCost = 0;
        }
    }

    public void SetSpeed(double speed)
    {
        EnsureControllable();
        if (!double.IsFinite(speed) || speed <= 0)
            throw new ArgumentOutOfRangeException(nameof(speed), "Speed must be finite and positive.");
        _speed = speed;
    }

    public void Pause()
    {
        EnsureControllable();
        IsPaused = true;
        _targetTick = null;
    }

    public void Resume()
    {
        EnsureControllable();
        if (IsAdvancing)
            throw new InvalidOperationException("Pause to cancel advancement before resuming.");
        IsPaused = false;
    }

    public void Step(ulong count = 1)
    {
        EnsureManualControl();
        if (count == 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        SetTarget(checked(Tick + count));
    }

    public void AdvanceBy(TimeSpan duration)
    {
        EnsureManualControl();
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));
        AdvanceTo(Elapsed + duration);
    }

    public void AdvanceTo(TimeSpan target)
    {
        EnsureManualControl();
        if (target < Elapsed)
            throw new ArgumentOutOfRangeException(nameof(target), "Target precedes current simulation time.");

        // Integer arithmetic avoids rounding an exact tick boundary up by accident.
        var ticks = (ulong)decimal.Ceiling((decimal)target.Ticks * TickRate / TimeSpan.TicksPerSecond);
        // TimeSpan is rounded to 100ns; recognize the exposed current timestamp.
        SetTarget(target == Elapsed ? Tick : ticks);
    }

    public int RunBatch(Action<SimulationTime> update, CancellationToken cancellation = default, Action? afterTick = null)
    {
        ArgumentNullException.ThrowIfNull(update);
        EnsureControllable();
        var completed = 0;
        while (completed < MaxTicksPerBatch && !cancellation.IsCancellationRequested && 
               (IsAdvancing || (!IsPaused && _pendingTicks >= 1 - 1e-9))) // 1e-9 for floating point precession
        {
            var manual = IsAdvancing;
            var nextTick = checked(Tick + 1);
            var time = new SimulationTime(nextTick, Delta, TimeAt(nextTick));
            var started = Stopwatch.GetTimestamp();
            _executing = true;
            try
            {
                update(time);
            }
            catch
            {
                _faulted = true;
                throw;
            }
            finally
            {
                _executing = false;
            }

            Tick = nextTick;
            var cost = Stopwatch.GetElapsedTime(started).TotalSeconds;
            _sampleTicks++;
            _sampleCost += cost;
            _sampleMaxCost = Math.Max(_sampleMaxCost, cost);
            completed++;

            if (manual)
            {
                if (Tick == _targetTick)
                    _targetTick = null;
            }
            else
            {
                _pendingTicks = Math.Max(0, _pendingTicks - 1);
            }
            afterTick?.Invoke();
        }
        return completed;
    }

    private void SetTarget(ulong target)
    {
        _ = TimeAt(target); // Validate the representable time before changing state.
        _pendingTicks = 0;
        _targetTick = target > Tick ? target : null;
    }

    private TimeSpan TimeAt(ulong tick) =>
        TimeSpan.FromTicks(checked((long)((decimal)tick * TimeSpan.TicksPerSecond / TickRate)));

    private void EnsureManualControl()
    {
        EnsureControllable();
        if (!IsPaused || IsAdvancing)
            throw new InvalidOperationException("Pause before stepping or advancing.");
    }

    private void EnsureControllable()
    {
        if (_faulted)
            throw new InvalidOperationException("The simulation clock faulted during a tick.");
        if (_executing)
            throw new InvalidOperationException("Clock controls cannot be changed inside a system update.");
    }
}
