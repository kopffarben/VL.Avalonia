using Avalonia;
using Avalonia.Logging;
using VL.Core.CompilerServices;
using Application = Avalonia.Application;

[assembly: AssemblyInitializer(typeof(VL.Avalonia.Skia.AvaloniaInitializer))]

namespace VL.Avalonia.Skia
{
    public sealed class AvaloniaInitializer : AssemblyInitializer<AvaloniaInitializer>
    {
        // There is already static Application.Current.Instance
        // in Avalonia, might it's better to use it?
        public static Application Instance;

        public static void Init()
        {
            ThreadDiag.LogInit();

            // Route Avalonia's internal traces (Layout/Visual/Property/...) to the VL ILogger.
            // TEMPORARY for the slider-handle-stuck investigation.
            global::Avalonia.Logging.Logger.Sink = new AvaloniaLogSink(LogEventLevel.Debug);

            Instance ??= AppBuilder
                .Configure<App>()
                .UseGammaSkia()
                .UseGammaSkiaDefaults()
                .SetupWithLifetime(new GammaSkiaWinFormsLifetime())
                .Instance;
        }

        sealed class App : Application
        {
            public override void Initialize()
            {
                base.Initialize();
            }

            public override void RegisterServices()
            {
                base.RegisterServices();
            }

            public override void OnFrameworkInitializationCompleted()
            {
                base.OnFrameworkInitializationCompleted();
            }
        }
    }
}
