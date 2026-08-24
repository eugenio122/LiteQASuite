using LiteQASuite.Core.Mvvm;
using LiteQASuite.Core.Workspace;
using Microsoft.Win32;

namespace LiteQASuite.Shell.ViewModels;

/// <summary>
/// ViewModel da janela de configurações do LiteQASuite — o embrião da tela
/// "Geral". Por ora, só o Workspace: mostra a raiz atual e permite trocá-la.
/// </summary>
public sealed class ConfigViewModel : ViewModelBase
{
    private readonly IWorkspaceService _workspace;

    public ConfigViewModel(IWorkspaceService workspace)
    {
        _workspace = workspace;
        ChangeWorkspaceCommand = new RelayCommand(ChangeWorkspace);
    }

    /// <summary>Raiz atual do Workspace, para exibição.</summary>
    public string WorkspaceRoot => _workspace.RootPath ?? "(não configurado)";

    public RelayCommand ChangeWorkspaceCommand { get; }

    private void ChangeWorkspace()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Escolha onde criar a pasta do Workspace do LiteQASuite"
        };

        if (dialog.ShowDialog() == true)
        {
            // Cria uma nova pasta "LiteQASuite Workspace" no local escolhido; o
            // conteúdo do workspace anterior não é movido.
            _workspace.Configure(dialog.FolderName);
            OnPropertyChanged(nameof(WorkspaceRoot));
        }
    }
}