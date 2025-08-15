using System.Collections.ObjectModel;

namespace VisualStudioFileTimeline.ViewModel;

public class ObservableList<T> : ObservableCollection<T>
{
    #region Public 构造函数

    public ObservableList()
    {
    }

    public ObservableList(List<T> list) : base(list)
    {
    }

    public ObservableList(IEnumerable<T> collection) : base(collection)
    {
    }

    #endregion Public 构造函数
}
