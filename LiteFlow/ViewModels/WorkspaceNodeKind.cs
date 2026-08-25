namespace LiteFlow.ViewModels;

/// <summary>
/// O que um nó da árvore do Workspace representa. Governa o ícone, o que o duplo
/// clique faz e quais itens do menu de contexto ficam habilitados.
/// </summary>
public enum WorkspaceNodeKind
{
    /// <summary>Pasta de squad/projeto. O primeiro nível.</summary>
    Squad,

    /// <summary>Pasta de ciclo/sprint, dentro de um squad.</summary>
    Cycle,

    /// <summary>Um cenário: a pasta com o <c>.lflow</c> dentro. Abre no editor.</summary>
    Scenario,

    /// <summary>Um relatório <c>.docx</c> exportado. Abre no programa padrão.</summary>
    WordReport,

    /// <summary>Um relatório <c>.pdf</c> exportado. Abre no programa padrão.</summary>
    PdfReport
}