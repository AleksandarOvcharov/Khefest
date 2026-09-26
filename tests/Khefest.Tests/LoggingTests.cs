using System.Collections.Concurrent;
using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Xunit;

namespace Khefest.Tests;

public class LoggingTests
{
    [Fact]
    public void LogManager_DispatchesToSinks()
    {
        var memorySink = new MemoryLogSink(100);
        LogManager.AddSink(memorySink);

        var logger = LogManager.GetLogger(KhefestSubsystem.Core, "TestLogger");
        logger.Info("Hello world test message");

        var entries = memorySink.GetRecentEntries();
        Assert.Contains(entries, e => e.Message == "Hello world test message");

        LogManager.RemoveSink(memorySink);
    }

    [Fact]
    public void SubsystemLogLevel_FiltersMessagesBelowThreshold()
    {
        var received = new ConcurrentBag<LogEntry>();
        var sink = new CallbackLogSink(entry => received.Add(entry));
        LogManager.AddSink(sink);

        LogManager.SetSubsystemLevel(KhefestSubsystem.Audio, LogLevel.Warning);
        var audioLogger = LogManager.GetLogger(KhefestSubsystem.Audio);

        audioLogger.Info("Should not be received");
        audioLogger.Warning("Should be received");
        audioLogger.Error("Should also be received");

        var list = received.ToList();
        Assert.DoesNotContain(list, e => e.Message == "Should not be received");
        Assert.Contains(list, e => e.Message == "Should be received");
        Assert.Contains(list, e => e.Message == "Should also be received");

        LogManager.RemoveSink(sink);
    }
}
