using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VisualStudioFileTimeline.ViewModel;

public abstract class NotifyPropertyChangedObject : INotifyPropertyChanged
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void SetProperty<T>(ref T value, T newValue, [CallerMemberName] string? propertyName = null)
    {
        if (PropertyChanged is { } propertyChanged
            && !EqualityComparer<T>.Default.Equals(value, newValue))
        {
            value = newValue;
            propertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
