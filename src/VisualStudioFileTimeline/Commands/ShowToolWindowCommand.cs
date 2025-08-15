using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using VisualStudioFileTimeline.View;
using Task = System.Threading.Tasks.Task;

namespace VisualStudioFileTimeline.Commands;

/// <summary>
/// 展示工具窗口命令
/// </summary>
internal sealed class ShowToolWindowCommand
{
    #region Public 字段

    /// <summary>
    /// Command ID.
    /// </summary>
    public const int CommandId = 0x0100;

    /// <summary>
    /// Command menu group (command set GUID).
    /// </summary>
    public static readonly Guid CommandSet = new("0400c725-1c6b-4c3b-ae72-61b31cce8dae");

    #endregion Public 字段

    #region Public 属性

    /// <summary>
    /// Gets the instance of the command.
    /// </summary>
    public static ShowToolWindowCommand? Instance
    {
        get;
        private set;
    }

    /// <summary>
    /// VS Package that provides this command, not null.
    /// </summary>
    public VisualStudioFileTimelinePackage Package { get; }

    #endregion Public 属性

    #region Private 构造函数

    private ShowToolWindowCommand(VisualStudioFileTimelinePackage package, OleMenuCommandService commandService)
    {
        Package = package ?? throw new ArgumentNullException(nameof(package));
        commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

        var menuCommandID = new CommandID(CommandSet, CommandId);
        var menuItem = new MenuCommand(Execute, menuCommandID);
        commandService.AddCommand(menuItem);
    }

    #endregion Private 构造函数

    #region Public 方法

    /// <summary>
    /// Initializes the singleton instance of the command.
    /// </summary>
    /// <param name="package">Owner package, not null.</param>
    public static async Task InitializeAsync(VisualStudioFileTimelinePackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService
                             ?? throw new NotSupportedException($"Cannot get {nameof(OleMenuCommandService)}");
        Instance = new ShowToolWindowCommand(package, commandService);
    }

    #endregion Public 方法

    #region Private 方法

    /// <summary>
    /// Shows the tool window when the menu item is clicked.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event args.</param>
    private void Execute(object sender, EventArgs e)
    {
        _ = Package.JoinableTaskFactory.RunAsync(async delegate
        {
            var window = await Package.ShowToolWindowAsync(typeof(TimelineToolWindow), 0, true, Package.DisposalToken);
            if (null == window
                || null == window.Frame)
            {
                throw new NotSupportedException("Cannot create tool window");
            }
        });
    }

    #endregion Private 方法
}
