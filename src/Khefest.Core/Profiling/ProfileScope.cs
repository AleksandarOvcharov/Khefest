namespace Khefest.Core.Profiling;

/// <summary>
/// High-performance zero-allocation ref struct for scoped profiling blocks.
/// Emits timing information to <see cref="Profiler"/> when disposed.
/// </summary>
public readonly ref struct ProfileScope
{
    private readonly bool _active;

    public ProfileScope(string name)
    {
        if (Profiler.IsEnabled)
        {
            Profiler.BeginSample(name);
            _active = true;
        }
        else
        {
            _active = false;
        }
    }

    public void Dispose()
    {
        if (_active)
        {
            Profiler.EndSample();
        }
    }
}
