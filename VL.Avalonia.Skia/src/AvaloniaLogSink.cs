using Avalonia.Logging;
using Microsoft.Extensions.Logging;
using VL.Core;

namespace VL.Avalonia.Skia
{
    /// <summary>
    /// TEMPORARY: Routes Avalonia's internal trace logs (Layout, Visual, Property, …)
    /// to the VL ILogger so we can see whether LayoutManager/MediaContext is reacting
    /// to property changes. Remove together with ThreadDiag once the bug is fixed.
    /// </summary>
    internal sealed class AvaloniaLogSink : ILogSink
    {
        private static readonly Lazy<ILogger> _logger = new(() =>
            AppHost.Current.LoggerFactory.CreateLogger("VL.Avalonia.Diag.Avalonia"));

        // Only show selected areas — Visual/Layout/Property are the ones we care about
        // for the "slider doesn't update visually" bug. Add/remove as needed.
        private static readonly HashSet<string> _interestingAreas = new(StringComparer.OrdinalIgnoreCase)
        {
            LogArea.Layout,
            LogArea.Visual,
            LogArea.Property,
            LogArea.Binding,
            LogArea.Control,
            LogArea.Animations,
        };

        private readonly LogEventLevel _minLevel;

        public AvaloniaLogSink(LogEventLevel minLevel)
        {
            _minLevel = minLevel;
        }

        public bool IsEnabled(LogEventLevel level, string area)
            => level >= _minLevel && _interestingAreas.Contains(area);

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
            => Emit(level, area, source, messageTemplate, Array.Empty<object?>());

        public void Log<T0>(LogEventLevel level, string area, object? source, string messageTemplate, T0 propertyValue0)
            => Emit(level, area, source, messageTemplate, new object?[] { propertyValue0 });

        public void Log<T0, T1>(LogEventLevel level, string area, object? source, string messageTemplate, T0 propertyValue0, T1 propertyValue1)
            => Emit(level, area, source, messageTemplate, new object?[] { propertyValue0, propertyValue1 });

        public void Log<T0, T1, T2>(LogEventLevel level, string area, object? source, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2)
            => Emit(level, area, source, messageTemplate, new object?[] { propertyValue0, propertyValue1, propertyValue2 });

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
            => Emit(level, area, source, messageTemplate, propertyValues);

        private void Emit(LogEventLevel level, string area, object? source, string messageTemplate, object?[] propertyValues)
        {
            if (!IsEnabled(level, area))
                return;

            var msLevel = level switch
            {
                LogEventLevel.Verbose => LogLevel.Trace,
                LogEventLevel.Debug => LogLevel.Debug,
                LogEventLevel.Information => LogLevel.Information,
                LogEventLevel.Warning => LogLevel.Warning,
                LogEventLevel.Error => LogLevel.Error,
                LogEventLevel.Fatal => LogLevel.Critical,
                _ => LogLevel.Information,
            };

            // Pre-format because Avalonia's templates use {} for positional args and our
            // structured-logging template parser would otherwise complain about mismatch.
            var formatted = TryFormat(messageTemplate, propertyValues);
            var sourceType = source?.GetType().Name ?? "(no source)";
            _logger.Value.Log(msLevel, "{Area} [{Source}] {Message}", area, sourceType, formatted);
        }

        private static string TryFormat(string template, object?[] values)
        {
            if (values is null || values.Length == 0)
                return template;
            try
            {
                // Avalonia uses Serilog-style "{Name}" placeholders. Replace each in order.
                var sb = new System.Text.StringBuilder(template.Length + 32);
                int i = 0, idx = 0;
                while (i < template.Length)
                {
                    var open = template.IndexOf('{', i);
                    if (open < 0)
                    {
                        sb.Append(template, i, template.Length - i);
                        break;
                    }
                    sb.Append(template, i, open - i);
                    var close = template.IndexOf('}', open);
                    if (close < 0)
                    {
                        sb.Append(template, open, template.Length - open);
                        break;
                    }
                    if (idx < values.Length)
                        sb.Append(values[idx++]?.ToString() ?? "null");
                    else
                        sb.Append(template, open, close - open + 1);
                    i = close + 1;
                }
                return sb.ToString();
            }
            catch
            {
                return template;
            }
        }
    }
}
