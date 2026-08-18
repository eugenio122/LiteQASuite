using LiteQASuite.Core;
using LiteQASuite.Core.Modules;
using LiteQASuite.Core.Mvvm;
using System.Collections.Generic;
using System.Linq;

namespace LiteQASuite.Shell.ViewModels;

/// <summary>
/// ViewModel da janela principal (a casca). Recebe a lista de módulos já
/// instanciados pelo composition root — só como <see cref="IModule"/>, sem
/// conhecer nenhuma implementação concreta — e controla qual está selecionado.
/// A <see cref="IModule.View"/> do módulo selecionado é hospedada na área de
/// conteúdo da ShellWindow.
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    /// <summary>Módulos disponíveis, na ordem em que o composition root os entregou.</summary>
    public IReadOnlyList<IModule> Modules { get; }

    private IModule? _selectedModule;

    /// <summary>Módulo atualmente exibido; a navegação lateral controla isto.</summary>
    public IModule? SelectedModule
    {
        get => _selectedModule;
        set => SetProperty(ref _selectedModule, value);
    }

    public ShellViewModel(IReadOnlyList<IModule> modules)
    {
        Modules = modules;
        SelectedModule = modules.FirstOrDefault();
    }
}