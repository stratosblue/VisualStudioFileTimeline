using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.UI;
using VisualStudioFileTimeline.VisualStudio;

namespace VisualStudioFileTimeline.ViewModel;

[DataContract]
public class TimelineToolWindowViewModel : NotifyPropertyChangedObject
{
    #region Private 字段

    private readonly VisualStudioExtensibility _extensibility;

    private FileTimeline? _fileTimeline;

    private ObservableList<TimelineItemViewModel> _items = [];

    #endregion Private 字段

    #region Public 属性

    [DataMember]
    public ObservableList<TimelineItemViewModel> Items
    {
        get => _items;
        set => SetProperty(ref _items, value);
    }

    #region Commands

    [DataMember]
    public AsyncCommand DeleteCommand { get; }

    [DataMember]
    public AsyncCommand OpenWithExplorerCommand { get; }

    [DataMember]
    public AsyncCommand RestoreContentCommand { get; }

    [DataMember]
    public AsyncCommand ViewContentCommand { get; }

    #endregion Commands

    #endregion Public 属性

    #region Public 构造函数

    public TimelineToolWindowViewModel(VisualStudioExtensibility extensibility)
    {
        DeleteCommand = new AsyncCommand((parameter, clientContext, token) =>
        {
            if (parameter is TimelineItemViewModel viewModel)
            {
                if (viewModel.Timeline.DeleteItem(viewModel.RawItem))
                {
                    Items.Remove(viewModel);
                }
            }
            return Task.CompletedTask;
        });

        OpenWithExplorerCommand = new AsyncCommand((parameter, clientContext, token) =>
        {
            if (parameter is TimelineItemViewModel viewModel)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{viewModel.FilePath}\""
                });
            }
            return Task.CompletedTask;
        });

        RestoreContentCommand = new AsyncCommand(async (parameter, clientContext, token) =>
        {
            if (parameter is TimelineItemViewModel viewModel
                && File.Exists(viewModel.FilePath)
                && await extensibility.Documents().GetOpenDocumentAsync(viewModel.Timeline.Resource, token) is { } document
                && await document.AsTextDocumentAsync(extensibility, token) is { } textDocument)
            {
                using var fs = File.OpenRead(viewModel.FilePath);
                using var reader = new StreamReader(fs);
                var text = await reader.ReadToEndAsync();
                await extensibility.Editor()
                                   .EditAsync(editorSource: batch =>
                                   {
                                       textDocument.AsEditable(batch)
                                                   .Replace(textDocument.Text, text);
                                   }, cancellationToken: token);
            }
        });

        ViewContentCommand = new AsyncCommand((parameter, clientContext, token) =>
        {
            if (parameter is TimelineItemViewModel viewModel)
            {
                return VisualStudioShellUtilities.OpenProvisionalFileViewAsync(viewModel.FilePath, token);
            }
            return Task.CompletedTask;
        });
        _extensibility = extensibility;
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
            if (removedIndex >= 0)
            {
                items.RemoveAt(removedIndex);
            }
            items.Insert(insertIndex, new(fileTimeline, fileTimelineItem, this));
        }
    }

    #endregion Public 方法
}
