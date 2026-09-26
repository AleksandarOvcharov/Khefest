namespace Khefest.Core.Resources;

/// <summary>
/// Strongly-typed unique identifier for resources.
/// </summary>
public readonly record struct ResourceId(ulong Value) : IComparable<ResourceId>
{
    private static ulong _nextId;

    public static ResourceId Invalid => new(0);

    public bool IsValid => Value != 0;

    public static ResourceId Generate()
    {
        return new ResourceId(Interlocked.Increment(ref _nextId));
    }

    public int CompareTo(ResourceId other) => Value.CompareTo(other.Value);

    public override string ToString() => $"Res#{Value}";
}

/// <summary>
/// Lightweight handle representing an allocated resource of type <typeparamref name="T"/>..
/// Zero-allocation value type safe to pass through rendering commands.
/// </summary>
public readonly record struct ResourceHandle<T>(ResourceId Id) where T : class, IResource
{
    public static ResourceHandle<T> Invalid => new(ResourceId.Invalid);

    public bool IsValid => Id.IsValid;
}
