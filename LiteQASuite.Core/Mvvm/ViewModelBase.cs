using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LiteQASuite.Core.Mvvm;

/// <summary>
/// Base de todos os ViewModels do LiteQASuite (Shell e módulos). Implementa
/// <see cref="INotifyPropertyChanged"/> e oferece <see cref="SetProperty{T}"/>
/// para cortar o boilerplate de notificação. Vive no Core porque toda tela em
/// XAML, de qualquer módulo, herda daqui.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Dispara PropertyChanged para a propriedade informada (ou a chamadora).</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Atribui o campo e notifica apenas se o valor mudou. Retorna <c>true</c> se
    /// houve mudança — útil para encadear efeitos colaterais só quando necessário.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}