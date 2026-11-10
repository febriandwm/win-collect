using System.Windows.Input;

namespace WinCollect.ViewModels;

/// <summary>
/// General-purpose ICommand implementation supporting both sync and async actions.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Func<Task>? _asyncExecute;
    private readonly Action? _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _asyncExecute = execute;
        _canExecute   = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
        => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            if (_asyncExecute != null)
                await _asyncExecute();
            else
                _execute?.Invoke();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
