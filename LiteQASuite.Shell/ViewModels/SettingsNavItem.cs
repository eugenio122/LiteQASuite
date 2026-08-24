using LiteQASuite.Shell.Views;
using System.Windows;
using System.Windows.Media;

namespace LiteQASuite.Shell.ViewModels;

/// <summary>
/// Entrada de navegação da tela de configurações do LiteQASuite. É tela da
/// <b>casca</b>, não um <c>IModule</c> — por isso é uma entrada de navegação
/// própria, sem sujar o contrato de módulo. Cria a <see cref="ConfigView"/> sob
/// demanda, com o <see cref="ConfigViewModel"/> como DataContext.
/// </summary>
public sealed class SettingsNavItem : NavItem
{
    private readonly ConfigViewModel _viewModel;
    private ConfigView? _view;

    public SettingsNavItem(ConfigViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    // TODO: localizar quando fizermos a passada de i18n do chrome do Shell (hoje o
    // título da janela também é fixo). Por ora, fixo como o "Geral" antigo.
    public override string DisplayName => "Configurações";

    public override Geometry Icon => Shell.Icons.Icons.Settings;

    public override FrameworkElement View => _view ??= new ConfigView { DataContext = _viewModel };
}