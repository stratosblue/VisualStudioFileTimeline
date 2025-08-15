using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using VisualStudioFileTimeline.VisualStudio;

namespace VisualStudioFileTimeline.ViewModel;

public class TimelineToolWindowViewModel : NotifyPropertyChangedObject
{
    #region Private 字段

    private string? _fileName;

    private FileTimeline? _fileTimeline;

    private ObservableList<TimelineItemViewModel> _items = [];

    #endregion Private 字段

    #region Public 属性

    public string? FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    public ObservableList<TimelineItemViewModel> Items
    {
        get => _items;
        set => SetProperty(ref _items, value);
    }

    public VisualStudioFileTimelinePackage Package { get; }

    #region Commands

    public ICommand DeleteCommand { get; }

    public ICommand OpenWithExplorerCommand { get; }

    public ICommand RestoreContentCommand { get; }

    public ICommand ViewContentCommand { get; }

    #endregion Commands

    #endregion Public 属性

    #region Public 构造函数

    public TimelineToolWindowViewModel(VisualStudioFileTimelinePackage package)
    {
        Package = package ?? throw new ArgumentNullException(nameof(package));

        DeleteCommand = new DelegateCommand<TimelineItemViewModel>((viewModel) =>
        {
            package.JoinableTaskFactory.RunAsync(async () =>
            {
                if (await viewModel.Timeline.DeleteItemAsync(viewModel.RawItem, default))
                {
                    Items.Remove(viewModel);
                }
            }).Task.Forget();
        }, static _ => true, package.JoinableTaskFactory);

        OpenWithExplorerCommand = new DelegateCommand((parameter) =>
        {
            if (parameter is TimelineItemViewModel viewModel)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{viewModel.FilePath}\""
                });
            }
        }, static _ => true, package.JoinableTaskFactory);

        RestoreContentCommand = new DelegateCommand((parameter) =>
        {
            if (parameter is TimelineItemViewModel viewModel
                && File.Exists(viewModel.FilePath))
            {
                var textReadTask = Task.Run(async () =>
                {
                    using var fs = File.OpenRead(viewModel.FilePath);
                    using var reader = new StreamReader(fs);
                    return await reader.ReadToEndAsync();
                });

                package.JoinableTaskFactory.RunAsync(async () =>
                {
                    if (await VisualStudioShellUtilities.GetTextBufferAsync(package, viewModel.Timeline.Resource, default) is { } textBuffer)
                    {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                        var text = await textReadTask;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks

                        textBuffer.Replace(new(0, textBuffer.CurrentSnapshot.Length), text);
                    }
                }).Task.Forget();
            }
        }, static _ => true, package.JoinableTaskFactory);

        ViewContentCommand = new DelegateCommand((parameter) =>
        {
            if (parameter is TimelineItemViewModel viewModel)
            {
                package.JoinableTaskFactory.RunAsync(() =>
                {
                    return VisualStudioShellUtilities.OpenProvisionalFileViewAsync(viewModel.FilePath, default);
                }).Task.Forget();
            }
        }, static _ => true, package.JoinableTaskFactory);
    }

    #endregion Public 构造函数

    #region Public 方法

    public void SelectedItem(TimelineItemViewModel selected)
    {
        if (_fileTimeline is not { } fileTimeline
            || fileTimeline != selected.Timeline)
        {
            return;
        }

        _ = VisualStudioShellUtilities.ShowFileTimelineComparisonWindowAsync(fileTimeline: fileTimeline,
                                                                             comparisonFilePath: selected.FilePath,
                                                                             comparisonFileDescription: $"{selected.Title} at {selected.Time.ToLongDateString()} {selected.Time.ToLongTimeString()}");
    }

    public void SetCurrentFileTimeline(FileTimeline fileTimeline)
    {
        _fileTimeline = fileTimeline;
        Items = new(fileTimeline.TimelineItems.Select(m => new TimelineItemViewModel(fileTimeline, m, this)));
        FileName = fileTimeline.FileName;
    }

    public void UnselectedItem(TimelineItemViewModel unselected)
    {
    }

    public void UpdateCurrentFileTimelineItems(IFileTimelineItem fileTimelineItem)
    {
        if (_fileTimeline is { } fileTimeline
            && Items is { } items)
        {
            var insertIndex = fileTimeline.AddOrUpdateItem(fileTimelineItem, out var removedIndex);

            Package.JoinableTaskFactory.RunAsync(async () =>
            {
                await Package.JoinableTaskFactory.SwitchToMainThreadAsync(default);

                if (removedIndex >= 0)
                {
                    items.RemoveAt(removedIndex);
                }
                items.Insert(insertIndex, new(fileTimeline, fileTimelineItem, this));

                return Task.CompletedTask;
            }).Task.Forget();
        }
    }

    #endregion Public 方法
}
