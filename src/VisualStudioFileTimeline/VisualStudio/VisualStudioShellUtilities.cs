using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace VisualStudioFileTimeline.VisualStudio;

internal static class VisualStudioShellUtilities
{
    #region Public 方法

    /// <summary>
    /// 使用<paramref name="package"/>获取MEF组件<typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="package"></param>
    /// <returns></returns>
    public static T? GetComponentService<T>(this Package package) where T : class
    {
        var componentModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<SComponentModel>(package) as IComponentModel;
        return componentModel?.GetService<T>();
    }

    /// <summary>
    /// 获取 <paramref name="moniker"/> 正在编辑的 <see cref="ITextBuffer"/>
    /// </summary>
    /// <param name="moniker"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<ITextBuffer?> GetTextBufferAsync(Package package, Uri moniker, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var fullPath = moniker.AbsolutePath;

        if (TryGetVsUIShellOpenDocument(out var vsUIShellOpenDocument))
        {
            var logicalView = Guid.Empty;
            var hr = vsUIShellOpenDocument.IsDocumentOpen(pHierCaller: default,
                                                          itemidCaller: default,
                                                          pszMkDocument: fullPath,
                                                          rguidLogicalView: ref logicalView,
                                                          grfIDO: default,
                                                          ppHierOpen: out var ppHierOpen,
                                                          pitemidOpen: default,
                                                          ppWindowFrame: out var windowFrame,
                                                          pfOpen: out var pfOpen);
            if (hr == 0
                && pfOpen == 1
                && windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out var docView) == 0)
            {
                var vsTextView = docView as IVsTextView;
                if (vsTextView == null
                    && docView is IVsCodeWindow vsCodeWindow)
                {
                    vsCodeWindow.GetPrimaryView(out vsTextView);
                }

                if (vsTextView?.GetBuffer(out var vsTextBuffer) == 0
                    && vsTextBuffer is not null)
                {
                    var adapterService = GetComponentService<IVsEditorAdaptersFactoryService>(package);
                    return adapterService?.GetDataBuffer(vsTextBuffer);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 检查文件 <paramref name="moniker"/> 是否为未保存编辑状态
    /// </summary>
    /// <param name="moniker"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<bool> IsDocumentDirtyAsync(Uri moniker, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var fullPath = moniker.AbsolutePath;

        if (TryGetVsUIShellOpenDocument(out var vsUIShellOpenDocument))
        {
            var logicalView = Guid.Empty;
            var hr = vsUIShellOpenDocument.IsDocumentOpen(pHierCaller: default,
                                                          itemidCaller: default,
                                                          pszMkDocument: fullPath,
                                                          rguidLogicalView: ref logicalView,
                                                          grfIDO: default,
                                                          ppHierOpen: out var ppHierOpen,
                                                          pitemidOpen: default,
                                                          ppWindowFrame: out var windowFrame,
                                                          pfOpen: out var pfOpen);
            if (hr == 0
                && pfOpen == 1
                && windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out var docData) == 0
                && docData is IVsPersistDocData vsPersistDocData
                && vsPersistDocData.IsDocDataDirty(out var isDirty) == 0)
            {
                return isDirty != 0;
            }
        }
        return false;
    }

    /// <summary>
    /// 检查文件 <paramref name="moniker"/> 是否为临时打开状态
    /// </summary>
    /// <param name="moniker"></param>
    /// <returns></returns>
    public static bool? IsProvisionalOpened(string moniker)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (TryGetVsUIShellOpenDocument(out var vsUIShellOpenDocument))
        {
            var logicalView = Guid.Empty;
            var hr = vsUIShellOpenDocument.IsDocumentOpen(pHierCaller: default,
                                                          itemidCaller: default,
                                                          pszMkDocument: moniker,
                                                          rguidLogicalView: ref logicalView,
                                                          grfIDO: default,
                                                          ppHierOpen: out var ppHierOpen,
                                                          pitemidOpen: default,
                                                          ppWindowFrame: out var windowFrame,
                                                          pfOpen: out var pfOpen);
            if (hr == 0
                && pfOpen == 1
                && windowFrame is not null)
            {
                return IsProvisionalOpened(windowFrame);
            }
        }

        return null;
    }

    public static bool IsProvisionalOpened(IVsWindowFrame windowFrame)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return windowFrame.GetProperty((int)__VSFPROPID5.VSFPROPID_IsProvisional, out var isProvisionalValue) == 0
               && isProvisionalValue is true;
    }

    /// <summary>
    /// 检查文件 <paramref name="moniker"/> 是否为临时打开状态
    /// </summary>
    /// <param name="moniker"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<bool?> IsProvisionalOpenedAsync(Uri moniker, CancellationToken cancellationToken = default)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        return IsProvisionalOpened(moniker.AbsolutePath);
    }

    public static async Task<IVsWindowFrame?> OpenProvisionalFileViewAsync(string filePath,
                                                                           CancellationToken cancellationToken = default)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (TryCreateDocument(provider: ServiceProvider.GlobalProvider,
                              fullPath: filePath,
                              logicalView: Guid.Empty,
                              hierarchy: out _,
                              itemID: out _,
                              windowFrame: out var vsWindowFrame) == 0
             && vsWindowFrame is not null)
        {
            vsWindowFrame.SetProperty((int)__VSFPROPID5.VSFPROPID_IsProvisional, true);
            vsWindowFrame.Show();
        }

        return vsWindowFrame;
    }

    public static async Task<IVsWindowFrame> ShowFileTimelineComparisonWindowAsync(FileTimeline fileTimeline,
                                                                                   string comparisonFilePath,
                                                                                   string comparisonFileDescription,
                                                                                   CancellationToken cancellationToken = default)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var diffOptions = (uint)__VSDIFFSERVICEOPTIONS.VSDIFFOPT_LeftFileIsTemporary;

        var differenceService = (IVsDifferenceService)Package.GetGlobalService(typeof(SVsDifferenceService));

        var vsWindowFrame = differenceService.OpenComparisonWindow2(leftFileMoniker: comparisonFilePath,
                                                                    rightFileMoniker: fileTimeline.Resource.AbsolutePath,
                                                                    caption: string.Format(Resources.Caption_ComparisonTitleFormat, fileTimeline.FileName),
                                                                    Tooltip: null,
                                                                    leftLabel: comparisonFileDescription,
                                                                    rightLabel: fileTimeline.FileName,
                                                                    inlineLabel: null,
                                                                    roles: null,
                                                                    grfDiffOptions: diffOptions);

        vsWindowFrame.SetProperty((int)__VSFPROPID5.VSFPROPID_IsProvisional, true);

        return vsWindowFrame;
    }

    #endregion Public 方法

    #region Private 方法

    private static bool TryGetVsUIShellOpenDocument(out IVsUIShellOpenDocument vsUIShellOpenDocument)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        vsUIShellOpenDocument = (ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument)!; //472没有NotNullWhen
        return vsUIShellOpenDocument is not null;
    }

    #endregion Private 方法

    #region Util from VsShellUtilities

    private static int TryCreateDocument(IServiceProvider provider, string fullPath, Guid logicalView, out IVsUIHierarchy? hierarchy, out uint itemID, out IVsWindowFrame? windowFrame)
    {
        if (string.IsNullOrEmpty(fullPath))
        {
            throw new ArgumentException(nameof(fullPath));
        }

        ThreadHelper.ThrowIfNotOnUIThread();

        int result = 0;

        if (!VsShellUtilities.IsDocumentOpen(provider: provider ?? throw new ArgumentNullException(nameof(provider)),
                                             fullPath: fullPath,
                                             logicalView: logicalView,
                                             hierarchy: out hierarchy,
                                             itemID: out itemID,
                                             windowFrame: out windowFrame))
        {
            if (provider.GetService(typeof(SVsUIShellOpenDocument)) is IVsUIShellOpenDocument vsUIShellOpenDocument)
            {
                result = vsUIShellOpenDocument.OpenDocumentViaProject(pszMkDocument: fullPath,
                                                                      rguidLogicalView: ref logicalView,
                                                                      ppSP: out var _,
                                                                      ppHier: out hierarchy,
                                                                      pitemid: out var _,
                                                                      ppWindowFrame: out windowFrame);
            }
        }
        else if (windowFrame != null
                 && provider.GetService(typeof(SVsUIShellOpenDocument)) is IVsUIShellOpenDocument3 vsUIShellOpenDocument2
                 && (vsUIShellOpenDocument2.NewDocumentState & 2u) != 0)
        {
            windowFrame.SetProperty(-5020, false);
        }

        return result;
    }

    #endregion Util from VsShellUtilities
}
