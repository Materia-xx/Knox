using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Knox.App.Logic.Mvvm;

/// <summary>
/// An async <see cref="ICommand"/> that runs a <see cref="Task"/>-returning
/// delegate, disabling itself while the operation is in flight so the UI can
/// reflect a busy state and avoid re-entrancy.
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning != value)
            {
                _isRunning = value;
                RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter) => await ExecuteAsync();

    /// <summary>Awaitable variant, useful for tests and chaining.</summary>
    public async Task ExecuteAsync()
    {
        if (!CanExecute(null))
        {
            return;
        }

        try
        {
            IsRunning = true;
            // NB: do NOT use ConfigureAwait(false) here. This runs UI command
            // handlers (navigation, alerts) whose continuations must resume on the
            // UI thread; otherwise Android throws "Animators may only be run on
            // Looper threads" when navigation animations run off-thread.
            await _execute();
        }
        finally
        {
            IsRunning = false;
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Generic async <see cref="ICommand"/> that receives a typed parameter. Disables
/// itself while the operation runs (re-entrancy guard / busy state).
/// </summary>
public sealed class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke(Cast(parameter)) ?? true);

    public async void Execute(object? parameter) => await ExecuteAsync(Cast(parameter));

    public async Task ExecuteAsync(T? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isRunning = true;
            RaiseCanExecuteChanged();
            // NB: do NOT use ConfigureAwait(false) here (see AsyncRelayCommand).
            await _execute(parameter);
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T? Cast(object? parameter) => parameter is T t ? t : default;
}
