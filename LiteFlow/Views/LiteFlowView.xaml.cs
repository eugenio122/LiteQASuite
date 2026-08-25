using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LiteFlow.ViewModels;

namespace LiteFlow.Views;

/// <summary>
/// A tela do LiteFlow. O code-behind cobre só os dois gestos que a
/// <see cref="TreeView"/> não expõe como comando.
/// </summary>
public partial class LiteFlowView : UserControl
{
    public LiteFlowView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Abrir por duplo clique, e não por clique simples, é deliberado — a árvore
    /// também serve para navegar e conferir o que existe, e trocar de cenário a
    /// cada clique de curiosidade custaria carregar um arquivo inteiro.
    /// </summary>
    private void OnWorkspaceTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not LiteFlowViewModel viewModel) return;
        if (WorkspaceTreeView.SelectedItem is not WorkspaceNode node) return;

        // O CanExecute filtra os nós de squad e de ciclo: neles o duplo clique
        // continua servindo para expandir, que é o comportamento nativo da árvore.
        if (viewModel.OpenNodeCommand.CanExecute(node))
        {
            viewModel.OpenNodeCommand.Execute(node);
            e.Handled = true;
        }
    }

    /// <summary>
    /// O clique direito não seleciona nada numa <see cref="TreeView"/> — e sem isso
    /// o menu de contexto agiria sobre o item que estava selecionado antes, e não
    /// sobre o que está debaixo do cursor. Num menu que tem "excluir cenário", esse
    /// detalhe é a diferença entre apagar o certo e apagar o errado.
    /// </summary>
    private void OnWorkspaceTreeRightClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;

        var item = FindAncestor<TreeViewItem>(source);
        if (item is not null) item.IsSelected = true;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}