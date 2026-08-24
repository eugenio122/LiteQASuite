using LiteQASuite.Core.Localization;
using LiteQASuite.Core.Modules;
using LiteQASuite.Core.Mvvm;
using LiteQASuite.Core.Workspace;
using System.Collections.Generic;
using System.Linq;

namespace LiteQASuite.Shell.ViewModels;

/// <summary>
/// ViewModel da janela principal (a casca). Monta a navegação unificada: a tela
/// de configurações no topo (como o "Geral" antigo) seguida dos módulos, todos
/// como <see cref="NavItem"/>. A área de conteúdo hospeda a <see cref="NavItem.View"/>
/// do item selecionado.
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    /// <summary>Entradas da navegação: Configurações no topo, depois os módulos.</summary>
    public IReadOnlyList<NavItem> NavItems { get; }

    private NavItem? _selected;

    /// <summary>Item atualmente exibido; a barra lateral controla isto.</summary>
    public NavItem? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    public ShellViewModel(IReadOnlyList<IModule> modules, ILanguageManager language, IWorkspaceService workspace)
    {
        var items = new List<NavItem>
        {
            new SettingsNavItem(new ConfigViewModel(workspace))   // no topo, como o "Geral" antigo
        };
        items.AddRange(modules.Select(m => new ModuleNavItem(m, language)));

        NavItems = items;
        Selected = NavItems.FirstOrDefault();   // abre na Config, como o antigo abria na Geral
    }
}