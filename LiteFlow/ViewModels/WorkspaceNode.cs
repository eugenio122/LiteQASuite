using System.Collections.ObjectModel;
using System.Windows.Media;
using LiteFlow.Icons;

namespace LiteFlow.ViewModels;

/// <summary>
/// Um nó da árvore do Workspace. A árvore tem quatro níveis —
/// <b>squad → ciclo → cenário → relatórios</b> — e este tipo serve a todos.
///
/// Um tipo só, e não quatro irmãos, porque a diferença entre eles é uma pergunta
/// (<see cref="Kind"/>) e não um comportamento: o que muda é o ícone, o que o duplo
/// clique faz e quais itens do menu de contexto valem.
///
/// <b>Só cenários com <c>.lflow</c> entram na árvore</b>, e sob eles aparecem só os
/// relatórios exportados — o <c>.json</c> do LiteJson fica de fora porque é
/// artefato de outro módulo, não coisa que o LiteFlow abra.
/// </summary>
public sealed class WorkspaceNode
{
    public WorkspaceNode(
        WorkspaceNodeKind kind,
        string displayName,
        string fullPath,
        string? squad = null,
        string? cycle = null,
        string? scenarioId = null)
    {
        Kind = kind;
        DisplayName = displayName;
        FullPath = fullPath;
        Squad = squad;
        Cycle = cycle;
        ScenarioId = scenarioId;
    }

    /// <summary>O que este nó representa.</summary>
    public WorkspaceNodeKind Kind { get; }

    /// <summary>
    /// O rótulo na árvore. Nos cenários é "ID — caso de teste", que é o que se lê
    /// para reconhecer o cenário semanas depois; nos demais, o nome da pasta ou do
    /// arquivo.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Caminho no disco: a pasta, nos três primeiros níveis; o arquivo, nos
    /// relatórios. É o que "abrir" e "abrir local do arquivo" usam.
    /// </summary>
    public string FullPath { get; }

    /// <summary>O squad do nó. <c>null</c> no próprio nó de squad.</summary>
    public string? Squad { get; }

    /// <summary>O ciclo do nó. <c>null</c> nos níveis acima dele.</summary>
    public string? Cycle { get; }

    /// <summary>O ID do cenário. Preenchido no cenário e nos relatórios dele.</summary>
    public string? ScenarioId { get; }

    /// <summary>Ícone do tipo, pintado com o Foreground do item.</summary>
    public Geometry Icon => WorkspaceIcons.For(Kind);

    /// <summary><c>true</c> no nó de cenário — o único que abre no editor.</summary>
    public bool IsScenario => Kind == WorkspaceNodeKind.Scenario;

    /// <summary><c>true</c> nos relatórios — os que abrem no programa padrão do Windows.</summary>
    public bool IsReport => Kind is WorkspaceNodeKind.WordReport or WorkspaceNodeKind.PdfReport;

    /// <summary>Os filhos do nó, conforme o nível.</summary>
    public ObservableCollection<WorkspaceNode> Children { get; } = new();
}