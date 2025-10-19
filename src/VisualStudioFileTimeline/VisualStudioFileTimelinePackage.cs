using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Serilog;
using Serilog.Events;
using VisualStudioFileTimeline.Commands;
using VisualStudioFileTimeline.Internal.Serilog;
using VisualStudioFileTimeline.Providers.Default;
using VisualStudioFileTimeline.Providers.Git;
using VisualStudioFileTimeline.Providers.VsCode;
using VisualStudioFileTimeline.View;
using VisualStudioFileTimeline.ViewModel;
using VisualStudioFileTimeline.VisualStudio;
using Task = System.Threading.Tasks.Task;

[assembly: ProvideBindingRedirection(AssemblyName = "Serilog", NewVersion = "4.3.0.0", OldVersionLowerBound = "4.0.0.0", OldVersionUpperBound = "4.2.0.0")]

namespace VisualStudioFileTimeline;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(PackageGuidString)]
[ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(UIContextGuids80.SolutionHasMultipleProjects, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(UIContextGuids80.SolutionHasSingleProject, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideToolWindow(typeof(TimelineToolWindow))]
public sealed class VisualStudioFileTimelinePackage : AsyncPackage
{
    #region Private 字段

    private Serilog.ILogger _innerLogger = Serilog.Core.Logger.None;

    #endregion Private 字段

    #region Public 属性

    public IServiceProvider? GlobalProvider { get; private set; }

    public Microsoft.Extensions.Logging.ILogger Logger { get; private set; } = NullLogger.Instance;

    #endregion Public 属性

    #region Public 字段

    /// <summary>
    /// VisualStudioFileTimelinePackage GUID string.
    /// </summary>
    public const string PackageGuidString = "ed87c08f-8e32-4b28-a29b-33e218354bdb";

    public const string PackageName = "VisualStudioFileTimeline";

    #endregion Public 字段

    #region Package Members

    /// <inheritdoc/>
    protected override object GetService(Type serviceType)
    {
        try
        {
            if (GlobalProvider?.GetService(serviceType) is { } service)
            {
                Logger.LogDebug("Get service {ServiceType} from GlobalProvider success. {Service}", serviceType, service);
                return service;
            }

            service = base.GetService(serviceType);
            Logger.LogDebug("Get service {ServiceType} from package base. {Service}", serviceType, service);
            return service;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Get {ServiceType} service from package failed", serviceType);
            throw;
        }
    }

    /// <inheritdoc/>
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await base.InitializeAsync(cancellationToken, progress);

        var options = VisualStudioFileTimelineOptions.LoadFromDefaultConfigurationFile(out var optionsLoadMessage);

        options.EnsureWorkingDirectory();

        if (Interlocked.Exchange(ref _innerLogger, CreateSerilogLogger(options)) is IDisposable disposableLogger)
        {
            disposableLogger.Dispose();
        }

        var services = new ServiceCollection();

        InitializeServices(services, options);

        GlobalProvider = services.BuildServiceProvider();

        Logger = GlobalProvider.GetRequiredService<ILoggerFactory>().CreateLogger<VisualStudioFileTimelinePackage>();

        if (!string.IsNullOrWhiteSpace(optionsLoadMessage))
        {
            Logger.LogError(optionsLoadMessage);
        }

        //HACK 好像第一次加载很慢？
        Logger.LogDebug("Package services build completed.");

        //获取以保证初始化
        GlobalProvider.GetRequiredService<RunningDocTableEventsListener>();

        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        await ShowToolWindowCommand.InitializeAsync(this);
    }

    #endregion Package Members

    #region Protected 方法

    protected override void Dispose(bool disposing)
    {
        Logger.LogDebug("Package disposing.");

        base.Dispose(disposing);

        if (GlobalProvider?.GetService<VisualStudioFileTimelineOptions>() is { } options)
        {
            VisualStudioFileTimelineOptions.SaveToDefaultConfigurationFile(options, out var optionsSaveMessage);

            if (!string.IsNullOrWhiteSpace(optionsSaveMessage))
            {
                Logger.LogError(optionsSaveMessage);
            }
        }

        (GlobalProvider as IDisposable)?.Dispose();

        Logger.LogDebug("Package disposed.");
        (_innerLogger as IDisposable)?.Dispose();
    }

    #endregion Protected 方法

    #region Private 方法

    private static Serilog.Core.Logger CreateSerilogLogger(VisualStudioFileTimelineOptions options)
    {
        var logFilePath = Path.Combine(options.EnsureWorkingDirectory("Logs"), "logs.log");

        var optionsLogLevel = options.LogLevel;

#if DEBUG
        optionsLogLevel = LogLevel.Trace;
#endif

        var level = optionsLogLevel switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            _ => LogEventLevel.Warning
        };

        return new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.With<ThreadInfoEnricher>()
            .WriteTo.Async(c =>
            {
                c.File(path: logFilePath,
                       restrictedToMinimumLevel: level,
                       outputTemplate: "[{ProcessId}] {Timestamp:yyyy-MM-dd HH:mm:ss.fff} {ThreadInfo} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                       fileSizeLimitBytes: 1024 * 1024 * 100,
                       rollOnFileSizeLimit: true,
                       retainedFileCountLimit: 5,
                       encoding: Encoding.UTF8,
                       retainedFileTimeLimit: TimeSpan.FromDays(30));
            }, 50)
            .MinimumLevel.Is(level)
            .CreateLogger();
    }

    private void InitializeServices(IServiceCollection services, VisualStudioFileTimelineOptions options)
    {
        AddFileTimelineProvider<LocalHistoryFileTimelineProvider>(services);
#if DEBUG
        AddFileTimelineProvider<GitFileTimelineProvider>(services);
#endif
        AddFileTimelineProvider<VsCodeFileTimelineProvider>(services);

        services.AddLogging(builder =>
        {
#if DEBUG
            builder.SetMinimumLevel(LogLevel.Trace);
#else
            builder.SetMinimumLevel(options.LogLevel);
#endif

            builder.AddSerilog(_innerLogger, dispose: false);
        });

        services.TryAddSingleton<Package>(this);
        services.TryAddSingleton<AsyncPackage>(this);
        services.TryAddSingleton<VisualStudioFileTimelinePackage>(this);

        services.TryAddSingleton<VisualStudioFileTimelineOptions>(options);

        services.TryAddSingleton<FileTimelineViewModel>();
        services.TryAddSingleton<TimelineToolWindowViewModel>();

        services.TryAddSingleton<FileTimelineManager>();
        services.TryAddSingleton<RunningDocTableEventsListener>();

        static void AddFileTimelineProvider<T>(IServiceCollection services)
            where T : class, IFileTimelineProvider
        {
            services.TryAddSingleton<T>();
            services.AddSingleton<IFileTimelineProvider>(serviceProvider => serviceProvider.GetRequiredService<T>());

            if (typeof(IFileTimelineStore).IsAssignableFrom(typeof(T)))
            {
                services.AddSingleton<IFileTimelineStore>(serviceProvider => (IFileTimelineStore)serviceProvider.GetRequiredService<T>());
            }
        }
    }

    #endregion Private 方法
}
