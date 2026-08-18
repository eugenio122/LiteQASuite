using System;
using System.Windows.Input;

namespace LiteQASuite.Core.Mvvm;

/// <summary>
/// <see cref="ICommand"/> simples para bindings de comando em WPF (botões, menus).
/// Recebe a ação a executar e, opcionalmente, a condição de habilitação. Vive no
/// Core para todos os ViewModels usarem.
///
/// <see cref="CanExecuteChanged"/> encadeia no <see cref="CommandManager.RequerySuggested"/>,
/// então o WPF reavalia a habilitação sozinho nas interações — na maioria dos
/// casos não é preciso dispará-lo na mão.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>Atalho para comandos sem parâmetro.</summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);
}