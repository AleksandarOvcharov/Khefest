using System.Collections.Concurrent;
using System.Text;
using Khefest.Core.Errors;
using Khefest.Core.Logging;

namespace Khefest.Core.Resources;

/// <summary>
/// Monitors alive resources, captures diagnostic stacks, and verifies leak-free shutdowns.
/// </summary>
public sealed class ResourceLifetimeTracker
{
    private readonly ConcurrentDictionary<ResourceId, IResource> _tracked = new();
    private readonly ILogger _logger = LogManager.GetLogger(KhefestSubsystem.Resource, "LifetimeTracker");

    public bool EnableLeakTracking { get; set; }

    public ResourceLifetimeTracker(bool enableLeakTracking = true)
    {
        EnableLeakTracking = enableLeakTracking;
    }

    public void Track(IResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _tracked.TryAdd(resource.Id, resource);
        _logger.Trace($"Tracked resource '{resource.Name}' ({resource.Id}, {resource.Type}, {resource.SizeInBytes} bytes)");
    }

    public void Untrack(ResourceId id)
    {
        if (_tracked.TryRemove(id, out var resource))
        {
            _logger.Trace($"Untracked resource '{resource.Name}' ({id})");
        }
    }

    public IReadOnlyCollection<IResource> GetActiveResources() => _tracked.Values.ToArray();

    public bool HasLeaks => CheckForLeaks().Count > 0;

    public IReadOnlyList<IResource> CheckForLeaks()
    {
        var leaked = _tracked.Values.Where(r => !r.IsDisposed).ToList();
        return leaked;
    }

    public string GenerateLeakReport()
    {
        var leaked = CheckForLeaks();
        if (leaked.Count == 0)
        {
            return "No resource leaks detected. All managed and native resources were cleanly disposed.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"=== KHEFEST RESOURCE LEAK REPORT ({leaked.Count} Leaks Detected) ===");
        long totalBytes = 0;
        foreach (var res in leaked)
        {
            totalBytes += res.SizeInBytes;
            sb.AppendLine($"[Leak] Name: '{res.Name}', Type: {res.Type}, Size: {res.SizeInBytes} bytes, ID: {res.Id.Value}");
            if (!string.IsNullOrEmpty(res.AllocationStackTrace))
            {
                sb.AppendLine("  Allocation Stack:");
                sb.AppendLine("  " + res.AllocationStackTrace.Replace("\n", "\n  "));
            }
        }
        sb.AppendLine($"Total Leaked Memory: {totalBytes} bytes ({totalBytes / 1024.0:F2} KB)");
        sb.AppendLine("==================================================================");

        return sb.ToString();
    }

    public void EnforceNoLeaks()
    {
        var leaked = CheckForLeaks();
        if (leaked.Count == 0)
        {
            return;
        }

        var first = leaked[0];
        _logger.Error($"Memory leak detected! {leaked.Count} resources were not disposed prior to shutdown. First leak: '{first.Name}' (ID {first.Id.Value}, {first.Type})");

        foreach (var res in leaked)
        {
            _logger.Warning($"Leaked: '{res.Name}' (ID {res.Id.Value}, {res.Type}, {res.SizeInBytes} bytes). Stack:\n{res.AllocationStackTrace ?? "No stack captured"}");
        }

        throw KhefestResourceException.LeakDetected(first.Name, first.Id.Value, first.AllocationStackTrace);
    }

    public void Clear()
    {
        _tracked.Clear();
    }
}
