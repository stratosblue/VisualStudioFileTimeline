using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VisualStudioFileTimeline.VisualStudio;

internal static class VisualStudioShellUtilities
{
    #region Public 方法

    /// <summary>
    /// 检查文件 <paramref name="moniker"/> 是否为临时打开状态
    /// </summary>
    /// <param name="moniker"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<bool?> IsProvisionalOpenedAsync(Uri moniker, CancellationToken cancellationToken = default)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var fullPath = moniker.AbsolutePath;

        if (ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShellOpenDocument)) is IVsUIShellOpenDocument vsUIShellOpenDocument)
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
                && windowFrame?.GetProperty((int)__VSFPROPID5.VSFPROPID_IsProvisional, out var isProvisionalValue) == 0)
            {
                return isProvisionalValue is true;
            }
        }

        return null;
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
