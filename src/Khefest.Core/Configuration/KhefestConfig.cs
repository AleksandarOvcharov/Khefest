namespace Khefest.Core.Configuration;

/// <summary>
/// Master configuration object for initializing Khefest applications.
/// </summary>
public sealed record KhefestConfig
{
    public WindowConfig Window { get; init; } = new();
    public GraphicsConfig Graphics { get; init; } = new();
    public MemoryConfig Memory { get; init; } = new();
    public LoggingConfig Logging { get; init; } = new();

    private readonly Dictionary<string, string> _customSettings = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> CustomSettings => _customSettings;

    public KhefestConfig WithSetting(string key, string value)
    {
        var copy = this with { };
        copy._customSettings[key] = value;
        return copy;
    }

    public string? GetSetting(string key) => _customSettings.GetValueOrDefault(key);
}

/// <summary>
/// Fluent builder for constructing a validated <see cref="KhefestConfig"/>.
/// </summary>
public sealed class KhefestConfigBuilder
{
    private WindowConfig _window = new();
    private GraphicsConfig _graphics = new();
    private MemoryConfig _memory = new();
    private LoggingConfig _logging = new();
    private readonly Dictionary<string, string> _customSettings = new(StringComparer.OrdinalIgnoreCase);

    public KhefestConfigBuilder ConfigureWindow(Func<WindowConfig, WindowConfig> configure)
    {
        _window = configure(_window);
        return this;
    }

    public KhefestConfigBuilder ConfigureGraphics(Func<GraphicsConfig, GraphicsConfig> configure)
    {
        _graphics = configure(_graphics);
        return this;
    }

    public KhefestConfigBuilder ConfigureMemory(Func<MemoryConfig, MemoryConfig> configure)
    {
        _memory = configure(_memory);
        return this;
    }

    public KhefestConfigBuilder ConfigureLogging(Func<LoggingConfig, LoggingConfig> configure)
    {
        _logging = configure(_logging);
        return this;
    }

    public KhefestConfigBuilder Set(string key, string value)
    {
        _customSettings[key] = value;
        return this;
    }

    public KhefestConfig Build()
    {
        // Validation rules
        if (_window.Width <= 0 || _window.Height <= 0)
        {
            throw new ArgumentException($"Invalid window dimensions: {_window.Width}x{_window.Height}. Dimensions must be positive.");
        }

        var config = new KhefestConfig
        {
            Window = _window,
            Graphics = _graphics,
            Memory = _memory,
            Logging = _logging
        };

        foreach (var kv in _customSettings)
        {
            config.WithSetting(kv.Key, kv.Value);
        }

        return config;
    }
}
