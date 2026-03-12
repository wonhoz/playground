namespace Net.Trace.ViewModels;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new(p));
    protected bool Set<T>(ref T f, T v, [CallerMemberName] string? p = null)
    {
        if (EqualityComparer<T>.Default.Equals(f, v)) return false;
        f = v; Notify(p); return true;
    }
}

public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? _) => canExecute?.Invoke() ?? true;
    public void Execute(object? _) => execute();
    public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
