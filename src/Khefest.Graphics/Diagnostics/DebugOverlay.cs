using System.Numerics;
using System.Text;
using Khefest.Core.Profiling;
using Khefest.Core.Resources;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.Text;
using Khefest.Graphics.TwoD;

namespace Khefest.Graphics.Diagnostics;

/// <summary>
/// In-engine real-time diagnostics overlay HUD presenting FPS, frame times, render statistics, memory usage, and CPU profiling.
/// </summary>
public sealed class DebugOverlay
{
    private readonly FrameStatistics _frameStats;
    private readonly RenderStatistics _renderStats;
    private readonly ResourceManager? _resourceManager;

    public bool Visible { get; set; } = true;

    public DebugOverlay(
        FrameStatistics frameStats,
        RenderStatistics renderStats,
        ResourceManager? resourceManager = null)
    {
        _frameStats = frameStats ?? throw new ArgumentNullException(nameof(frameStats));
        _renderStats = renderStats ?? throw new ArgumentNullException(nameof(renderStats));
        _resourceManager = resourceManager;
    }

    /// <summary>
    /// Generates a structured multi-line text report of the current engine diagnostics.
    /// </summary>
    public string GetFormattedReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== KHEFEST ENGINE DIAGNOSTICS ===");
        sb.AppendLine($"Performance: {_frameStats.FramesPerSecond:F1} FPS | {_frameStats.FrameTimeMilliseconds:F2} ms (Frame #{_frameStats.FrameNumber})");

        var (avgMs, minMs, maxMs) = Profiler.GetFrameStatistics();
        if (avgMs > 0)
        {
            sb.AppendLine($"CPU Frame: Avg {avgMs:F2} ms | Min {minMs:F2} ms | Max {maxMs:F2} ms");
        }

        sb.AppendLine($"Render Stats: {_renderStats.TotalDrawCalls} Draw Calls ({_renderStats.DrawCallCount} standard, {_renderStats.IndexedDrawCallCount} indexed)");
        sb.AppendLine($"Geometry: {_renderStats.TriangleCount:N0} Tris | {_renderStats.VertexCount:N0} Verts | {_renderStats.IndexCount:N0} Indices");
        sb.AppendLine($"State Changes: {_renderStats.PipelineSwitches} Pipelines | {_renderStats.ShaderSwitches} Shaders | {_renderStats.TextureBinds} Textures");

        if (_resourceManager != null)
        {
            var resStats = _resourceManager.GetStatistics();
            sb.AppendLine($"Resources: {resStats.TotalResourceCount} Active | {resStats.TotalAllocatedBytes / 1024.0 / 1024.0:F2} MB Allocated");
        }

        sb.AppendLine($"GC Memory: {_frameStats.ManagedMemoryBytes / 1024.0 / 1024.0:F2} MB (GC0: {_frameStats.Gen0Collections}, GC1: {_frameStats.Gen1Collections}, GC2: {_frameStats.Gen2Collections})");

        var lastFrame = Profiler.LastCompletedFrame;
        if (lastFrame != null && lastFrame.RootSamples.Count > 0)
        {
            sb.AppendLine("CPU Profiler Breakdown:");
            foreach (var sample in lastFrame.RootSamples)
            {
                AppendSample(sb, sample, 1);
            }
        }

        sb.AppendLine("==================================");
        return sb.ToString();
    }

    private static void AppendSample(StringBuilder sb, ProfileSample sample, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 2);
        sb.AppendLine($"{indent}• {sample.Name}: {sample.ElapsedMilliseconds:F3} ms ({sample.CallCount} calls)");
        foreach (var child in sample.Children)
        {
            AppendSample(sb, child, indentLevel + 1);
        }
    }

    /// <summary>
    /// Renders the diagnostics HUD overlay onto the screen using a <see cref="SpriteBatch"/> with the default font.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Vector2 position)
    {
        if (!Visible) return;
        ArgumentNullException.ThrowIfNull(spriteBatch);

        string report = GetFormattedReport();
        string[] lines = report.Split('\n');

        float y = position.Y;
        const float lineHeight = 18f;

        foreach (var line in lines)
        {
            string cleanLine = line.TrimEnd('\r');
            Color4 textColor = cleanLine.StartsWith("===") ? Color4.Yellow :
                               cleanLine.StartsWith("Performance:") ? Color4.Green :
                               cleanLine.StartsWith("Render Stats:") ? Color4.Cyan :
                               Color4.White;

            spriteBatch.DrawString(cleanLine, new Vector2(position.X, y), textColor);
            y += lineHeight;
        }
    }

    /// <summary>
    /// Renders the diagnostics HUD overlay onto the screen using a custom <see cref="BitmapFont"/>.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, BitmapFont font, Vector2 position)
    {
        if (!Visible) return;
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(font);

        string report = GetFormattedReport();
        string[] lines = report.Split('\n');

        float y = position.Y;
        float lineHeight = font.LineHeight > 0 ? font.LineHeight : 18f;

        foreach (var line in lines)
        {
            string cleanLine = line.TrimEnd('\r');
            Color4 textColor = cleanLine.StartsWith("===") ? Color4.Yellow :
                               cleanLine.StartsWith("Performance:") ? Color4.Green :
                               cleanLine.StartsWith("Render Stats:") ? Color4.Cyan :
                               Color4.White;

            font.DrawString(spriteBatch, cleanLine, new Vector2(position.X, y), textColor);
            y += lineHeight;
        }
    }
}
