using Microsoft.Extensions.Logging;
using VL.Core;

namespace VL.Avalonia.Skia
{
    /// <summary>
    /// TEMPORARY diagnostic helper to verify the init-thread-lottery hypothesis
    /// for the "slider handle stuck" bug. Logs the thread on which key entry
    /// points first run, then stays silent. Remove once the bug is fixed.
    /// </summary>
    internal static class ThreadDiag
    {
        // Lazy because AppHost.Current may not be ready at type-load time on
        // every host. CreateLogger gives us the standard VL category/recorder.
        private static readonly Lazy<ILogger> _logger = new(() =>
            AppHost.Current.LoggerFactory.CreateLogger("VL.Avalonia.Diag"));

        // First-call latches
        private static int _initLogged;
        private static int _dispatcherCtorLogged;
        private static int _layerCtorLogged;
        private static int _firstRenderLogged;
        private static int _firstSignalLogged;
        private static int _firstSignaledInvokeLogged;
        private static int _firstNotifyLogged;
        private static int _firstBindingPropertyWriteLogged;

        // Periodic counters
        private const long SignalReportInterval = 50;
        private static long _signalTotal;
        private static long _signalNextReport = SignalReportInterval;

        private const long SignaledInvokeReportInterval = 50;
        private static long _signaledInvokeTotal;
        private static long _signaledInvokeNextReport = SignaledInvokeReportInterval;

        private const long RenderReportInterval = 120; // ~2s at 60fps
        private static long _renderTotal;
        private static long _renderNextReport = RenderReportInterval;

        // CurrentThreadIsLoopThread counters
        private static long _isLoopThreadTrue;
        private static long _isLoopThreadFalse;
        private const long IsLoopThreadReportInterval = 256;
        private static long _isLoopThreadNextReport = IsLoopThreadReportInterval;

        // Captured threads — let us answer "are they the same?"
        private static Thread? _initThread;
        private static Thread? _dispatcherMainThread;
        private static Thread? _firstRenderThread;
        private static Thread? _firstSignalThread;
        private static Thread? _firstNotifyThread;
        private static Thread? _firstBindingWriteThread;

        public static void LogInit()
        {
            if (Interlocked.Exchange(ref _initLogged, 1) != 0)
                return;
            _initThread = Thread.CurrentThread;
            _logger.Value.LogInformation(
                "AvaloniaInitializer.Init() running on {Thread}",
                Describe(_initThread));
        }

        public static void LogDispatcherCtor(Thread mainThread)
        {
            if (Interlocked.Exchange(ref _dispatcherCtorLogged, 1) != 0)
                return;
            _dispatcherMainThread = mainThread;
            _logger.Value.LogInformation(
                "GammaDispatcherImpl ctor — _mainThread = {MainThread}, current = {CurrentThread}",
                Describe(mainThread),
                Describe(Thread.CurrentThread));
            if (mainThread != Thread.CurrentThread)
            {
                _logger.Value.LogWarning(
                    "_mainThread differs from current thread — Dispatcher captured a different thread than the caller");
            }
        }

        public static void LogLayerCtor()
        {
            if (Interlocked.Exchange(ref _layerCtorLogged, 1) != 0)
                return;
            _logger.Value.LogInformation(
                "AvaloniaLayer ctor on {Thread}",
                Describe(Thread.CurrentThread));
        }

        public static void LogRender()
        {
            if (Interlocked.Exchange(ref _firstRenderLogged, 1) == 0)
            {
                _firstRenderThread = Thread.CurrentThread;
                _logger.Value.LogInformation(
                    "First AvaloniaLayer.Render on {Thread}",
                    Describe(_firstRenderThread));
                ReportConsistency();
            }
            var total = Interlocked.Increment(ref _renderTotal);
            var next = Interlocked.Read(ref _renderNextReport);
            if (total >= next
                && Interlocked.CompareExchange(ref _renderNextReport, next + RenderReportInterval, next) == next)
            {
                _logger.Value.LogInformation(
                    "Render heartbeat: total={Total}, signals={Signals}, signaledInvokes={SignaledInvokes}",
                    total,
                    Interlocked.Read(ref _signalTotal),
                    Interlocked.Read(ref _signaledInvokeTotal));
            }
        }

        public static void LogSignal()
        {
            if (Interlocked.Exchange(ref _firstSignalLogged, 1) == 0)
            {
                _firstSignalThread = Thread.CurrentThread;
                _logger.Value.LogInformation(
                    "First GammaDispatcherImpl.Signal() called from {Thread}",
                    Describe(_firstSignalThread));
            }
            var total = Interlocked.Increment(ref _signalTotal);
            var next = Interlocked.Read(ref _signalNextReport);
            if (total >= next
                && Interlocked.CompareExchange(ref _signalNextReport, next + SignalReportInterval, next) == next)
            {
                _logger.Value.LogInformation(
                    "Signal() heartbeat: total={Total} (invoked={Invoked})",
                    total,
                    Interlocked.Read(ref _signaledInvokeTotal));
            }
        }

        public static void LogSignaledInvoke()
        {
            if (Interlocked.Exchange(ref _firstSignaledInvokeLogged, 1) == 0)
            {
                _logger.Value.LogInformation(
                    "First InvokeSignaled (Dispatcher pump) on {Thread}",
                    Describe(Thread.CurrentThread));
            }
            var total = Interlocked.Increment(ref _signaledInvokeTotal);
            var next = Interlocked.Read(ref _signaledInvokeNextReport);
            if (total >= next
                && Interlocked.CompareExchange(ref _signaledInvokeNextReport, next + SignaledInvokeReportInterval, next) == next)
            {
                _logger.Value.LogInformation(
                    "InvokeSignaled heartbeat: total={Total}",
                    total);
            }
        }

        public static void LogFirstNotify()
        {
            if (Interlocked.Exchange(ref _firstNotifyLogged, 1) != 0)
                return;
            _firstNotifyThread = Thread.CurrentThread;
            _logger.Value.LogInformation(
                "First Notify (input event) on {Thread}",
                Describe(_firstNotifyThread));
        }

        public static void LogFirstBindingWrite(string propertyName)
        {
            if (Interlocked.Exchange(ref _firstBindingPropertyWriteLogged, 1) != 0)
                return;
            _firstBindingWriteThread = Thread.CurrentThread;
            _logger.Value.LogInformation(
                "First TwoWayBinding -> SetCurrentValue ({Property}) from {Thread}",
                propertyName,
                Describe(_firstBindingWriteThread));
            ReportConsistency();
        }

        public static void CountLoopThreadCheck(bool isLoopThread)
        {
            if (isLoopThread)
                Interlocked.Increment(ref _isLoopThreadTrue);
            else
                Interlocked.Increment(ref _isLoopThreadFalse);

            var total = _isLoopThreadTrue + _isLoopThreadFalse;
            var next = Interlocked.Read(ref _isLoopThreadNextReport);
            if (total >= next
                && Interlocked.CompareExchange(
                    ref _isLoopThreadNextReport,
                    next + IsLoopThreadReportInterval,
                    next) == next)
            {
                _logger.Value.LogInformation(
                    "CurrentThreadIsLoopThread stats: true={TrueCount}, false={FalseCount}, total={Total}",
                    _isLoopThreadTrue,
                    _isLoopThreadFalse,
                    total);
            }
        }

        private static void ReportConsistency()
        {
            if (_dispatcherMainThread is null)
                return;

            Compare("init-thread", _initThread);
            Compare("first-render-thread", _firstRenderThread);
            Compare("first-binding-write-thread", _firstBindingWriteThread);
            Compare("first-notify-thread", _firstNotifyThread);

            void Compare(string label, Thread? other)
            {
                if (other is null)
                    return;
                var same = other == _dispatcherMainThread;
                if (same)
                {
                    _logger.Value.LogInformation(
                        "consistency OK: {Label} ({Thread}) matches _mainThread",
                        label,
                        Describe(other));
                }
                else
                {
                    _logger.Value.LogWarning(
                        "consistency MISMATCH: {Label} ({Thread}) differs from _mainThread ({MainThread})",
                        label,
                        Describe(other),
                        Describe(_dispatcherMainThread));
                }
            }
        }

        private static string Describe(Thread? t)
        {
            if (t is null)
                return "<null>";
            var name = string.IsNullOrEmpty(t.Name) ? "(unnamed)" : t.Name;
            var bg = t.IsBackground ? " bg" : "";
            var pool = t.IsThreadPoolThread ? " pool" : "";
            return $"#{t.ManagedThreadId} '{name}'{bg}{pool}";
        }
    }
}
