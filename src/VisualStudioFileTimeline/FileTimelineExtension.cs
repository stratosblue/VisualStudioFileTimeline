using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.Extensibility;
using VisualStudioFileTimeline.Internal;
using VisualStudioFileTimeline.Providers.Default;
using VisualStudioFileTimeline.Providers.VsCode;
using VisualStudioFileTimeline.ViewModel;
using VisualStudioFileTimeline.VisualStudio;

namespace VisualStudioFileTimeline;

[VisualStudioContribution]
public class FileTimelineExtension : Extension
{
    #region Public 属性

    /// <inheritdoc/>
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        LoadedWhen = ActivationConstraint.Or(ActivationConstraint.SolutionState(SolutionState.NoSolution),
                                             ActivationConstraint.SolutionState(SolutionState.Exists),
                                             ActivationConstraint.SolutionState(SolutionState.FullyLoaded),
                                             ActivationConstraint.SolutionState(SolutionState.Empty),
                                             ActivationConstraint.SolutionState(SolutionState.SingleProject),
                                             ActivationConstraint.SolutionState(SolutionState.MultipleProject),
                                             ActivationConstraint.SolutionState(SolutionState.Building)),
        RequiresInProcessHosting = true,
    };

    #endregion Public 属性

    #region Protected 方法

    /// <inheritdoc />
    protected override void InitializeServices(IServiceCollection serviceCollection)
    {
        base.InitializeServices(serviceCollection);

        AddFileTimelineProvider<LocalHistoryFileTimelineProvider>(serviceCollection);
        AddFileTimelineProvider<VsCodeFileTimelineProvider>(serviceCollection);

        serviceCollection.TryAddSingleton<FileTimelineViewModel>();
        serviceCollection.TryAddSingleton<FileTimelineManager>();
        serviceCollection.TryAddSingleton<DocumentEventsListener>();
        serviceCollection.TryAddSingleton<TimelineToolWindowAccessor>();

        static void AddFileTimelineProvider<T>(IServiceCollection serviceCollection)
            where T : class, IFileTimelineProvider, IFileTimelineStore
        {
            serviceCollection.TryAddSingleton<T>();
            serviceCollection.AddSingleton<IFileTimelineProvider>(serviceProvider => serviceProvider.GetRequiredService<T>());
            serviceCollection.AddSingleton<IFileTimelineStore>(serviceProvider => serviceProvider.GetRequiredService<T>());
        }
    }

    #endregion Protected 方法
}
