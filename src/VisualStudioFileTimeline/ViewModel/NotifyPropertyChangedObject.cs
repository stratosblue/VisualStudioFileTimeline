using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VisualStudioFileTimeline.ViewModel;

public abstract class NotifyPropertyChangedObject : INotifyPropertyChanged
{
    #region Public 事件

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion Public 事件

    #region Protected 方法

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (PropertyChanged is { } propertyChanged)
        {
            propertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    protected void SetProperty<T>(ref T value, T newValue, [CallerMemberName] string? propertyName = null)
    {
        if (PropertyChanged is { } propertyChanged
            && !EqualityComparer<T>.Default.Equals(value, newValue))
        {
            value = newValue;
            propertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion Protected 方法
}
