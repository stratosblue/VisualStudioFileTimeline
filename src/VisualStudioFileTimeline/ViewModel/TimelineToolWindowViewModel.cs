using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using VisualStudioFileTimeline.VisualStudio;

namespace VisualStudioFileTimeline.ViewModel;

public class TimelineToolWindowViewModel : NotifyPropertyChangedObject
{
    #region Private 字段

    private readonly ILogger _logger;

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

    public bool IsVisible { get; set; }

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

    public TimelineToolWindowViewModel(VisualStudioFileTimelinePackage package, ILogger<TimelineToolWindowViewModel> logger)
    {
        Package = package ?? throw new ArgumentNullException(nameof(package));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        DeleteCommand = new DelegateCommand((parameter) =>
        {
            _logger.LogInformation("DeleteCommand calling. {Parameter}", parameter);

            if (parameter is TimelineItemViewModel viewModel)
            {
                package.JoinableTaskFactory.RunAsync(async () =>
                {
                    _logger.LogInformation("DeleteCommand Running. {Item}", viewModel);
                    try
                    {
                        if (await viewModel.Timeline.DeleteItemAsync(viewModel.RawItem, default))
                        {
                            Items.Remove(viewModel);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "DeleteCommand Failed. {Item}", viewModel);
                    }
                }).Task.Forget();
            }
        }, static _ => true, package.JoinableTaskFactory);

        OpenWithExplorerCommand = new DelegateCommand((parameter) =>
        {
            _logger.LogInformation("OpenWithExplorerCommand calling. {Parameter}", parameter);

            if (parameter is TimelineItemViewModel viewModel)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{viewModel.FilePath}\""
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OpenWithExplorerCommand Failed. {Item}", viewModel);
                }
            }
        }, static _ => true, package.JoinableTaskFactory);

        RestoreContentCommand = new DelegateCommand((parameter) =>
        {
            _logger.LogInformation("RestoreContentCommand calling. {Parameter}", parameter);

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
                    _logger.LogInformation("RestoreContentCommand Running. {Item}", viewModel);

                    try
                    {
                        if (await VisualStudioShellUtilities.GetTextBufferAsync(package, viewModel.Timeline.Resource, default) is { } textBuffer)
                        {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                            var text = await textReadTask;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks

                            textBuffer.Replace(new(0, textBuffer.CurrentSnapshot.Length), text);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "RestoreContentCommand Failed. {Item}", viewModel);
                        throw;
                    }
                }).Task.Forget();
            }
        }, static _ => true, package.JoinableTaskFactory);

        ViewContentCommand = new DelegateCommand((parameter) =>
        {
            _logger.LogInformation("ViewContentCommand calling. {Parameter}", parameter);

            if (parameter is TimelineItemViewModel viewModel)
            {
                package.JoinableTaskFactory.RunAsync(async () =>
                {
                    _logger.LogInformation("ViewContentCommand Running. {Item}", viewModel);
                    try
                    {
                        await VisualStudioShellUtilities.OpenProvisionalFileViewAsync(viewModel.FilePath, default);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "ViewContentCommand Failed. {Item}", viewModel);
                    }
                }).Task.Forget();
            }
        }, static _ => true, package.JoinableTaskFactory);
    }

    #endregion Public 构造函数

    #region Public 方法

    public void SelectedItem(TimelineItemViewModel selected)
    {
        _logger.LogInformation("Item {Item} selected.", selected);

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
        _logger.LogInformation("Item {Item} unselected.", unselected);
    }

    public void UpdateCurrentFileTimelineItems(IFileTimelineItem fileTimelineItem, IEnumerable<string>? dropedItemIdentifiers)
    {
        _logger.LogInformation("Update current file timeline items with {Item}.", fileTimelineItem);

        if (_fileTimeline is { } fileTimeline
            && Items is { } items)
        {
            var insertIndex = fileTimeline.AddOrUpdateItem(fileTimelineItem, out var removedIndex);

            _logger.LogInformation("Update current file timeline items with {Item} at {InsertIndex} - {RemovedIndex}.", fileTimelineItem, insertIndex, removedIndex);

            Package.JoinableTaskFactory.RunAsync(async () =>
            {
                _logger.LogDebug("Update current file timeline items with {Item} switch to main thread.", fileTimelineItem);
                try
                {
                    await Package.JoinableTaskFactory.SwitchToMainThreadAsync(default);

                    if (removedIndex >= 0)
                    {
                        items.RemoveAt(removedIndex);
                    }
                    items.Insert(insertIndex, new(fileTimeline, fileTimelineItem, this));

                    if (dropedItemIdentifiers is not null)
                    {
                        foreach (var identifier in dropedItemIdentifiers)
                        {
                            if (fileTimeline.Remove(identifier, out removedIndex)
                                && removedIndex >= 0)
                            {
                                items.RemoveAt(removedIndex);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Update current file timeline items with {Item} failed.", fileTimelineItem);
                }
                return Task.CompletedTask;
            }).Task.Forget();
        }
    }

    #endregion Public 方法
}
