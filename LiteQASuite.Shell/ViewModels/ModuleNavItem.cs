using LiteQASuite.Core.Localization;
using LiteQASuite.Core.Modules;
using LiteQASuite.Shell.Icons;
using System.Windows;
using System.Windows.Media;

namespace LiteQASuite.Shell.ViewModels;

/// <summary>
/// Entrada de navegação que embrulha um <see cref="IModule"/>. Como o
/// <see cref="IModule"/> não é <c>INotifyPropertyChanged</c> (de propósito, para
/// o contrato ficar enxuto), este item assina o <see cref="ILanguageManager.LanguageChanged"/>
/// e re-notifica o rótulo — a relocalização ao vivo mora aqui, na casca. O ícone
/// vem do mapa módulo→ícone do Shell, pelo Id do módulo.
/// </summary>
public sealed class ModuleNavItem : NavItem
{
    private readonly IModule _module;

    public ModuleNavItem(IModule module, ILanguageManager language)
    {
        _module = module;
        language.LanguageChanged += () => OnPropertyChanged(nameof(DisplayName));
    }

    public override string DisplayName => _module.DisplayName;

    public override Geometry Icon => Icons.Icons.ForModule(_module.Id);

    public override FrameworkElement View => _module.View;
}