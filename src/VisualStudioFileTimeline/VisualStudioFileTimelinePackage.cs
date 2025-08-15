using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VisualStudioFileTimeline.Commands;
using VisualStudioFileTimeline.Internal;
using VisualStudioFileTimeline.Providers.Default;
using VisualStudioFileTimeline.Providers.VsCode;
using VisualStudioFileTimeline.View;
using VisualStudioFileTimeline.ViewModel;
using VisualStudioFileTimeline.VisualStudio;
using Task = System.Threading.Tasks.Task;

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
    #region Public 属性

    public IServiceProvider? GlobalProvider { get; private set; }

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
        if (GlobalProvider?.GetService(serviceType) is { } service)
        {
            return service;
        }
        return base.GetService(serviceType);
    }

    /// <inheritdoc/>
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        //Debugger.Launch();

        await base.InitializeAsync(cancellationToken, progress);

        var services = new ServiceCollection();

        InitializeServices(services);

        GlobalProvider = services.BuildServiceProvider();

        //获取以保证初始化
        GlobalProvider.GetRequiredService<RunningDocTableEventsListener>();

        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        await ShowToolWindowCommand.InitializeAsync(this);
    }

    #endregion Package Members

    #region Private 方法

    private void InitializeServices(IServiceCollection serviceCollection)
    {
        AddFileTimelineProvider<LocalHistoryFileTimelineProvider>(serviceCollection);
        AddFileTimelineProvider<VsCodeFileTimelineProvider>(serviceCollection);

        serviceCollection.TryAddSingleton<FileTimelineViewModel>();
        serviceCollection.TryAddSingleton<TimelineToolWindowViewModel>();

        serviceCollection.TryAddSingleton<VisualStudioFileTimelinePackage>(this);

        serviceCollection.TryAddSingleton<FileTimelineManager>();
        serviceCollection.TryAddSingleton<TimelineToolWindowAccessor>();
        serviceCollection.TryAddSingleton<RunningDocTableEventsListener>();

        static void AddFileTimelineProvider<T>(IServiceCollection serviceCollection)
            where T : class, IFileTimelineProvider, IFileTimelineStore
        {
            serviceCollection.TryAddSingleton<T>();
            serviceCollection.AddSingleton<IFileTimelineProvider>(serviceProvider => serviceProvider.GetRequiredService<T>());
            serviceCollection.AddSingleton<IFileTimelineStore>(serviceProvider => serviceProvider.GetRequiredService<T>());
        }
    }

    #endregion Private 方法
}
