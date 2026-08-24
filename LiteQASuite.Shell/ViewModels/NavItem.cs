using LiteQASuite.Core.Mvvm;
using System.Windows;
using System.Windows.Media;

namespace LiteQASuite.Shell.ViewModels;

/// <summary>
/// Base de uma entrada da navegação da casca. Unifica módulos e telas próprias do
/// Shell (como a de configurações) sob a mesma barra lateral: a área de conteúdo
/// hospeda a <see cref="View"/> do item selecionado, seja ele módulo ou config.
/// Herda <see cref="ViewModelBase"/> para que o <see cref="DisplayName"/> possa
/// relocalizar ao vivo.
/// </summary>
public abstract class NavItem : ViewModelBase
{
    /// <summary>Rótulo do item — some do trilho de ícones, mas vira a tooltip.</summary>
    public abstract string DisplayName { get; }

    /// <summary>Ícone vetorial exibido na barra lateral.</summary>
    public abstract Geometry Icon { get; }

    /// <summary>View hospedada na área de conteúdo quando este item está selecionado.</summary>
    public abstract FrameworkElement View { get; }
}