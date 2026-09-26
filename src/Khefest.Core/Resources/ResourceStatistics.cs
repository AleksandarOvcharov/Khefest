namespace Khefest.Core.Resources;

/// <summary>
/// Snapshot of current resource allocations and memory utilization.
/// </summary>
public sealed record ResourceStatistics
{
    public int TotalResourceCount { get; init; }
    public long TotalAllocatedBytes { get; init; }
    public IReadOnlyDictionary<ResourceType, int> CountsByType { get; init; } = new Dictionary<ResourceType, int>();
    public IReadOnlyDictionary<ResourceType, long> BytesByType { get; init; } = new Dictionary<ResourceType, long>();
}
