using Microsoft.Extensions.Logging;
using VL.Core;

namespace VL.Avalonia.Helpers;

/// <summary>
/// TEMPORARY diagnostic helper to verify the init-thread-lottery hypothesis
/// for the "slider handle stuck" bug. Logs the thread on which the first
/// channel->property write happens, then stays silent. Mirror of
/// VL.Avalonia.Skia.ThreadDiag for the platform-agnostic VL.Avalonia
/// assembly. Remove together with ThreadDiag once the bug is fixed.
/// </summary>
internal static class BindingThreadDiag
{
    private static readonly Lazy<ILogger> _logger = new(() =>
        AppHost.Current.LoggerFactory.CreateLogger("VL.Avalonia.Diag"));

    private static int _firstBindingWriteLogged;
    private static int _firstBindingSkipLogged;

    private const long ReportInterval = 25;
    private static long _writeTotal;
    private static long _writeNextReport = ReportInterval;
    private static long _skipTotal;
    private static long _skipNextReport = ReportInterval;

    public static void LogBindingWrite(string propertyName, object? oldValue, object? newValue)
    {
        if (Interlocked.Exchange(ref _firstBindingWriteLogged, 1) == 0)
        {
            var t = Thread.CurrentThread;
            var name = string.IsNullOrEmpty(t.Name) ? "(unnamed)" : t.Name;
            var bg = t.IsBackground ? " bg" : "";
            var pool = t.IsThreadPoolThread ? " pool" : "";
            _logger.Value.LogInformation(
                "First TwoWayBinding -> SetCurrentValue ({Property}) from #{ThreadId} '{ThreadName}'{Background}{Pool} (old={Old}, new={New})",
                propertyName,
                t.ManagedThreadId,
                name,
                bg,
                pool,
                oldValue,
                newValue);
        }
        var total = Interlocked.Increment(ref _writeTotal);
        var next = Interlocked.Read(ref _writeNextReport);
        if (total >= next
            && Interlocked.CompareExchange(ref _writeNextReport, next + ReportInterval, next) == next)
        {
            _logger.Value.LogInformation(
                "Binding write heartbeat ({Property}): writes={Writes}, skipped={Skipped}, latest old={Old}, new={New}",
                propertyName,
                total,
                Interlocked.Read(ref _skipTotal),
                oldValue,
                newValue);
        }
    }

    public static void LogBindingSkip(string propertyName, object? currentValue, object? incomingValue)
    {
        if (Interlocked.Exchange(ref _firstBindingSkipLogged, 1) == 0)
        {
            _logger.Value.LogInformation(
                "First TwoWayBinding SKIP ({Property}) — Equals(current={Current}, incoming={Incoming}) was true",
                propertyName,
                currentValue,
                incomingValue);
        }
        var total = Interlocked.Increment(ref _skipTotal);
        var next = Interlocked.Read(ref _skipNextReport);
        if (total >= next
            && Interlocked.CompareExchange(ref _skipNextReport, next + ReportInterval, next) == next)
        {
            _logger.Value.LogInformation(
                "Binding skip heartbeat ({Property}): skipped={Skipped}, current={Current}, incoming={Incoming}",
                propertyName,
                total,
                currentValue,
                incomingValue);
        }
    }
}
