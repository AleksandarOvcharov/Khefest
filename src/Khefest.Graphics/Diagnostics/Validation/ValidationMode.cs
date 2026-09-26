namespace Khefest.Graphics.Diagnostics.Validation;

/// <summary>
/// Operational mode for GPU API validation layer.
/// </summary>
public enum ValidationMode
{
    /// <summary>
    /// Validation is disabled for maximum throughput.
    /// </summary>
    Disabled,

    /// <summary>
    /// Warnings are logged for non-fatal irregularities, execution continues.
    /// </summary>
    Warning,

    /// <summary>
    /// Strict mode: any contract violation immediately throws an exception.
    /// </summary>
    Strict
}
